using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 몬스터에게 잡혔을 때의 독립 연출/처리. 몬스터는 PlayerCaughtSequence.TriggerCaught() 만 호출한다.
///
/// 첫 피격 (파손 0): 조작 차단 → 쓰러짐 → 눈 감김 → 암전 → Eye/Arm/Leg 랜덤 파손(PlayerBodyStatus API)
///                    → PlayerSpawn 으로 Respawn → 일어남 → 눈 뜸 → 조작 복구 (패널티 유지)
/// 두 번째 피격 (이미 파손): 쓰러짐 → 눈 감김 → 기존 GameProgress.TriggerGameOver()
///
/// 진행 중에는 TriggerCaught 재호출이 무시된다 (한 번 잡힐 때 한 번만 실행).
/// 카메라 연출만 사용한다 (Player 몸/Rigidbody 변경 없음).
/// </summary>
[DefaultExecutionOrder(300)] // PlayerLimpCamera(100) 이후 LateUpdate에서 카메라 오프셋 덮어씀
[DisallowMultipleComponent]
public class PlayerCaughtSequence : MonoBehaviour
{
    static PlayerCaughtSequence instance;

    /// <summary>연출 진행 중 (게임오버로 끝난 경우 계속 true).</summary>
    public static bool IsRunning => instance != null && instance.running;

    [Header("Fall")]
    public float fallDuration = 0.85f;
    public float fallDrop = 0.9f;          // 카메라 Y 하강량(m)
    public float fallPitch = 28f;          // 바닥 쪽으로 기울어지는 각도
    public float fallRoll = 4f;            // 아주 작게

    [Header("Eyelids")]
    public float closeDuration = 0.6f;
    public float closeDelay = 0.25f;       // 쓰러지기 시작 후 눈 감기 시작까지
    public float blackHold = 0.7f;         // 완전 암전 유지(파손/리스폰 처리 포함)
    public float openDuration = 0.8f;

    [Header("Get Up")]
    public float getUpDuration = 1.0f;

    [Header("Debug")]
    [Tooltip("F9 = 강제로 잡힘 (에디터/개발 빌드 전용)")]
    public bool debugKey = true;

    FirstPersonController controller;
    PlayerInteraction interaction;
    PlayerBodyStatus body;
    CharacterController cc;

    bool running;
    Transform caughtBy;

    [Header("Face Monster")]
    public float faceDuration = 0.3f;
    public float faceHold = 0.17f;
    public float faceHeadHeight = 1.5f;   // MonsterStun.FacePosition 이 없을 때 monster.position + up * 이 값
    [Tooltip("포획 시 카메라 ↔ 몬스터 Head 본 수평 거리(m). 모델이 길어서(머리가 피벗 앞 ~1.7m) 피벗이 아니라 Head 기준으로 맞춘다.")]
    public float captureHeadDistance = 1.35f;

    [Header("Lunge (덮침)")]
    [Tooltip("덮침 시간(초).")] public float lungeDuration = 0.2f;
    [Tooltip("덮친 뒤 카메라 ↔ Head 본 수평 거리(m). 메시 최근접 정점은 이보다 약 0.37m 가깝다.")]
    public float lungeHeadDistance = 0.8f;

    [Header("Impact Camera (확!)")]
    public float impactDuration = 0.12f;
    [Tooltip("카메라가 뒤로 밀리는 거리(m).")] public float impactPush = 0.1f;
    [Tooltip("Pitch 흔들림(도, 위로 젖혀짐).")] public float impactPitch = 3.5f;
    [Tooltip("X/Y 충격(m).")] public Vector2 impactShift = new Vector2(0.012f, -0.02f);
    [Tooltip("Roll(도) - 아주 작게.")] public float impactRoll = 0.7f;

    [Header("Bite Sound Hook (소리 없으면 아무 일도 안 함)")]
    public AudioSource biteAudio;
    public AudioClip biteClip;
    public UnityEvent onBiteImpact;

