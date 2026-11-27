using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 비콘이 정확히 2개 활성화되는 순간(GameProgress, 튜토리얼 비콘 제외) 한 번만 재생되는 E-07 Hound(Monster 2) 등장 컷씬.
/// ExitCutsceneController 와 같은 방식: 조작 잠금 → HUD 숨김 → 몬스터 AI 정지 → 시네마 카메라 →
/// 침대더미를 부수고 Hound 가 방 밖으로 뛰쳐나옴(카메라 흔들림/파편) → 플레이어 카메라 복귀 → HUD/조작 복구 → Hound AI 활성화.
///
/// 해제 전의 Hound: AI(MonsterHoundAI) 비활성 + 침대더미 Collider 로 물리 차단 + NavMeshObstacle(carve) 로 경로 차단.
///   (침대더미는 NavMesh Bake 에서 Ignore(NavMeshModifier) → 파괴 후 baked NavMesh 가 문 앞을 그대로 연결한다.)
/// 파괴 순간: 침대더미가 실제 Rigidbody(중력) 로 날아가 바닥에 떨어지고 Collider 는 유지(통행 차단), 장애물(NavMeshObstacle) 비활성 → Hound 가 정상 pathfinding 가능.
///   착지한 침대는 kinematic 으로 고정 + (Hound 경로를 막지 않을 때만) carve NavMeshObstacle 로 복도의 실제 장애물이 된다.
/// 씬 재시작(Retry) 시 씬이 다시 로드되므로 Release 상태도 초기 상태로 돌아간다.
/// </summary>
public class Monster2ReleaseSequence : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }
    /// <summary>이번 씬에서 Hound 가 이미 풀렸는지.</summary>
    public static bool IsReleased { get; private set; }

    [Header("대상")]
    public MonsterHoundAI hound;
    [Tooltip("침대더미를 이루는 오브젝트들 (bed_010 (5) 등). 파괴 순간 Collider 제거 + 파편 이동.")]
    public Transform[] bedPile;
    [Tooltip("해제 전 경로를 막는 NavMeshObstacle(carve). 파괴 순간 비활성.")]
    public NavMeshObstacle navBarrier;

    [Header("시네마 카메라")]
    public Camera cutsceneCamera;
    [Tooltip("카메라 시작/끝 위치 (시선은 lookTarget 쪽).")]
    public Transform camStart, camEnd, lookTarget;

    [Header("Hound 연출")]
    [Tooltip("문 앞(방 밖) 도착 지점. NavMesh 위여야 한다.")]
    public Transform houndExitPoint;
    public float roarTime = 1.1f;
    public float burstTime = 0.75f;
    public float holdAfter = 1.0f;
    public float fadeTime = 0.25f;
    public float shakeAmplitude = 0.22f;

    [Header("침대 비산 (실제 물리)")]
    [Tooltip("수평 초속(m/s) 최소/최대. 클수록 멀리 날아간다.")]
    public Vector2 launchSpeed = new Vector2(7f, 11f);
    [Tooltip("위로 향하는 초속(m/s) 최소/최대.")]
    public Vector2 launchUp = new Vector2(3f, 6f);
    [Tooltip("비산 방향 편차(도). Hound 가 달려 나가는 방향(수평) 기준, +는 북쪽(카메라 반대편).")]
    public Vector2 launchYaw = new Vector2(8f, 30f);
    [Tooltip("착지 후 정착(sleep)을 기다리는 최대 시간(초). 암전 중에 기다린다.")]
    public float settleMaxSeconds = 3f;
    [Tooltip("Hound 가 달려 나가는 직선 통로 반폭. 이 통로로 떨어질 침대는 방향을 틀고, 통로에 정착한 침대는 NavMeshObstacle 을 만들지 않는다.")]
    public float laneHalfWidth = 1.6f;

    [Header("원장실 접근 보장 (침대가 영구적으로 막지 않는 구역)")]
    [Tooltip("Hound 의 출발점(방 안)에서 출구(houndExitPoint) 방향으로 이어지는 '방 내부 + 입구 + 출구 앞 복도' 띠의 반폭. 방 폭 전체를 덮어야 한다.")]
    public float reservedHalfWidth = 2.6f;
    [Tooltip("Hound 출발점 뒤쪽(방 안쪽 끝)으로 확보할 길이.")]
    public float reservedBehind = 5.5f;
    [Tooltip("출구 지점 너머 복도로 확보할 길이.")]
    public float reservedBeyondExit = 3.5f;
    [Tooltip("침대 가장자리와 확보 구역 사이에 두는 최소 여유(플레이어 캡슐이 지나갈 폭).")]
    public float reservedClearance = 0.45f;

    [Header("몇 번째 비콘에서 (정확히 이 개수)")]
    public int releaseAtBeaconCount = 2;

    bool played;
    Image fade;
    Canvas fadeCanvas;
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();

    static readonly int HashState = Animator.StringToHash("State");
    static readonly int HashIsMoving = Animator.StringToHash("IsMoving");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsPlaying = false; IsReleased = false; }

    void Awake()
    {
        IsPlaying = false;
        IsReleased = false;   // 씬 재시작(Retry) 시 Release 상태 초기화
        if (cutsceneCamera != null) cutsceneCamera.enabled = false;
        // 해제 전: AI 는 게임플레이에 관여하지 않는다.
        if (hound != null) hound.enabled = false;
    }

    void OnEnable() => GameProgress.Changed += OnProgress;

    void OnDisable()
    {
        GameProgress.Changed -= OnProgress;
        EyeDamageEffect.Unsuppress(this);
        ShowHud();
        IsPlaying = false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // [테스트 전용 — Editor / Development Build 에서만 컴파일. Release Build 에는 코드 자체가 없다]
    // F11: 활성 비콘 수를 정확히 releaseAtBeaconCount(=2) 로 만든다. 부족한 만큼만 Beacon.DebugComplete(= 실제 완료와 같은 진행 상태 갱신)로 처리하므로
    //      GameProgress.Changed → 정상 경로로 Monster 2 등장 컷씬이 한 번만 시작된다. 이미 2개 이상이면 아무 것도 하지 않는다(중복 진행도/컷씬 없음).
    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.f11Key.wasPressedThisFrame) return;
        if (played || IsPlaying || GameProgress.IsEnded || GameMenuManager.MenuOpen) return;
        if (SkillCheckManager.Instance != null && SkillCheckManager.Instance.IsActive) return;
        if (BeaconPipeQte.IsOpen) return;
        int need = releaseAtBeaconCount - GameProgress.ActiveBeacons;
        if (need <= 0) { Debug.Log("[Monster2] F11: 이미 활성 비콘 " + GameProgress.ActiveBeacons + "개 - 추가 처리 없음"); return; }
        int done = 0;
        foreach (var b in FindObjectsByType<Beacon>(FindObjectsSortMode.None))
        {
            if (done >= need) break;
            if (b.isTutorial) continue;
            if (b.DebugComplete()) done++;
        }
        Debug.Log("[Monster2] F11 debug: completed " + done + " beacon(s) -> " + GameProgress.ActiveBeacons + "/" + GameProgress.RequiredBeacons);
    }
