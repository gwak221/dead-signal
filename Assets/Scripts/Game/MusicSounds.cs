using System.Collections.Generic;
using DeadSignal.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 음악 · 진행 소리 호출 담당 (게임 코드). 게임 시작 때 스스로 생기므로 씬에 배치할 필요가 없다.
///
///   메뉴 음악 : 타이틀 화면(GameMenuManager.InTitle)에 있는 동안 bgm_menu 루프. 게임 시작 시 페이드 아웃.
///               타이틀은 일시정지(AudioListener.pause, timeScale 0) 상태라 항목은 ignorePause, 페이드는 unscaled 시간.
///   추격 음악 : 쫓는 적(MonsterChase.IsChasing / MonsterHoundAI.IsChasingPlayer) 수가 0 → 1 이상이 되면 bgm_chase 루프.
///               적이 여러 마리여도 하나만. 0이 되고 endGraceTime이 지나면 0.8초 페이드 아웃 + sfx_chase_escaped(따돌림).
///               (기존 2.5초 페이드는 따돌림 소리가 추격 음악에 묻혀서 줄임)
///               잡혀서 끝나면(PlayerCaughtSequence) 따돌림 소리 없이 빠르게 끈다.
///   결말      : GameProgress.IsEnded가 되면 1회 — 탈출이면 bgm_escape, 아니면 bgm_gameover. 추격 음악은 끈다.
/// </summary>
[DisallowMultipleComponent]
public class MusicSounds : MonoBehaviour
{
    [Header("메뉴")]
    public string menuMusic = "bgm_menu";
    public float menuFadeIn = 1.5f;
    public float menuFadeOut = 1.2f;

    [Header("추격")]
    public string chaseMusic = "bgm_chase";
    public string chaseEscapedSound = "sfx_chase_escaped";
    [Tooltip("추격 시작 시 음악이 커지는 시간(초)")] public float chaseFadeIn = 0.6f;
    [Tooltip("따돌렸을 때 음악이 작아지는 시간(초). 길면 따돌림 소리가 추격 음악에 묻힌다")] public float chaseFadeOut = 0.8f;
    [Tooltip("잡히거나 게임이 끝났을 때 음악이 작아지는 시간(초)")] public float chaseCutFade = 0.15f;
    [Tooltip("추격이 끝나고 이 시간(초) 안에 다시 쫓기면 음악을 끊지 않고 이어간다")] public float endGraceTime = 1f;
    [Tooltip("새로 생긴 적을 다시 찾는 간격(초)")] public float rescanInterval = 2f;
    [Tooltip("추격 시작/끝을 콘솔에 남긴다 (에디터·개발 빌드만)")] public bool logChase = true;

    [Header("결말")]
    public string gameOverMusic = "bgm_gameover";
    public string escapeMusic = "bgm_escape";

    static MusicSounds instance;

    SoundHandle menu = SoundHandle.None;
    float menuFade;

    SoundHandle chase = SoundHandle.None;
    float chaseFade;
    bool chasing;
    float noChaserSince = -1f;
    float nextScan;
    readonly List<MonsterChase> chasers = new List<MonsterChase>();
    readonly List<MonsterHoundAI> hounds = new List<MonsterHoundAI>();

    bool endedSeen;
    int lastCount;

