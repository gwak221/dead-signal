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

    [Header("Fall & Killcam")]
    public float fallDuration = 0.75f;
    public float fallDrop = 0.88f;          // 카메라 Y 하강량(m) — 바닥/무릎 높이에서 올려다보는 쓰러짐
    [Tooltip("쓰러지며 뒤로 물러나는 거리(m, 벽 충돌 시 자동 축소). 몬스터 상체/머리가 화면 안에 다 들어오게 한다.")]
    public float fallBack = 0.45f;
    [Tooltip("쓰러지며 몬스터를 올려다보는 기본 상향 각도(음수 = 위로 올려다봄, 타겟 부재 시 폴백).")]
    public float fallPitch = -16f;
    [Tooltip("고개가 옆으로 꺾이는 기울기(도).")]
    public float fallRoll = 18f;
    public float killcamHoldFirstHit = 0.35f; // 쓰러져 몬스터/피를 바라보는 짧은 여운 (1차)
    public float killcamHoldFatal = 0.55f;    // 쓰러져 몬스터/피를 바라보는 여운 (2차 사망)

    [Header("Cutout Blackout (급암전)")]
    [Tooltip("몬스터 응시 직후 전체 화면이 툭 끊기듯 암전되는 시간(초).")]
    public float cutoutDuration = 0.15f;
    public float blackHold = 0.55f;           // 완전 암전 유지(파손/리스폰 처리 포함)
    public float openDuration = 0.65f;        // 1차 피격 부활 시 전체 시야 복귀 시간

    [Header("Get Up")]
    public float getUpDuration = 1.0f;

    [Header("Debug")]
    [Tooltip("F10 = Monster 1에게 강제로 잡힘, F12 = Monster 2(Hound)에게 강제로 잡힘 (에디터/개발 빌드 전용). F9 는 탈출구 컷씬, F11 은 Monster 2 등장 컷씬에 사용.")]
    public bool debugKey = true;

    FirstPersonController controller;
    PlayerInteraction interaction;
    PlayerBodyStatus body;
    CharacterController cc;

    bool running;
    bool currentFirstHit;
    Transform caughtBy;
    bool savedAgentEnabled;
    bool caughtDisabledHound;
    Vector3 savedHoundPos;
    Quaternion savedHoundRot;
    PlayerBloodDeathEffect bloodEffect;

    [Header("Face Monster")]
    public float faceDuration = 0.3f;
    public float faceHold = 0.17f;
    public float faceHeadHeight = 1.5f;   // MonsterStun.FacePosition 이 없을 때 monster.position + up * 이 값
    [Tooltip("포획 시 카메라 ↔ 몬스터 Head 본 수평 거리(m). 모델이 길어서(머리가 피벗 앞 ~1.7m) 피벗이 아니라 Head 기준으로 맞춘다.")]
    public float captureHeadDistance = 1.35f;

    [Header("Lunge (덮침)")]
    [Tooltip("덮침 시간(초).")] public float lungeDuration = 0.2f;
    [Tooltip("덮친 뒤 카메라 ↔ Head 본 수평 거리(m). 메시 최근접 정점은 이보다 약 0.37m 가깝다.")]
    public float lungeHeadDistance = 1.0f;

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
    float fallStartPitch;
    float fallTargetPitch = -16f;
    float effectiveFallBack = 0.45f;
    float fallRollSign = 1f;
    Vector3 impactPos;   // 공격 충격 카메라 오프셋 (로컬)
    Vector3 impactEuler;

    Image cutoutImage;
    GameObject cutoutCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    // AfterSceneLoad 는 프로세스 시작 시 한 번만 실행된다. Restart / Quit to Title 로 씬을 다시 불러와도
    // 새 Player 에 다시 붙도록 sceneLoaded 로 재부착한다 (안 그러면 재시작 후 잡힘 연출/부상이 통째로 빠진다).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Ensure();

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
        s.CaptureHoundSnapshot();
        s.StartCoroutine(s.Run());
    }

    /// <summary>Monster 2 (E-07 Hound) 잡힘/공격 시퀀스 실행 (F12 디버그 등).</summary>
    public static void TriggerCaughtHound()
    {
        var hound = FindFirstObjectByType<MonsterHoundAI>(FindObjectsInactive.Include);
        if (hound == null)
        {
            Debug.LogWarning("[PlayerCaughtSequence] F12: 씬에서 MonsterHoundAI(Monster 2)를 찾지 못했습니다.");
            return;
        }
        TriggerCaught(hound.transform);
    }

    void CaptureHoundSnapshot()
    {
        var hound = caughtBy != null ? caughtBy.GetComponent<MonsterHoundAI>() : null;
        caughtDisabledHound = hound != null && !hound.enabled;
        if (caughtDisabledHound)
        {
            savedHoundPos = hound.transform.position;
            savedHoundRot = hound.transform.rotation;
        }
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugKey && Keyboard.current != null)
        {
            if (Keyboard.current.f10Key.wasPressedThisFrame)
                TriggerCaught();
            else if (Keyboard.current.f12Key.wasPressedThisFrame)
                TriggerCaughtHound();
        }
