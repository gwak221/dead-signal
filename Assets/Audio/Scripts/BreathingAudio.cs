using UnityEngine;

/// <summary>
/// 심박수(BPM)에 따라 대기 숨소리와 긴장 숨소리를 크로스페이드한다.
/// tenseBpm 이상이면 긴장, calmBpm 이하로 내려가야 대기로 돌아온다 (경계에서 왔다 갔다 방지).
/// HeartRateController.Instance를 읽기만 하고 수정하지 않는다.
/// </summary>
public class BreathingAudio : MonoBehaviour
{
    public SoundCue idleBreathCue;
    public SoundCue tenseBreathCue;

    [Tooltip("이 BPM 이상이면 긴장 숨소리")]
    public float tenseBpm = 120f;
    [Tooltip("이 BPM 이하로 내려가야 대기 숨소리로 복귀")]
    public float calmBpm = 110f;
    [Tooltip("두 숨소리가 바뀌는 데 걸리는 시간(초)")]
    public float crossfadeTime = 1.2f;

    LoopSoundEmitter idleLoop;
    LoopSoundEmitter tenseLoop;
    bool tense;
    float mix; // 0 = 대기, 1 = 긴장

    public bool IsTense => tense;
    /// <summary>외부에서 끌 때 (잡힘 연출, 컷신 등).</summary>
    public bool Muted { get; set; }

    void Awake()
    {
        idleLoop = CreateLoop(idleBreathCue);
        tenseLoop = CreateLoop(tenseBreathCue);
    }

    LoopSoundEmitter CreateLoop(SoundCue cue)
    {
        var loop = gameObject.AddComponent<LoopSoundEmitter>();
        loop.cue = cue;
        loop.fadeInTime = 0.6f;
        loop.fadeOutTime = 0.6f;
        loop.randomStartTime = true;
        return loop;
    }

    void Update()
    {
        var hr = HeartRateController.Instance;
        if (hr != null)
        {
            float bpm = hr.CurrentBpm;
            if (!tense && bpm >= tenseBpm) tense = true;
            else if (tense && bpm <= calmBpm) tense = false;
        }

        mix = Mathf.MoveTowards(mix, tense ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, crossfadeTime));

        bool on = !Muted;
        if (idleLoop.IsPlaying != on) idleLoop.SetPlaying(on);
        if (tenseLoop.IsPlaying != on) tenseLoop.SetPlaying(on);
        idleLoop.volumeWeight = 1f - mix;
        tenseLoop.volumeWeight = mix;
    }
}
