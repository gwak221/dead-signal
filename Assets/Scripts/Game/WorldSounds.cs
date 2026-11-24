using System.Collections.Generic;
using DeadSignal.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 월드 소리 호출 담당 (게임 코드). 게임 시작 때 스스로 생기므로 씬에 배치할 필요가 없다.
/// 게임 이벤트를 듣고 AudioManager를 이름으로 부를 뿐, 게임 로직은 바꾸지 않는다.
///
///   일반 문(DoorPair)   : 열림 door_open (적이 열면 door_open_monster, 없으면 door_open), 닫힘 door_close, 잠김 door_locked
///   비밀번호 문         : 열림 door_open, 닫힘 door_close
///   금고(SafeDoor)      : 열림 pz_safe_open (등록 전엔 무음), 닫힘 door_close, 잠김 door_locked
///   탈출 문             : 램프가 모두 켜진 뒤 문이 움직이기 시작할 때(openDelay 후) esc_door_open (2D)
///   잠김 소리는 같은 문에서 lockedCooldown 안에 다시 나지 않는다 (연타 방지).
///   비콘                : 비활성 동안 bcn_hum_off, 활성 동안 bcn_hum_on 루프 (바뀔 때 페이드). 위치는 비콘의 "Audio" 자식(없으면 비콘)
///                         배터리 장착 bcn_battery_insert → 0.35초 뒤 bcn_activate. 파이프 미니게임 소리는 클립을 찾는 중(비워 둠)
///   아이템 상자 QTE     : SkillCheckManager 성공 qte_success / 실패 qte_fail (2D)
/// </summary>
[DisallowMultipleComponent]
public class WorldSounds : MonoBehaviour
{
    [Header("문")]
    public string doorOpenSound = "door_open";
    public string doorOpenMonsterSound = "door_open_monster";
    public string doorCloseSound = "door_close";
    public string doorLockedSound = "door_locked";
    public string safeOpenSound = "pz_safe_open";
    [Tooltip("같은 문에서 잠김 소리가 다시 나기까지 최소 시간(초)")] public float lockedCooldown = 0.4f;

    [Header("탈출 문")]
    public string escapeDoorOpenSound = "esc_door_open";

    [Header("비콘")]
    public string beaconHumOffSound = "bcn_hum_off";
    public string beaconHumOnSound = "bcn_hum_on";
    public string beaconBatterySound = "bcn_battery_insert";
    public string beaconActivateSound = "bcn_activate";
    [Header("아이템 상자 QTE (SkillCheckManager, LootQteGate)")]
    public string qteSuccessSound = "qte_success";
    public string qteFailSound = "qte_fail";
    [Tooltip("배터리 장착 소리 후 활성화 소리까지 간격(초)")] public float beaconActivateDelay = 0.35f;
    [Tooltip("작동음 루프가 새로 시작될 때 페이드인(초)")] public float humFadeIn = 1.5f;
    [Tooltip("작동음 루프가 바뀌거나 꺼질 때 페이드아웃(초)")] public float humFadeOut = 0.6f;

    static WorldSounds instance;
    readonly Dictionary<Object, float> lastLocked = new Dictionary<Object, float>();
    EscapeDoorController escapeWall;

