using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// MonsterHoundAI의 상태와 NavMeshAgent 이동 속도를 읽어
/// Monster_2_AnimatorController(Idle / 01_Walk / 02_ChargePrep / 03_Charge / 04_Chase / 05_Attack)를 구동합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MonsterHoundAI))]
public class MonsterHoundAnimator : MonoBehaviour
{
    [Header("참조 컴포넌트 (비워둘 시 자동 탐색)")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private MonsterHoundAI houndAI;

    [Header("이동 판정 및 배속 동기화")]
    [Tooltip("Patrol 상태에서 이 속도 이상일 때 걷기 애니메이션 재생")]
    public float moveThreshold = 0.12f;
    public float speedDampTime = 0.1f;

    [Header("클립 기준 속도 (발 미끄러짐 방지)")]
    public float baseWalkSpeed = 0.95f;
    public float baseChargeSpeed = 10.5f;
    public float baseChaseSpeed = 5.0f;

    [Tooltip("02_ChargePrep 원본 클립 길이(초). chargePrepDuration에 맞춰 자동 배속 조절")]
    public float chargePrepClipLength = 1.167f;

    private static readonly int HashState = Animator.StringToHash("State");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");

    private Vector3 lastPosition;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (houndAI == null) houndAI = GetComponent<MonsterHoundAI>();
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        if (animator == null || houndAI == null) return;

        float dt = Time.deltaTime;
        float actualSpeed = 0f;
        if (dt > 0.0001f)
        {
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            actualSpeed = delta.magnitude / dt;
        }
        lastPosition = transform.position;

        float agentSpeed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
        float moveSpeed = Mathf.Max(agentSpeed, actualSpeed);

        int animState = 0; // 0: Idle, 1: Walk, 2: ChargePrep, 3: Charge, 4: Chase, 5: Attack
        float targetAnimSpeed = 1f;
        bool isMoving = false;

        switch (houndAI.CurrentState)
        {
            case MonsterHoundAI.HoundState.Patrol:
                bool agentCanWalk = agent == null || (agent.enabled && agent.isOnNavMesh && !agent.isStopped);
                bool walking = houndAI.enabled && agentCanWalk && moveSpeed >= moveThreshold;
                isMoving = walking;
                animState = walking ? 1 : 0;
                if (walking && baseWalkSpeed > 0.01f)
                    targetAnimSpeed = Mathf.Clamp(moveSpeed / baseWalkSpeed, 0.75f, 1.8f);
                break;

            case MonsterHoundAI.HoundState.ChargePrep:
                animState = 2;
                if (houndAI.chargePrepDuration > 0.05f)
                    targetAnimSpeed = Mathf.Clamp(chargePrepClipLength / houndAI.chargePrepDuration, 0.5f, 2.5f);
                break;

            case MonsterHoundAI.HoundState.Charge:
                isMoving = true;
                animState = 3;
                if (baseChargeSpeed > 0.01f)
                    targetAnimSpeed = Mathf.Clamp(moveSpeed / baseChargeSpeed, 1.0f, 1.85f);
                break;

            case MonsterHoundAI.HoundState.AcceleratedChase:
                isMoving = true;
                animState = 4;
                if (baseChaseSpeed > 0.01f)
                    targetAnimSpeed = moveSpeed >= moveThreshold
                        ? Mathf.Clamp(moveSpeed / baseChaseSpeed, 0.95f, 1.6f)
                        : 0.75f;
                break;

            case MonsterHoundAI.HoundState.Attack:
                animState = 5;
                targetAnimSpeed = 1f;
                break;

            case MonsterHoundAI.HoundState.Recovery:
                animState = 0;
                targetAnimSpeed = 1.0f; // 충돌 정지 중 대기/경직 호흡
                break;
        }

        animator.SetInteger(HashState, animState);
        animator.SetBool(HashIsMoving, isMoving);
        animator.SetFloat(HashSpeed, moveSpeed, speedDampTime, dt);
        animator.speed = targetAnimSpeed;
    }
}