    [Header("Heart Rate")]
    [Tooltip("첫 피격 후 부활 직후 BPM (80~100 권장).")]
    public float resumeBpm = 90f;
    // 카메라 연출 상태 (0 = 정상, 1 = 완전히 쓰러짐)
    float camAmount;
    bool camActive;
    Vector3 impactPos;   // 공격 충격 카메라 오프셋 (로컬)
    Vector3 impactEuler;

    RectTransform lidTop, lidBottom;
    GameObject lidCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => Ensure();

    static PlayerCaughtSequence Ensure()
    {
        if (instance != null) return instance;
        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc == null) return null;
        var s = fpc.GetComponent<PlayerCaughtSequence>();
        if (s == null) s = fpc.gameObject.AddComponent<PlayerCaughtSequence>();
        instance = s;
        return s;
    }

    void Awake()
    {
        instance = this;
        controller = GetComponent<FirstPersonController>();
        interaction = GetComponent<PlayerInteraction>();
        body = GetComponent<PlayerBodyStatus>();
        cc = GetComponent<CharacterController>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>몬스터 공격 진입점. 연출 중/숨는 중 재호출은 무시.</summary>
    public static void TriggerCaught() => TriggerCaught(null);

    /// <summary>attacker = 잡은 몬스터 (그쪽을 바라본다). null이면 씬의 몬스터 탐색.</summary>
    public static void TriggerCaught(Transform attacker)
    {
        var s = Ensure();
        if (s == null || s.running) return;
        if (PlayerHide.IsPlayerHiding) return;
        s.caughtBy = attacker != null ? attacker : (FindFirstObjectByType<MonsterChase>() != null ? FindFirstObjectByType<MonsterChase>().transform : null);
        s.StartCoroutine(s.Run());
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugKey && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            TriggerCaught();
#endif
    }

    IEnumerator Run()
    {
        running = true;
        if (body == null) body = GetComponent<PlayerBodyStatus>();
        bool firstHit = body == null || body.DamagedCount == 0;
        Debug.Log("[PlayerCaughtSequence] Caught! firstHit=" + firstHit);

        // 진행 중인 QTE가 있으면 (이벤트 없이) 닫는다.
        SkillCheckManager.CancelActive();

        // 조작 차단: 이동/Sprint/시선/손전등(ControlsEnabled) + 상호작용/인벤토리 선택·Drop(InteractionEnabled)
        if (controller != null) controller.SetControlsEnabled(false);
        if (interaction != null) interaction.SetInteractionEnabled(false);

        EnsureEyelids();
        // 잡은 몬스터 쪽으로 시선을 부드럽게 돌리고 잠깐 바라본다 (등 돌린 상태에서도 몬스터가 보이도록)
        PrepareCapture();
        yield return FaceMonster();
        camActive = true;               // 덮침/충격 카메라 오프셋 적용 시작 (camAmount=0 이라 정상 위치)
        yield return Lunge();
        yield return Impact();

        camActive = true;

        // 쓰러짐 + 눈 감김 (눈은 약간 늦게 시작)
        float t = 0f;
        float total = Mathf.Max(fallDuration, closeDelay + closeDuration);
        while (t < total)
        {
            t += Time.deltaTime;
            camAmount = Ease(Mathf.Clamp01(t / fallDuration), true);
            SetLids(Mathf.Clamp01((t - closeDelay) / closeDuration));
            yield return null;
        }
        camAmount = 1f;
        SetLids(1f);

        if (!firstHit)
        {
            // 두 번째 피격: Respawn/신규 파손 없이 기존 Game Over
            yield return new WaitForSeconds(0.3f);
            GameProgress.TriggerGameOver();
            yield break; // running 유지: 게임 오버 후 연출 재진입 없음
        }

        // 완전 암전 상태에서: 랜덤 부위 파손 → Respawn
        yield return new WaitForSeconds(blackHold * 0.4f);
        ApplyRandomDamage();
        Respawn();
        if (controller != null) controller.SetLookPitch(0f); // 부활 시선은 수평
        RelocateMonster();
        if (HeartRateController.Instance != null) HeartRateController.Instance.SetBpm(resumeBpm); // 암전 동안 어느 정도 회복된 상태로 재개
        yield return new WaitForSeconds(blackHold * 0.6f);

        // 일어남 + 눈 뜸
        t = 0f;
        total = Mathf.Max(getUpDuration, openDuration);
        while (t < total)
        {
            t += Time.deltaTime;
            camAmount = 1f - Ease(Mathf.Clamp01(t / getUpDuration), false);
            SetLids(1f - Mathf.Clamp01(t / openDuration));
            yield return null;
        }
        camAmount = 0f;
        SetLids(0f);
        camActive = false;
        ApplyCamera(0f); // 정상 위치 복귀 (이후는 PlayerLimpCamera 등 기존 로직)
        if (lidCanvas != null) lidCanvas.SetActive(false);

        // 조작 복구 (파손 패널티는 PlayerBodyStatus가 유지)
        if (controller != null) controller.SetControlsEnabled(true);
        if (interaction != null) interaction.SetInteractionEnabled(true);
        running = false;
        Debug.Log("[PlayerCaughtSequence] Recovered. Damaged=" + (body != null ? body.DamagedCount : -1));
    }


    IEnumerator FaceMonster()
    {
        if (caughtBy == null || controller == null || controller.playerCamera == null) yield break;

        Vector3 camPos = controller.playerCamera.transform.position;
        Vector3 target = TargetPoint();
        Vector3 d = target - camPos;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        if (flat.sqrMagnitude < 0.0001f) yield break;

        float startYaw = transform.eulerAngles.y;
        float endYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float yawDelta = Mathf.DeltaAngle(startYaw, endYaw);                 // 최단 회전 방향 (|delta| <= 180)
        float startPitch = controller.LookPitch;
        float endPitch = Mathf.Clamp(-Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg, -60f, 60f);

        float t = 0f;
        while (t < faceDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / faceDuration));
            transform.rotation = Quaternion.Euler(0f, startYaw + yawDelta * k, 0f);
            controller.SetLookPitch(Mathf.Lerp(startPitch, endPitch, k));
            yield return null;
        }
        transform.rotation = Quaternion.Euler(0f, startYaw + yawDelta, 0f);
        controller.SetLookPitch(endPitch);
        yield return new WaitForSeconds(faceHold);
    }

    /// <summary>
    /// 덮침: 몬스터 전체를 짧고 빠르게(가속) 카메라 쪽으로 보간. NavMeshAgent는 연출 동안만 끄고 끝나면 켜서 Warp(상태 복구).
    /// 최종 거리는 Head 본 기준 lungeHeadDistance 로 제한 (메시 관통 방지). 이미 더 가까우면 움직이지 않는다.
    /// </summary>
    IEnumerator Lunge()
    {
        if (caughtBy == null) yield break;
        var agent = caughtBy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var head = FindDeep(caughtBy, "Head");

        Vector3 p = transform.position;
        Vector3 start = caughtBy.position;
        Vector3 away = start - p; away.y = 0f;
        if (away.sqrMagnitude < 0.0004f) yield break;
        away.Normalize();

        Vector3 headOff = head != null ? head.position - start : -away * 0.8f;
        headOff.y = 0f;
        Vector3 headNow = p + (start + headOff - p);                       // 현재 Head (수평)
        float curDist = new Vector3(headNow.x - p.x, 0f, headNow.z - p.z).magnitude;
        Vector3 end = start;
        if (curDist > lungeHeadDistance)
            end = p + away * lungeHeadDistance - headOff;                  // Head 가 lungeHeadDistance 에 오도록
        end.y = start.y;

        bool agentWas = agent != null && agent.enabled;
        if (agentWas) agent.enabled = false;

        float t = 0f;
        while (t < lungeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Pow(Mathf.Clamp01(t / lungeDuration), 1.7f);   // 가속: 끝에서 확 들어옴
            caughtBy.position = Vector3.Lerp(start, end, k);
            yield return null;
        }
        caughtBy.position = end;

        if (agentWas)
        {
            agent.enabled = true;
            if (UnityEngine.AI.NavMesh.SamplePosition(end, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                agent.Warp(hit.position);
            if (agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; agent.isStopped = true; }
        }
    }

    /// <summary>공격이 닿는 순간 카메라에 짧은 충격 (뒤로 밀림 + Pitch 젖힘 + 아주 작은 X/Y/Roll). 바로 Fall 로 이어진다.</summary>
    IEnumerator Impact()
    {
        onBiteImpact?.Invoke();
        if (biteAudio != null && biteClip != null) biteAudio.PlayOneShot(biteClip); // 소리 없으면 아무것도 안 함

        float t = 0f;
        while (t < impactDuration)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / impactDuration);
            float k = x < 0.25f ? Mathf.SmoothStep(0f, 1f, x / 0.25f) : 1f - Mathf.SmoothStep(0f, 1f, (x - 0.25f) / 0.75f);
            impactPos = new Vector3(impactShift.x * k, impactShift.y * k, -impactPush * k);
            impactEuler = new Vector3(-impactPitch * k, 0f, impactRoll * k);
            yield return null;
        }
        impactPos = Vector3.zero;
        impactEuler = Vector3.zero;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>시선 목표: Head 본 → 없으면 MonsterStun.FacePosition → 없으면 position + up.</summary>
    Vector3 TargetPoint()
    {
        var head = FindDeep(caughtBy, "Head");
        if (head != null) return head.position;
        var stun = caughtBy.GetComponent<MonsterStun>();
        return stun != null ? stun.FacePosition : caughtBy.position + Vector3.up * faceHeadHeight;
    }

    /// <summary>
    /// 포획 순간: 몬스터 즉시 정지 → 플레이어 쪽으로 돌려 세우고 → Head 본이 카메라에서 captureHeadDistance 만큼 떨어지도록
    /// 몬스터를 NavMesh 위에서 뒤로 물린다 (카메라/플레이어는 움직이지 않음 → 카메라가 벽에 밀리지 않는다).
    /// 원인: 모델이 길어 Head가 피벗보다 ~1.7m 앞에 있는데, 공격 판정은 피벗 거리(1.5m)라 얼굴이 카메라와 겹쳤다.
    /// </summary>
    void PrepareCapture()
    {
        if (caughtBy == null) return;
        var agent = caughtBy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }

        Vector3 p = transform.position;
        Vector3 away = caughtBy.position - p; away.y = 0f;
        if (away.sqrMagnitude < 0.0004f) away = -transform.forward; // 완전히 겹침: 플레이어 뒤쪽으로
        away.Normalize();
        Vector3 fwd = -away;                                          // 몬스터가 플레이어를 바라봄
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        // Head 본의 몬스터 로컬 수평 오프셋 (피벗 기준)
        var head = FindDeep(caughtBy, "Head");
        Vector3 local = head != null ? caughtBy.InverseTransformPoint(head.position) : new Vector3(0f, 0f, 0.8f);
        Vector3 headOffset = rot * new Vector3(local.x, 0f, local.z);

        Vector3 desiredHead = p + away * captureHeadDistance;
        Vector3 desired = desiredHead - headOffset;
        desired.y = caughtBy.position.y;

        // NavMesh 위로, 벽에 막히면 막히기 직전까지만
        Vector3 result = desired;
        if (UnityEngine.AI.NavMesh.SamplePosition(p, out var from, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            if (UnityEngine.AI.NavMesh.Raycast(from.position, desired, out var edge, UnityEngine.AI.NavMesh.AllAreas))
                result = edge.position;
            else if (UnityEngine.AI.NavMesh.SamplePosition(desired, out var onMesh, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                result = onMesh.position;
        }

        if (agent != null && agent.enabled) agent.Warp(result);
        else caughtBy.position = result;
        caughtBy.rotation = rot;
    }

    void RelocateMonster()
    {
        var chase = caughtBy != null ? caughtBy.GetComponent<MonsterChase>() : FindFirstObjectByType<MonsterChase>();
        var points = MonsterSpawnPoints.Instance != null ? MonsterSpawnPoints.Instance : FindFirstObjectByType<MonsterSpawnPoints>();
        if (chase != null && points != null) points.RelocateMonster(chase, transform.position);
    }
    void ApplyRandomDamage()
    {
        if (body == null) return;
        var part = (BodyPart)Random.Range(0, 3);
        Debug.Log("[PlayerCaughtSequence] Random damage -> " + part);
        body.SetPenalty(part, true); // 기존 API: Eye/Arm/Leg 패널티가 즉시 적용됨
    }

    void Respawn()
    {
        var go = GameObject.Find("PlayerSpawn");
        if (go == null)
        {
            Debug.LogWarning("[PlayerCaughtSequence] 'PlayerSpawn' 없음 - 현재 위치 유지");
            return;
        }
        bool wasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
        if (cc != null) cc.enabled = wasEnabled;
    }

    // ───────────────────────── 카메라 ─────────────────────────

    static float Ease(float x, bool accelerate) => accelerate ? x * x : 1f - (1f - x) * (1f - x);

    void LateUpdate()
    {
        if (camActive) ApplyCamera(camAmount);
    }

    void ApplyCamera(float a)
    {
        if (controller == null || controller.playerCamera == null) return;
        var cam = controller.playerCamera.transform;
        cam.localPosition = controller.firstPersonPosition + new Vector3(0f, -fallDrop * a, 0f) + impactPos;
        cam.localRotation = Quaternion.Euler(fallPitch * a + impactEuler.x, 0f, fallRoll * a + impactEuler.z);
    }

    // ───────────────────────── 눈꺼풀 ─────────────────────────

    void EnsureEyelids()
    {
        if (lidCanvas != null) { lidCanvas.SetActive(true); return; }

        lidCanvas = new GameObject("EyelidCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = lidCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150; // GameProgress(200)의 Game Over 패널보다 아래
        var scaler = lidCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        lidTop = MakeLid("EyelidTop", true);
        lidBottom = MakeLid("EyelidBottom", false);
        SetLids(0f);
    }

    RectTransform MakeLid(string name, bool top)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(lidCanvas.transform, false);
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    /// <summary>closed 0 = 완전히 열림, 1 = 화면 중앙에서 맞닿아 완전 암전.</summary>
    void SetLids(float closed)
    {
        if (lidTop == null || lidBottom == null) return;
        closed = Mathf.Clamp01(closed);
        // 위 패널: 위쪽 기준 높이 = 화면 절반 * closed(+여유). 아래 패널은 대칭.
        float h = 0.5f * closed;
        float over = closed >= 1f ? 0.002f : 0f; // 완전 닫힘 시 틈 방지
        lidTop.anchorMin = new Vector2(0f, 1f - h - over);
        lidTop.anchorMax = new Vector2(1f, 1f);
        lidBottom.anchorMin = new Vector2(0f, 0f);
        lidBottom.anchorMax = new Vector2(1f, h + over);
        lidTop.offsetMin = lidTop.offsetMax = Vector2.zero;
        lidBottom.offsetMin = lidBottom.offsetMax = Vector2.zero;
        lidTop.gameObject.SetActive(closed > 0f);
        lidBottom.gameObject.SetActive(closed > 0f);
    }
}
