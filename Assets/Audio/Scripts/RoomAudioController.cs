using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방마다 다른 소리. AudioSystem 프리팹에 하나.
/// 매 프레임 AudioListener 위치가 어느 RoomAudioZone 안에 있는지 보고, 그 방의 RoomAudioProfile로 부드럽게 바꾼다.
///   - 잔향: 리스너를 따라다니는 AudioReverbZone 하나의 값을 프로필 값으로 보간
///           (SoundCue의 roomReverb 설정에 따라 소리마다 잔향을 받을지 결정. UI/BGM은 기본적으로 안 받음)
///   - 방 환경음: 프로필마다 LoopSoundEmitter를 하나씩 만들어 크로스페이드
///   - 드론 볼륨: WorldAudio가 DroneWeight를 곱해서 쓴다
///   - 랜덤 소리: 방에 있는 동안 플레이어 주변에서 가끔 재생
/// 어떤 Zone에도 없으면 defaultProfile (복도) 사용.
/// </summary>
public class RoomAudioController : MonoBehaviour
{
    public static RoomAudioController Instance { get; private set; }

    [Tooltip("어떤 방에도 속하지 않을 때 (복도 등)")]
    public RoomAudioProfile defaultProfile;
    [Tooltip("방이 바뀔 때 잔향/드론이 바뀌는 속도 (클수록 빠름)")]
    public float transitionSpeed = 2.5f;
    [Tooltip("방 환경음 페이드 시간(초)")]
    public float ambienceFade = 1.5f;
    [Tooltip("방이 바뀌는 경계에서 왔다갔다 깜빡이지 않도록, 새 방에 이 시간(초) 이상 있어야 바뀐다")]
    public float switchDelay = 0.15f;
    [Tooltip("방이 바뀔 때 콘솔에 로그")]
    public bool logRoomChanges = false;

    [Header("Occlusion (벽 너머 소리 먹먹하게)")]
    public AudioOcclusion.Settings occlusion = new AudioOcclusion.Settings();

    public RoomAudioProfile CurrentProfile { get; private set; }
    public RoomAudioZone CurrentZone { get; private set; }
    /// <summary>WorldAudio가 드론 볼륨에 곱하는 값</summary>
    public float DroneWeight { get; private set; } = 1f;

    AudioReverbZone reverbZone;
    RoomReverb currentReverb;
    readonly Dictionary<SoundCue, LoopSoundEmitter> loops = new Dictionary<SoundCue, LoopSoundEmitter>();
    RoomAudioZone pendingZone;
    float pendingSince;
    float nextRandom;
    Transform cachedListener;

    void Awake()
    {
        Instance = this;
        var go = new GameObject("RoomReverb");
        go.transform.SetParent(transform, false);
        reverbZone = go.AddComponent<AudioReverbZone>();
        // 리스너를 따라다니며 항상 안쪽에 있도록 아주 크게
        reverbZone.minDistance = 5000f;
        reverbZone.maxDistance = 5001f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnValidate() => AudioOcclusion.ClearWallCache();

    void Start()
    {
        var listener = Listener();
        CurrentZone = listener != null ? RoomAudioZone.Find(listener.position) : null;
        CurrentProfile = CurrentZone != null ? CurrentZone.profile : defaultProfile;
        currentReverb = CurrentProfile != null ? CurrentProfile.GetReverb() : RoomReverb.FromPreset(RoomReverbPreset.Off);
        currentReverb.ApplyTo(reverbZone);
        DroneWeight = CurrentProfile != null ? CurrentProfile.droneWeight : 1f;
        ApplyAmbience(CurrentProfile);
        ScheduleRandom();
    }

    void Update()
    {
        var listener = Listener();
        if (listener == null) return;
        reverbZone.transform.position = listener.position;

        // 현재 방 판정 (경계 깜빡임 방지 딜레이)
        var zone = RoomAudioZone.Find(listener.position);
        if (zone != CurrentZone)
        {
            if (zone != pendingZone) { pendingZone = zone; pendingSince = Time.time; }
            else if (Time.time - pendingSince >= switchDelay) SwitchTo(zone);
        }
        else { pendingZone = zone; pendingSince = Time.time; }

        // 잔향/드론 보간
        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);
        var targetReverb = CurrentProfile != null ? CurrentProfile.GetReverb() : RoomReverb.FromPreset(RoomReverbPreset.Off);
        currentReverb = RoomReverb.MoveTowards(currentReverb, targetReverb, t);
        currentReverb.ApplyTo(reverbZone);
        DroneWeight = Mathf.Lerp(DroneWeight, CurrentProfile != null ? CurrentProfile.droneWeight : 1f, t);

        // 방 안 랜덤 소리
        if (CurrentProfile != null && CurrentProfile.randomOneShot != null && Time.time >= nextRandom)
        {
            ScheduleRandom();
            Vector2 d = CurrentProfile.randomDistance;
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(Mathf.Min(d.x, d.y), Mathf.Max(d.x, d.y));
            Vector3 p = listener.position + new Vector3(dir.x, 0f, dir.y) * dist;
            // 방 밖으로 나가면 방 중심 쪽으로 당긴다
            if (CurrentZone != null && !CurrentZone.Contains(p)) p = Vector3.Lerp(p, CurrentZone.transform.position, 0.6f);
            SoundEmitter.PlayAt(CurrentProfile.randomOneShot, p);
        }
    }

    void SwitchTo(RoomAudioZone zone)
    {
        CurrentZone = zone;
        var next = zone != null ? zone.profile : defaultProfile;
        if (next == CurrentProfile) return;
        if (logRoomChanges) Debug.Log($"[RoomAudio] {(CurrentProfile ? CurrentProfile.name : "none")} → {(next ? next.name : "none")} ({(zone ? zone.name : "default")})");
        CurrentProfile = next;
        ApplyAmbience(next);
        ScheduleRandom();
    }

    void ApplyAmbience(RoomAudioProfile p)
    {
        SoundCue want = p != null ? p.ambienceLoop : null;
        if (want != null && !loops.ContainsKey(want))
        {
            var go = new GameObject("RoomAmbience_" + want.name);
            go.transform.SetParent(transform, false);
            var loop = go.AddComponent<LoopSoundEmitter>();
            loop.cue = want;
            loops[want] = loop;
        }
        foreach (var kv in loops)
        {
            kv.Value.fadeInTime = ambienceFade;
            kv.Value.fadeOutTime = ambienceFade;
            kv.Value.SetPlaying(kv.Key == want);
        }
    }

    void ScheduleRandom()
    {
        if (CurrentProfile == null) { nextRandom = float.MaxValue; return; }
        Vector2 r = CurrentProfile.randomInterval;
        nextRandom = Time.time + Random.Range(Mathf.Min(r.x, r.y), Mathf.Max(r.x, r.y));
    }

    Transform Listener()
    {
        if (cachedListener == null || !cachedListener.gameObject.activeInHierarchy)
        {
            var l = FindFirstObjectByType<AudioListener>();
            cachedListener = l != null ? l.transform : null;
        }
        return cachedListener;
    }
}
