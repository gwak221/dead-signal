using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 전체 사운드 연결 컴포넌트. AudioSystem 프리팹에 하나.
/// 오브젝트가 많거나(문, 형광등) 하나뿐인(손전등, 비밀번호 UI, 진행도) 대상의 이벤트를 받아 재생한다.
/// 씬/프리팹에 아무것도 추가하지 않고 런타임에 연결하므로 씬 충돌이 없다.
///   문(DoorPair) / 비밀번호 문 / 금고 / 탈출 문
///   손전등 켜기/끄기, 눈뽕 충전/발사, 배터리 교체, 빈 딸깍
///   비밀번호 키 입력, 정답/오답
///   마지막 비콘 → 탈출 안내, 게임 오버, 탈출 성공
///   환경음: 추격 중 드론 줄이기, 멀리서 나는 랜덤 소리, 형광등/비상등 웅웅, 숨었을 때 소리 먹먹하게
/// </summary>
public class WorldAudio : MonoBehaviour
{
    [Header("Doors")]
    public SoundCue doorOpenCue;
    public SoundCue doorCloseCue;
    public SoundCue doorOpenMonsterCue;
    public SoundCue doorLockedCue;
    public SoundCue safeOpenCue;
    public SoundCue escapeDoorOpenCue;

    [Header("Flashlight")]
    public SoundCue flashlightOnCue;
    public SoundCue flashlightOffCue;
    public SoundCue flashChargeCue;
    public SoundCue flashCue;
    public SoundCue flashlightReloadCue;
    public SoundCue flashlightEmptyCue;

    [Header("Keypad")]
    public SoundCue keypadPressCue;
    public SoundCue keypadOkCue;
    public SoundCue keypadNgCue;

    [Header("Progress")]
    public SoundCue allBeaconsCue;
    public SoundCue gameOverCue;
    public SoundCue escapedCue;

    [Header("Ambience")]
    [Tooltip("AudioSystem 안의 드론 루프. 추격 중에는 chaseDuck 비율로 줄인다")]
    public LoopSoundEmitter drone;
    [Range(0f, 1f)] public float chaseDuck = 0.35f;
    [Tooltip("플레이어 주변 랜덤 위치에서 나는 소리 (RandomVocalizer, 리스너를 따라다님)")]
    public RandomVocalizer randomAmbience;

    public SoundCue fluorescentCue;
    [Tooltip("이름이 이걸로 시작하는 Light에 형광등 웅웅 소리를 붙인다")]
    public string fluorescentLightPrefix = "Fluo";
    public float fluorescentSpacing = 9f;
    public int fluorescentMax = 24;

    public SoundCue emergencyCue;
    public string emergencyLightPrefix = "Emergency";
    public float emergencySpacing = 6f;
    public int emergencyMax = 16;

    [Header("Hiding (숨었을 때 소리 먹먹하게)")]
    public bool muffleWhenHiding = true;
    public float muffledCutoff = 1400f;
    public float muffleSpeed = 4f;

    FlashlightController flashlight;
    PasswordUIController keypad;
    EscapeDoorController escapeWall;
    AudioLowPassFilter listenerFilter;
    bool escapeUnlockedSeen;
    bool endedSeen;

    void OnEnable()
    {
        DoorPair.AnyMoved += HandleDoorPair;
        PasswordDoor.AnyMoved += HandlePasswordDoor;
        SafeDoor.AnyMoved += HandleSafe;
        SafeDoor.AnyLocked += HandleSafeLocked;
        DoorPair.AnyLocked += HandleDoorPairLocked;
        GameProgress.Changed += HandleProgress;
    }

    void OnDisable()
    {
        DoorPair.AnyMoved -= HandleDoorPair;
        PasswordDoor.AnyMoved -= HandlePasswordDoor;
        SafeDoor.AnyMoved -= HandleSafe;
        SafeDoor.AnyLocked -= HandleSafeLocked;
        DoorPair.AnyLocked -= HandleDoorPairLocked;
        GameProgress.Changed -= HandleProgress;
        if (flashlight != null) UnhookFlashlight();
        if (keypad != null) { keypad.OnTyped -= HandleTyped; keypad.OnSubmitted -= HandleSubmitted; }
        if (escapeWall != null) escapeWall.onAllLampsActive.RemoveListener(HandleEscapeOpening);
    }

    void Start()
    {
        SpawnLightHums(fluorescentCue, fluorescentLightPrefix, fluorescentSpacing, fluorescentMax);
        SpawnLightHums(emergencyCue, emergencyLightPrefix, emergencySpacing, emergencyMax);

        escapeWall = FindFirstObjectByType<EscapeDoorController>();
        if (escapeWall != null) escapeWall.onAllLampsActive.AddListener(HandleEscapeOpening);
    }

    float nextLateHook;

