using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 서류 Text 모드용 하단 자막. 게임을 멈추지 않고(조작·화면 그대로) 글을 잠깐 띄웠다가 저절로 사라진다.
///  - 처음 Show 할 때 Canvas 를 자동 생성한다(씬에 둘 필요 없음).
///  - 새 글을 띄우면 이전 글을 바로 교체한다.
///  - 표시 시간은 Time.deltaTime 기준이라 일시정지(timeScale 0) 중에는 멈춘다. 메뉴가 열려 있으면 숨긴다.
///  - 정렬 순서 125: 숨기 화면(120) 위, 포스터/서류 오버레이(130) 아래.
/// </summary>
public class DocumentSubtitleUI : MonoBehaviour
{
    const int SortingOrder = 125;
    const float FadeIn = 0.25f;
    const float FadeOut = 0.6f;
    const float PanelAlpha = 0.75f;     // 검은 배경 진하기
    const float BottomAnchor = 0.13f;   // 화면 아래에서 13% 위 (상호작용 안내 문구 위)
    const float MaxTextWidth = 1300f;   // 기준 해상도(1920) 에서 글 한 줄 최대 폭
    const float PadX = 36f, PadY = 20f; // 글과 배경 가장자리 사이 여백

    static DocumentSubtitleUI instance;

    CanvasGroup group;
    Text label;
    RectTransform panel;
    float hold;      // 완전히 보이는 시간
    float elapsed;
    bool showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    /// <summary>글을 하단에 띄운다. seconds 는 완전히 보이는 시간(페이드 제외).</summary>
    public static void Show(string text, float seconds)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Ensure();
        instance.label.text = text;
        instance.hold = Mathf.Max(0.5f, seconds);
        instance.elapsed = 0f;
        instance.showing = true;
        instance.gameObject.SetActive(true);
        instance.group.alpha = 0f;
        instance.FitPanel();
    }

    /// <summary>글 크기에 맞춰 배경 상자 크기를 잡는다. 짧은 글은 좁게, 긴 글은 MaxTextWidth 에서 줄바꿈.</summary>
    void FitPanel()
    {
        var gen = label.cachedTextGeneratorForLayout;
        float ppu = label.pixelsPerUnit;
        float prefW = gen.GetPreferredWidth(label.text, label.GetGenerationSettings(new Vector2(MaxTextWidth, 0f))) / ppu;
        float w = Mathf.Min(Mathf.Ceil(prefW) + 2f, MaxTextWidth);
        float h = gen.GetPreferredHeight(label.text, label.GetGenerationSettings(new Vector2(w, 0f))) / ppu;
        panel.sizeDelta = new Vector2(w + PadX * 2f, Mathf.Ceil(h) + PadY * 2f);
    }

    /// <summary>즉시 숨긴다.</summary>
    public static void Hide()
    {
        if (instance == null) return;
        instance.showing = false;
        instance.group.alpha = 0f;
        instance.gameObject.SetActive(false);
    }

    public static bool IsShowing => instance != null && instance.showing;

    /// <summary>글 길이로 읽을 시간을 추정 (기본 2.5초 + 글자당 0.07초, 3~14초).</summary>
    public static float AutoSeconds(string text)
    {
        int n = string.IsNullOrEmpty(text) ? 0 : text.Replace("\n", "").Replace(" ", "").Length;
        return Mathf.Clamp(2.5f + n * 0.07f, 3f, 14f);
    }

    void Update()
    {
        if (!showing) return;

        // 잡힘 연출이나 포스터/서류 오버레이가 뜨면 자막은 바로 치운다
        if (PlayerCaughtSequence.IsRunning || PosterInspect.IsInspecting || DocumentInspect.IsInspecting) { Hide(); return; }

        elapsed += Time.deltaTime;
        float a;
        if (elapsed < FadeIn) a = elapsed / FadeIn;
        else if (elapsed < FadeIn + hold) a = 1f;
        else a = 1f - (elapsed - FadeIn - hold) / FadeOut;

        if (a <= 0f && elapsed > FadeIn) { Hide(); return; }
        group.alpha = GameMenuManager.MenuOpen ? 0f : Mathf.Clamp01(a);
    }

    static void Ensure()
    {
        if (instance != null) return;

        var root = new GameObject("DocumentSubtitleUI");
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // 검은 배경 상자: 화면 하단 가운데 기준(아래쪽 고정), 글 크기에 맞춰 Show 때마다 크기를 다시 잡는다 → 줄이 늘면 위로 자란다
        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(root.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, BottomAnchor);
        panel.pivot = new Vector2(0.5f, 0f);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, PanelAlpha);
        bg.raycastTarget = false;

        var rt = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(panel, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(PadX, PadY);
        rt.offsetMax = new Vector2(-PadX, -PadY);

        var label = rt.gameObject.AddComponent<Text>();
        label.font = PosterOverlayUI.LoadFont();
        label.fontSize = 30;
        label.lineSpacing = 1.2f;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = new Color(0.92f, 0.90f, 0.85f);
        label.raycastTarget = false;
        rt.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

        instance = root.AddComponent<DocumentSubtitleUI>();
        instance.group = group;
        instance.label = label;
        instance.panel = panel;
        root.SetActive(false);
    }
}
