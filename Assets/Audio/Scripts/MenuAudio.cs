using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴 사운드 연결 컴포넌트. AudioSystem 프리팹에 하나.
///   메뉴 음악 : 타이틀 화면(GameMenuManager.InTitle)에서만 재생, 게임 시작 시 페이드 아웃
///   버튼 소리 : GameMenuManager가 런타임에 만드는 버튼에 UISoundTrigger를 자동으로 붙인다
///   일시정지  : ESC로 메뉴가 열릴 때 클릭 소리
/// 메뉴 중에는 AudioListener.pause와 timeScale 0이므로, 쓰는 cue는 ignoreListenerPause가 켜져 있어야 한다.
/// </summary>
public class MenuAudio : MonoBehaviour
{
    public SoundCue hoverCue;
    public SoundCue clickCue;
    public SoundCue menuMusicCue;
    [Tooltip("ESC로 일시정지 메뉴가 열릴 때 소리 (비우면 없음)")]
    public SoundCue pauseOpenCue;

    LoopSoundEmitter music;
    SoundEmitter emitter;
    bool wasMenuOpen;
    bool first = true;
    float nextScan;

    void Awake()
    {
        emitter = GetComponent<SoundEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<SoundEmitter>();
        music = gameObject.AddComponent<LoopSoundEmitter>();
        music.cue = menuMusicCue;
        music.useUnscaledTime = true;
        music.randomStartTime = false;
        music.fadeInTime = 1.5f;
        music.fadeOutTime = 1.2f;
    }

    void Update()
    {
        var menu = GameMenuManager.Instance;

        bool title = GameMenuManager.InTitle;
        if (music.IsPlaying != title) music.SetPlaying(title);

        bool open = GameMenuManager.MenuOpen;
        if (!first && open && !wasMenuOpen && !title) emitter.Play(pauseOpenCue);
        wasMenuOpen = open;
        first = false;

        // 버튼은 GameMenuManager가 런타임에 만든다 → 주기적으로 찾아서 소리 연결
        if (menu != null && Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            foreach (var b in menu.GetComponentsInChildren<Button>(true))
            {
                if (b.GetComponent<UISoundTrigger>() != null) continue;
                var t = b.gameObject.AddComponent<UISoundTrigger>();
                t.hoverCue = hoverCue;
                t.clickCue = clickCue;
            }
        }
    }
}
