using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 쫓김 BGM과 따돌림 소리. 씬에 하나 (AudioSystem 프리팹 안).
/// 쫓는 적이 0 → 1이 되면 BGM을 켜고, 1 → 0이 되면 BGM을 끄고 따돌림 소리를 낸다.
/// 적이 여러 마리여도 BGM은 하나만 난다. 플레이어가 잡혀서 추격이 끝난 경우에는 따돌림 소리를 내지 않는다.
/// 씬의 MonsterChase를 자동으로 찾는다. 나중에 생기는 적(적 2 등)은 Register로 추가하거나 rescanInterval로 다시 찾는다.
/// </summary>
public class ChaseMusicController : MonoBehaviour
{
    public static ChaseMusicController Instance { get; private set; }

    public SoundCue chaseBgmCue;
    public SoundCue escapedCue;

    [Tooltip("추격 시작 시 BGM이 커지는 시간(초)")]
    public float fadeInTime = 0.6f;
    [Tooltip("추격 해제 시 BGM이 작아지는 시간(초)")]
    public float fadeOutTime = 2.5f;
    [Tooltip("추격이 끝나고 이 시간(초) 안에 다시 쫓기면 BGM을 끊지 않고 이어간다")]
    public float endGraceTime = 1.0f;
    [Tooltip("새로 생긴 적을 다시 찾는 간격(초). 0이면 시작할 때 한 번만")]
    public float rescanInterval = 2f;

    readonly List<MonsterChase> chasers = new List<MonsterChase>();
    LoopSoundEmitter bgm;
    bool chasing;
    float noChaserSince = -1f;
    float nextScan;

    public bool IsChaseMusicOn => chasing;
    public int ChasingCount { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        bgm = gameObject.AddComponent<LoopSoundEmitter>();
        bgm.cue = chaseBgmCue;
        bgm.randomStartTime = false;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Start() => Rescan();

    public void Register(MonsterChase m)
    {
        if (m != null && !chasers.Contains(m)) chasers.Add(m);
    }

    void Rescan()
    {
        foreach (var m in FindObjectsByType<MonsterChase>(FindObjectsSortMode.None)) Register(m);
    }

    void Update()
    {
        if (rescanInterval > 0f && Time.time >= nextScan) { nextScan = Time.time + rescanInterval; Rescan(); }

        int count = 0;
        for (int i = chasers.Count - 1; i >= 0; i--)
        {
            var m = chasers[i];
            if (m == null) { chasers.RemoveAt(i); continue; }
            if (m.isActiveAndEnabled && m.IsChasing) count++;
        }
        ChasingCount = count;

        bool caught = PlayerCaughtSequence.IsRunning;

        if (count > 0 && !caught)
        {
            noChaserSince = -1f;
            if (!chasing) StartChase();
            return;
        }

        if (!chasing) return;

        if (caught) { StopChase(escaped: false); return; } // 잡힘: 따돌림 없이 바로 끔
        if (noChaserSince < 0f) noChaserSince = Time.time;
        if (Time.time - noChaserSince >= endGraceTime) StopChase(escaped: true);
    }

    void StartChase()
    {
        chasing = true;
        bgm.cue = chaseBgmCue;
        bgm.fadeInTime = fadeInTime;
        bgm.SetPlaying(true);
    }

    void StopChase(bool escaped)
    {
        chasing = false;
        noChaserSince = -1f;
        bgm.fadeOutTime = escaped ? fadeOutTime : 0.15f;
        bgm.SetPlaying(false);
        if (escaped) SoundEmitter.Play2D(escapedCue);
    }
}
