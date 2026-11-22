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
/// - Pause / Settings / Controls 는 제목을 제외한 항목이 왼쪽에서 스와이프되며, 마우스를 올리면 밝아지고 화살표가 붙는다.
/// </summary>
[DefaultExecutionOrder(1000)]
public class GameMenuManager : MonoBehaviour
{
    enum State { Title, TitleControls, Playing, Paused, PauseControls, PauseSettings, PauseLog }

    public static GameMenuManager Instance { get; private set; }
    /// <summary>Title/Pause 메뉴가 열려 있는 동안 true.</summary>
    public static bool MenuOpen => Instance != null && Instance.state != State.Playing;
    /// <summary>타이틀 화면(조작법 포함)인지. 메뉴 음악용.</summary>
    public static bool InTitle => Instance != null && (Instance.state == State.Title || Instance.state == State.TitleControls);

    static readonly Color Ink = new Color(0.02f, 0.02f, 0.02f, 1f);
    static readonly Color Bone = new Color(0.86f, 0.84f, 0.78f, 1f);
    static readonly Color Dim = new Color(0.55f, 0.53f, 0.49f, 1f);
    static readonly Color Blood = new Color(0.55f, 0.08f, 0.06f, 1f);

    State state = State.Title;
    FirstPersonController controller;
    PlayerInteraction interaction;
    bool prevControls, prevInteraction;
    float prevTimeScale = 1f;

    GameObject root, titlePanel, pausePanel, controlsPanel, settingsPanel;
    RectTransform pauseItems;
    RectTransform logRoot;
    CanvasGroup logGroup;
    float logSlide;
    float logTarget;
    TMP_Text titleText;
    bool controlsFromPause;
    float glitchTimer;
    CanvasScaler menuScaler;