    /// <summary>추격 음악이 켜져 있는지 (환경음 줄이기 등에 사용).</summary>
    public static bool IsChaseMusicOn => instance != null && instance.chasing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[MusicSounds]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MusicSounds>();
    }

    void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        menu.Stop(); chase.Stop();
        menu = chase = SoundHandle.None;
        chasing = false;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // 타이틀로 나가기 = 씬 재로드. 진행 상태를 처음으로 돌린다.
    void HandleSceneLoaded(Scene s, LoadSceneMode m)
    {
        chase.Stop(chaseCutFade);
        chase = SoundHandle.None;
        chasing = false;
        noChaserSince = -1f;
        chasers.Clear();
        hounds.Clear();
        nextScan = 0f;
        endedSeen = false;
    }

    void Update()
    {
        UpdateMenu();
        UpdateChase();
        UpdateEnding();
    }

    // ───────────── 메뉴 ─────────────

    void UpdateMenu()
    {
        bool title = GameMenuManager.InTitle;
        if (title)
        {
            if (!menu.IsPlaying)
            {
                menu = AudioManager.Play(menuMusic);
                menuFade = menuFadeIn > 0f ? 0f : 1f;
                menu.SetVolume(menuFade);
            }
            if (menuFade < 1f)
            {
                menuFade = Mathf.MoveTowards(menuFade, 1f, Time.unscaledDeltaTime / menuFadeIn);
                menu.SetVolume(menuFade);
            }
        }
        else if (menu.IsPlaying)
        {
            menu.Stop(menuFadeOut);
        }
    }

    // ───────────── 추격 ─────────────

    void Rescan()
    {
        foreach (var c in FindObjectsByType<MonsterChase>(FindObjectsSortMode.None))
            if (!chasers.Contains(c)) chasers.Add(c);
        foreach (var h in FindObjectsByType<MonsterHoundAI>(FindObjectsSortMode.None))
            if (!hounds.Contains(h)) hounds.Add(h);
    }

    int CountChasers()
    {
        int count = 0;
        for (int i = chasers.Count - 1; i >= 0; i--)
        {
            var c = chasers[i];
            if (c == null) { chasers.RemoveAt(i); continue; }
            if (c.isActiveAndEnabled && c.IsChasing) count++;
        }
        for (int i = hounds.Count - 1; i >= 0; i--)
        {
            var h = hounds[i];
            if (h == null) { hounds.RemoveAt(i); continue; }
            if (h.isActiveAndEnabled && h.IsChasingPlayer) count++;
        }
        return count;
    }

    void UpdateChase()
    {
        if (Time.time >= nextScan) { nextScan = Time.time + rescanInterval; Rescan(); }

        int count = CountChasers();
        bool caught = PlayerCaughtSequence.IsRunning;
        bool ended = GameProgress.IsEnded;

        lastCount = count;
        if (count > 0 && !caught && !ended)
        {
            noChaserSince = -1f;
            if (!chasing) StartChase();
        }
        else if (chasing)
        {
            if (caught || ended) StopChase(escaped: false);   // 잡힘 / 결말: 따돌림 없이 바로 끔
            else
            {
                if (noChaserSince < 0f) noChaserSince = Time.time;
                if (Time.time - noChaserSince >= endGraceTime) StopChase(escaped: true);
            }
        }

        if (chasing && chaseFade < 1f)
        {
            chaseFade = Mathf.MoveTowards(chaseFade, 1f, Time.deltaTime / Mathf.Max(0.01f, chaseFadeIn));
            chase.SetVolume(chaseFade);
        }
    }

    void StartChase()
    {
        chasing = true;
        chase.Stop(chaseCutFade);
        chase = AudioManager.Play(chaseMusic);
        Log("추격 시작 — 쫓는 적 " + lastCount + "마리");
        chaseFade = chaseFadeIn > 0f ? 0f : 1f;
        chase.SetVolume(chaseFade);
    }

    void StopChase(bool escaped)
    {
        chasing = false;
        noChaserSince = -1f;
        chase.Stop(escaped ? chaseFadeOut : chaseCutFade);
        chase = SoundHandle.None;
        if (escaped) AudioManager.Play(chaseEscapedSound);
        Log(escaped ? "추격 끝 — 따돌림 (" + chaseEscapedSound + ")" : "추격 끝 — 잡힘/결말 (따돌림 소리 없음)");
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    void Log(string msg)
    {
        if (logChase) Debug.Log("[MusicSounds] " + msg);
    }

    // ───────────── 결말 ─────────────

    void UpdateEnding()
    {
        if (endedSeen || !GameProgress.IsEnded) return;
        endedSeen = true;
        AudioManager.Play(GameProgress.HasEscaped ? escapeMusic : gameOverMusic);
    }
}
