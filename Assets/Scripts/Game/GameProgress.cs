using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    void RefreshHud()
    {
        if (hudText == null) return;
        bool open = EscapeUnlocked;
        hudText.text = open
            ? "ALL BEACONS ONLINE  -  FIND THE EXIT"
            : "BEACONS  " + activeBeacons + " / " + RequiredBeacons;
        hudText.color = open ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.85f, 0.4f);
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

        hudText = MakeText(canvasGo.transform, "BeaconHud", 34, TextAlignmentOptions.Center);
        var rt = hudText.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -24f);
        rt.sizeDelta = new Vector2(900f, 60f);

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