    static readonly string[,] ControlRows =
    {
        { "WASD",      "Move" },
        { "MOUSE",     "Look" },
        { "L-SHIFT",   "Sprint" },
        { "E",         "Interact / Use" },
        { "F",         "Pick Up" },
        { "R-MOUSE",   "Flashlight" },
        { "R",         "Reload" },
        { "1 / 2 / 3", "Inventory Slot" },
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

        TickLogSlide();

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
            case State.PauseSettings:
                CloseSettings();
                break;
            case State.PauseLog:
                if (!RecoveredItemLog.ClosePopup())
                    CloseRecoveredLog();
                break;
        }
    }

    static bool EscPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    static bool CanPause()
    {
        if (SkillCheckManager.Instance != null && SkillCheckManager.Instance.IsActive) return false;
        if (BeaconPipeQte.IsOpen) return false;   // 파이프 퍼즐은 ESC 로 퍼즐만 취소한다 (일시정지 메뉴와 동시에 열지 않음)
        if (Monster2ReleaseSequence.IsPlaying) return false; // Monster 2 등장 컷씬 중 ESC 일시정지 금지
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
        controlsFromPause = state == State.Paused || state == State.PauseLog;
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

    void OpenSettings()
    {
        state = State.PauseSettings;
        ShowOnly(settingsPanel);
    }

    void CloseSettings()
    {
        state = State.Paused;
        ShowOnly(pausePanel);
    }

    void OpenRecoveredLog()
    {
        if (state == State.PauseLog)
        {
            CloseRecoveredLog();
            return;
        }
        state = State.PauseLog;
        logTarget = 1f;
    }

    void CloseRecoveredLog()
    {
        RecoveredItemLog.ClosePopup();
        state = State.Paused;
        logTarget = 0f;
    }

    void TickLogSlide()
    {
        if (pauseItems == null || pausePanel == null || !pausePanel.activeInHierarchy) return;
        float dt = Time.unscaledDeltaTime;
        logSlide = Mathf.Lerp(logSlide, logTarget, 1f - Mathf.Exp(-18f * dt));
        if (Mathf.Abs(logSlide - logTarget) < 0.001f) logSlide = logTarget;
        ApplyLogPose();
    }

    void ApplyLogPose()
    {
        if (pauseItems != null)
            pauseItems.anchoredPosition = new Vector2(500f * logSlide, 0f);
        if (logRoot != null)
            logRoot.anchoredPosition = new Vector2(-900f - (1f - logSlide) * 220f, 10f);
        if (logGroup != null)
        {
            logGroup.alpha = logSlide;
            bool open = logSlide > 0.45f;
            logGroup.blocksRaycasts = open;
            logGroup.interactable = open;
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
        settingsPanel.SetActive(false);
        root.SetActive(false);
    }

    void ShowOnly(GameObject panel)
    {
        root.SetActive(true);
        titlePanel.SetActive(panel == titlePanel);
        pausePanel.SetActive(panel == pausePanel);
        controlsPanel.SetActive(panel == controlsPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        if (panel == pausePanel && state != State.PauseLog)
        {
            logSlide = 0f;
            logTarget = 0f;
            ApplyLogPose();
            RecoveredItemLog.ClosePopup();
        }
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
        pausePanel = MakeVignettePanel("PausePanel", canvasGo.transform);
        MakeHeading(pausePanel.transform, "PAUSED", 230f, 22f);
        var items = new GameObject("PauseItems", typeof(RectTransform));
        items.transform.SetParent(pausePanel.transform, false);
        pauseItems = (RectTransform)items.transform;
        pauseItems.anchorMin = pauseItems.anchorMax = new Vector2(0.5f, 0.5f);
        pauseItems.pivot = new Vector2(0.5f, 0.5f);
        pauseItems.anchoredPosition = Vector2.zero;
        pauseItems.sizeDelta = Vector2.zero;
        MakeMenuRow(pauseItems, "RESUME", new Vector2(-190f, 80f), Resume, null, true, 36f, 0f);
        MakeMenuRow(pauseItems, "SETTINGS", new Vector2(-190f, 4f), OpenSettings, null, true, 36f, 0.05f);
        MakeMenuRow(pauseItems, "CONTROLS", new Vector2(-190f, -72f), OpenControls, null, true, 36f, 0.10f);
        MakeMenuRow(pauseItems, "RECOVERED ITEMS LOG", new Vector2(-190f, -148f), OpenRecoveredLog, null, true, 32f, 0.15f);
        MakeMenuRow(pauseItems, "QUIT TO TITLE", new Vector2(-190f, -224f), QuitToTitle, null, true, 36f, 0.20f);
        pausePanel.AddComponent<MenuColumn>();
        BuildRecoveredLog(pausePanel.transform);

        // ── Settings (Pause 전용) ──
        settingsPanel = MakeVignettePanel("SettingsPanel", canvasGo.transform);
        MakeHeading(settingsPanel.transform, "SETTINGS", 230f, 14f);
        var settings = settingsPanel.AddComponent<SettingsMenu>();
        var sens = MakeMenuRow(settingsPanel.transform, "SENSITIVITY", new Vector2(-190f, 48f), settings.CycleSensitivity, "100%", true, 36f, 0f);
        var vol = MakeMenuRow(settingsPanel.transform, "VOLUME", new Vector2(-190f, -40f), settings.CycleVolume, "100%", true, 36f, 0.05f);
        var window = MakeMenuRow(settingsPanel.transform, "WINDOW", new Vector2(-190f, -128f), settings.ToggleWindow, "WINDOW", true, 36f, 0.10f);
        MakeMenuRow(settingsPanel.transform, "BACK", new Vector2(-190f, -216f), CloseSettings, null, true, 36f, 0.15f);
        settings.Bind(sens.value, vol.value, window.value);
        settingsPanel.AddComponent<MenuColumn>();

        // ── Controls (Title / Pause 공용) ──
        controlsPanel = MakeVignettePanel("ControlsPanel", canvasGo.transform);
        MakeHeading(controlsPanel.transform, "CONTROLS", 430f, 14f);
        BuildControlRows(controlsPanel.transform);
        var controlsColumn = controlsPanel.AddComponent<MenuColumn>();
        controlsColumn.focusFirst = false;

        root.SetActive(false);
    }

    void BuildControlRows(Transform parent)
    {
        const float startY = 292f;
        const float step = 48f;
        int n = ControlRows.GetLength(0);
        for (int i = 0; i < n; i++)
        {
            MakeMenuRow(parent, ControlRows[i, 0], new Vector2(-220f, startY - i * step), null,
                ControlRows[i, 1], false, 28f, i * 0.035f);
        }
        MakeMenuRow(parent, "BACK", new Vector2(-220f, startY - n * step - 28f), CloseControls, null, true, 36f, n * 0.035f);
    }

    void BuildRecoveredLog(Transform parent)
    {
        var go = new GameObject("RecoveredLog", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        logRoot = (RectTransform)go.transform;
        logRoot.anchorMin = logRoot.anchorMax = new Vector2(0.5f, 0.5f);
        logRoot.pivot = new Vector2(0f, 0.5f);
        logRoot.sizeDelta = new Vector2(640f, 560f);
        logRoot.anchoredPosition = new Vector2(-1120f, 10f);
        logGroup = go.GetComponent<CanvasGroup>();
        logGroup.alpha = 0f;
        logGroup.blocksRaycasts = false;
        logGroup.interactable = false;

        var title = MakeText(go.transform, "Title", "RECOVERED ITEMS", 44, Bone, Vector2.zero, new Vector2(600f, 64f));
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.characterSpacing = 6f;
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
        tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(4f, -36f);
        tr.sizeDelta = new Vector2(600f, 64f);

        var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(go.transform, false);
        var rr = (RectTransform)rule.transform;
        rr.anchorMin = rr.anchorMax = new Vector2(0f, 1f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.anchoredPosition = new Vector2(4f, -112f);
        rr.sizeDelta = new Vector2(220f, 2f);
        var img = rule.GetComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = new Color(Bone.r, Bone.g, Bone.b, 0.45f);
        img.raycastTarget = false;

        var entries = new GameObject("Entries", typeof(RectTransform));
        entries.transform.SetParent(go.transform, false);
        var er = (RectTransform)entries.transform;
        er.anchorMin = new Vector2(0f, 0f);
        er.anchorMax = new Vector2(1f, 1f);
        er.offsetMin = new Vector2(4f, 16f);
        er.offsetMax = new Vector2(-12f, -128f);
        RecoveredItemLog.Build(er, parent);
    }

    void MakeHeading(Transform parent, string text, float y, float spacing)
    {
        var t = MakeText(parent, "Heading", text, 76, Bone, new Vector2(0f, y), new Vector2(1100f, 100f));
        t.characterSpacing = spacing;
        MakeRule(parent, y - 72f);
    }

    static void MakeRule(Transform parent, float y)
    {
        var go = new GameObject("Rule", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(240f, 2f);
        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = new Color(Bone.r, Bone.g, Bone.b, 0.45f);
        img.raycastTarget = false;
    }

    MenuRowMotion MakeMenuRow(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, string value, bool selectable, float fontSize, float delay)
    {
        float height = selectable ? 70f : 44f;
        var go = new GameObject("Row_" + label.Replace("/", "-"), typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        go.SetActive(false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(760f, height);
        rt.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = selectable;

        var group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        TMP_Text chevron = null;
        if (selectable)
        {
            chevron = MakeText(go.transform, "Mark", ">", fontSize, Bone, Vector2.zero, new Vector2(36f, height));
            var crt = chevron.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0f, 0.5f);
            crt.anchoredPosition = new Vector2(4f, 0f);
            crt.sizeDelta = new Vector2(36f, height);
            chevron.alignment = TextAlignmentOptions.MidlineLeft;
            chevron.color = new Color(Bone.r, Bone.g, Bone.b, 0f);
        }

        float labelW = value == null ? 700f : selectable ? 440f : 250f;
        float valueW = selectable ? 220f : 470f;
        var labelText = MakeText(go.transform, "Label", label, fontSize, selectable ? Dim : Bone, Vector2.zero, new Vector2(labelW, height));
        labelText.characterSpacing = selectable ? 4f : 1.5f;
        var lrt = labelText.rectTransform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = new Vector2(selectable ? 36f : 8f, 0f);
        lrt.sizeDelta = new Vector2(labelW, height);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text valueText = null;
        if (!string.IsNullOrEmpty(value))
        {
            valueText = MakeText(go.transform, "Value", value, fontSize, selectable ? Dim : new Color(0.62f, 0.60f, 0.55f, 1f), Vector2.zero, new Vector2(valueW, height));
            valueText.characterSpacing = 1.5f;
            var vrt = valueText.rectTransform;
            vrt.anchorMin = vrt.anchorMax = new Vector2(1f, 0.5f);
            vrt.pivot = new Vector2(1f, 0.5f);
            vrt.anchoredPosition = new Vector2(-8f, 0f);
            vrt.sizeDelta = new Vector2(valueW, height);
            valueText.alignment = TextAlignmentOptions.MidlineRight;
        }

        if (selectable)
        {
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onClick != null) btn.onClick.AddListener(onClick);
        }

        var motion = go.AddComponent<MenuRowMotion>();
        motion.rect = rt;
        motion.group = group;
        motion.label = labelText;
        motion.chevron = chevron;
        motion.value = valueText;
        motion.restX = pos.x;
        motion.restY = pos.y;
        motion.delay = delay;
        motion.selectable = selectable;
        go.SetActive(true);
        return motion;
    }

    GameObject MakeVignettePanel(string name, Transform parent)
    {
        var go = MakePanel(name, parent, Color.white);
        var img = go.GetComponent<Image>();
        img.sprite = VignetteSprite();
        img.type = Image.Type.Simple;
        img.color = Color.white;
        return go;
    }

    static Sprite whiteSprite;
    static Sprite vignetteSprite;

    static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply(false);
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }

    static Sprite VignetteSprite()
    {
        if (vignetteSprite != null) return vignetteSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f;
                float dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx * 0.82f + dy * dy);
                float a = Mathf.Lerp(0.82f, 0.96f, Mathf.SmoothStep(0.08f, 1.05f, r));
                px[y * n + x] = new Color(0f, 0f, 0f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false);
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        vignetteSprite.hideFlags = HideFlags.HideAndDontSave;
        return vignetteSprite;
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

/// <summary>메뉴가 켜질 때 첫 선택 항목에 화살표를 둔다.</summary>
public class MenuColumn : MonoBehaviour
{
    public bool focusFirst = true;

    void OnEnable()
    {
        if (!focusFirst)
        {
            MenuRowMotion.Focus(null);
            return;
        }

        var rows = GetComponentsInChildren<MenuRowMotion>(true);
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null || !rows[i].selectable) continue;
            MenuRowMotion.Focus(rows[i]);
            return;
        }
    }
}

/// <summary>왼쪽에서 스와이프되고, 마우스를 올리면 밝아지며 화살표가 붙는다.</summary>
public class MenuRowMotion : MonoBehaviour, IPointerEnterHandler
{
    public RectTransform rect;
    public CanvasGroup group;
    public TMP_Text label;
    public TMP_Text chevron;
    public TMP_Text value;
    public float restX;
    public float restY;
    public float delay;
    public bool selectable = true;

    static readonly Color Hot = new Color(0.94f, 0.91f, 0.84f, 1f);
    static readonly Color Cold = new Color(0.42f, 0.40f, 0.37f, 1f);

    static MenuRowMotion focused;
    float elapsed;
    float hot;
    float nudge;

    public static void Focus(MenuRowMotion row) => focused = row;

    public void OnPointerEnter(PointerEventData e)
    {
        if (selectable) Focus(this);
    }

    void OnEnable()
    {
        elapsed = 0f;
        hot = 0f;
        nudge = 0f;
        Apply(0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        elapsed += dt;
        if (selectable)
        {
            float k = 1f - Mathf.Exp(-14f * dt);
            bool on = focused == this;
            hot = Mathf.Lerp(hot, on ? 1f : 0f, k);
            nudge = Mathf.Lerp(nudge, on ? 18f : 0f, k);
        }
        Apply((elapsed - delay) / 0.36f);
    }

    void Apply(float u)
    {
        float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(u), 3f);
        if (group != null)
        {
            group.alpha = e;
            bool ready = selectable && e > 0.45f;
            group.blocksRaycasts = ready;
            group.interactable = ready;
        }
        if (rect != null)
            rect.anchoredPosition = new Vector2(restX + Mathf.Lerp(-380f, nudge, e), restY);
        if (!selectable || label == null) return;

        label.color = Color.Lerp(Cold, Hot, hot);
        if (value != null) value.color = Color.Lerp(Cold, Hot, hot);
        if (chevron != null)
        {
            var c = Hot;
            c.a = hot;
            chevron.color = c;
        }
    }
}

/// <summary>일시정지 설정. 항목을 누르면 다음 값으로 넘어간다.</summary>
public class SettingsMenu : MonoBehaviour
{
    TMP_Text sensText;
    TMP_Text volText;
    TMP_Text windowText;
    FirstPersonController fps;

    static readonly float[] Sens = { 0.03f, 0.05f, 0.08f, 0.12f, 0.18f };
    static readonly float[] Vol = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f };

    public void Bind(TMP_Text sens, TMP_Text vol, TMP_Text window)
    {
        sensText = sens;
        volText = vol;
        windowText = window;
        Refresh();
    }

    void OnEnable() => Refresh();

    public void CycleSensitivity()
    {
        if (fps == null) fps = FindFirstObjectByType<FirstPersonController>();
        if (fps == null) return;
        fps.mouseSensitivity = Next(fps.mouseSensitivity, Sens);
        Refresh();
    }

    public void CycleVolume()
    {
        AudioListener.volume = Prev(AudioListener.volume, Vol);
        Refresh();
    }

    public void ToggleWindow()
    {
        Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
        Refresh();
    }

    void Refresh()
    {
        if (sensText == null) return;
        if (fps == null) fps = FindFirstObjectByType<FirstPersonController>();
        float sens = fps != null ? fps.mouseSensitivity : 0.05f;
        sensText.text = Mathf.RoundToInt(sens / 0.05f * 100f) + "%";
        if (volText != null) volText.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
        if (windowText != null) windowText.text = Screen.fullScreen ? "FULLSCREEN" : "WINDOW";
    }

    static float Next(float current, float[] steps) => steps[Step(current, steps, 1)];
    static float Prev(float current, float[] steps) => steps[Step(current, steps, -1)];

    static int Step(float current, float[] steps, int dir)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < steps.Length; i++)
        {
            float d = Mathf.Abs(steps[i] - current);
            if (d < bestD) { bestD = d; best = i; }
        }
        return (best + dir + steps.Length) % steps.Length;
    }
}
