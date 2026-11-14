using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Title Screen / Controls / Pause Menu.
/// - 기존 게임플레이 스크립트는 건드리지 않고 공개 스위치(FirstPersonController.SetControlsEnabled,
///   PlayerInteraction.SetInteractionEnabled)와 Time.timeScale만 사용한다.
/// - Title 상태: timeScale 0 + 조작/상호작용 잠금 + 커서 해제 → 몬스터 AI/플레이어 정지.
/// - Pause 상태: 같은 방식. 재개 시 '일시정지 직전에 켜져 있던 것만' 복구(숨기/QTE 등 다른 시스템 잠금 유지).
/// - QTE 진행 중 / 잡힘 연출 / GAME OVER·ESCAPED 중에는 ESC를 무시한다.
/// - Quit to Title: 씬을 다시 로드(항상 Title부터 시작).
/// </summary>
[DefaultExecutionOrder(1000)]
public class GameMenuManager : MonoBehaviour
{
    enum State { Title, TitleControls, Playing, Paused, PauseControls }

    public static GameMenuManager Instance { get; private set; }
    /// <summary>Title/Pause 메뉴가 열려 있는 동안 true.</summary>
    public static bool MenuOpen => Instance != null && Instance.state != State.Playing;

    static readonly Color Ink = new Color(0.02f, 0.02f, 0.02f, 1f);
    static readonly Color Bone = new Color(0.86f, 0.84f, 0.78f, 1f);
    static readonly Color Dim = new Color(0.55f, 0.53f, 0.49f, 1f);
    static readonly Color Blood = new Color(0.55f, 0.08f, 0.06f, 1f);

    State state = State.Title;
    FirstPersonController controller;
    PlayerInteraction interaction;
    bool prevControls, prevInteraction;
    float prevTimeScale = 1f;

    GameObject root, titlePanel, pausePanel, controlsPanel;
    TMP_Text titleText;
    bool controlsFromPause;
    float glitchTimer;
    CanvasScaler menuScaler;

    static readonly string[,] ControlRows =
    {
        { "WASD",      "Move" },
        { "MOUSE",     "Look" },
        { "L-SHIFT",   "Sprint" },
        { "E",         "Interact" },
        { "R-MOUSE",   "Flashlight" },
        { "1 / 2 / 3", "Inventory Slot" },
        { "F",         "Drop Item" },
        { "SPACE",     "QTE / Skill Check" },
        { "ESC",       "Pause / Controls" },
    };