#endif


    void OnProgress()
    {
        if (played || GameProgress.ActiveBeacons != releaseAtBeaconCount) return;   // 정확히 N개가 된 순간 한 번만
        played = true;
        StartCoroutine(Run());
    }

    // ───────────────────────── 시퀀스 ─────────────────────────

    IEnumerator Run()
    {
        yield return null;   // Beacon 이 상태를 모두 갱신한 뒤부터

        while (PlayerCaughtSequence.IsRunning || ExitCutsceneController.IsPlaying) yield return null;

        if (GameProgress.IsEnded || hound == null || cutsceneCamera == null)
        {
            ReleaseNow();   // 안전장치: 컷씬 불가 시에도 풀어 준다
            yield break;
        }

        IsPlaying = true;
        EyeDamageEffect.Suppress(this);   // 컷씬 화면에는 플레이어 눈 손상 효과(UI/후처리)를 적용하지 않는다
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var pi = FindFirstObjectByType<PlayerInteraction>();
        bool hadControls = fpc != null && fpc.ControlsEnabled;
        bool hadInteraction = pi != null && pi.InteractionEnabled;
        Camera playerCam = fpc != null && fpc.playerCamera != null ? fpc.playerCamera : Camera.main;

        // 1) 조작 잠금 + Monster 1 정지 (Monster 2 의 AI 는 아직 꺼져 있음)
        if (fpc != null) fpc.SetControlsEnabled(false);
        if (pi != null) pi.SetInteractionEnabled(false);
        var monsters = FindObjectsByType<MonsterChase>(FindObjectsSortMode.None);
        var wasEnabled = new bool[monsters.Length];
        for (int i = 0; i < monsters.Length; i++)
        {
            wasEnabled[i] = monsters[i].enabled;
            monsters[i].enabled = false;
            var ag = monsters[i].GetComponent<NavMeshAgent>();
            if (ag != null && ag.enabled && ag.isOnNavMesh) { ag.isStopped = true; ag.velocity = Vector3.zero; }
        }
        hound.enabled = false;

        // 2) 암전 → HUD 숨김 → 시네마 카메라
        EnsureFade();
        yield return Fade(0f, 1f);
        HideHud();
        if (playerCam != null) playerCam.enabled = false;
        cutsceneCamera.enabled = true;
        Transform ct = cutsceneCamera.transform;
        ct.SetPositionAndRotation(camStart.position, LookRot(camStart.position));

        var houndAnimator = hound.GetComponent<MonsterHoundAnimator>();
        var anim = hound.GetComponent<Animator>();
        var agent = hound.GetComponent<NavMeshAgent>();
        if (houndAnimator != null) houndAnimator.enabled = false;   // 컷씬 동안 애니메이터 파라미터를 직접 제어
        if (agent != null) agent.enabled = false;
        Vector3 houndStart = hound.transform.position;
        Vector3 exit = houndExitPoint != null ? houndExitPoint.position : houndStart + hound.transform.forward * 7f;
        exit.y = houndStart.y;

        yield return Fade(1f, 0f);

        // 3) 포효(ChargePrep): 침대더미 뒤에서 으르렁 + 카메라 천천히 접근
        if (anim != null) { anim.SetInteger(HashState, 2); anim.SetBool(HashIsMoving, false); }
        float t = 0f;
        while (t < roarTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / roarTime);
            ct.position = Vector3.Lerp(camStart.position, camEnd.position, k * 0.5f) + Shake(0.04f + 0.05f * k);
            ct.rotation = LookRot(ct.position);
            // 침대더미가 살짝 흔들림
            foreach (var b in bedPile) if (b != null) b.position += new Vector3(0f, 0f, Mathf.Sin(Time.time * 55f) * 0.0025f);
            yield return null;
        }

        // 4) 충돌: 침대더미 파괴(Collider 제거 + 파편 비산) + 장애물 해제 + Hound 돌진
        var debris = LaunchPile();
        if (navBarrier != null) navBarrier.enabled = false;
        if (anim != null) { anim.SetInteger(HashState, 3); anim.SetBool(HashIsMoving, true); }

        Quaternion faceExit = Quaternion.LookRotation(new Vector3(exit.x - houndStart.x, 0f, exit.z - houndStart.z).normalized, Vector3.up);
        hound.transform.rotation = faceExit;

        t = 0f;
        float shakeT = 0f;
        bool pairsRestored = false;
        while (t < burstTime + holdAfter)
        {
            float dt = Time.deltaTime;
            t += dt; shakeT += dt;

            float run = Mathf.Clamp01(t / burstTime);
            float e = 1f - (1f - run) * (1f - run);   // 침대를 뚫는 순간 빠르게 → 도착 지점에서 감속
            hound.transform.position = Vector3.Lerp(houndStart, exit, e);
            if (run >= 1f && anim != null && anim.GetInteger(HashState) != 0) { anim.SetInteger(HashState, 5); anim.SetBool(HashIsMoving, false); }

            if (!pairsRestored && t > 0.45f) { SetDebrisPairCollisions(debris, false); pairsRestored = true; }

            float shakeFade = Mathf.Clamp01(1f - shakeT / (burstTime + holdAfter * 0.6f));
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (burstTime + holdAfter)));
            ct.position = Vector3.Lerp(Vector3.Lerp(camStart.position, camEnd.position, 0.5f), camEnd.position, k) + Shake(shakeAmplitude * shakeFade);
            // 시선: 처음엔 침대더미(lookTarget), Hound 가 튀어 나오면서 Hound 를 따라간다
            float follow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / burstTime)) * 0.85f;
            Vector3 baseLook = lookTarget != null ? lookTarget.position : houndStart + Vector3.up;
            Vector3 aim = Vector3.Lerp(baseLook, hound.transform.position + Vector3.up * 1.0f, follow);
            ct.rotation = Quaternion.LookRotation(aim - ct.position, Vector3.up);
            yield return null;
        }

        // 5) 암전 → Hound 를 NavMesh 에 올리고 AI 활성화 → 플레이어 카메라/HUD/조작 복귀
        yield return Fade(0f, 1f);
        cutsceneCamera.enabled = false;
        if (playerCam != null) playerCam.enabled = true;
        ShowHud();

        // 암전 중에 침대가 완전히 정착할 때까지 물리를 계속 돌린다 → 정지 후 kinematic 으로 고정(Collider 유지)
        yield return SettleDebris(debris);
        FinishDebris(debris);
        PlaceHoundOnNavMesh(agent);
        if (anim != null) { anim.SetInteger(HashState, 0); anim.SetBool(HashIsMoving, false); }
        if (houndAnimator != null) houndAnimator.enabled = true;
        ReleaseNow();

        if (fpc != null && hadControls) fpc.SetControlsEnabled(true);
        if (pi != null && hadInteraction) pi.SetInteractionEnabled(true);
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] == null) continue;
            monsters[i].enabled = wasEnabled[i];
            var ag = monsters[i].GetComponent<NavMeshAgent>();
            if (ag != null && ag.enabled && ag.isOnNavMesh) ag.isStopped = false;
        }
        IsPlaying = false;
        EyeDamageEffect.Unsuppress(this);
        yield return Fade(1f, 0f);
        yield return VerifyHoundPath();
    }

    /// <summary>컷씬 없이 즉시 해제(안전장치): 침대를 물리로 날려 보내고 AI 활성화.</summary>
    void ReleaseNow()
    {
        if (!IsReleased && bedPile != null && !pileLaunched)
        {
            var d = LaunchPile();
            StartCoroutine(SettleAfterFallback(d));
        }
        if (navBarrier != null) navBarrier.enabled = false;
        var agent = hound != null ? hound.GetComponent<NavMeshAgent>() : null;
        if (agent != null && !agent.enabled) PlaceHoundOnNavMesh(agent);
        if (hound != null) hound.enabled = true;
        IsReleased = true;
        Debug.Log("[Monster2] Released (E-07 Hound AI active)");
    }

    IEnumerator SettleAfterFallback(List<Debris> d)
    {
        yield return new WaitForSeconds(0.45f);
        SetDebrisPairCollisions(d, false);
        yield return SettleDebris(d);
        FinishDebris(d);
    }

    void PlaceHoundOnNavMesh(NavMeshAgent agent)
    {
        if (agent == null) return;
        Vector3 p = hound.transform.position;
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) p = hit.position;
        agent.enabled = true;
        agent.Warp(p);
    }

    // ───────────────────────── 침대더미 파괴 ─────────────────────────

    class Debris { public Transform t; public Rigidbody rb; public Collider[] cols; }

    bool pileLaunched;
    Vector3 laneOrigin;   // 침대를 날리는 순간 Hound 가 있던 자리(= 침대더미 뒤). 여기서 houndExitPoint 쪽으로 달려 나간다

    /// <summary>침대마다 Rigidbody(중력) 를 붙이고 Collider 를 유지한 채 날려 보낸다. Hound 와는 충돌하지 않는다.</summary>
    List<Debris> LaunchPile()
    {
        pileLaunched = true;
        var debris = new List<Debris>();
        Vector3 outward = hound != null && houndExitPoint != null
            ? new Vector3(houndExitPoint.position.x - hound.transform.position.x, 0f, houndExitPoint.position.z - hound.transform.position.z).normalized
            : Vector3.left;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.left;
        laneOrigin = hound != null ? hound.transform.position : Vector3.zero;
        var houndCols = hound != null ? hound.GetComponentsInChildren<Collider>(true) : new Collider[0];

        var list = new List<Transform>();
        foreach (var b in bedPile) if (b != null) list.Add(b);
        // 방향이 서로 조금씩 다르게: 층화 샘플링 후 셔플
        var yaws = new List<float>();
        for (int i = 0; i < list.Count; i++)
            yaws.Add(Mathf.Lerp(launchYaw.x, launchYaw.y, (i + Random.value) / Mathf.Max(1, list.Count)));
        for (int i = yaws.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (yaws[i], yaws[j]) = (yaws[j], yaws[i]); }

        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            var cols = b.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols)
            {
                c.isTrigger = false;
                if (c is MeshCollider mc) mc.convex = true;   // 비-convex MeshCollider 는 동적 Rigidbody 불가
                c.enabled = true;
                foreach (var hc in houndCols) if (hc != null) Physics.IgnoreCollision(c, hc, true);
            }

            var rb = b.GetComponent<Rigidbody>();
            if (rb == null) rb = b.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 40f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.35f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.None;

            float speed = Random.Range(launchSpeed.x, launchSpeed.y);
            float up = Random.Range(launchUp.x, launchUp.y);
            float yaw = yaws[i];
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * outward;

            // 예측 착지 지점이 Hound 가 달려 나가는 통로에 들어가면 방향을 북쪽으로 더 튼다
            for (int k = 0; k < 4 && LandsInLane(b.position, dir * speed, up, laneOrigin, outward); k++)
            {
                yaw += 12f;
                dir = Quaternion.Euler(0f, yaw, 0f) * outward;
            }

            rb.linearVelocity = dir * speed + Vector3.up * up;
            rb.angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));
            debris.Add(new Debris { t = b, rb = rb, cols = cols });
        }

        SetDebrisPairCollisions(debris, true);   // 처음엔 서로 겹쳐 있어 충돌을 잠깐 끈다(폭발 방지). 흩어진 뒤 다시 켠다.
        return debris;
    }

    bool LandsInLane(Vector3 start, Vector3 horizVel, float upVel, Vector3 laneOrigin, Vector3 laneDir)
    {
        float g = Mathf.Abs(Physics.gravity.y);
        float h = Mathf.Max(0f, start.y);
        float T = (upVel + Mathf.Sqrt(upVel * upVel + 2f * g * h)) / g;
        Vector3 land = start + new Vector3(horizVel.x, 0f, horizVel.z) * T;
        Vector3 rel = land - laneOrigin;
        rel.y = 0f;
        float along = Vector3.Dot(rel, laneDir);
        float side = Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, laneDir)));
        return along > 0f && side < laneHalfWidth + 1.2f;
    }

    /// <summary>침대끼리의 충돌 무시 on/off (ignore=true 면 서로 통과).</summary>
    void SetDebrisPairCollisions(List<Debris> debris, bool ignore)
    {
        for (int i = 0; i < debris.Count; i++)
            for (int j = i + 1; j < debris.Count; j++)
                foreach (var a in debris[i].cols) foreach (var c in debris[j].cols)
                    if (a != null && c != null) Physics.IgnoreCollision(a, c, ignore);
    }

    /// <summary>모든 침대가 sleep 할 때까지(최대 settleMaxSeconds) 기다린다.</summary>
    IEnumerator SettleDebris(List<Debris> debris)
    {
        float t = 0f;
        yield return new WaitForFixedUpdate();
        while (t < settleMaxSeconds)
        {
            bool allSleep = true;
            foreach (var d in debris)
                if (d.t != null && d.rb != null && !d.rb.IsSleeping()) { allSleep = false; break; }
            if (allSleep && t > 0.3f) break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    readonly List<NavMeshObstacle> bedObstacles = new List<NavMeshObstacle>();

    /// <summary>정착한 침대를 고정(kinematic, Collider 유지) + 통로를 막지 않는 침대에만 carve 장애물을 만든다.</summary>
    void FinishDebris(List<Debris> debris)
    {
        Vector3 houndPos = hound != null ? hound.transform.position : Vector3.zero;
        var houndCols = hound != null ? hound.GetComponentsInChildren<Collider>(true) : new Collider[0];
        foreach (var d in debris)
        {
            if (d.t == null) continue;
            if (d.rb != null)
            {
                if (!d.rb.isKinematic) { d.rb.linearVelocity = Vector3.zero; d.rb.angularVelocity = Vector3.zero; }
                d.rb.isKinematic = true;
            }
            // 땅 아래로 빠졌으면 바닥 위로 끌어올림
            if (d.t.position.y < -0.5f) { var p = d.t.position; p.y = 0.3f; d.t.position = p; }
            foreach (var c in d.cols)
            {
                if (c == null) continue;
                c.enabled = true;
                foreach (var hc in houndCols) if (hc != null) Physics.IgnoreCollision(c, hc, false);
            }
        }
        SetDebrisPairCollisions(debris, false);

        ClearReservedZone(debris);   // 원장실 내부/입구/출구 앞 복도에 정착한 침대는 같은 연출 안에서 주변 빈 바닥으로 옮긴다

        Vector3 laneDir = houndExitPoint != null ? houndExitPoint.position - laneOrigin : Vector3.left;
        laneDir.y = 0f;
        laneDir = laneDir.sqrMagnitude > 0.01f ? laneDir.normalized : Vector3.left;
        Vector3 laneSide = Vector3.Cross(Vector3.up, laneDir);

        foreach (var d in debris)
        {
            if (d.t == null || d.cols.Length == 0 || d.cols[0] == null) continue;
            Bounds bb = d.cols[0].bounds;
            foreach (var c in d.cols) if (c != null) bb.Encapsulate(c.bounds);
            if (bb.max.y < 0.15f) continue;   // 바닥에 납작하게 붙은 파편은 장애물 불필요
            // Hound 가 달려 나가는 통로/출구 위에 정착한 침대는 장애물을 만들지 않는다 (Monster 2 경로를 다시 막지 않음)
            Vector3 rel = bb.center - laneOrigin; rel.y = 0f;
            float side = Mathf.Abs(Vector3.Dot(rel, laneSide));
            float halfW = Mathf.Max(bb.extents.x, bb.extents.z);
            float laneLen = houndExitPoint != null ? Vector3.Distance(new Vector3(houndExitPoint.position.x, 0f, houndExitPoint.position.z), new Vector3(laneOrigin.x, 0f, laneOrigin.z)) : 8f;
            float along = Vector3.Dot(rel, laneDir);
            if (side < laneHalfWidth + halfW * 0.6f && along > -1f && along < laneLen + 3f) continue;   // 달려 나가는 통로(+출구 앞 3m)
            Vector3 relNow = bb.center - houndPos; relNow.y = 0f;
            if (relNow.magnitude < 1.5f + halfW) continue;   // Hound 가 서 있는 자리

            var bc = d.t.GetComponent<BoxCollider>();
            var ob = d.t.gameObject.AddComponent<NavMeshObstacle>();
            ob.shape = NavMeshObstacleShape.Box;
            ob.carving = true;
            ob.carveOnlyStationary = false;
            ob.carvingMoveThreshold = 0.05f;
            ob.carvingTimeToStationary = 0f;
            if (bc != null) { ob.center = bc.center; ob.size = bc.size; }
            else { ob.center = d.t.InverseTransformPoint(bb.center); ob.size = bb.size; }
            bedObstacles.Add(ob);
        }
    }


    // ───────────────────────── 원장실 접근 보장 ─────────────────────────

    /// <summary>
    /// 침대는 무작위로 날아가 아무 데나 정착하므로, 입구를 막거나 방 안에 남는 경우가 생긴다.
    /// 폭발/비산 연출은 그대로 두고, 정착이 끝난 뒤(암전 중) 확보 구역(방 내부 + 입구 + 출구 앞 복도)에 걸친 침대만
    /// 구역 밖의 가장 가까운 빈 바닥으로 옮긴다. 침대는 삭제/비활성화하지 않고 Collider 도 그대로 유지한다.
    /// </summary>
    void ClearReservedZone(List<Debris> debris)
    {
        if (debris == null || debris.Count == 0) return;
        Vector3 dir = houndExitPoint != null ? houndExitPoint.position - laneOrigin : Vector3.left;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.left;
        dir.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float laneLen = houndExitPoint != null
            ? Vector3.Distance(new Vector3(houndExitPoint.position.x, 0f, houndExitPoint.position.z), new Vector3(laneOrigin.x, 0f, laneOrigin.z))
            : 8f;
        int moved = 0, failed = 0;

        foreach (var d in debris)
        {
            if (d.t == null || d.cols == null || d.cols.Length == 0) continue;
            Bounds bb = BoundsOf(d);
            if (!IntersectsZone(bb, laneOrigin, dir, side, laneLen)) continue;

            // 자기 Collider 는 후보 검사에서 잠시 뺀다
            foreach (var c in d.cols) if (c != null) c.enabled = false;
            bool found = false;
            Vector3 best = bb.center;
            for (float r = 0.5f; r <= 16f && !found; r += 0.5f)
            {
                int steps = Mathf.Max(8, Mathf.RoundToInt(r * 8f));
                for (int k = 0; k < steps; k++)
                {
                    float a = (k + 0.5f) / steps * Mathf.PI * 2f;
                    Vector3 cand = new Vector3(bb.center.x + Mathf.Cos(a) * r, bb.center.y, bb.center.z + Mathf.Sin(a) * r);
                    var cb = new Bounds(cand, bb.size);
                    if (IntersectsZone(cb, laneOrigin, dir, side, laneLen)) continue;
                    if (!NavMesh.SamplePosition(new Vector3(cand.x, 0.05f, cand.z), out NavMeshHit nh, 1.0f, NavMesh.AllAreas)) continue;
                    // 다른 침대 위에 얹혀 있던 경우도 있으므로, 옮길 때는 바닥에 내려놓는다 (AABB 바닥 = 바닥면)
                    cand.y = nh.position.y + bb.extents.y + 0.02f;
                    // 바닥과 겹치지 않도록 상하를 조금 줄여서 벽/가구/다른 침대와의 겹침만 검사
                    Vector3 half = bb.extents * 0.98f;
                    Vector3 ctr = cand + Vector3.up * 0.1f;
                    half.y = Mathf.Max(0.05f, half.y - 0.1f);
                    if (Physics.CheckBox(ctr, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    best = cand; found = true; break;
                }
            }
            foreach (var c in d.cols) if (c != null) c.enabled = true;

            if (!found) { failed++; Debug.LogWarning("[Monster2] 확보 구역 밖에 침대 자리를 찾지 못했습니다: " + d.t.name); continue; }
            d.t.position += best - bb.center;
            moved++;
        }
        Physics.SyncTransforms();
        Debug.Log("[Monster2] 원장실 확보 구역 정리: 이동 " + moved + "개, 실패 " + failed + "개 / 침대 " + debris.Count + "개");
    }

    static Bounds BoundsOf(Debris d)
    {
        Bounds b = default; bool any = false;
        foreach (var c in d.cols)
        {
            if (c == null) continue;
            if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
        }
        return b;
    }

    bool IntersectsZone(Bounds b, Vector3 origin, Vector3 dir, Vector3 side, float laneLen)
    {
        float aMin = float.MaxValue, aMax = float.MinValue, sMin = float.MaxValue, sMax = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3(i < 2 ? b.min.x : b.max.x, 0f, (i % 2 == 0) ? b.min.z : b.max.z) - new Vector3(origin.x, 0f, origin.z);
            float a = Vector3.Dot(p, dir), s = Vector3.Dot(p, side);
            aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a);
            sMin = Mathf.Min(sMin, s); sMax = Mathf.Max(sMax, s);
        }
        float c = reservedClearance;
        return aMax > -reservedBehind - c && aMin < laneLen + reservedBeyondExit + c
            && sMax > -reservedHalfWidth - c && sMin < reservedHalfWidth + c;
    }

    /// <summary>
    /// carve 가 반영된 뒤에도 Hound 가 플레이어 쪽으로 갈 수 있는지 확인한다.
    /// 경로가 Partial 이어도 원인이 침대라고 단정하지 않는다: 플레이어가 잠긴 문(튜토리얼 문 등) 너머에 있으면 침대와 무관하게 Partial 이다.
    /// 그래서 침대 장애물을 잠시 끄고 다시 계산해 '정말 침대 때문일 때만' 해제하고, 아니면 장애물을 그대로 둔다.
    /// </summary>
    IEnumerator VerifyHoundPath()
    {
        yield return new WaitForSeconds(0.6f);
        if (hound == null || bedObstacles.Count == 0) yield break;
        var agent = hound.GetComponent<NavMeshAgent>();
        var player = FindFirstObjectByType<FirstPersonController>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || player == null) yield break;
        Vector3 target = player.transform.position;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 6f, NavMesh.AllAreas)) target = hit.position;

        var path = new NavMeshPath();
        bool ok = agent.CalculatePath(target, path) && path.status == NavMeshPathStatus.PathComplete;
        Debug.Log("[Monster2] bed obstacles=" + bedObstacles.Count + " hound->player path " + path.status);
        if (ok) yield break;

        // 원인 분리: 침대 장애물을 끄고 다시 계산
        foreach (var ob in bedObstacles) if (ob != null) ob.enabled = false;
        yield return new WaitForSeconds(0.4f);
        var path2 = new NavMeshPath();
        bool okWithout = agent.isOnNavMesh && agent.CalculatePath(target, path2) && path2.status == NavMeshPathStatus.PathComplete;
        if (okWithout)
        {
            Debug.LogWarning("[Monster2] 침대 NavMeshObstacle 이 Hound 경로를 막아 모두 해제했습니다.");
            yield break;
        }
        foreach (var ob in bedObstacles) if (ob != null) ob.enabled = true;
        Debug.Log("[Monster2] 침대와 무관한 이유로 플레이어까지 경로가 끊겨 있습니다(잠긴 문 등). 침대 장애물은 유지합니다.");
    }

    // ───────────────────────── 카메라 ─────────────────────────

    Quaternion LookRot(Vector3 from)
    {
        Vector3 to = lookTarget != null ? lookTarget.position : (hound != null ? hound.transform.position + Vector3.up * 1f : from + Vector3.forward);
        Vector3 dir = to - from;
        return dir.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(dir, Vector3.up);
    }

    static Vector3 Shake(float amp) => amp <= 0f ? Vector3.zero : new Vector3(
        (Mathf.PerlinNoise(Time.time * 38f, 0.1f) - 0.5f) * 2f * amp,
        (Mathf.PerlinNoise(0.3f, Time.time * 41f) - 0.5f) * 2f * amp,
        (Mathf.PerlinNoise(Time.time * 33f, 7.7f) - 0.5f) * 2f * amp * 0.6f);

    // ───────────────────────── HUD / Fade ─────────────────────────

    void HideHud()
    {
        hiddenCanvases.Clear();
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c == fadeCanvas || !c.enabled) continue;
            if (c.renderMode == RenderMode.WorldSpace) continue;
            c.enabled = false;
            hiddenCanvases.Add(c);
        }
    }

    void ShowHud()
    {
        foreach (var c in hiddenCanvases)
            if (c != null) c.enabled = true;
        hiddenCanvases.Clear();
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, fadeTime))));
            yield return null;
        }
        SetFade(to);
    }

    void SetFade(float a)
    {
        if (fade == null) return;
        fade.color = new Color(0f, 0f, 0f, a);
        fade.gameObject.SetActive(a > 0.001f);
    }

    void EnsureFade()
    {
        if (fade != null) return;
        var go = new GameObject("Monster2ReleaseFade", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var c = go.GetComponent<Canvas>();
        fadeCanvas = c;
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 5000;
        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        fade = img.GetComponent<Image>();
        fade.raycastTarget = false;
        var rt = fade.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        SetFade(0f);
    }
}
