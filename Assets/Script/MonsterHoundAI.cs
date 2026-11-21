using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 두 번째 몬스터: 실험체 E-07 "하운드" AI.
/// - 부채꼴 시야(벽에만 막히는 무제한 레이캐스트, 360도 근접 감지·심박수 배율 없음)
/// - 플레이어 발견 시 돌진 준비(02_ChargePrep) → 일직선 고속 돌진(03_Charge, 닫힌 문 강제 개방)
/// - 돌진 적중 시 덮치기 공격(05_Attack), 빗나가면 짧은 가속 추격(04_Chase) 후 회복(저속 순찰 복귀)
/// - 플레이어가 캐비넷(HideSpot)에 숨으면 Monster 1 과 같이 발각/추격을 해제하고 순찰 복귀 (프로젝트 공통 은신 규칙)
/// - 손전등 스턴: 공통 MonsterStun / FlashlightController 구조 사용 (Monster 1 과 동일 조건·배터리 소모)
/// - 추격 BGM: ChaseMusicController 가 IsChasingPlayer 를 Monster 1 과 함께 센다
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(LineRenderer))]
public class MonsterHoundAI : MonoBehaviour
{
    public enum HoundState
    {
        Patrol,
        ChargePrep,
        Charge,
        AcceleratedChase,
        Attack,
        Recovery
    }

    [Header("타겟")]
    public Transform target;

    [Header("공격 및 포획 설정")]
    public float attackRange = 1.6f;
    [Tooltip("돌진 중 플레이어(또는 숨은 캐비넷) 충돌 판정 반경")]
    public float chargeHitRadius = 1.65f;
    [Tooltip("잡힘 연출 종료 후 다음 순찰까지 멈추는 시간(초)")]
    public float attackStopDuration = 4f;
    private float attackStopTimer = 0f;

    [Header("이동 속도 (극단적 대사 변화)")]
    [Tooltip("평소 저속 순찰 속도 (m/s)")]
    public float patrolSpeed = 1.5f;
    [Tooltip("돌진 시 일직선 고속 이동 속도 (m/s) — 기존 13.0의 1.3배")]
    public float chargeSpeed = 16.9f;
    [Tooltip("돌진 후 저속 추격 속도 (m/s) — 기존 7.5의 0.8배")]
    public float acceleratedChaseSpeed = 6.0f;

    [Header("돌진 및 추격 타이밍")]
    [Tooltip("발견 후 돌진 준비 동작 지속 시간(초). 02_ChargePrep 길이(1.17s)에 맞춤")]
    public float chargePrepDuration = 1.17f;
    [Tooltip("켜면 물체/벽에 부딪힐 때까지 시간 제한 없이 일직선 돌진 유지")]
    public bool unlimitedChargeDuration = true;
    [Tooltip("unlimitedChargeDuration이 꺼져 있을 때만 쓰이는 일직선 돌진 최대 지속 시간(초)")]
    public float chargeDuration = 1.1f;
    [Tooltip("돌진 종료 후 재돌진까지 필요한 추격 쿨타임(초)")]
    public float chargeCooldownDuration = 4.0f;
    [Tooltip("(기존 호환용 유지) 추격 기본 타이머")]
    public float acceleratedChaseDuration = 4.0f;
    [Tooltip("물체에 부딪혀 멈춘 뒤 재판정까지 정지하는 시간(초)")]
    public float recoveryDuration = 1.0f;
    [Tooltip("추격 중 시야가 벽에 가려졌을 때 순찰 복귀 전 짧은 유예(초)")]
    public float chaseLoseSightGrace = 1.2f;
    [Tooltip("돌진 준비 중 플레이어 방향 조준 회전 속도 (도/초)")]
    public float prepTurnSpeed = 420f;
    [Tooltip("돌진 중 닫힌 문을 강제로 열어젖히는 시간(초)")]
    public float doorBurstDuration = 0.12f;
    [Tooltip("이 값 이상의 충돌 각도(cos)로 물체/벽에 부딪히면 돌진 중단")]
    [Range(0.15f, 0.95f)]
    public float wallStopImpactDot = 0.25f;

    [Header("부채꼴 시야 설정 (벽에 막히는 무제한 레이캐스트)")]
    [Tooltip("켜면 거리 제한 없이 maxRaycastDistance까지 벽에만 막히는 레이캐스트 사용")]
    public bool unlimitedViewDistance = true;
    [Tooltip("무제한 시야 모드 시 레이캐스트 최대 거리(m)")]
    public float maxRaycastDistance = 180f;
    [Tooltip("unlimitedViewDistance가 꺼져 있을 때만 사용되는 시야 거리(m)")]
    public float fallbackViewRadius = 35f;
    [Range(0f, 360f)]
    public float viewAngle = 75f;
    [Tooltip("바닥 시야 부채꼴(LineRenderer) 표시 최대 거리(m)")]
    public float visualFovDistance = 30f;

