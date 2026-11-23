using UnityEngine;

/// <summary>
/// SoundCue를 끊김 없이 반복 재생하는 공용 컴포넌트. 켜고 끌 때 페이드한다.
/// 한 오브젝트에 여러 개 붙여도 된다 (각자 자기 AudioSource를 만든다).
/// 사용: SetPlaying(true/false), 또는 volumeWeight로 다른 루프와 크로스페이드.
/// </summary>
public class LoopSoundEmitter : MonoBehaviour
{
    public SoundCue cue;
    [Tooltip("시작하자마자 재생")]
    public bool playOnStart = false;
    [Tooltip("켤 때 0 → 최대까지 걸리는 시간(초)")]
    public float fadeInTime = 0.5f;
    [Tooltip("끌 때 최대 → 0까지 걸리는 시간(초)")]
    public float fadeOutTime = 0.8f;
    [Tooltip("재생 위치를 랜덤으로 시작 (같은 루프가 여러 곳에서 동시에 날 때 겹침 방지)")]
    public bool randomStartTime = true;
    [Tooltip("추가 볼륨 배율 (다른 스크립트가 크로스페이드에 사용)")]
    [Range(0f, 1f)] public float volumeWeight = 1f;
    [Tooltip("페이드에 unscaled 시간 사용 (timeScale 0인 메뉴에서도 페이드)")]
    public bool useUnscaledTime = false;

    AudioSource source;
    bool wantPlaying;
    float fade; // 0~1

    public bool IsPlaying => wantPlaying;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
    }

    void Start()
    {
        if (playOnStart) SetPlaying(true, instant: true);
    }

    public void SetPlaying(bool on) => SetPlaying(on, false);

    public void SetPlaying(bool on, bool instant)
    {
        wantPlaying = on;
        if (on) EnsureStarted();
        if (instant) fade = on ? 1f : 0f;
        Apply();
        if (!on && instant && source != null) source.Stop();
    }

    /// <summary>재생 중에 다른 cue로 바꾼다 (짧게 끊기므로 같은 계열 소리끼리 사용).</summary>
    public void SetCue(SoundCue newCue)
    {
        if (newCue == cue) return;
        cue = newCue;
        if (source != null && source.isPlaying) { source.Stop(); EnsureStarted(); }
    }

    void EnsureStarted()
    {
        if (source == null || cue == null) return;
        if (source.isPlaying) return;
        var clip = cue.PickClip();
        if (clip == null) return;
        cue.ApplyTo(source);
        source.clip = clip;
        source.pitch = cue.RandomPitch();
        if (randomStartTime && clip.length > 0.1f) source.time = Random.Range(0f, clip.length * 0.95f);
        source.Play();
    }

    void Update()
    {
        // 켜져 있어야 하는데 멈춰 있으면 (처음엔 클립이 없었다가 나중에 생긴 경우 등) 다시 시작
        if (wantPlaying && source != null && !source.isPlaying) EnsureStarted();

        float target = wantPlaying ? 1f : 0f;
        float dur = wantPlaying ? fadeInTime : fadeOutTime;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        fade = dur <= 0f ? target : Mathf.MoveTowards(fade, target, dt / dur);
        Apply();
        if (!wantPlaying && fade <= 0f && source != null && source.isPlaying) source.Stop();
    }

    void Apply()
    {
        if (source == null) return;
        float v = cue != null ? cue.volume : 0f;
        source.volume = v * fade * volumeWeight;
    }

    void OnDisable()
    {
        fade = 0f;
        if (source != null) source.Stop();
    }

    void OnEnable()
    {
        if (wantPlaying) EnsureStarted();
    }
}
