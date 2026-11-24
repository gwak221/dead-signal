using DeadSignal.Audio;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 소리 호출 담당 (게임 코드). 게임 시작 때 스스로 생기므로 씬에 배치할 필요가 없다.
///   버튼     : GameMenuManager가 실행 중에 만드는 버튼을 주기적으로 찾아 UIButtonSound를 붙인다 → 마우스 올림 ui_hover, 클릭 ui_click
///   일시정지 : ESC로 일시정지 메뉴가 열릴 때 ui_click
/// 메뉴 중에는 AudioListener.pause라서 두 항목은 ignorePause가 켜져 있어야 한다.
/// </summary>
[DisallowMultipleComponent]
public class UISounds : MonoBehaviour
{
    public string hoverSound = "ui_hover";
    public string clickSound = "ui_click";
    [Tooltip("ESC로 일시정지 메뉴가 열릴 때 소리 (비우면 없음)")] public string pauseOpenSound = "ui_click";

    static UISounds instance;
    public static UISounds Instance => instance;

    bool wasMenuOpen;
    bool first = true;
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[UISounds]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UISounds>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        var menu = GameMenuManager.Instance;
        if (menu == null) { first = true; return; } // 씬 재로드 중

        bool open = GameMenuManager.MenuOpen;
        if (!first && open && !wasMenuOpen && !GameMenuManager.InTitle && !string.IsNullOrEmpty(pauseOpenSound))
            AudioManager.Play(pauseOpenSound);
        wasMenuOpen = open;
        first = false;

        // 버튼은 GameMenuManager가 실행 중에 만든다 → 주기적으로 찾아서 소리 연결 (메뉴 중엔 timeScale 0이라 unscaled)
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            foreach (var b in menu.GetComponentsInChildren<Button>(true))
                if (b.GetComponent<UIButtonSound>() == null) b.gameObject.AddComponent<UIButtonSound>();
        }
    }
}
