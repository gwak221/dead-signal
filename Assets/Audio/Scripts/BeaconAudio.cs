using UnityEngine;

/// <summary>
/// 비콘 사운드 연결 컴포넌트. 비콘(Beacon) 아래 "Audio" 자식에 붙는다.
///   비활성/활성 기계음 : Beacon.IsActivated를 보고 두 루프를 전환 (F9 디버그 완료도 반영)
///   QTE 성공/실패      : Beacon.AnyQteResult (이 비콘 것만)
///   배터리 장착 → 활성화 : Beacon.AnyActivated (장착 소리 후 activateDelay 뒤 활성화 소리)
/// </summary>
public class BeaconAudio : MonoBehaviour
{
    public SoundCue humOffCue;
    public SoundCue humOnCue;
    public SoundCue qteSuccessCue;
    public SoundCue qteFailCue;
    public SoundCue batteryInsertCue;
    public SoundCue activateCue;
    [Tooltip("배터리 장착 소리 후 활성화 소리까지 간격(초)")]
    public float activateDelay = 0.35f;

    Beacon beacon;
    SoundEmitter emitter;
    LoopSoundEmitter humOff;
    LoopSoundEmitter humOn;

    void Awake()
    {
        beacon = GetComponentInParent<Beacon>();
        emitter = GetComponent<SoundEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<SoundEmitter>();
        humOff = CreateLoop(humOffCue);
        humOn = CreateLoop(humOnCue);
    }

    LoopSoundEmitter CreateLoop(SoundCue cue)
    {
        var l = gameObject.AddComponent<LoopSoundEmitter>();
        l.cue = cue;
        l.fadeInTime = 1.5f;
        l.fadeOutTime = 0.6f;
        return l;
    }

    void OnEnable()
    {
        Beacon.AnyQteResult += HandleQte;
        Beacon.AnyActivated += HandleActivated;
    }

    void OnDisable()
    {
        Beacon.AnyQteResult -= HandleQte;
        Beacon.AnyActivated -= HandleActivated;
    }

    void Update()
    {
        bool on = beacon != null && beacon.IsActivated;
        if (humOn.IsPlaying != on) humOn.SetPlaying(on);
        if (humOff.IsPlaying == on) humOff.SetPlaying(!on);
    }

    void HandleQte(Beacon b, bool success)
    {
        if (b != beacon) return;
        emitter.Play(success ? qteSuccessCue : qteFailCue);
    }

    void HandleActivated(Beacon b)
    {
        if (b != beacon) return;
        emitter.Play(batteryInsertCue);
        Invoke(nameof(PlayActivate), activateDelay);
    }

    void PlayActivate() => emitter.Play(activateCue);
}