#endif
    }

    PlayerBloodDeathEffect EnsureBloodEffect()
    {
        if (bloodEffect == null) bloodEffect = GetComponent<PlayerBloodDeathEffect>();
        if (bloodEffect == null) bloodEffect = gameObject.AddComponent<PlayerBloodDeathEffect>();
        return bloodEffect;
    }

    IEnumerator Run()
    {
        running = true;
        // 포획 확정: 진행 중인 은신 진입/퇴장 연출과 은신 상태, E : EXIT 안내를 모두 정리한다 (부활 후 이전 캐비닛으로 순간이동 방지)
        HideSpot.CancelAllPending();
        if (PlayerHide.Instance != null) PlayerHide.Instance.ForceReset();
        if (body == null) body = GetComponent<PlayerBodyStatus>();
        bool firstHit = body == null || body.DamagedCount == 0;
        currentFirstHit = firstHit;
        EnsureBloodEffect().ResetImmediate();
        Debug.Log("[PlayerCaughtSequence] Caught! firstHit=" + firstHit);

        // 진행 중인 QTE가 있으면 (이벤트 없이) 닫는다.
        SkillCheckManager.CancelActive();
        BeaconPipeQte.CancelActive();

        // 조작 차단: 이동/Sprint/시선/손전등(ControlsEnabled) + 상호작용/인벤토리 선택(InteractionEnabled)
        if (controller != null) controller.SetControlsEnabled(false);
        if (interaction != null) interaction.SetInteractionEnabled(false);

        EnsureBlackCutout();
        fallRollSign = Random.value < 0.5f ? -1f : 1f;
        // 호출자(MonsterChase.AttackPlayer 등)가 같은 프레임에 agent.isStopped/ResetPath 를 마칠 수 있도록 1프레임 양보
        yield return null;
        // 잡은 몬스터 쪽으로 시선을 부드럽게 돌리고 잠깐 바라본다 (등 돌린 상태에서도 몬스터가 보이도록)
        PrepareCapture();
        yield return FaceMonster();
        fallStartPitch = controller != null ? controller.LookPitch : 0f;
        camActive = true;               // 덮침/충격 카메라 오프셋 적용 시작 (camAmount=0 이라 정상 위치)
        yield return Lunge();
        effectiveFallBack = ComputeSafeFallBack();
        fallTargetPitch = ComputeFallTargetPitch();
        yield return Impact();

        camActive = true;

        // 쓰러짐 (고개가 옆으로 꺾이며 바닥에서 몬스터를 올려다봄 — 검은 막/조임 없음)
        float t = 0f;
        float fallTotal = Mathf.Max(0.05f, fallDuration);
        while (t < fallTotal)
        {
            t += Time.deltaTime;
            camAmount = Ease(Mathf.Clamp01(t / fallTotal), true);
            if (bloodEffect != null) bloodEffect.SetFallProgress(camAmount, 0f);
            yield return null;
        }
        camAmount = 1f;
        if (bloodEffect != null) bloodEffect.SetFallProgress(1f, 0f);

        // 쓰러진 채 몬스터와 피 물든 시야를 잠시 응시
        float linger = firstHit ? killcamHoldFirstHit : killcamHoldFatal;
        if (linger > 0f) yield return new WaitForSeconds(linger);

        // 마지막 순간 전체 화면 급암전 (컷아웃)
        float ct = 0f;
        float cutDur = Mathf.Max(0.02f, cutoutDuration);
        while (ct < cutDur)
        {
            ct += Time.deltaTime;
            float k = Mathf.Clamp01(ct / cutDur);
            SetBlackFade(k * k);
            if (bloodEffect != null) bloodEffect.SetFallProgress(1f, k * k);
            yield return null;
        }
        SetBlackFade(1f);
        if (bloodEffect != null) bloodEffect.SetFallProgress(1f, 1f);

        if (!firstHit)
        {
            // 두 번째 피격: 붉은 화면·피 흘림 상태에서 급암전 후 기존 Game Over 호출
            if (bloodEffect != null) bloodEffect.LockFatal();
            yield return new WaitForSeconds(0.12f);
            GameProgress.TriggerGameOver();
            yield break; // running 유지: 게임 오버 후 연출 재진입 없음
        }

        // 완전 암전 상태에서: 랜덤 부위 파손 → Respawn
        yield return new WaitForSeconds(blackHold * 0.4f);
        ApplyRandomDamage();
        Respawn();
        fallStartPitch = 0f;
        if (controller != null) controller.SetLookPitch(0f); // 부활 시선은 수평
        RestoreAgentAfterCutscene();
        RelocateMonster();
        if (HeartRateController.Instance != null) HeartRateController.Instance.SetBpm(resumeBpm); // 암전 동안 어느 정도 회복된 상태로 재개
        yield return new WaitForSeconds(blackHold * 0.6f);
        if (bloodEffect != null) bloodEffect.BeginRecovery(Mathf.Max(getUpDuration, openDuration) + 1.4f);

        // 일어남 + 전체 시야 복귀
        t = 0f;
        float recoverTotal = Mathf.Max(getUpDuration, openDuration);
        while (t < recoverTotal)
        {
            t += Time.deltaTime;
            camAmount = 1f - Ease(Mathf.Clamp01(t / getUpDuration), false);
            SetBlackFade(1f - Mathf.Clamp01(t / openDuration));
            yield return null;
        }
        camAmount = 0f;
        SetBlackFade(0f);
        camActive = false;
        ApplyCamera(0f); // 정상 위치 복귀 (이후는 PlayerLimpCamera 등 기존 로직)
        if (cutoutCanvas != null) cutoutCanvas.SetActive(false);

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
        TriggerAttackerAttack();
        yield return new WaitForSeconds(faceHold);
    }

    void TriggerAttackerAttack()
    {
        if (caughtBy == null) return;
        var movementAnim = caughtBy.GetComponent<MonsterMovementAnimator>();
        if (movementAnim != null)
        {
            movementAnim.TriggerAttack();
            return;
        }
        var hound = caughtBy.GetComponent<MonsterHoundAI>();
        if (hound != null)
            hound.ForceCaughtAttackState();
    }

    /// <summary>
    /// 덮침: 몬스터 전체를 짧고 빠르게(가속) 카메라 쪽으로 보간. NavMeshAgent는 연출 동안 비활성화 상태를 유지해
    /// Killcam 응시 중 옆 침대/벽 NavMesh로 순간이동하지 않도록 막는다.
    /// 최종 거리는 Head 본 기준 lungeHeadDistance 로 제한 (메시 관통 방지). 이미 더 가까우면 움직이지 않는다.
    /// </summary>
    IEnumerator Lunge()
    {
        if (caughtBy == null) yield break;
        var head = FindDeep(caughtBy, "Head");

        Vector3 p = transform.position;
        Vector3 start = caughtBy.position;
        Vector3 away = start - p; away.y = 0f;
        if (away.sqrMagnitude < 0.0004f) yield break;
        away.Normalize();

        Vector3 headOff = head != null ? head.position - start : -away * 0.8f;
        headOff.y = 0f;
        Vector3 headNow = start + headOff;                                 // 현재 Head (수평)
        float curDist = new Vector3(headNow.x - p.x, 0f, headNow.z - p.z).magnitude;
        Vector3 end = start;
        if (curDist > lungeHeadDistance)
            end = p + away * lungeHeadDistance - headOff;                  // Head 가 lungeHeadDistance 에 오도록
        end.y = start.y;

        float t = 0f;
        while (t < lungeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Pow(Mathf.Clamp01(t / lungeDuration), 1.7f);   // 가속: 끝에서 확 들어옴
            caughtBy.position = Vector3.Lerp(start, end, k);
            yield return null;
        }
        caughtBy.position = end;
    }

    /// <summary>공격이 닿는 순간 카메라에 짧은 충격 (뒤로 밀림 + Pitch 젖힘 + 아주 작은 X/Y/Roll). 바로 Fall 로 이어진다.</summary>
    IEnumerator Impact()
    {
        onBiteImpact?.Invoke();
        if (biteAudio != null && biteClip != null) biteAudio.PlayOneShot(biteClip); // 소리 없으면 아무것도 안 함
        if (bloodEffect != null) bloodEffect.TriggerHit(currentFirstHit);

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
        savedAgentEnabled = agent != null && agent.enabled;
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

        float baseY = Mathf.Abs(caughtBy.position.y - p.y) > 0.35f ? p.y - 0.04f : caughtBy.position.y;
        Vector3 desiredHead = p + away * captureHeadDistance;
        Vector3 desired = desiredHead - headOffset;
        desired.y = baseY;

        // 같은 바닥 높이(±0.35m)이면서 시선 축에서 크게 벗어나지 않을 때만 NavMesh 보정 적용 (침대 위/인접 NavMesh 섬 스냅 방지)
        Vector3 result = desired;
        if (UnityEngine.AI.NavMesh.SamplePosition(p, out var from, 1.2f, UnityEngine.AI.NavMesh.AllAreas)
            && Mathf.Abs(from.position.y - baseY) <= 0.35f)
        {
            if (UnityEngine.AI.NavMesh.Raycast(from.position, desired, out var edge, UnityEngine.AI.NavMesh.AllAreas)
                && Mathf.Abs(edge.position.y - baseY) <= 0.35f
                && Vector2.Distance(new Vector2(edge.position.x, edge.position.z), new Vector2(desired.x, desired.z)) <= 0.45f)
            {
                result = edge.position;
            }
            else if (UnityEngine.AI.NavMesh.SamplePosition(desired, out var onMesh, 0.5f, UnityEngine.AI.NavMesh.AllAreas)
                && Mathf.Abs(onMesh.position.y - baseY) <= 0.35f
                && Vector2.Distance(new Vector2(onMesh.position.x, onMesh.position.z), new Vector2(desired.x, desired.z)) <= 0.45f)
            {
                result = onMesh.position;
            }
        }

        // 연출 동안 NavMeshAgent를 꺼두어 비네비 바닥(침대 주변 등)에서 몬스터가 옆으로 튕겨나가지 않게 고정
        if (agent != null) agent.enabled = false;
        caughtBy.position = result;
        caughtBy.rotation = rot;
    }

    void RestoreAgentAfterCutscene()
    {
        if (caughtBy == null) return;
        var agent = caughtBy.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = savedAgentEnabled;
        savedAgentEnabled = false;
    }

    void RelocateMonster()
    {
        var hound = caughtBy != null ? caughtBy.GetComponent<MonsterHoundAI>() : null;
        if (hound != null)
        {
            if (caughtDisabledHound)
            {
                caughtDisabledHound = false;
                hound.RestoreDebugPosition(savedHoundPos, savedHoundRot);
                return;
            }
            var houndPoints = MonsterSpawnPoints.Instance != null ? MonsterSpawnPoints.Instance : FindFirstObjectByType<MonsterSpawnPoints>();
            if (houndPoints != null) houndPoints.RelocateHound(hound, transform.position);
            return;
        }

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

    float ComputeSafeFallBack()
    {
        if (controller == null || controller.playerCamera == null || fallBack <= 0f)
            return 0f;

        Transform pivot = controller.playerCamera.transform.parent != null
            ? controller.playerCamera.transform.parent
            : controller.transform;
        Vector3 backDir = -controller.transform.forward;
        backDir.y = 0f;
        if (backDir.sqrMagnitude < 0.0001f) return 0f;
        backDir.Normalize();

        float maxAllowed = fallBack;
        float[] heights = { -fallDrop * 0.25f, -fallDrop };
        for (int h = 0; h < heights.Length; h++)
        {
            Vector3 origin = pivot.TransformPoint(controller.firstPersonPosition + new Vector3(0f, heights[h], 0f));
            var hits = Physics.SphereCastAll(origin, 0.16f, backDir, maxAllowed + 0.14f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                float clear = Mathf.Max(0f, hits[i].distance - 0.14f);
                if (clear < maxAllowed) maxAllowed = clear;
            }
        }
        return maxAllowed;
    }

    float ComputeFallTargetPitch()
    {
        if (caughtBy == null || controller == null || controller.playerCamera == null)
            return fallPitch;

        Transform pivot = controller.playerCamera.transform.parent != null
            ? controller.playerCamera.transform.parent
            : controller.transform;
        Vector3 fallenCamPos = pivot.TransformPoint(
            controller.firstPersonPosition + new Vector3(0f, -fallDrop, -effectiveFallBack));
        Vector3 headTarget = TargetPoint();
        Vector3 chestTarget = caughtBy.position + Vector3.up * 0.72f;
        Vector3 aimPoint = Vector3.Lerp(chestTarget, headTarget, 0.88f);
        Vector3 d = aimPoint - fallenCamPos;
        float flat = new Vector2(d.x, d.z).magnitude;
        if (flat < 0.05f) return fallPitch;
        return Mathf.Clamp(-Mathf.Atan2(d.y, flat) * Mathf.Rad2Deg, -36f, -12f);
    }

    void ApplyCamera(float a)
    {
        if (controller == null || controller.playerCamera == null) return;
        var cam = controller.playerCamera.transform;
        controller.SetLookPitch(Mathf.Lerp(fallStartPitch, fallTargetPitch, a));
        cam.localPosition = controller.firstPersonPosition
            + new Vector3(0f, -fallDrop * a, -effectiveFallBack * a)
            + impactPos;
        cam.localRotation = Quaternion.Euler(impactEuler.x, 0f, fallRollSign * fallRoll * a + impactEuler.z);
    }

    // ───────────────────────── 전체 급암전 컷아웃 ─────────────────────────

    void EnsureBlackCutout()
    {
        if (cutoutCanvas != null) { cutoutCanvas.SetActive(true); return; }

        cutoutCanvas = new GameObject("DeathCutoutCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = cutoutCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 155; // 혈흔(147~149) 위, GameProgress(200) 아래
        var scaler = cutoutCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var go = new GameObject("BlackCutout", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(cutoutCanvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        cutoutImage = go.GetComponent<Image>();
        cutoutImage.color = new Color(0f, 0f, 0f, 0f);
        cutoutImage.raycastTarget = false;
        cutoutImage.gameObject.SetActive(false);
    }

    /// <summary>alpha 0 = 완전 투명, 1 = 전체 화면 암전 (사방 조임 없이 균일 페이드).</summary>
    void SetBlackFade(float alpha)
    {
        if (cutoutImage == null) return;
        alpha = Mathf.Clamp01(alpha);
        cutoutImage.color = new Color(0f, 0f, 0f, alpha);
        cutoutImage.gameObject.SetActive(alpha > 0.001f);
    }
}