    // 비콘마다 작동음 루프 하나 (비활성 → bcn_hum_off, 활성 → bcn_hum_on)
    class BeaconHum
    {
        public Beacon beacon;
        public Transform point;     // 소리 위치: 비콘 아래 "Audio" 자식이 있으면 거기, 없으면 비콘
        public SoundHandle handle = SoundHandle.None;
        public bool playingOn;      // 지금 루프가 hum_on 인지
        public float fade = 1f;     // 페이드인 진행 0~1
    }
    readonly List<BeaconHum> beaconHums = new List<BeaconHum>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[WorldSounds]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<WorldSounds>();
    }

    void OnEnable()
    {
        DoorPair.AnyMoved += HandleDoorPair;
        DoorPair.AnyLocked += HandleDoorPairLocked;
        PasswordDoor.AnyMoved += HandlePasswordDoor;
        SafeDoor.AnyMoved += HandleSafe;
        SafeDoor.AnyLocked += HandleSafeLocked;
        Beacon.AnyActivated += HandleBeaconActivated;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HookEscapeWall();
        CollectBeacons();
    }

    void OnDisable()
    {
        DoorPair.AnyMoved -= HandleDoorPair;
        DoorPair.AnyLocked -= HandleDoorPairLocked;
        PasswordDoor.AnyMoved -= HandlePasswordDoor;
        SafeDoor.AnyMoved -= HandleSafe;
        SafeDoor.AnyLocked -= HandleSafeLocked;
        Beacon.AnyActivated -= HandleBeaconActivated;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnhookEscapeWall();
        CancelInvoke();
        StopAllCoroutines();
        UnhookSkillCheck();
        StopBeaconHums(0f);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void HandleSceneLoaded(Scene s, LoadSceneMode m)
    {
        UnhookEscapeWall();
        CancelInvoke();
        lastLocked.Clear();
        HookEscapeWall();
        CollectBeacons();
    }

    // ───────────── 문 ─────────────

    void HandleDoorPair(DoorPair door, bool opening, bool byPlayer)
    {
        string name = !opening ? doorCloseSound
                    : !byPlayer && AudioManager.Has(doorOpenMonsterSound) ? doorOpenMonsterSound
                    : doorOpenSound;
        AudioManager.Play(name, door.GetDoorCenter());
    }

    void HandleDoorPairLocked(DoorPair door) => PlayLocked(door, door.GetDoorCenter());

    void HandlePasswordDoor(PasswordDoor door, bool opening)
        => AudioManager.Play(opening ? doorOpenSound : doorCloseSound, door.transform.position);

    void HandleSafe(SafeDoor safe, bool opening)
    {
        if (opening)
        {
            // 금고 여는 소리는 클립이 생기면 등록 (docs/spec/audio-list.md 대기 목록)
            if (AudioManager.Has(safeOpenSound)) AudioManager.Play(safeOpenSound, safe.transform.position);
        }
        else AudioManager.Play(doorCloseSound, safe.transform.position);
    }

    void HandleSafeLocked(SafeDoor safe) => PlayLocked(safe, safe.transform.position);

    void PlayLocked(Object door, Vector3 position)
    {
        if (lastLocked.TryGetValue(door, out float t) && Time.time - t < lockedCooldown) return;
        lastLocked[door] = Time.time;
        AudioManager.Play(doorLockedSound, position);
    }

    // ───────────── 탈출 문 ─────────────

    void HookEscapeWall()
    {
        escapeWall = FindFirstObjectByType<EscapeDoorController>();
        if (escapeWall != null) escapeWall.onAllLampsActive.AddListener(HandleEscapeOpening);
    }

    void UnhookEscapeWall()
    {
        if (escapeWall != null) escapeWall.onAllLampsActive.RemoveListener(HandleEscapeOpening);
        escapeWall = null;
    }

    // onAllLampsActive 후 openDelay 만큼 기다렸다가 문이 움직이기 시작한다 (EscapeDoorController.OpenRoutine)
    void HandleEscapeOpening()
    {
        if (escapeWall == null) return;
        Invoke(nameof(PlayEscapeDoor), escapeWall.openDelay);
    }

    void PlayEscapeDoor() => AudioManager.Play(escapeDoorOpenSound);

    // ───────────── 비콘 ─────────────

    void CollectBeacons()
    {
        StopBeaconHums(0f);
        beaconHums.Clear();
        foreach (var b in FindObjectsByType<Beacon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            beaconHums.Add(new BeaconHum { beacon = b, point = SoundPoint(b) });
        }
    }

    // 소리 위치: 비콘 아래 "Audio" 자식이 있으면 거기. 없으면 모델(렌더러) 중심에 실행 중에만 쓰는 점을 만든다.
    // 비콘 피벗은 바닥에 있어서 그대로 쓰면 소리가 발밑에서 난다.
    static Transform SoundPoint(Beacon b)
    {
        Transform t = b.transform.Find("Audio");
        if (t != null) return t;
        t = b.transform.Find("[AudioPoint]");
        if (t != null) return t;

        var renderers = b.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return b.transform;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        var go = new GameObject("[AudioPoint]");
        go.transform.SetParent(b.transform, false);
        go.transform.position = bounds.center;
        return go.transform;
    }

    void StopBeaconHums(float fadeOut)
    {
        foreach (var h in beaconHums)
        {
            h.handle.Stop(fadeOut);
            h.handle = SoundHandle.None;
        }
    }

    // 비콘 상태(IsActivated)를 보고 작동음 루프를 고른다. 비콘이 꺼진 오브젝트면 소리도 끈다.
    void Update()
    {
        HookSkillCheck(); // SkillCheckManager는 늦게 생기거나 씬과 함께 바뀔 수 있다
        UpdateBeaconHums();
    }

    void UpdateBeaconHums()
    {
        for (int i = beaconHums.Count - 1; i >= 0; i--)
        {
            var h = beaconHums[i];
            if (h.beacon == null) { h.handle.Stop(humFadeOut); beaconHums.RemoveAt(i); continue; }

            if (!h.beacon.isActiveAndEnabled)
            {
                if (h.handle.IsPlaying) h.handle.Stop(humFadeOut);
                h.handle = SoundHandle.None;
                continue;
            }

            bool on = h.beacon.IsActivated;
            if (!h.handle.IsPlaying || h.playingOn != on)
            {
                h.handle.Stop(humFadeOut);
                h.handle = AudioManager.Play(on ? beaconHumOnSound : beaconHumOffSound, h.point);
                h.playingOn = on;
                h.fade = humFadeIn > 0f ? 0f : 1f;
                if (h.handle.IsValid) h.handle.SetVolume(h.fade);
            }

            if (h.fade < 1f && h.handle.IsValid)
            {
                h.fade = Mathf.MoveTowards(h.fade, 1f, Time.deltaTime / humFadeIn);
                h.handle.SetVolume(h.fade);
            }
        }
    }

    void HandleBeaconActivated(Beacon b)
    {
        Vector3 p = PointOf(b);
        AudioManager.Play(beaconBatterySound, p);
        StartCoroutine(PlayLater(beaconActivateSound, p, beaconActivateDelay));
    }

    // ───────────── 아이템 상자 QTE ─────────────
    // SkillCheckManager의 OnSuccess / OnFail은 실제 판정 때만 발생한다 (잡혀서 취소될 때는 안 남).
    // 비콘은 파이프 미니게임(BeaconPipeQte)으로 바뀌어 이 소리를 쓰지 않는다.

    SkillCheckManager skillCheck;

    void HookSkillCheck()
    {
        var sk = SkillCheckManager.Instance;
        if (sk == skillCheck) return;
        UnhookSkillCheck();
        skillCheck = sk;
        if (skillCheck == null) return;
        skillCheck.OnSuccess?.AddListener(HandleQteSuccess);
        skillCheck.OnFail?.AddListener(HandleQteFail);
    }

    void UnhookSkillCheck()
    {
        if (skillCheck != null)
        {
            skillCheck.OnSuccess?.RemoveListener(HandleQteSuccess);
            skillCheck.OnFail?.RemoveListener(HandleQteFail);
        }
        skillCheck = null;
    }

    void HandleQteSuccess() => AudioManager.Play(qteSuccessSound);

    void HandleQteFail() => AudioManager.Play(qteFailSound);

    Vector3 PointOf(Beacon b)
    {
        foreach (var h in beaconHums) if (h.beacon == b) return h.point.position;
        return b.transform.position;
    }

    System.Collections.IEnumerator PlayLater(string name, Vector3 position, float delay)
    {
        yield return new WaitForSeconds(delay);
        AudioManager.Play(name, position);
    }
}
