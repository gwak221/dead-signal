using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 몬스터가 이동할 때 경로 상의 문(DoorPair)을 감지하고,
/// 문을 통과(벽 뚫기)하지 않고 직접 문을 열어 문이 열린 뒤 지나가도록 제어하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterDoorOpener : MonoBehaviour
{
    private NavMeshAgent agent;
    private MonsterChase chase;

    [Header("문 감지 및 열기 설정")]
    [Tooltip("전방/주변 문을 탐지하는 반경 (미터)")]
    public float detectRadius = 2.4f;

    [Tooltip("문과의 거리가 이 값 이내로 들어오면 문을 열기 시작합니다.")]
    public float openTriggerDistance = 2.0f;

    [Tooltip("문이 열릴 때까지 기다리기 위해 멈추는 거리 (미터)")]
    public float waitStopDistance = 1.35f;

    [Tooltip("몬스터가 통과하기 위해 문이 열려야 하는 최소 진행도 (0 = 닫힘, 1 = 완전히 열림)")]
    [Range(0.5f, 1.0f)]
    public float requiredOpenProgress = 0.75f;

    [Header("문 통과 후 닫힘 설정")]
    [Tooltip("몬스터가 통과한 후 문을 자동으로 다시 닫을지 여부 (기본: false로 열어둠)")]
    public bool autoCloseAfterPass = false;
    public float autoCloseDelay = 4.0f;

    // 현재 상호작용 중인 문
    private DoorPair activeDoor = null;
    private bool hasPassedActiveDoor = false;
    private float waitTimer = 0f;

    [Tooltip("문 앞에서 이 시간(초) 넘게 기다려도 열리지 않으면 대기를 포기한다 (영구 정지 방지).")]
    public float maxWaitSeconds = 4f;

    /// <summary>
    /// 문이 열리기를 문 앞에서 대기 중인지 여부.
    /// MonsterChase에서 stuckTimer를 초기화하여 경로 취소를 방지하는 데 사용됩니다.
    /// </summary>
    public bool IsWaitingForDoor { get; private set; } = false;

    // 감지 성능 최적화를 위한 버퍼
    private Collider[] overlapColliders = new Collider[16];

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        chase = GetComponent<MonsterChase>();
    }

    void Update()
    {
        // 컷씬(MonsterChase 비활성) / 잡힘 연출 중에는 agent 를 건드리지 않는다
        if ((chase != null && !chase.enabled) || PlayerCaughtSequence.IsRunning) return;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            ResetDoorState();
            return;
        }

        // 문이 비활성/제외 대상이 되면 해제
        if (activeDoor != null && (!activeDoor.isActiveAndEnabled || !IsEligible(activeDoor))) ResetDoorState();

        // 현재 활성 문이 없는 경우 주변 문 탐색
        if (activeDoor == null)
        {
            FindApproachingDoor();
        }

        // 활성 문이 있는 경우 처리
        if (activeDoor != null)
        {
            ProcessActiveDoor();
        }
    }

    /// <summary>
    /// 몬스터가 향하고 있는 방향의 문을 탐색합니다.
    /// </summary>
    private void FindApproachingDoor()
    {
        Vector3 monsterPos = transform.position;
        int hitCount = Physics.OverlapSphereNonAlloc(monsterPos, detectRadius, overlapColliders);

        DoorPair bestDoor = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = overlapColliders[i];
            if (col == null) continue;

            DoorPair door = col.GetComponentInParent<DoorPair>();
            if (door == null) continue;

            // 약품장이나 통과용이 아닌 문은 제외
            if (!IsEligible(door)) continue;

            Vector3 doorCenter = door.GetDoorCenter();
            Vector3 toDoor = doorCenter - monsterPos;
            toDoor.y = 0f;
            float dist = toDoor.magnitude;

            if (dist > detectRadius) continue;

            // 몬스터가 해당 문 방향으로 이동하거나 바라보고 있는지 확인
            Vector3 moveDir = agent.velocity.sqrMagnitude > 0.1f ? agent.velocity.normalized : transform.forward;
            
            // 네비메시 경로의 다음 목표 지점이 있다면 그 방향도 고려
            if (agent.hasPath && agent.steeringTarget != Vector3.zero)
            {
                Vector3 toSteering = agent.steeringTarget - monsterPos;
                toSteering.y = 0f;
                if (toSteering.sqrMagnitude > 0.05f)
                    moveDir = toSteering.normalized;
            }

            float dot = Vector3.Dot(moveDir, toDoor.normalized);
            // 문을 향해 가고 있거나 문이 바로 전방(dot > 0.2f)에 있는 경우
            if (dot > 0.2f && dist < bestDist)
            {
                bestDist = dist;
                bestDoor = door;
            }
        }

        if (bestDoor != null)
        {
            activeDoor = bestDoor;
            hasPassedActiveDoor = false;
        }
    }

    /// <summary>
    /// 감지된 문에 대해 열기, 대기 및 통과 처리를 수행합니다.
    /// </summary>
    private void ProcessActiveDoor()
    {
        if (activeDoor == null) return;

        Vector3 monsterPos = transform.position;
        Vector3 doorCenter = activeDoor.GetDoorCenter();
        Vector3 toDoor = doorCenter - monsterPos;
        toDoor.y = 0f;
        float distToDoor = toDoor.magnitude;

        // 1. 문 열기 트리거
        if (distToDoor <= openTriggerDistance)
        {
            if (!activeDoor.IsOpen)
            {
                activeDoor.Open();
            }
        }

        // 2. 문이 아직 통과할 만큼 충분히 열리지 않았는데 너무 가까이 접근한 경우:
        //    문을 뚫고 지나가지 않도록 잠시 멈춰서 문이 열리기를 기다립니다.
        if (activeDoor.OpenProgress < requiredOpenProgress)
        {
            if (distToDoor <= waitStopDistance)
            {
                waitTimer += Time.deltaTime;
                if (waitTimer > maxWaitSeconds) { ResetDoorState(); return; }
                IsWaitingForDoor = true;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;

                // 문 쪽을 자연스럽게 바라봄
                if (toDoor.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toDoor.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 180f * Time.deltaTime);
                }
                return;
            }
        }

        waitTimer = 0f;

        // 3. 문이 충분히 열렸거나 이미 통과 가능한 상태인 경우
        if (IsWaitingForDoor)
        {
            // 대기 해제 및 이동 재개
            IsWaitingForDoor = false;
            agent.isStopped = false;
        }

        // 4. 문 통과 판정
        // 몬스터가 문과 매우 가까워졌거나 이미 문을 지나친 경우
        if (distToDoor <= waitStopDistance)
        {
            hasPassedActiveDoor = true;
        }

        // 문을 지난 후 충분히 멀어지면 활성 문 해제
        if (hasPassedActiveDoor && distToDoor > waitStopDistance + 0.8f)
        {
            DoorPair finishedDoor = activeDoor;
            if (autoCloseAfterPass && finishedDoor != null)
            {
                StartCoroutine(AutoCloseDoorRoutine(finishedDoor, autoCloseDelay));
            }

            activeDoor = null;
            hasPassedActiveDoor = false;
            IsWaitingForDoor = false;
        }
    }

    /// <summary>몬스터가 열어도 되는 문인지: 일반 통행 문만. 수납장 / 숨기 캐비넷 / 탈출 벽은 제외.</summary>
    static bool IsEligible(DoorPair door)
    {
        if (door == null || !door.isPassableDoor) return false;
        if (door.GetComponentInParent<HideSpot>() != null) return false;
        if (door.GetComponentInParent<EscapeDoorController>() != null) return false;
        if (door.name.ToLowerInvariant().Contains("cabinet")) return false;
        return true;
    }

    private System.Collections.IEnumerator AutoCloseDoorRoutine(DoorPair door, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (door != null && door.IsOpen)
        {
            door.Close();
        }
    }

    public void ResetDoorState()
    {
        if (IsWaitingForDoor && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
        activeDoor = null;
        hasPassedActiveDoor = false;
        IsWaitingForDoor = false;
        waitTimer = 0f;
    }

    void OnDisable()
    {
        ResetDoorState();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, waitStopDistance);

        if (activeDoor != null)
        {
            Gizmos.color = IsWaitingForDoor ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, activeDoor.GetDoorCenter());
        }
    }
}