    void Update()
    {
        // 손전등/비밀번호 UI는 런타임에 생기거나 늦게 준비될 수 있어 주기적으로 연결 시도
        if (Time.unscaledTime >= nextLateHook && (flashlight == null || keypad == null))
        {
            nextLateHook = Time.unscaledTime + 0.5f;
            if (flashlight == null && (flashlight = FindFirstObjectByType<FlashlightController>()) != null) HookFlashlight();
            if (keypad == null && (keypad = PasswordUIController.Instance) != null) { keypad.OnTyped += HandleTyped; keypad.OnSubmitted += HandleSubmitted; }
        }

        var listener = AudioListenerTransform();

        // 추격 중 드론 줄이기
        if (drone != null)
        {
            bool chase = ChaseMusicController.Instance != null && ChaseMusicController.Instance.IsChaseMusicOn;
            drone.volumeWeight = Mathf.MoveTowards(drone.volumeWeight, chase ? chaseDuck : 1f, Time.deltaTime * 0.8f);
        }

        // 랜덤 환경음은 리스너 주변에서
        if (randomAmbience != null && listener != null) randomAmbience.transform.position = listener.position;

        // 숨었을 때 먹먹하게
        if (muffleWhenHiding && listener != null)
        {
            if (listenerFilter == null || listenerFilter.transform != listener)
            {
                listenerFilter = listener.GetComponent<AudioLowPassFilter>();
                if (listenerFilter == null) { listenerFilter = listener.gameObject.AddComponent<AudioLowPassFilter>(); listenerFilter.cutoffFrequency = 22000f; }
            }
            float target = PlayerHide.IsPlayerHiding ? muffledCutoff : 22000f;
            // 로그 스케일로 보간해야 자연스럽다
            float cur = Mathf.Log(listenerFilter.cutoffFrequency), tgt = Mathf.Log(target);
            listenerFilter.cutoffFrequency = Mathf.Exp(Mathf.MoveTowards(cur, tgt, muffleSpeed * Time.deltaTime));
        }
    }

    Transform cachedListener;
    Transform AudioListenerTransform()
    {
        if (cachedListener == null || !cachedListener.gameObject.activeInHierarchy)
        {
            var l = FindFirstObjectByType<AudioListener>();
            cachedListener = l != null ? l.transform : null;
        }
        return cachedListener;
    }

    // ───────────── 문 ─────────────

    void HandleDoorPair(DoorPair door, bool opening, bool byPlayer)
    {
        SoundCue cue = opening ? (byPlayer ? doorOpenCue : (doorOpenMonsterCue != null ? doorOpenMonsterCue : doorOpenCue)) : doorCloseCue;
        SoundEmitter.PlayAt(cue, door.GetDoorCenter());
    }

    void HandlePasswordDoor(PasswordDoor door, bool opening) => SoundEmitter.PlayAt(opening ? doorOpenCue : doorCloseCue, door.transform.position);

    void HandleSafe(SafeDoor safe, bool opening) => SoundEmitter.PlayAt(opening ? safeOpenCue : doorCloseCue, safe.transform.position);

    void HandleSafeLocked(SafeDoor safe) => SoundEmitter.PlayAt(doorLockedCue, safe.transform.position);

    void HandleDoorPairLocked(DoorPair door) => SoundEmitter.PlayAt(doorLockedCue, door.GetDoorCenter());

    void HandleEscapeOpening()
    {
        if (escapeWall == null) return;
        Invoke(nameof(PlayEscapeDoor), escapeWall.openDelay);
    }

    void PlayEscapeDoor()
    {
        Vector3 p = escapeWall.doorLeft != null && escapeWall.doorRight != null
            ? (escapeWall.doorLeft.position + escapeWall.doorRight.position) * 0.5f
            : escapeWall.transform.position;
        SoundEmitter.PlayAt(escapeDoorOpenCue, p);
    }

    // ───────────── 손전등 ─────────────

    void HookFlashlight()
    {
        flashlight.OnLightToggled += HandleLight;
        flashlight.OnChargeStarted += HandleCharge;
        flashlight.OnMonsterStunned += HandleFlash;
        flashlight.OnReloadStarted += HandleReload;
        flashlight.OnEmptyTriggered += HandleEmpty;
    }

    void UnhookFlashlight()
    {
        flashlight.OnLightToggled -= HandleLight;
        flashlight.OnChargeStarted -= HandleCharge;
        flashlight.OnMonsterStunned -= HandleFlash;
        flashlight.OnReloadStarted -= HandleReload;
        flashlight.OnEmptyTriggered -= HandleEmpty;
    }

    void HandleLight(bool on) => SoundEmitter.Play2D(on ? flashlightOnCue : flashlightOffCue);
    void HandleCharge() => SoundEmitter.Play2D(flashChargeCue);
    void HandleFlash(MonsterStun _) => SoundEmitter.Play2D(flashCue);
    void HandleReload() => SoundEmitter.Play2D(flashlightReloadCue);
    void HandleEmpty() => SoundEmitter.Play2D(flashlightEmptyCue);

    // ───────────── 비밀번호 ─────────────

    void HandleTyped() => SoundEmitter.Play2D(keypadPressCue);
    void HandleSubmitted(bool ok) => SoundEmitter.Play2D(ok ? keypadOkCue : keypadNgCue);

    // ───────────── 진행 ─────────────

    void HandleProgress()
    {
        if (!escapeUnlockedSeen && GameProgress.ActiveBeacons > 0 && GameProgress.EscapeUnlocked)
        {
            escapeUnlockedSeen = true;
            SoundEmitter.Play2D(allBeaconsCue);
        }
        if (!endedSeen && GameProgress.IsEnded)
        {
            endedSeen = true;
            SoundEmitter.Play2D(GameProgress.HasEscaped ? escapedCue : gameOverCue);
        }
    }

    // ───────────── 형광등 / 비상등 ─────────────

    void SpawnLightHums(SoundCue cue, string prefix, float spacing, int max)
    {
        if (cue == null || string.IsNullOrEmpty(prefix) || max <= 0) return;
        var picked = new List<Vector3>();
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.InstanceID))
        {
            if (!light.name.StartsWith(prefix)) continue;
            Vector3 p = light.transform.position;
            bool tooClose = false;
            foreach (var q in picked) if ((q - p).sqrMagnitude < spacing * spacing) { tooClose = true; break; }
            if (tooClose) continue;
            picked.Add(p);

            var go = new GameObject("Hum_" + cue.name);
            go.transform.SetParent(light.transform, false);
            var loop = go.AddComponent<LoopSoundEmitter>();
            loop.cue = cue;
            loop.fadeInTime = 2f;
            loop.SetPlaying(true);
            if (picked.Count >= max) break;
        }
    }
}
