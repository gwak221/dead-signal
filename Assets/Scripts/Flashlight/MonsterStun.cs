using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// 몬스터 스턴 (손전등 눈부심 등).
///
/// MonsterChase 내부는 건드리지 않는다. 스턴 동안
///  - MonsterChase 컴포넌트를 잠시 비활성화 (추적/순찰 상태 값은 그대로 보존됨)
///  - NavMeshAgent.isStopped = true (경로는 유지)
/// 스턴이 끝나면 둘 다 원래 상태로 돌려서 기존 AI가 이어서 동작한다.
///
/// 애니메이션 연결:
///  - Animator에 stunTrigger / stunBool / recoverTrigger 이름의 파라미터가 있으면 자동으로 사용.
///  - 또는 onStunStart / onStunEnd(UnityEvent), Stunned / Recovered(C# event)에 연결.
///  - 애니메이션이 없을 때를 위한 간단한 '고개 젖힘' 절차적 연출(useProceduralFlinch) 포함.
/// </summary>
[DisallowMultipleComponent]
public class MonsterStun : MonoBehaviour
{
    static readonly List<MonsterStun> all = new List<MonsterStun>();
    public static IReadOnlyList<MonsterStun> All => all;

    [Header("Stun")]
    [Tooltip("기본 스턴 시간(초). 손전등은 FlashlightController.stunDuration 값을 넘겨준다.")]
    public float defaultStunDuration = 2.5f;
    [Tooltip("스턴이 끝난 뒤 몬스터 쪽 면역 시간(초). 손전등 재사용 대기시간(3초)은 FlashlightItem에서 따로 관리하므로 여기서는 짧은 안전장치만 둔다.")]
    public float restunCooldown = 0.5f;

    [Header("Aim Points (auto if empty)")]
    [Tooltip("얼굴 위치. 비우면 Renderer 범위 위쪽을 사용. Flash도 여기서 터진다.")]
    public Transform facePoint;
    [Range(0f, 1f)] public float autoFaceHeight = 0.7f;

    [Header("References (auto-found if empty)")]
    public NavMeshAgent agent;
    [Tooltip("스턴 동안 비활성화할 AI 스크립트들. 비우면 같은 오브젝트의 MonsterChase.")]
    public Behaviour[] aiBehavioursToPause;
    public Animator animator;

    [Header("Animator Parameters (있을 때만 사용)")]
    public string stunTrigger = "Stun";
    public string stunBool = "IsStunned";
    public string recoverTrigger = "StunEnd";

    [Header("Procedural Flinch (애니메이션 없을 때)")]
    public bool useProceduralFlinch = true;
    [Tooltip("젖혀질 대상. 비우면 몬스터 루트.")]
    public Transform flinchTarget;
    [Tooltip("고개가 뒤로 젖혀지는 최대 각도(도).")]
    [Range(0f, 40f)] public float flinchAngle = 14f;
    [Tooltip("스턴 유지 중 남아있는 젖힘 비율.")]
    [Range(0f, 1f)] public float holdTiltRatio = 0.3f;
    [Tooltip("움찔하며 떨리는 정도(도).")]
    [Range(0f, 5f)] public float shakeAngle = 1.5f;

    [Header("Events")]
    public UnityEvent onStunStart;
    public UnityEvent onStunEnd;
    public event Action<MonsterStun> Stunned;
    public event Action<MonsterStun> Recovered;

    bool isStunned;
    float stunStart;
    float stunEnd;
    float cooldownUntil;

    bool[] behaviourWasEnabled;
    bool agentWasStopped;
    bool agentUpdateRotation;
    Quaternion flinchBaseRotation;
    Renderer[] renderers;

    public bool IsStunned => isStunned;
    public bool IsOnCooldown => !isStunned && Time.time < cooldownUntil;
    public bool CanBeStunned => enabled && !isStunned && Time.time >= cooldownUntil;
    public float StunRemaining => isStunned ? Mathf.Max(0f, stunEnd - Time.time) : 0f;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (flinchTarget == null) flinchTarget = transform;

        if (aiBehavioursToPause == null || aiBehavioursToPause.Length == 0)
        {
            var chase = GetComponent<MonsterChase>();
            aiBehavioursToPause = chase != null ? new Behaviour[] { chase } : new Behaviour[0];
        }

        renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }

    void OnDisable()
    {
        all.Remove(this);
        if (isStunned) EndStun();
    }

    // ───────────────────────── 조준점 ─────────────────────────

    public Vector3 FacePosition
    {
        get
        {
            if (facePoint != null) return facePoint.position;
            Bounds b = GetBounds();
            return b.center + Vector3.up * b.extents.y * autoFaceHeight;
        }
    }

    public Vector3 CenterPosition => GetBounds().center;

    Bounds GetBounds()
    {
        bool has = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || r is LineRenderer) continue; // 시야 부채꼴 LineRenderer 제외
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        if (!has) b = new Bounds(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        return b;
    }

    // ───────────────────────── 스턴 ─────────────────────────

    public bool TryStun() => TryStun(defaultStunDuration);

    public bool TryStun(float duration)
    {
        if (!CanBeStunned || duration <= 0f)
            return false;

        isStunned = true;
        stunStart = Time.time;
        stunEnd = Time.time + duration;

        // AI 일시 정지 (상태 값은 그대로 보존)
        behaviourWasEnabled = new bool[aiBehavioursToPause.Length];
        for (int i = 0; i < aiBehavioursToPause.Length; i++)
        {
            var b = aiBehavioursToPause[i];
            behaviourWasEnabled[i] = b != null && b.enabled;
            if (b != null) b.enabled = false;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agentWasStopped = agent.isStopped;
            agentUpdateRotation = agent.updateRotation;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            if (useProceduralFlinch && flinchTarget == transform)
                agent.updateRotation = false; // 젖힘 연출과 회전 충돌 방지
        }

        if (flinchTarget != null)
            flinchBaseRotation = flinchTarget.localRotation;

        SetAnimator(true);
        onStunStart?.Invoke();
        Stunned?.Invoke(this);
        return true;
    }

    /// <summary>스턴 강제 해제 (디버그/특수 이벤트용).</summary>
    public void EndStun()
    {
        if (!isStunned) return;
        isStunned = false;
        cooldownUntil = Time.time + restunCooldown;

        if (useProceduralFlinch && flinchTarget != null)
            flinchTarget.localRotation = flinchBaseRotation;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = agentWasStopped;
            agent.updateRotation = agentUpdateRotation;
        }

        if (behaviourWasEnabled != null)
        {
            for (int i = 0; i < aiBehavioursToPause.Length && i < behaviourWasEnabled.Length; i++)
                if (aiBehavioursToPause[i] != null && behaviourWasEnabled[i])
                    aiBehavioursToPause[i].enabled = true;
        }

        SetAnimator(false);
        onStunEnd?.Invoke();
        Recovered?.Invoke(this);
    }

    void Update()
    {
        if (!isStunned) return;

        if (Time.time >= stunEnd)
        {
            EndStun();
            return;
        }

        if (useProceduralFlinch && flinchTarget != null)
            flinchTarget.localRotation = flinchBaseRotation * Quaternion.Euler(-FlinchAngle(), 0f, 0f);
    }

    float FlinchAngle()
    {
        float t = Time.time - stunStart;
        float remain = stunEnd - Time.time;

        // 0~0.08초: 확 젖혀짐 → 이후 holdTiltRatio까지 내려와 유지 → 마지막 0.3초에 원위치
        float a;
        if (t < 0.08f) a = 1f - (1f - t / 0.08f) * (1f - t / 0.08f);
        else a = Mathf.Lerp(holdTiltRatio, 1f, Mathf.Exp(-(t - 0.08f) * 6f));

        if (remain < 0.3f) a *= remain / 0.3f;

        float shake = Mathf.Sin(t * 47f) * shakeAngle * Mathf.Exp(-t * 3f);
        return a * flinchAngle + shake;
    }

    void SetAnimator(bool stunned)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (var p in animator.parameters)
        {
            if (stunned && p.name == stunTrigger && p.type == AnimatorControllerParameterType.Trigger) animator.SetTrigger(stunTrigger);
            if (!stunned && p.name == recoverTrigger && p.type == AnimatorControllerParameterType.Trigger) animator.SetTrigger(recoverTrigger);
            if (p.name == stunBool && p.type == AnimatorControllerParameterType.Bool) animator.SetBool(stunBool, stunned);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(FacePosition, 0.15f);
    }
}
