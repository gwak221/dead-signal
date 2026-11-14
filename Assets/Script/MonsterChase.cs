using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterChase : MonoBehaviour
{
    public Transform target;
    private NavMeshAgent agent;

    [Header("공격 설정")]
    public float attackRange = 1.5f;      
    public float attackStopDuration = 4f; 
    private float attackStopTimer = 0f;

    [Header("이동 속도 (HeartRate 미사용)")]
    public float patrolSpeed = 2.5f;
    public float chaseSpeed = 6.5f; // 걷기 5 / 달리기 9 사이

    [Header("근접 감지 설정 (360도 전방향)")]
    [Tooltip("시야각에 상관없이(등 뒤나 옆) 이 반경 내에 플레이어가 들어오면 즉시 감지합니다.")]
    public float closeDetectRadius = 3.6f; // 이전 2.8
    private float detectionMultiplier = 1f;
    public float CurrentCloseRadius => closeDetectRadius * detectionMultiplier;

    [Header("심박수 연동 시야 범위 설정")]
    [Tooltip("안정 BPM(배율 1.0)에서의 전방 시야 거리. 실제 거리 = 이 값 x HeartRateController.DetectionMultiplier. (이전 8)")]
    public float baseViewRadius = 12f;
    public float maxViewMultiplier = 1.1f;   
    [HideInInspector] public float viewRadius = 8f;
    [Range(0, 360)]
    public float viewAngle = 90f;
    [Tooltip("끄면(기본) 사방향 360도 감지. 켜면 근접 반경 밖에서는 viewAngle 부채꼴 안에서만 감지(구 방식).")]
    public bool limitToFov = false;

    [Header("추적 유지 시간")]
    public float loseTargetTime = 5f;
    private float loseTimer = 0f;
    private bool _isChasing = false;
    /// <summary>플레이어를 실제로 감지해 추격 중인지 (읽기 전용 외부 상태).</summary>
    public bool IsChasing => _isChasing;
    /// <summary>추격 시작/종료 이벤트 (UI·연출 시스템이 구독. MonsterChase는 구독자를 모른다).</summary>
    public event System.Action<MonsterChase> OnChaseStarted;
    public event System.Action<MonsterChase> OnChaseEnded;
    private bool isChasing
    {
        get => _isChasing;
        set
        {
            if (_isChasing == value) return;
            _isChasing = value;
            if (value) OnChaseStarted?.Invoke(this); else OnChaseEnded?.Invoke(this);
        }
    }

    [Header("추격 시 몬스터 색상 변경")]
    public Color chaseColor = new Color(1f, 0.15f, 0.15f, 1f);
    public float colorChangeSpeed = 5f;
    private List<Material> monsterMaterials = new List<Material>();
    private List<Color> originalColors = new List<Color>();

    [Header("스마트 광역 순찰 설정")]
    public float patrolRadius = 28f;      
    public float visitedRoomRadius = 12f; 
    public float rotationSpeed = 135f;    

    private List<Vector3> visitedHistory = new List<Vector3>();
    private const int MAX_HISTORY = 12;

    private bool isScanningRoom = false;
    private List<Quaternion> scanRotations = new List<Quaternion>();
    private int currentScanIndex = 0;
    private float scanHoldTimer = 0f;
    private float stuckTimer = 0f;

    // 일반 문(DoorPair)을 직접 열고 통과시키는 컴포넌트 (없으면 Start 에서 한 번만 추가)
    private MonsterDoorOpener doorOpener;
    private bool WaitingForDoor => doorOpener != null && doorOpener.IsWaitingForDoor;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        doorOpener = GetComponent<MonsterDoorOpener>();
        if (doorOpener == null) doorOpener = gameObject.AddComponent<MonsterDoorOpener>();
        RemoveRangeVisuals();

        agent.angularSpeed = 360f;
        agent.acceleration = 16f;
        agent.autoBraking = true;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }


        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r is LineRenderer) continue; // (구버전 씬 호환)
            foreach (Material mat in r.materials)
            {
                monsterMaterials.Add(mat);
                if (mat.HasProperty("_BaseColor")) originalColors.Add(mat.GetColor("_BaseColor"));
                else if (mat.HasProperty("_Color")) originalColors.Add(mat.color);
                else originalColors.Add(Color.white);
            }
        }

        viewRadius = baseViewRadius * detectionMultiplier;
        RecordVisitedPoint(transform.position);
        PickSmartPatrolPoint();
    }

    void Update()
    {
        // 잡힘 연출 중에는 몬스터 정지 (끝난 뒤 attackStopDuration 만큼 더 정지)
        if (PlayerCaughtSequence.IsRunning)
        {
            isChasing = false;
            loseTimer = 0f;
            attackStopTimer = attackStopDuration;
            if (doorOpener != null) doorOpener.ResetDoorState();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            return;
        }

        // 0. 플레이어가 캐비넷에 숨은 경우 즉시 추격 해제 및 순찰 복귀
        if (PlayerHide.IsPlayerHiding)
        {
            if (isChasing)
            {
                isChasing = false;
                loseTimer = 0f;
                RecordVisitedPoint(transform.position);
                PickSmartPatrolPoint();
            }
        }

        if (target == null || !agent.enabled || !agent.isOnNavMesh) return;

        // 1. 공격 후 4초 정지 처리
        if (attackStopTimer > 0f)
        {
            attackStopTimer -= Time.deltaTime;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            UpdateStatsByHeartRate();
            UpdateMonsterColor();

            if (attackStopTimer <= 0f)
            {
                agent.isStopped = false;
                PickSmartPatrolPoint();
            }
            return;
        }

        // 2. 플레이어 접촉 공격 판정
        float distToPlayer = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(target.position.x, 0f, target.position.z)
        );

        if (distToPlayer <= attackRange && !PlayerHide.IsPlayerHiding)
        {
            AttackPlayer();
            return;
        }

        // 3. 시야 감지 판정
        if (IsPlayerInSight())
        {
            isChasing = true;
            isScanningRoom = false;
            if (!WaitingForDoor) agent.isStopped = false;   // 문이 열리길 기다리는 중이면 정지 유지
            loseTimer = loseTargetTime;
        }
        else if (isChasing)
        {
            loseTimer -= Time.deltaTime;
            if (loseTimer <= 0f)
            {
                isChasing = false;
                RecordVisitedPoint(transform.position);
                PickSmartPatrolPoint();
            }
        }

        // 4. 상태별 이동
        if (isChasing)
        {
            agent.SetDestination(target.position);
        }
        else
        {
            SmartPatrolBehavior();
        }

        UpdateStatsByHeartRate();
        UpdateMonsterColor();
    }

    /// <summary>잡힘 연출 중 암전 상태에서 호출: 새 위치로 안전하게 재배치하고 추격/순찰 상태를 초기화.</summary>
    public void Relocate(Vector3 pos, Quaternion rot)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        isChasing = false;
        isScanningRoom = false;
        loseTimer = 0f;
        stuckTimer = 0f;
        scanHoldTimer = 0f;
        if (doorOpener != null) doorOpener.ResetDoorState();
        attackStopTimer = attackStopDuration; // 부활 직후 잠시 정지 후 순찰 시작
        visitedHistory.Clear();
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            if (NavMesh.SamplePosition(pos, out var navHit, 2f, NavMesh.AllAreas)) pos = navHit.position;
            agent.Warp(pos);
            transform.rotation = rot;
            if (agent.isOnNavMesh)
            {
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
            else Debug.LogWarning("[MonsterChase] Relocate: NavMesh 위가 아님 " + pos);
        }
        else transform.SetPositionAndRotation(pos, rot);
        RecordVisitedPoint(transform.position);
    }

    private void AttackPlayer()
    {
        isChasing = false;
        isScanningRoom = false;
        loseTimer = 0f;
        if (doorOpener != null) doorOpener.ResetDoorState();

        // 잡힘 연출/피해 처리는 PlayerCaughtSequence가 전담 (한 번만 실행)
        PlayerCaughtSequence.TriggerCaught(transform);

        attackStopTimer = attackStopDuration;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
    }

    // HeartRate 시스템은 이번 통합에서 생략: 추격/순찰 상태에 따라 고정 속도 사용.
    private void UpdateStatsByHeartRate()
    {
        agent.speed = isChasing ? chaseSpeed : patrolSpeed;
        // 심박수가 높을수록 탐지 "거리"만 증가 (FOV/LOS/Raycast 판정은 그대로)
        var hr = HeartRateController.Instance;
        detectionMultiplier = hr != null ? hr.DetectionMultiplier : 1f;
        viewRadius = baseViewRadius * detectionMultiplier;
    }

    private void SmartPatrolBehavior()
    {
        if (isScanningRoom)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            if (currentScanIndex < scanRotations.Count)
            {
                Quaternion targetRot = scanRotations[currentScanIndex];
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                if (Quaternion.Angle(transform.rotation, targetRot) < 2.5f)
                {
                    scanHoldTimer += Time.deltaTime;
                    if (scanHoldTimer >= 0.4f)
                    {
                        scanHoldTimer = 0f;
                        currentScanIndex++;
                    }
                }
            }
            else
            {
                isScanningRoom = false;
                agent.isStopped = false;
                PickSmartPatrolPoint();
            }
            return;
        }

        // 문이 열리길 기다리는 동안은 '막힘'으로 보지 않는다 (경로를 강제로 바꾸지 않음)
        if (WaitingForDoor)
        {
            stuckTimer = 0f;
            return;
        }

        if (!agent.pathPending && agent.velocity.sqrMagnitude < 0.05f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= 0.7f)
            {
                stuckTimer = 0f;
                PickSmartPatrolPoint();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.35f)
        {
            RecordVisitedPoint(transform.position);
            TryStartOpenRoomScan();
        }
    }

    private void RecordVisitedPoint(Vector3 pos)
    {
        if (visitedHistory.Count > 0)
        {
            float distFromLast = Vector3.Distance(pos, visitedHistory[visitedHistory.Count - 1]);
            if (distFromLast < 5f) return;
        }

        visitedHistory.Add(pos);
        if (visitedHistory.Count > MAX_HISTORY)
        {
            visitedHistory.RemoveAt(0);
        }
    }

    private void TryStartOpenRoomScan()
    {
        Vector3 eyePos = transform.position + Vector3.up * 0.8f;

        if (Physics.Raycast(eyePos, transform.forward, out RaycastHit forwardHit, 2.5f))
        {
            // 앞이 통과 가능한 일반 문이면 벽으로 보지 않는다 (문을 열고 지나가야 함)
            DoorPair hitDoor = forwardHit.collider.GetComponentInParent<DoorPair>();
            if (hitDoor == null || !hitDoor.isPassableDoor)
            {
                PickSmartPatrolPoint();
                return;
            }
        }

        if (NavMesh.FindClosestEdge(transform.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
        {
            if (edgeHit.distance < 1.3f)
            {
                PickSmartPatrolPoint();
                return;
            }
        }

        scanRotations.Clear();
        float[] checkAngles = { -60f, 60f };

        foreach (float angle in checkAngles)
        {
            Quaternion rot = transform.rotation * Quaternion.Euler(0f, angle, 0f);
            Vector3 dir = rot * Vector3.forward;

            if (!Physics.Raycast(eyePos, dir, 3.0f))
            {
                scanRotations.Add(rot);
            }
        }

        if (scanRotations.Count > 0)
        {
            isScanningRoom = true;
            currentScanIndex = 0;
            scanHoldTimer = 0f;
        }
        else
        {
            PickSmartPatrolPoint();
        }
    }

    private void PickSmartPatrolPoint()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        Vector3 bestPoint = transform.position;
        float bestScore = float.MinValue;

        for (int i = 0; i < 24; i++)
        {
            float angle = i * (360f / 24f) + Random.Range(-10f, 10f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float dist = Random.Range(10f, patrolRadius);

            Vector3 candidate = transform.position + dir * dist;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                float wallDist = 0f;
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
                {
                    wallDist = edgeHit.distance;
                }
                if (wallDist < 0.8f) continue;

                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;

                float pathLength = GetPathLength(path);
                if (pathLength < 6f) continue;

                float visitPenalty = 0f;
                for (int h = 0; h < visitedHistory.Count; h++)
                {
                    float distToVisited = Vector3.Distance(hit.position, visitedHistory[h]);
                    if (distToVisited < visitedRoomRadius)
                    {
                        float recencyWeight = 1f + ((float)h / visitedHistory.Count) * 2f;
                        visitPenalty += (visitedRoomRadius - distToVisited) * 25f * recencyWeight;
                    }
                }

                float score = (pathLength * 2.5f) + (wallDist * 3f) - visitPenalty;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = hit.position;
                }
            }
        }

        if (bestScore == float.MinValue)
        {
            if (visitedHistory.Count > 2)
                visitedHistory.RemoveRange(0, visitedHistory.Count / 2);
            else
                visitedHistory.Clear();

            Vector2 rand = Random.insideUnitCircle.normalized * 12f;
            Vector3 fallback = transform.position + new Vector3(rand.x, 0f, rand.y);
            if (NavMesh.SamplePosition(fallback, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                bestPoint = hit.position;
            }
        }

        agent.SetDestination(bestPoint);
    }

    private float GetPathLength(NavMeshPath path)
    {
        float length = 0f;
        if (path.corners.Length < 2) return length;

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        }
        return length;
    }

    private bool IsPlayerInSight()
    {
        if (target == null) return false;
        // 플레이어가 캐비넷에 숨어 있으면 감지 불가
        if (PlayerHide.IsPlayerHiding) return false;

        Vector3 dirToTarget = target.position - transform.position;
        dirToTarget.y = 0;
        float distToPlayer = dirToTarget.magnitude;

        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
        Vector3 targetCenter = target.position + Vector3.up * 1.0f;
        Vector3 rayDir = (targetCenter - rayOrigin).normalized;
        float dist = Vector3.Distance(rayOrigin, targetCenter);

        // 360도 감지: 방향과 무관하게 '감지 거리' 안이면 후보 → Line of Sight(벽/문/장애물) 통과 시 감지.
        // 감지 거리 = max(심박수 연동 시야 거리, 근접 반경). (둘 다 HeartRateController.DetectionMultiplier 반영)
        float range = Mathf.Max(viewRadius, CurrentCloseRadius);
        if (distToPlayer <= range)
        {
            if (limitToFov && distToPlayer > CurrentCloseRadius)
            {
                float angle = Vector3.Angle(transform.forward, dirToTarget.normalized);
                if (angle > viewAngle / 2f) return false;
            }
            if (HasClearRaycast(rayOrigin, rayDir, dist))
                return true;
        }
        return false;
    }

    private bool HasClearRaycast(Vector3 rayOrigin, Vector3 rayDir, float dist)
    {
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDir, dist);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.collider.isTrigger) continue;

            if (hit.transform == target || hit.transform.IsChildOf(target))
            {
                return true;
            }
            return false;
        }
        return false;
    }

    private void UpdateMonsterColor()
    {
        for (int i = 0; i < monsterMaterials.Count; i++)
        {
            Material mat = monsterMaterials[i];
            if (mat == null) continue;

            Color targetColor = isChasing ? chaseColor : originalColors[i];

            if (mat.HasProperty("_BaseColor"))
            {
                Color current = mat.GetColor("_BaseColor");
                mat.SetColor("_BaseColor", Color.Lerp(current, targetColor, Time.deltaTime * colorChangeSpeed));
            }
            if (mat.HasProperty("_Color"))
            {
                mat.color = Color.Lerp(mat.color, targetColor, Time.deltaTime * colorChangeSpeed);
            }
        }
    }

    /// <summary>
    /// 감지 범위 표시(LineRenderer 등)는 게임 화면/빌드에 절대 나오면 안 된다.
    /// 구버전 씬에 남아 있는 LineRenderer / ProximityRangeIndicator 를 런타임에 제거한다.
    /// 개발용 시각화는 Scene View 전용 OnDrawGizmosSelected 로만 제공한다.
    /// </summary>
    private void RemoveRangeVisuals()
    {
        foreach (var lr in GetComponentsInChildren<LineRenderer>(true))
            Destroy(lr);
        var prox = transform.Find("ProximityRangeIndicator");
        if (prox != null) Destroy(prox.gameObject);
    }

#if UNITY_EDITOR
    // Scene View 에서 몬스터를 선택했을 때만 보이는 개발용 Gizmo (Game View / 빌드에는 그려지지 않는다).
    private void OnDrawGizmosSelected()
    {
        float mul = Application.isPlaying ? detectionMultiplier : 1f;
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 0.6f);
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.DrawWireDisc(transform.position + Vector3.up * 0.05f, Vector3.up, closeDetectRadius * mul);
        float r = baseViewRadius * mul;
        UnityEditor.Handles.color = new Color(1f, 1f, 0.3f, 0.6f);
        Vector3 l = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
        UnityEditor.Handles.DrawWireArc(transform.position + Vector3.up * 0.05f, Vector3.up, l, viewAngle, r);
        UnityEditor.Handles.DrawLine(transform.position, transform.position + l * r);
        UnityEditor.Handles.DrawLine(transform.position, transform.position + Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward * r);
    }
#endif
}
