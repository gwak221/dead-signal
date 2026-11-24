using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터의 NavMeshAgent 이동 상태를 감지하여 Animator의 파라미터(IsMoving, Speed, IsChasing)를 실시간 갱신하고,
/// 이동 속도에 맞춰 발걸음 애니메이션 재생 속도를 동기화하는 컴포넌트입니다.
/// 기존 MonsterChase.cs 코드를 일절 수정하지 않고 독립적으로 동작합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterMovementAnimator : MonoBehaviour
{
    [Header("참조 컴포넌트 (비워둘 시 자동 탐색)")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private MonsterChase monsterChase;

    [Header("이동 판정 설정")]
    [Tooltip("이 속도 이상일 때 걷는 애니메이션으로 전환됩니다.")]
    public float moveThreshold = 0.15f;

    [Tooltip("애니메이터 파라미터 감쇠 시간(초)")]
    public float speedDampTime = 0.1f;

    [Header("발걸음 속도 동기화 (발 미끄러짐 방지)")]
    [Tooltip("이동 속도에 비례하여 걷기/뛰기 애니메이션 재생 속도를 동적으로 조절합니다.")]
    public bool syncAnimSpeedWithAgent = true;

    [Tooltip("기본 걷기 속도 기준치 (m/s)")]
    public float baseMoveSpeed = 2.5f;

    [Tooltip("기본 추격(Run) 속도 기준치 (m/s)")]
    public float baseRunSpeed = 6.5f;

    [Tooltip("공격 애니메이션 재생 중 배속 고정 시간 (초)")]
    public float attackAnimDuration = 1.15f;

    [Tooltip("최소 애니메이션 배속")]
    public float minAnimSpeed = 0.8f;

    [Tooltip("최대 애니메이션 배속")]
    public float maxAnimSpeed = 2.2f;

    // Animator 파라미터 해시 캐싱 (성능 최적화)
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsChasing = Animator.StringToHash("IsChasing");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashRunState = Animator.StringToHash("Run");

    private float attackTimer = 0f;

    /// <summary>
    /// 플레이어 포착/공격 시 Attack 트리거를 발동하고 1.0배속으로 공격 클립을 재생합니다.
    /// </summary>
    public void TriggerAttack()
    {
        attackTimer = attackAnimDuration;
        if (animator != null)
        {
            animator.speed = 1.0f;
            animator.ResetTrigger(HashAttack);
            animator.SetTrigger(HashAttack);
        }
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (monsterChase == null) monsterChase = GetComponent<MonsterChase>();
    }

    void OnEnable()
    {
        if (monsterChase == null) monsterChase = GetComponent<MonsterChase>();
        if (monsterChase != null) monsterChase.OnAttack += HandleChaseAttack;
    }

    void OnDisable()
    {
        if (monsterChase != null) monsterChase.OnAttack -= HandleChaseAttack;
    }

    void HandleChaseAttack(MonsterChase _)
    {
        // PlayerCaughtSequence 진행 중에는 카메라가 몬스터를 마주본 직후 동기화해 TriggerAttack을 호출한다.
        if (!PlayerCaughtSequence.IsRunning)
            TriggerAttack();
    }

    void Start()
    {
        // 몬스터가 NavMesh에서 약간 벗어난 위치에 배치되어 있더라도 자동으로 가장 가까운 유효 NavMesh 지점으로 워프 안착
        if (agent != null && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }

    void Update()
    {
        if (animator == null || agent == null) return;

        if (attackTimer > 0f)
        {
            attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
        }

        // NavMesh 위에 배치되지 않은 경우 안전 처리
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            animator.SetBool(HashIsMoving, false);
            animator.SetFloat(HashSpeed, 0f);
            animator.SetBool(HashIsChasing, false);
            if (syncAnimSpeedWithAgent) animator.speed = 1.0f;
            return;
        }

        // 1. 현재 에이전트의 이동 속도 측정
        float currentSpeed = agent.velocity.magnitude;

        // 2. 이동 중인지 판정
        // 에이전트가 멈춰있거나(isStopped), 임계 속도 이하이면 정지 상태
        bool isMoving = !agent.isStopped && currentSpeed >= moveThreshold;

        // 3. Animator 파라미터 전달
        animator.SetBool(HashIsMoving, isMoving);
        animator.SetFloat(HashSpeed, currentSpeed, speedDampTime, Time.deltaTime);

        // 4. 추격 상태(IsChasing) 전달
        bool isChasing = monsterChase != null && monsterChase.IsChasing;
        animator.SetBool(HashIsChasing, isChasing);

        // 5. 발 미끄러짐 방지를 위한 애니메이션 재생 속도 동기화
        if (syncAnimSpeedWithAgent)
        {
            if (attackTimer > 0f || animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
            {
                animator.speed = 1.0f;
            }
            else if (isMoving)
            {
                float refSpeed = (isChasing && baseRunSpeed > 0f && animator.HasState(0, HashRunState))
                    ? baseRunSpeed
                    : baseMoveSpeed;
                if (refSpeed > 0f)
                {
                    float speedRatio = currentSpeed / refSpeed;
                    animator.speed = Mathf.Clamp(speedRatio, minAnimSpeed, maxAnimSpeed);
                }
            }
            else
            {
                // 정지(Idle) 시에는 자연스러운 정상 속도(1.0x)로 호흡
                animator.speed = 1.0f;
            }
        }
    }
}
