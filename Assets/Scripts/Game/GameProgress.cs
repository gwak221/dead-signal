using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 알파테스트용 전역 진행 상태: 활성 비콘 수, 탈출구 해금, ESCAPED/CLEAR UI.
/// 씬에 없으면 첫 접근 시 자동 생성된다.
/// </summary>
public class GameProgress : MonoBehaviour
{
    static GameProgress instance;

    public static GameProgress Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameProgress>();
                if (instance == null)
                    instance = new GameObject("GameProgress").AddComponent<GameProgress>();
            }
            return instance;
        }
    }

    public static event Action Changed;

    [SerializeField] int defaultRequiredBeacons = 4;

    int registeredBeacons;
    int activeBeacons;
    bool escaped;
    bool gameOver;
    GameObject gameOverPanel;
    TMP_Text hudText;
    RectTransform beaconRoot;
    GameObject clearPanel;

    public static int ActiveBeacons => Instance.activeBeacons;
    public static int RequiredBeacons => Instance.registeredBeacons > 0 ? Instance.registeredBeacons : Instance.defaultRequiredBeacons;
    public static bool EscapeUnlocked => ActiveBeacons >= RequiredBeacons;
    public static bool HasEscaped => Instance.escaped;
    /// <summary>GAME OVER / ESCAPED 화면 표시 중인지 (메뉴가 ESC를 무시하는 데 사용).</summary>
    public static bool IsEnded => instance != null && (instance.escaped || instance.gameOver);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        Changed = null;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildUI();
    }

    void Start() => RefreshHud();

    public static void RegisterBeacon()
    {
        Instance.registeredBeacons++;
        Instance.RefreshHud();
        Changed?.Invoke();
    }

    public static void ReportBeaconActivated()
    {
        var g = Instance;
        g.activeBeacons++;
        Debug.Log("[GameProgress] Beacons " + g.activeBeacons + "/" + RequiredBeacons + (EscapeUnlocked ? " - ESCAPE UNLOCKED" : ""));
        g.RefreshHud();
        Changed?.Invoke();
    }

    public static void TriggerGameOver()
    {
        var g = Instance;
        if (g.gameOver || g.escaped) return;
        g.gameOver = true;
        Debug.Log("[GameProgress] GAME OVER");

        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) fpc.SetControlsEnabled(false);
        var pi = FindFirstObjectByType<PlayerInteraction>();
        if (pi != null) pi.SetInteractionEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (g.gameOverPanel != null) g.gameOverPanel.SetActive(true);
        Changed?.Invoke();
    }

    public static void TriggerEscape()
    {
        var g = Instance;
        if (g.escaped || g.gameOver || !EscapeUnlocked) return;
        g.escaped = true;
        Debug.Log("[GameProgress] ESCAPED - CLEAR");

        var fpc = FindFirstObjectByType<FirstPersonController>();
        if (fpc != null) fpc.SetControlsEnabled(false);
        var pi = FindFirstObjectByType<PlayerInteraction>();
        if (pi != null) pi.SetInteractionEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (g.clearPanel != null) g.clearPanel.SetActive(true);
        Changed?.Invoke();
    }

    void Update()
    {
        if ((escaped || gameOver) && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex : 0);
    }

    void LateUpdate()
    {
        if (beaconRoot == null)
        {
            var hr = GameObject.Find("HeartRateUI");
            if (hr == null) return;
            BuildBeaconHud(hr.transform);
            RefreshHud();
        }
        PlaceBeaconHud();
    }

    void RefreshHud()
    {
        if (hudText == null) return;
        bool open = EscapeUnlocked;
        hudText.text = "<size=72%>x</size>   " + activeBeacons + "/" + RequiredBeacons;
        hudText.color = open ? new Color(0.45f, 0.95f, 0.55f) : new Color(0.93f, 0.93f, 0.9f);
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("GameProgressCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        clearPanel = new GameObject("ClearPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        clearPanel.transform.SetParent(canvasGo.transform, false);
        var prt = (RectTransform)clearPanel.transform;
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        clearPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.85f);

        var title = MakeText(clearPanel.transform, "Title", 110, TextAlignmentOptions.Center);
        title.text = "ESCAPED";
        title.color = new Color(0.35f, 1f, 0.45f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        title.rectTransform.sizeDelta = new Vector2(1400f, 160f);

        var sub = MakeText(clearPanel.transform, "Sub", 48, TextAlignmentOptions.Center);
        sub.text = "CLEAR\n<size=60%>R : Restart</size>";
        sub.rectTransform.anchoredPosition = new Vector2(0f, -90f);
        sub.rectTransform.sizeDelta = new Vector2(1000f, 200f);
        clearPanel.SetActive(false);

        gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        gameOverPanel.transform.SetParent(canvasGo.transform, false);
        var grt = (RectTransform)gameOverPanel.transform;
        grt.anchorMin = Vector2.zero;
        grt.anchorMax = Vector2.one;
        grt.offsetMin = grt.offsetMax = Vector2.zero;
        gameOverPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.85f);
        var gt = MakeText(gameOverPanel.transform, "Title", 110, TextAlignmentOptions.Center);
        gt.text = "GAME OVER";
        gt.color = new Color(1f, 0.25f, 0.25f);
        gt.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        gt.rectTransform.sizeDelta = new Vector2(1400f, 160f);
        var gs = MakeText(gameOverPanel.transform, "Sub", 48, TextAlignmentOptions.Center);
        gs.text = "<size=60%>R : Restart</size>";
        gs.rectTransform.anchoredPosition = new Vector2(0f, -90f);
        gs.rectTransform.sizeDelta = new Vector2(1000f, 200f);
        gameOverPanel.SetActive(false);
    }

    void PlaceBeaconHud()
    {
        if (beaconRoot == null) return;
        var hrGo = GameObject.Find("HeartRateUI");
        if (hrGo == null) return;
        var hr = (RectTransform)hrGo.transform;
        if (beaconRoot.parent != hr.parent)
            beaconRoot.SetParent(hr.parent, false);

        beaconRoot.anchorMin = beaconRoot.anchorMax = new Vector2(1f, 1f);
        beaconRoot.pivot = new Vector2(1f, 1f);
        float right = hr.anchoredPosition.x + (1f - hr.pivot.x) * hr.sizeDelta.x;
        float ringBottom = hr.anchoredPosition.y - hr.pivot.y * hr.sizeDelta.y;
        beaconRoot.anchoredPosition = new Vector2(right, ringBottom - 8f);
    }

    void BuildBeaconHud(Transform heartRate)
    {
        var root = new GameObject("BeaconHud", typeof(RectTransform));
        root.transform.SetParent(heartRate.parent, false);
        beaconRoot = (RectTransform)root.transform;
        beaconRoot.anchorMin = beaconRoot.anchorMax = new Vector2(1f, 1f);
        beaconRoot.pivot = new Vector2(1f, 1f);
        beaconRoot.sizeDelta = new Vector2(168f, 46f);

        Frame(root.transform, "Outer", Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Color(0.05f, 0.045f, 0.04f, 1f), new Color(0.05f, 0.045f, 0.04f, 0.92f), 4f, 168, 46);

        var icon = new GameObject("IconBox", typeof(RectTransform));
        icon.transform.SetParent(root.transform, false);
        var ir = (RectTransform)icon.transform;
        ir.anchorMin = new Vector2(0f, 0.5f);
        ir.anchorMax = new Vector2(0f, 0.5f);
        ir.pivot = new Vector2(0f, 0.5f);
        ir.anchoredPosition = new Vector2(5f, 0f);
        ir.sizeDelta = new Vector2(34f, 34f);
        Frame(icon.transform, "IconFrame", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.045f, 0.04f, 1f), new Color(0.05f, 0.045f, 0.04f, 0.92f), 2f, 34, 34);

        var glyphGo = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        glyphGo.transform.SetParent(icon.transform, false);
        var gr = (RectTransform)glyphGo.transform;
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = new Vector2(5f, 5f);
        gr.offsetMax = new Vector2(-5f, -5f);
        var glyph = glyphGo.GetComponent<Image>();
        glyph.sprite = BeaconGlyph();
        glyph.color = new Color(0.93f, 0.93f, 0.9f, 1f);
        glyph.raycastTarget = false;
        glyph.preserveAspect = true;

        hudText = MakeText(root.transform, "Count", 20, TextAlignmentOptions.MidlineLeft);
        var tr = hudText.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0f, 0.5f);
        tr.offsetMin = new Vector2(46f, 4f);
        tr.offsetMax = new Vector2(-8f, -4f);
    }

    static void Frame(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color border, Color fill, float thickness, int width, int height)
    {
        var borderGo = new GameObject(name, typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(parent, false);
        var br = (RectTransform)borderGo.transform;
        br.anchorMin = anchorMin;
        br.anchorMax = anchorMax;
        br.offsetMin = offsetMin;
        br.offsetMax = offsetMax;
        var borderImg = borderGo.GetComponent<Image>();
        borderImg.sprite = HudRoughSprite.Get(width * 2, height * 2);
        borderImg.type = Image.Type.Simple;
        borderImg.color = border;
        borderImg.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(borderGo.transform, false);
        var fr = (RectTransform)fillGo.transform;
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(thickness, thickness);
        fr.offsetMax = new Vector2(-thickness, -thickness);
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.sprite = White();
        fillImg.color = fill;
        fillImg.raycastTarget = false;
    }

    static Sprite white;

    static Sprite White()
    {
        if (white != null) return white;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply(false);
        white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        white.hideFlags = HideFlags.HideAndDontSave;
        return white;
    }

    static Sprite BeaconGlyph()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        var px = new Color32[n * n];
        var ink = new Color32(255, 255, 255, 255);

        void Disc(int cx, int cy, int r)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if ((uint)x >= n || (uint)y >= n) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) px[y * n + x] = ink;
            }
        }

        void Box(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if ((uint)x < n && (uint)y < n) px[y * n + x] = ink;
        }

        Box(16, 6, 48, 26);
        Box(22, 12, 42, 20);
        for (int y = 12; y <= 20; y++)
        for (int x = 22; x <= 42; x++)
            px[y * n + x] = new Color32(0, 0, 0, 0);
        Disc(32, 16, 3);
        Box(30, 26, 34, 46);
        Disc(32, 50, 6);
        for (int i = 0; i < 3; i++)
        {
            int y = 46 + i * 5;
            Box(40, y, 44 + i * 4, y + 2);
            Box(20 - i * 4, y, 24, y + 2);
        }

        tex.SetPixels32(px);
        tex.Apply(false);
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    static TMP_Text MakeText(Transform parent, string name, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        t.richText = true;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return t;
    }
}