    [Header("시각 연출 (시야 부채꼴 & 발광 눈)")]
    [Tooltip("프로젝트 규칙: 감지 범위 표시는 게임 화면에 나오면 안 된다. 개발 확인용으로만 켠다.")]
    public bool showFovIndicator = false;
    public Color patrolFovColor = new Color(0.75f, 0.2f, 0.45f, 0.45f);
    public Color alertFovColor = new Color(1f, 0.12f, 0.12f, 0.78f);
    public Color alertEyeEmission = new Color(8f, 1.2f, 0.4f, 1f);
    public float visualLerpSpeed = 6f;

    [Header("스마트 광역 순찰 설정")]
    public float patrolRadius = 28f;
    public float visitedRoomRadius = 12f;
    public float rotationSpeed = 120f;

    public HoundState CurrentState => state;
    public bool IsAlerted => state == HoundState.ChargePrep || state == HoundState.Charge || state == HoundState.AcceleratedChase || state == HoundState.Attack;
    /// <summary>플레이어를 발견해 추격 중인가 (추격 BGM 카운트용. 공격(잡힘) 중에는 false — MonsterChase.IsChasing 와 같은 의미).</summary>
    public bool IsChasingPlayer => hasSpottedPlayer && state != HoundState.Attack;

    private MonsterStun stun;

    private NavMeshAgent agent;
    private LineRenderer lineRenderer;
    private MonsterDoorOpener doorOpener;

    private HoundState state = HoundState.Patrol;
    private bool hasSpottedPlayer = false;
    private float stateTimer = 0f;
    private float chargeElapsedTime = 0f;
    private float chargeCooldownTimer = 0f;
    private float chaseLoseSightTimer = 0f;
    private bool recoveryFromAttack = false;
    private bool shouldTurnInRecovery = false;
    private Vector3 recoveryTurnDir = Vector3.forward;
    private Vector3 chargeDirection = Vector3.forward;
    private Vector3 lastKnownTargetPos;
    private bool hasLastKnownPos = false;
    private bool attackTriggeredThisCycle = false;

    private readonly List<Vector3> visitedHistory = new List<Vector3>();
    private const int MAX_HISTORY = 12;
    private bool isScanningRoom = false;
    private readonly List<Quaternion> scanRotations = new List<Quaternion>();
    private int currentScanIndex = 0;
    private float scanHoldTimer = 0f;
    private float stuckTimer = 0f;

    private readonly RaycastHit[] rayBuffer = new RaycastHit[32];
    private readonly Collider[] doorOverlapBuffer = new Collider[16];
    private readonly List<Material> eyeMaterials = new List<Material>();
    private readonly List<Color> baseEyeEmissions = new List<Color>();

    private float EffectiveViewDistance => unlimitedViewDistance ? maxRaycastDistance : fallbackViewRadius;

    void Awake()
    {
        // 손전등 스턴: Monster 1 과 같은 공통 MonsterStun / FlashlightController 구조를 그대로 사용한다.
        stun = GetComponent<MonsterStun>();
        if (stun == null) stun = gameObject.AddComponent<MonsterStun>();
        stun.Stunned += OnStunned;
    }

    void OnDestroy()
    {
        if (stun != null) stun.Stunned -= OnStunned;
    }

    /// <summary>스턴 시작: 돌진 준비/돌진은 즉시 중단하고 Recovery 로 전환 → 스턴 종료 후 기존 로직대로 추격/순찰 복귀.</summary>
    private void OnStunned(MonsterStun _)
    {
        if (state == HoundState.ChargePrep || state == HoundState.Charge)
            EnterRecovery(fromAttack: false, impactNormal: -chargeDirection);
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();

        doorOpener = GetComponent<MonsterDoorOpener>();
        if (doorOpener == null)
            doorOpener = gameObject.AddComponent<MonsterDoorOpener>();

        agent.angularSpeed = 360f;
        agent.acceleration = 16f;
        agent.autoBraking = true;
        agent.speed = patrolSpeed;

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.enabled = showFovIndicator;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }

        CacheEyeMaterials();

