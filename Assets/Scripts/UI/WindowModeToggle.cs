using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Windows 빌드 전용: Alt + Enter 로 Windowed ↔ Fullscreen Window(Borderless) 전환.
/// - 게임 시작/Title/Pause/QTE 어디서든 동작 (Time.timeScale 과 무관, 게임 입력에는 관여하지 않음)
/// - 전체화면에서 다시 누르면 직전 창 크기/모드로 복귀
/// - Unity 기본 Alt+Enter(Allow Fullscreen Switch)는 꺼 두고 이 스크립트만 사용 → 항상 Borderless 로 전환(호환성 우선)
/// 씬에 배치할 필요 없이 자동 생성된다.
/// </summary>
public class WindowModeToggle : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    int savedWidth = 1280, savedHeight = 720;
    FullScreenMode savedMode = FullScreenMode.Windowed;
    bool cursorWasLocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<WindowModeToggle>() != null) return;
        var go = new GameObject("WindowModeToggle");
        DontDestroyOnLoad(go);
        go.AddComponent<WindowModeToggle>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
        if (alt && kb.enterKey.wasPressedThisFrame) Toggle();
    }

    void Toggle()
    {
        cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;

        if (Screen.fullScreenMode == FullScreenMode.Windowed)
        {
            // 창 → Fullscreen Window (Borderless, 바탕화면 해상도)
            savedWidth = Screen.width;
            savedHeight = Screen.height;
            savedMode = FullScreenMode.Windowed;
            var d = Screen.mainWindowDisplayInfo;
            Screen.SetResolution(d.width, d.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            // 전체화면 → 이전 창 모드/크기
            Screen.SetResolution(Mathf.Max(640, savedWidth), Mathf.Max(360, savedHeight), FullScreenMode.Windowed);
        }

        // 모드 전환 후 커서 상태 복원 (게임 중이면 다시 잠금)
        if (cursorWasLocked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
#endif
}
