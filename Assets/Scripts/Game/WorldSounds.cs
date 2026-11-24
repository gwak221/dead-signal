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

    static WorldSounds instance;
    readonly Dictionary<Object, float> lastLocked = new Dictionary<Object, float>();
    EscapeDoorController escapeWall;

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
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HookEscapeWall();
    }

    void OnDisable()
    {
        DoorPair.AnyMoved -= HandleDoorPair;
        DoorPair.AnyLocked -= HandleDoorPairLocked;
        PasswordDoor.AnyMoved -= HandlePasswordDoor;
        SafeDoor.AnyMoved -= HandleSafe;
        SafeDoor.AnyLocked -= HandleSafeLocked;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnhookEscapeWall();
        CancelInvoke();
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
}