        if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 20f, NavMesh.AllAreas))
            agent.Warp(hit.position);

        RecordVisitedPoint(transform.position);
        EnterPatrol(pickNewPoint: true);
    }

    void CacheEyeMaterials()
    {
        eyeMaterials.Clear();
        baseEyeEmissions.Clear();

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer) continue;
            foreach (Material mat in r.materials)
            {
                if (mat == null) continue;
                if (mat.name.IndexOf("GlowingEye", StringComparison.OrdinalIgnoreCase) >= 0 && mat.HasProperty("_EmissionColor"))
                {
                    eyeMaterials.Add(mat);
                    baseEyeEmissions.Add(mat.GetColor("_EmissionColor"));
                }
            }
        }
    }

    void Update()
    {
        DrawFieldOfView();
        UpdateVisualFeedback();

        // 잡힘 연출 진행 중에는 AI 일시 정지 (PlayerCaughtSequence가 위치/시선 연출 제어)
        if (PlayerCaughtSequence.IsRunning)
        {
            if (state == HoundState.Attack || IsAlerted)
            {
                attackStopTimer = attackStopDuration;
            }
            if (doorOpener != null) doorOpener.ResetDoorState();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            return;
        }

        if (target == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        // 은신 공통 규칙(Monster 1 과 동일): 플레이어가 캐비넷에 숨으면 발각 상태를 즉시 해제하고 순찰로 복귀.
        // (원본 Hound 의 '발각 후 숨어도 추적 유지'보다 DEAD SIGNAL 공통 은신 규칙을 우선)
        if (PlayerHide.IsPlayerHiding && hasSpottedPlayer && state != HoundState.Attack)
        {
            hasSpottedPlayer = false;
            if (state == HoundState.ChargePrep || state == HoundState.AcceleratedChase ||
                (state == HoundState.Recovery && !recoveryFromAttack))
                EnterPatrol(pickNewPoint: true);
        }

        // 포획 후 잠시 정지 상태 처리
        if (attackStopTimer > 0f)
        {
            attackStopTimer -= Time.deltaTime;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            if (attackStopTimer <= 0f)
            {
                EnterPatrol(pickNewPoint: true);
            }
            return;
        }

        // 순찰 또는 추격 중에만 돌진 재사용 쿨타임 감소 (충돌 직후 1초 멈춤 동안에는 쿨타임 차감 안 함)
        if (chargeCooldownTimer > 0f && (state == HoundState.Patrol || state == HoundState.AcceleratedChase))
        {
            chargeCooldownTimer = Mathf.Max(0f, chargeCooldownTimer - Time.deltaTime);
        }

        switch (state)
        {
            case HoundState.Patrol:
                UpdatePatrol();
                break;
            case HoundState.ChargePrep:
                UpdateChargePrep();
                break;
            case HoundState.Charge:
                UpdateCharge();
                break;
            case HoundState.AcceleratedChase:
                UpdateAcceleratedChase();
                break;
            case HoundState.Attack:
                UpdateAttack();
                break;
            case HoundState.Recovery:
                UpdateRecovery();
                break;
        }
    }

    // ───────────────────────── 상태 진입 ─────────────────────────

    private void EnterPatrol(bool pickNewPoint)
    {
        state = HoundState.Patrol;
        hasSpottedPlayer = false;
        isScanningRoom = false;
        attackTriggeredThisCycle = false;

        if (doorOpener != null) doorOpener.enabled = true;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = patrolSpeed;
            if (pickNewPoint) PickSmartPatrolPoint();
        }
    }

    private void EnterChargePrep()
    {
        state = HoundState.ChargePrep;
        hasSpottedPlayer = true;
        isScanningRoom = false;
        stateTimer = chargePrepDuration;
        attackTriggeredThisCycle = false;

        Vector3 aimTarget = GetEffectiveTargetPosition();
        lastKnownTargetPos = aimTarget;
        hasLastKnownPos = true;

        Vector3 dir = aimTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) chargeDirection = dir.normalized;
        else chargeDirection = transform.forward;

        if (doorOpener != null)
        {
            doorOpener.ResetDoorState();
            doorOpener.enabled = false;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.updateRotation = true;
        }
    }

    private void EnterCharge()
    {
        state = HoundState.Charge;
        hasSpottedPlayer = true;
        chargeElapsedTime = 0f;
        stateTimer = chargeDuration;

        // 돌진 시작 순간 최종 직선 방향 확정
        Vector3 aimPos = hasLastKnownPos ? lastKnownTargetPos : GetEffectiveTargetPosition();
        Vector3 dir = aimPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            chargeDirection = dir.normalized;
        else
            chargeDirection = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        if (chargeDirection.sqrMagnitude < 0.001f) chargeDirection = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(chargeDirection, Vector3.up);

        // 돌진 중에는 DoorOpener가 문 앞에서 멈추지 않도록 잠시 끄고, 직접 문을 열어젖힘
        if (doorOpener != null)
        {
            doorOpener.ResetDoorState();
            doorOpener.enabled = false;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.updateRotation = false;
        }
    }

    private void EnterAcceleratedChase()
    {
        state = HoundState.AcceleratedChase;
        hasSpottedPlayer = true;
        chaseLoseSightTimer = 0f;
        stateTimer = acceleratedChaseDuration;

        if (doorOpener != null) doorOpener.enabled = true;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = acceleratedChaseSpeed;
            agent.SetDestination(GetChaseNavDestination());
        }
    }

    private void EnterAttack()
    {
        if (attackTriggeredThisCycle) return;
        attackTriggeredThisCycle = true;
        hasSpottedPlayer = false;
        state = HoundState.Attack;
        stateTimer = 0.2f;

        if (doorOpener != null)
        {
            doorOpener.ResetDoorState();
            doorOpener.enabled = true;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.updateRotation = true;
        }

        // 캐비넷 등에 숨은 상태라면 강제로 밖으로 꺼낸 뒤 기존 잡힘 연출 실행
        if (PlayerHide.IsPlayerHiding && PlayerHide.Instance != null)
        {
            PlayerHide.Instance.Exit();
        }

        attackStopTimer = attackStopDuration;
        PlayerCaughtSequence.TriggerCaught(transform);
    }

    private void EnterRecovery(bool fromAttack = false, Vector3? impactNormal = null)
    {
        state = HoundState.Recovery;
        recoveryFromAttack = fromAttack;
        isScanningRoom = false;
        stateTimer = recoveryDuration;

        if (fromAttack)
        {
            hasSpottedPlayer = false;
            shouldTurnInRecovery = false;
        }
        else
        {
            // 돌진 충돌 후 1초 멈춤 진입 → 쿨타임 4초 설정 (정지 해제 후 추격/순찰 시 차감)
            chargeCooldownTimer = chargeCooldownDuration;

            Vector3 probeOrigin = transform.position + Vector3.up * 0.75f;
            bool wallRightAhead = impactNormal.HasValue || Physics.SphereCast(probeOrigin, 0.35f, transform.forward, out _, 2.2f, ~0, QueryTriggerInteraction.Ignore);

            Vector3 awayDir = impactNormal.HasValue && impactNormal.Value.sqrMagnitude > 0.01f
                ? Vector3.Reflect(chargeDirection, impactNormal.Value.normalized)
                : -chargeDirection;
            awayDir.y = 0f;
            if (awayDir.sqrMagnitude < 0.01f || Vector3.Dot(awayDir.normalized, chargeDirection) > 0.6f)
                awayDir = -chargeDirection;

            Vector3 lookBack = awayDir.normalized;
            if (hasLastKnownPos)
            {
                Vector3 toLast = lastKnownTargetPos - transform.position;
                toLast.y = 0f;
                if (toLast.sqrMagnitude > 0.25f && Vector3.Dot(toLast.normalized, chargeDirection) < 0.25f)
                    lookBack = toLast.normalized;
            }
            recoveryTurnDir = lookBack;
            shouldTurnInRecovery = wallRightAhead;
        }

        if (doorOpener != null) doorOpener.enabled = true;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
            agent.updateRotation = true;
            agent.speed = patrolSpeed;
        }
    }

    // ───────────────────────── 상태 업데이트 ─────────────────────────

    private void UpdatePatrol()
    {
        agent.speed = patrolSpeed;

        // 부채꼴 시야(무제한 레이캐스트, 벽에 막힘)로 플레이어 포착
        if (IsPlayerInSight(allowHiddenIfAlerted: false))
        {
            if (chargeCooldownTimer <= 0f)
                EnterChargePrep();
            else
                EnterAcceleratedChase();
            return;
        }

        SmartPatrolBehavior();
    }

    private void UpdateChargePrep()
    {
        stateTimer -= Time.deltaTime;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        // 이미 발각된 상태에서는 캐비넷에 숨더라도 계속 조준/추적
        if (IsPlayerInSight(allowHiddenIfAlerted: true) || PlayerHide.IsPlayerHiding)
        {
            lastKnownTargetPos = GetEffectiveTargetPosition();
            hasLastKnownPos = true;
        }

        Vector3 toAim = (hasLastKnownPos ? lastKnownTargetPos : GetEffectiveTargetPosition()) - transform.position;
        toAim.y = 0f;
        if (toAim.sqrMagnitude > 0.001f)
        {
            chargeDirection = toAim.normalized;
            Quaternion targetRot = Quaternion.LookRotation(chargeDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, prepTurnSpeed * Time.deltaTime);
        }

        if (stateTimer <= 0f)
        {
            EnterCharge();
        }
    }

    private void UpdateCharge()
    {
        float dt = Time.deltaTime;
        chargeElapsedTime += dt;
        stateTimer -= dt;
        float stepDist = chargeSpeed * dt;

        transform.rotation = Quaternion.LookRotation(chargeDirection, Vector3.up);

        // 1. 전방 닫힌 통과 가능 문(DoorPair) 감지 시 강제로 열어젖히며 통과
        BurstOpenDoorsAhead(stepDist + 1.6f);

        // 2. 전방 물체/벽 충돌 검사 (통과 가능한 문짝·플레이어·숨은 캐비넷 제외)
        bool hitWall = CheckChargeWallCollision(stepDist, out float allowedStep, out Vector3 hitNormal);
        float moveDist = hitWall ? Mathf.Max(0f, allowedStep) : stepDist;

        Vector3 startPos = transform.position;
        if (moveDist > 0.0001f)
        {
            agent.Move(chargeDirection * moveDist);
        }
        Vector3 endPos = transform.position;

        // 3. 돌진 중 몸이 직접 닿은 경우에만 공격(포획) 발동 (감지 범위 진입만으로 추격 전환 절대 금지)
        if (IsPlayerHitAlongSegment(startPos, endPos, chargeHitRadius))
        {
            EnterAttack();
            return;
        }

        // 4. 물체/벽 충돌 또는 NavMesh 막힘(또는 옵션상 시간 만료) 시 즉시 1초 정지(Recovery) 진입
        float actualFlatMove = HorizontalDistance(startPos, endPos);
        bool stalledOnNavEdge = chargeElapsedTime > 0.10f && stepDist > 0.01f && actualFlatMove < stepDist * 0.20f;
        bool timeExpired = !unlimitedChargeDuration && stateTimer <= 0f;

        if (hitWall || stalledOnNavEdge || timeExpired)
        {
            EnterRecovery(fromAttack: false, impactNormal: hitWall ? hitNormal : -chargeDirection);
        }
    }

    private void UpdateAcceleratedChase()
    {
        agent.isStopped = false;
        agent.speed = acceleratedChaseSpeed;

        bool inSight = IsPlayerInSight(allowHiddenIfAlerted: true);
        if (inSight)
        {
            chaseLoseSightTimer = 0f;
            lastKnownTargetPos = GetEffectiveTargetPosition();
            hasLastKnownPos = true;
        }
        else
        {
            chaseLoseSightTimer += Time.deltaTime;
        }

        Vector3 chaseDest = GetChaseNavDestination();
        agent.SetDestination(chaseDest);

        if (CanReachAndAttackPlayer(attackRange))
        {
            EnterAttack();
            return;
        }

        // 부채꼴 시야(벽에 막힘) 안에서 추격 중 돌진 쿨타임이 다 돌면 다시 돌진
        if (inSight && chargeCooldownTimer <= 0f)
        {
            EnterChargePrep();
            return;
        }

        // 시야에서 벗어나 유예 시간 초과 시 순찰 복귀
        if (!inSight && chaseLoseSightTimer >= chaseLoseSightGrace)
        {
            EnterPatrol(pickNewPoint: true);
        }
    }

    private void UpdateAttack()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f && !PlayerCaughtSequence.IsRunning)
        {
            EnterRecovery(fromAttack: true);
        }
    }

    private void UpdateRecovery()
    {
        stateTimer -= Time.deltaTime;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        // 충돌 직후 0.45초는 멈춰 있다가 정지 후반부(0.55초)에 열린 복도 방향으로 회전 완료
        if (shouldTurnInRecovery && recoveryDuration > 0.1f && stateTimer <= recoveryDuration * 0.55f)
        {
            Quaternion targetRot = Quaternion.LookRotation(recoveryTurnDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, prepTurnSpeed * 0.90f * Time.deltaTime);
        }

        if (stateTimer <= 0f)
        {
            // 1초 멈춤 후에도 원래 부채꼴 시야(거리 무제한, 벽에 막힘) 안에 있으면 저속 추격, 아니면 순찰 복귀
            if (!recoveryFromAttack && IsPlayerInSight(allowHiddenIfAlerted: hasSpottedPlayer))
            {
                EnterAcceleratedChase();
            }
            else
            {
                EnterPatrol(pickNewPoint: true);
            }
        }
    }

    // ───────────────────────── 돌진 보조 판정 ─────────────────────────

    private void BurstOpenDoorsAhead(float checkDistance)
    {
        Vector3 center = transform.position + Vector3.up * 1.0f + chargeDirection * (checkDistance * 0.5f);
        float radius = Mathf.Max(agent.radius + 0.35f, checkDistance * 0.55f);
        int count = Physics.OverlapSphereNonAlloc(center, radius, doorOverlapBuffer, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider col = doorOverlapBuffer[i];
            if (col == null) continue;

            DoorPair door = col.GetComponentInParent<DoorPair>();
            if (door == null || !door.isPassableDoor) continue;

            Vector3 toDoor = door.GetDoorCenter() - transform.position;
            toDoor.y = 0f;
            if (toDoor.sqrMagnitude > 0.01f && Vector3.Dot(chargeDirection, toDoor.normalized) < -0.1f)
                continue;

            if (!door.IsFullyOpen)
                door.BurstOpen(doorBurstDuration);
        }
    }

    private bool CheckChargeWallCollision(float stepDist, out float safeMoveDist, out Vector3 hitNormal)
    {
        safeMoveDist = stepDist;
        hitNormal = -chargeDirection;

        Vector3 pBottom = transform.position + Vector3.up * 0.45f;
        Vector3 pTop = transform.position + Vector3.up * 1.15f;
        float castRadius = Mathf.Max(0.25f, agent.radius * 0.72f);
        float castDist = stepDist + 0.30f;

        int hitCount = Physics.CapsuleCastNonAlloc(pBottom, pTop, castRadius, chargeDirection, rayBuffer, castDist, ~0, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0) return false;

        Array.Sort(rayBuffer, 0, hitCount, HitDistanceComparer.Instance);

        HideSpot hidingSpot = PlayerHide.IsPlayerHiding && PlayerHide.Instance != null ? PlayerHide.Instance.CurrentSpot : null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = rayBuffer[i];
            Transform ht = hit.transform;
            if (ht == transform || ht.IsChildOf(transform)) continue;
            if (target != null && (ht == target || ht.IsChildOf(target))) continue;

            // 이미 발각된 상태에서 플레이어가 숨어있는 캐비넷은 벽으로 치지 않고 돌진 타격 대상으로 처리
            if (hasSpottedPlayer && hidingSpot != null && (ht == hidingSpot.transform || ht.IsChildOf(hidingSpot.transform)))
                continue;

            // 통과 가능한 문(DoorPair)의 문짝(leafL/leafR)은 강제로 열어젖히며 통과하므로 물체 충돌 제외
            DoorPair door = ht.GetComponentInParent<DoorPair>();
            if (door != null && door.isPassableDoor && IsDoorLeafTransform(door, ht))
            {
                if (!door.IsFullyOpen) door.BurstOpen(doorBurstDuration);
                continue;
            }

            // 바닥/경사로 등 수평면 오탐 제외
            if (Mathf.Abs(hit.normal.y) > 0.7f) continue;

            Vector3 flatNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (flatNormal.sqrMagnitude < 0.04f) continue;

            flatNormal.Normalize();
            float impactDot = Vector3.Dot(-flatNormal, chargeDirection);
            if (impactDot < wallStopImpactDot) continue;

            hitNormal = flatNormal;
            safeMoveDist = Mathf.Max(0f, hit.distance - 0.1f);
            return true;
        }

        return false;
    }

    private static bool IsDoorLeafTransform(DoorPair door, Transform t)
    {
        if (door.leafL != null && (t == door.leafL.transform || t.IsChildOf(door.leafL.transform))) return true;
        if (door.leafR != null && (t == door.leafR.transform || t.IsChildOf(door.leafR.transform))) return true;
        return false;
    }

    private bool IsPlayerHitAlongSegment(Vector3 segStart, Vector3 segEnd, float hitRadius)
    {
        if (target == null) return false;
        if (PlayerHide.IsPlayerHiding) return false;   // 숨은 플레이어는 캐비넷을 관통해 공격하지 않음

        float dPlayer = DistancePointToSegmentXZ(target.position, segStart, segEnd);
        if (dPlayer <= hitRadius && HasClearSightToPoint(target.position + Vector3.up * 1.0f, allowCurrentHideSpot: hasSpottedPlayer))
            return true;

        if (hasSpottedPlayer && PlayerHide.IsPlayerHiding && PlayerHide.Instance != null && PlayerHide.Instance.CurrentSpot != null)
        {
            HideSpot spot = PlayerHide.Instance.CurrentSpot;
            if (spot.exitPoint != null && DistancePointToSegmentXZ(spot.exitPoint.position, segStart, segEnd) <= hitRadius)
                return true;
            if (DistancePointToSegmentXZ(spot.transform.position, segStart, segEnd) <= hitRadius + 0.35f)
                return true;
        }

        return false;
    }

    private bool CanReachAndAttackPlayer(float range)
    {
        if (target == null) return false;
        if (PlayerHide.IsPlayerHiding) return false;   // 숨은 플레이어는 공격 대상 아님 (Monster 1 과 동일)

        if (HorizontalDistance(transform.position, target.position) <= range)
        {
            if (HasClearSightToPoint(target.position + Vector3.up * 1.0f, allowCurrentHideSpot: hasSpottedPlayer))
                return true;
        }

        if (hasSpottedPlayer && PlayerHide.IsPlayerHiding && PlayerHide.Instance != null && PlayerHide.Instance.CurrentSpot != null)
        {
            HideSpot spot = PlayerHide.Instance.CurrentSpot;
            if (spot.exitPoint != null && HorizontalDistance(transform.position, spot.exitPoint.position) <= range)
                return true;
        }

        return false;
    }

    // ───────────────────────── 시야 & 캐비넷 판정 ─────────────────────────

    private bool IsPlayerInSight(bool allowHiddenIfAlerted)
    {
        if (target == null) return false;

        // 발각 전에 미리 캐비넷에 숨어있다면 안전
        if (PlayerHide.IsPlayerHiding && !(allowHiddenIfAlerted && hasSpottedPlayer))
            return false;

        Vector3 targetPos = GetEffectiveTargetPosition();
        Vector3 dirToTarget = targetPos - transform.position;
        dirToTarget.y = 0f;
        float flatDist = dirToTarget.magnitude;

        if (flatDist > EffectiveViewDistance) return false;

        if (flatDist > 0.05f)
        {
            float angle = Vector3.Angle(transform.forward, dirToTarget / flatDist);
            if (angle > viewAngle * 0.5f) return false;
        }

        return HasClearSightToPoint(GetEffectiveTargetSightPoint(), allowCurrentHideSpot: allowHiddenIfAlerted && hasSpottedPlayer);
    }

    private bool HasClearSightToPoint(Vector3 targetPoint, bool allowCurrentHideSpot)
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1.15f;
        Vector3 delta = targetPoint - rayOrigin;
        float dist = delta.magnitude;
        if (dist <= 0.05f) return true;
        Vector3 rayDir = delta / dist;

        int count = Physics.RaycastNonAlloc(rayOrigin, rayDir, rayBuffer, dist, ~0, QueryTriggerInteraction.Ignore);
        if (count == 0) return true;

        Array.Sort(rayBuffer, 0, count, HitDistanceComparer.Instance);
        HideSpot hidingSpot = allowCurrentHideSpot && PlayerHide.IsPlayerHiding && PlayerHide.Instance != null
            ? PlayerHide.Instance.CurrentSpot
            : null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = rayBuffer[i];
            Transform ht = hit.transform;
            if (ht == transform || ht.IsChildOf(transform)) continue;

            if (target != null && (ht == target || ht.IsChildOf(target)))
                return true;

            // 이미 발각된 상태에서 플레이어가 들어간 캐비넷의 콜라이더는 투과
            if (hidingSpot != null && (ht == hidingSpot.transform || ht.IsChildOf(hidingSpot.transform)))
                continue;

            return false;
        }

        return true;
    }

    private Vector3 GetEffectiveTargetPosition()
    {
        if (PlayerHide.IsPlayerHiding && PlayerHide.Instance != null && PlayerHide.Instance.CurrentSpot != null)
        {
            HideSpot spot = PlayerHide.Instance.CurrentSpot;
            if (spot.viewPoint != null) return spot.viewPoint.position;
            if (spot.hidePoint != null) return spot.hidePoint.position;
        }
        return target != null ? target.position : transform.position;
    }

    private Vector3 GetEffectiveTargetSightPoint()
    {
        if (PlayerHide.IsPlayerHiding && PlayerHide.Instance != null && PlayerHide.Instance.CurrentSpot != null)
        {
            HideSpot spot = PlayerHide.Instance.CurrentSpot;
            if (spot.viewPoint != null) return spot.viewPoint.position;
            if (spot.hidePoint != null) return spot.hidePoint.position + Vector3.up * 0.8f;
            return spot.transform.position + Vector3.up * 1.15f;
        }
        return target != null ? target.position + Vector3.up * 1.0f : transform.position + Vector3.up * 1.0f;
    }

    private Vector3 GetChaseNavDestination()
    {
        if (hasSpottedPlayer && PlayerHide.IsPlayerHiding && PlayerHide.Instance != null && PlayerHide.Instance.CurrentSpot != null)
        {
            HideSpot spot = PlayerHide.Instance.CurrentSpot;
            if (spot.exitPoint != null) return spot.exitPoint.position;
        }
        return target != null ? target.position : transform.position;
    }

    // ───────────────────────── 재배치 (MonsterSpawnPoints 연동) ─────────────────────────

    /// <summary>잡힘 연출 중 암전 상태에서 호출: 새 위치로 안전하게 재배치하고 상태를 초기화.</summary>
    public void Relocate(Vector3 pos, Quaternion rot)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (doorOpener != null)
        {
            doorOpener.ResetDoorState();
            doorOpener.enabled = true;
        }

        state = HoundState.Patrol;
        hasSpottedPlayer = false;
        isScanningRoom = false;
        attackTriggeredThisCycle = false;
        recoveryFromAttack = false;
        shouldTurnInRecovery = false;
        stateTimer = 0f;
        chargeElapsedTime = 0f;
        chargeCooldownTimer = 0f;
        chaseLoseSightTimer = 0f;
        stuckTimer = 0f;
        scanHoldTimer = 0f;
        attackStopTimer = attackStopDuration;
        visitedHistory.Clear();

        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                pos = navHit.position;
            agent.Warp(pos);
            transform.rotation = rot;
            if (agent.isOnNavMesh)
            {
                agent.updateRotation = true;
                agent.velocity = Vector3.zero;
                agent.isStopped = true;
            }
            else
            {
                Debug.LogWarning("[MonsterHoundAI] Relocate: NavMesh 위가 아님 " + pos);
            }
        }
        else
        {
            transform.SetPositionAndRotation(pos, rot);
        }

        RecordVisitedPoint(transform.position);
    }

    // ───────────────────────── 스마트 광역 순찰 (1번 몬스터 참고) ─────────────────────────

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
                    if (scanHoldTimer >= 0.5f)
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

        if (doorOpener != null && doorOpener.IsWaitingForDoor)
        {
            stuckTimer = 0f;
            return;
        }

        if (!agent.pathPending && agent.velocity.sqrMagnitude < 0.04f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= 0.8f)
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
            visitedHistory.RemoveAt(0);
    }

    private void TryStartOpenRoomScan()
    {
        Vector3 eyePos = transform.position + Vector3.up * 1.0f;

        if (Physics.Raycast(eyePos, transform.forward, out RaycastHit forwardHit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
        {
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

            if (!Physics.Raycast(eyePos, dir, 3.0f, ~0, QueryTriggerInteraction.Ignore))
                scanRotations.Add(rot);
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
            float angle = i * (360f / 24f) + UnityEngine.Random.Range(-10f, 10f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float dist = UnityEngine.Random.Range(10f, patrolRadius);
            Vector3 candidate = transform.position + dir * dist;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                float wallDist = 0f;
                if (NavMesh.FindClosestEdge(hit.position, out NavMeshHit edgeHit, NavMesh.AllAreas))
                    wallDist = edgeHit.distance;
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

            Vector2 rand = UnityEngine.Random.insideUnitCircle.normalized * 12f;
            Vector3 fallback = transform.position + new Vector3(rand.x, 0f, rand.y);
            if (NavMesh.SamplePosition(fallback, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                bestPoint = hit.position;
        }

        agent.SetDestination(bestPoint);
    }

    private static float GetPathLength(NavMeshPath path)
    {
        float length = 0f;
        if (path.corners.Length < 2) return length;
        for (int i = 0; i < path.corners.Length - 1; i++)
            length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        return length;
    }

    // ───────────────────────── 시각 연출 ─────────────────────────

    private void UpdateVisualFeedback()
    {
        bool alert = IsAlerted;
        Color targetFov = alert ? alertFovColor : patrolFovColor;
        if (lineRenderer != null)
        {
            Color cur = Color.Lerp(lineRenderer.startColor, targetFov, Time.deltaTime * visualLerpSpeed);
            lineRenderer.startColor = cur;
            lineRenderer.endColor = cur;
        }

        for (int i = 0; i < eyeMaterials.Count; i++)
        {
            Material mat = eyeMaterials[i];
            if (mat == null) continue;
            Color targetEm = alert ? alertEyeEmission : baseEyeEmissions[i];
            Color curEm = mat.GetColor("_EmissionColor");
            mat.SetColor("_EmissionColor", Color.Lerp(curEm, targetEm, Time.deltaTime * visualLerpSpeed));
        }
    }

    private void DrawFieldOfView()
    {
        if (lineRenderer == null || !showFovIndicator) return;

        int segments = 32;
        lineRenderer.positionCount = segments + 2;

        Vector3[] points = new Vector3[segments + 2];
        points[0] = new Vector3(0f, 0.05f, 0f);

        float maxDrawDist = Mathf.Min(visualFovDistance, EffectiveViewDistance);
        float startAngle = -viewAngle * 0.5f;
        float angleStep = viewAngle / segments;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.8f;

        HideSpot hidingSpot = hasSpottedPlayer && PlayerHide.IsPlayerHiding && PlayerHide.Instance != null
            ? PlayerHide.Instance.CurrentSpot
            : null;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Vector3 localDir = Quaternion.Euler(0f, currentAngle, 0f) * Vector3.forward;
            Vector3 worldDir = transform.TransformDirection(localDir);

            float hitDist = maxDrawDist;
            int hitCount = Physics.RaycastNonAlloc(rayOrigin, worldDir, rayBuffer, maxDrawDist, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                Array.Sort(rayBuffer, 0, hitCount, HitDistanceComparer.Instance);
                for (int h = 0; h < hitCount; h++)
                {
                    Transform ht = rayBuffer[h].transform;
                    if (ht == transform || ht.IsChildOf(transform)) continue;
                    if (target != null && (ht == target || ht.IsChildOf(target))) continue;
                    if (hidingSpot != null && (ht == hidingSpot.transform || ht.IsChildOf(hidingSpot.transform))) continue;

                    hitDist = rayBuffer[h].distance;
                    break;
                }
            }

            points[i + 1] = localDir * hitDist + new Vector3(0f, 0.05f, 0f);
        }

        lineRenderer.SetPositions(points);
    }

    // ───────────────────────── 유틸리티 ─────────────────────────

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float DistancePointToSegmentXZ(Vector3 point, Vector3 segA, Vector3 segB)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 a = new Vector2(segA.x, segA.z);
        Vector2 b = new Vector2(segB.x, segB.z);
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-6f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return Vector2.Distance(p, a + ab * t);
    }

    private sealed class HitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