    void Awake()
    {
        Instance = this;
        Time.timeScale = 0f;      // 다른 스크립트의 Start 전에 정지
        AudioListener.pause = true;
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Start()
    {
        controller = FindFirstObjectByType<FirstPersonController>();
        interaction = FindFirstObjectByType<PlayerInteraction>();
        EnsureUiInputModule();

        // FirstPersonController.Start 가 커서를 잠갔으므로 (실행 순서 1000 = 마지막) 여기서 되돌린다.
        if (controller != null) controller.SetControlsEnabled(false);
        if (interaction != null) interaction.SetInteractionEnabled(false);
        ShowOnly(titlePanel);
        SetCursor(true);
    }

    void Update()
    {
        // 창 크기/화면비가 바뀌어도 메뉴가 잘리지 않게: 16:9 보다 넓으면 높이 기준, 좁으면 너비 기준으로 스케일.
        if (menuScaler != null && Screen.height > 0)
            menuScaler.matchWidthOrHeight = Screen.width / (float)Screen.height >= 16f / 9f ? 1f : 0f;

        if (state == State.Title)
        {
            AnimateTitle();
            return;
        }
        if (state == State.TitleControls)
        {
            if (EscPressed()) CloseControls();
            return;
        }

        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

        switch (state)
        {
            case State.Playing:
                if (CanPause()) Pause();
                break;
            case State.Paused:
                Resume();
                break;
            case State.PauseControls:
                CloseControls();
                break;
        }
    }

    static bool EscPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    static bool CanPause()
    {
        if (SkillCheckManager.Instance != null && SkillCheckManager.Instance.IsActive) return false;
        if (PlayerCaughtSequence.IsRunning) return false;
        if (ExitCutsceneController.IsPlaying) return false; // 탈출구 컷씬 중 ESC 일시정지 금지
        if (GameProgress.IsEnded) return false;
        return true;
    }

    // ───────────────────────── 상태 전환 ─────────────────────────

    void StartGame()
    {
        state = State.Playing;
        HideAll();
        // 최초 START: 플레이어 조작 활성화. (Title 중에는 아무도 이 값을 바꾸지 않았다.)
        if (controller != null) controller.SetControlsEnabled(true);
        if (interaction != null) interaction.SetInteractionEnabled(true);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SetCursor(false);
    }

    void Pause()
    {
        prevControls = controller != null && controller.ControlsEnabled;
        prevInteraction = interaction != null && interaction.InteractionEnabled;
        prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;

        if (controller != null) controller.SetControlsEnabled(false);
        if (interaction != null) interaction.SetInteractionEnabled(false);
        Time.timeScale = 0f;
        AudioListener.pause = true;

        state = State.Paused;
        ShowOnly(pausePanel);
        SetCursor(true);
    }

    void Resume()
    {
        state = State.Playing;
        HideAll();
        if (controller != null && prevControls) controller.SetControlsEnabled(true);
        if (interaction != null && prevInteraction) interaction.SetInteractionEnabled(true);
        Time.timeScale = prevTimeScale;
        AudioListener.pause = false;
        SetCursor(false);
    }

    void OpenControls()
    {
        controlsFromPause = state == State.Paused;
        state = controlsFromPause ? State.PauseControls : State.TitleControls;
        ShowOnly(controlsPanel);
    }

    void CloseControls()
    {
        if (controlsFromPause)
        {
            state = State.Paused;
            ShowOnly(pausePanel);
        }
        else
        {
            state = State.Title;
            ShowOnly(titlePanel);
        }
    }

    void QuitToTitle()
    {
        // 씬 재로드 → 새 GameMenuManager.Awake 가 다시 Title 상태로 시작한다.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        int idx = SceneManager.GetActiveScene().buildIndex;
        if (idx >= 0) SceneManager.LoadScene(idx);
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play 모드만 종료
#else
        Application.Quit();
#endif
    }

    static void SetCursor(bool menu)
    {
        Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = menu;
    }

    void HideAll()
    {
        titlePanel.SetActive(false);
        pausePanel.SetActive(false);
        controlsPanel.SetActive(false);
        root.SetActive(false);
    }

    void ShowOnly(GameObject panel)
    {
        root.SetActive(true);
        titlePanel.SetActive(panel == titlePanel);
        pausePanel.SetActive(panel == pausePanel);
        controlsPanel.SetActive(panel == controlsPanel);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // 씬의 EventSystem 이 구 Input Module 이면 클릭이 안 되므로 교체.
    static void EnsureUiInputModule()
    {
        var es = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        }
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Destroy(old);
            es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    // ───────────────────────── 타이틀 연출 (불안정한 깜빡임) ─────────────────────────

    void AnimateTitle()
    {
        if (titleText == null) return;
        float t = Time.unscaledTime;
        float a = 0.9f + 0.1f * Mathf.Sin(t * 2.3f);
        glitchTimer -= Time.unscaledDeltaTime;
        if (glitchTimer <= 0f)
        {
            glitchTimer = Random.Range(0.6f, 2.6f);
            if (Random.value < 0.5f) a = Random.Range(0.05f, 0.35f); // 순간 깜빡
        }
        var c = Bone; c.a = Mathf.Clamp01(a);
        titleText.color = c;
        float jitter = Mathf.PerlinNoise(t * 9f, 0.3f) > 0.9f ? Random.Range(-4f, 4f) : 0f;
        titleText.rectTransform.anchoredPosition = new Vector2(jitter, 190f);
    }

    // ───────────────────────── UI 생성 ─────────────────────────

    void BuildUI()
    {
        var canvasGo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        menuScaler = scaler;
        root = canvasGo;

        // ── Title ──
        titlePanel = MakePanel("TitlePanel", canvasGo.transform, Ink);
        titleText = MakeText(titlePanel.transform, "Title", "DEAD SIGNAL", 150, Bone, new Vector2(0f, 190f), new Vector2(1500f, 220f));
        titleText.fontStyle = FontStyles.Bold;
        titleText.characterSpacing = 18f;
        MakeButton(titlePanel.transform, "START", new Vector2(0f, -20f), StartGame);
        MakeButton(titlePanel.transform, "CONTROLS", new Vector2(0f, -120f), OpenControls);
        MakeButton(titlePanel.transform, "QUIT", new Vector2(0f, -220f), QuitApp);

        // ── Pause ──
        pausePanel = MakePanel("PausePanel", canvasGo.transform, new Color(0f, 0f, 0f, 0.92f));
        MakeText(pausePanel.transform, "Paused", "PAUSED", 100, Bone, new Vector2(0f, 190f), new Vector2(1200f, 150f)).characterSpacing = 14f;
        MakeButton(pausePanel.transform, "RESUME", new Vector2(0f, 20f), Resume);
        MakeButton(pausePanel.transform, "CONTROLS", new Vector2(0f, -80f), OpenControls);
        MakeButton(pausePanel.transform, "QUIT TO TITLE", new Vector2(0f, -180f), QuitToTitle);

        // ── Controls (Title / Pause 공용) ──
        controlsPanel = MakePanel("ControlsPanel", canvasGo.transform, Ink);
        MakeText(controlsPanel.transform, "Heading", "CONTROLS", 80, Bone, new Vector2(0f, 400f), new Vector2(1200f, 110f)).characterSpacing = 14f;
        BuildControlRows(controlsPanel.transform);
        MakeButton(controlsPanel.transform, "BACK", new Vector2(0f, -440f), CloseControls);

        root.SetActive(false);
    }

    void BuildControlRows(Transform parent)
    {
        const float rowH = 66f, startY = 290f;
        for (int i = 0; i < ControlRows.GetLength(0); i++)
        {
            float y = startY - i * rowH;

            // 키캡
            var cap = new GameObject("Key_" + ControlRows[i, 0], typeof(RectTransform), typeof(Image));
            cap.transform.SetParent(parent, false);
            var crt = (RectTransform)cap.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(-250f, y);
            crt.sizeDelta = new Vector2(320f, 52f);
            var img = cap.GetComponent<Image>();
            img.color = new Color(0.16f, 0.15f, 0.14f, 1f);
            img.raycastTarget = false;
            var outline = cap.AddComponent<Outline>();
            outline.effectColor = new Color(0.6f, 0.57f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var k = MakeText(cap.transform, "Label", ControlRows[i, 0], 30, Bone, Vector2.zero, new Vector2(320f, 52f));
            k.fontStyle = FontStyles.Bold;

            // 기능
            var f = MakeText(parent, "Action_" + i, ControlRows[i, 1], 34, Dim, new Vector2(250f, y), new Vector2(560f, 52f));
            f.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    static GameObject MakePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true; // 뒤쪽 HUD 클릭 차단
        return go;
    }

    static TMP_Text MakeText(Transform parent, string name, string text, float size, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return t;
    }

    static Button MakeButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(520f, 80f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // 투명 히트 영역
        img.raycastTarget = true;

        var text = MakeText(go.transform, "Label", label, 52, Color.white, Vector2.zero, new Vector2(520f, 80f));
        text.characterSpacing = 8f;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = text;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = Dim;
        cb.highlightedColor = Color.white;
        cb.selectedColor = Bone;
        cb.pressedColor = Blood;
        cb.fadeDuration = 0.05f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        // 마우스 올리면 [ ] 괄호 표시
        var hover = go.AddComponent<MenuButtonHover>();
        hover.label = text;
        hover.baseText = label;
        text.text = label;
        return btn;
    }
}

/// <summary>버튼에 마우스를 올리면 "[ START ]" 처럼 표시.</summary>
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text label;
    public string baseText;

    public void OnPointerEnter(PointerEventData e) { if (label != null) label.text = "[ " + baseText + " ]"; }
    public void OnPointerExit(PointerEventData e) { if (label != null) label.text = baseText; }
    void OnDisable() { if (label != null) label.text = baseText; }
}
