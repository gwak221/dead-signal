using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
public class MonsterChase : MonoBehaviour
{
    public Transform target; // 추적할 대상 (플레이어)
    private NavMeshAgent agent;
    private LineRenderer lineRenderer;

    [Header("시야 범위 설정")]
    public float viewRadius = 8f;        // 감지 최대 거리
    [Range(0, 360)]
    public float viewAngle = 90f;        // 부채꼴 시야각 (전방 기준)

    [Header("추적 유지 시간")]
    public float loseTargetTime = 5f;    // 시야 벗어난 후 추적을 유지할 시간
    private float loseTimer = 0f;
    private bool isChasing = false;

    [Header("평상시 순찰(Wander) 설정")]
    public float patrolRadius = 15f;     // 랜덤으로 이동할 최대 반경
    public float patrolWaitTime = 2f;    // 도착 후 다음 장소로 갈 때까지 대기 시간
    private float patrolTimer = 0f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();

        // LineRenderer 초기 세팅
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        // 시작하자마자 첫 순찰 위치 잡기
        GoToRandomWanderPoint();
    }

    void Update()
    {
        // 1. 실시간으로 붉은 부채꼴 그리기
        DrawFieldOfView();

        if (target == null || !agent.enabled || !agent.isOnNavMesh) return;

        // 2. 플레이어 인식 체크
        if (IsPlayerInSight())
        {
            isChasing = true;
            loseTimer = loseTargetTime; // 감지 중일 때는 타이머 리셋
        }
        else if (isChasing)
        {
            loseTimer -= Time.deltaTime;
            if (loseTimer <= 0f)
            {
                isChasing = false;
                patrolTimer = patrolWaitTime; // 즉시 다음 순찰지로 이동 준비
            }
        }

        // 3. 상태별 행동 지정
        if (isChasing)
        {
            // [추적 상태] 플레이어를 향해 맹렬히 이동
            agent.SetDestination(target.position);
        }
        else
        {
            // [순찰 상태] 제멋대로 배회하기
            PatrolBehavior();
        }
    }

    // 부채꼴 범위 판정
    private bool IsPlayerInSight()
    {
        Vector3 dirToTarget = target.position - transform.position;
        dirToTarget.y = 0; // 높이 차이 무시

        if (dirToTarget.magnitude <= viewRadius)
        {
            float angle = Vector3.Angle(transform.forward, dirToTarget.normalized);
            if (angle <= viewAngle / 2f)
            {
                // 장애물에 가려졌는지 검사 (옵션: 레이캐스트)
                // 만약 벽 뒤의 플레이어를 무시하고 싶다면 여기에 Physics.Raycast를 넣을 수 있습니다.
                return true;
            }
        }
        return false;
    }

    // 평상시 배회 시스템
    private void PatrolBehavior()
    {
        // 목적지에 거의 도달했거나 경로가 끊긴 경우
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            patrolTimer += Time.deltaTime;
            if (patrolTimer >= patrolWaitTime)
            {
                GoToRandomWanderPoint();
                patrolTimer = 0f;
            }
        }
    }

    // 맵 안에서 이동 가능한 랜덤 좌표 찾아 이동 (NavMesh 기반)
    private void GoToRandomWanderPoint()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;

        NavMeshHit hit;
        // 탐색 반경 내에서 몬스터가 걸어갈 수 있는 "합법적인 바닥"을 찾음
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, 1))
        {
            agent.SetDestination(hit.position);
        }
    }

    // 붉은 부채꼴 렌더링 (게임 창 표시용)
    private void DrawFieldOfView()
    {
        int segments = 30; // 곡선을 표현할 조각 수
        lineRenderer.positionCount = segments + 2; // 중심점 + 호 파트 + 중심점으로 돌아오기

        Vector3[] points = new Vector3[segments + 2];
        points[0] = Vector3.zero; // 몬스터 중심 (로컬 좌표)

        float startAngle = -viewAngle / 2f;
        float angleStep = viewAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            // 몬스터 앞방향(Z축) 기준 회전
            Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
            // 몬스터 높이에서 살짝 띄워 바닥에 묻히지 않도록 함 (Y: 0.05)
            points[i + 1] = dir * viewRadius + new Vector3(0, 0.05f, 0); 
        }

        lineRenderer.SetPositions(points);
    }

    // Scene 뷰 기즈모 (에디터용)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, viewRadius);
    }
}