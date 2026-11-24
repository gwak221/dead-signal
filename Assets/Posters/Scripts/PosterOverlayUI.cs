using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PosterInspect 의 화면 UI. 처음 Show 할 때 Canvas 를 자동 생성한다(씬에 둘 필요 없음).
///  - 화면을 어둡게 하고 이미지를 화면 중앙에 원본 비율로 크게 띄운다. 조명과 무관하게 항상 선명하다.
///  - 정렬 순서 130: 숨기 화면(120) 위, 잡힘 연출(150)·Game Over(200)·일시정지 메뉴(1000) 아래.
///  - 한글 설명을 위해 Windows 시스템 폰트(맑은 고딕)를 쓰는 기본 Text 를 사용한다(프로젝트 TMP 폰트에는 한글 없음).
/// </summary>
public static class PosterOverlayUI
{
    const int SortingOrder = 130;
    const float DimAlpha = 0.5f;          // 뒷 배경을 50% 어둡게
    const float MaxWidthRatio = 0.82f;    // 화면 대비 이미지 최대 가로
    const float MaxHeightRatio = 0.78f;   // 화면 대비 이미지 최대 세로 (아래 문구 자리 남김)

    static GameObject root;
    static CanvasGroup group;
    static RawImage image;
    static AspectRatioFitter fitter;
    static Text captionText;
    static Text hintText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        root = null; group = null; image = null; fitter = null; captionText = null; hintText = null;
    }

    public static void Show(Texture tex, string caption)
    {
        Ensure();
        image.texture = tex;
        image.enabled = tex != null;
        if (tex != null && tex.height > 0) fitter.aspectRatio = (float)tex.width / tex.height;

        bool hasCaption = !string.IsNullOrWhiteSpace(caption);
        captionText.text = hasCaption ? caption : "";
        captionText.gameObject.SetActive(hasCaption);

        root.SetActive(true);
    }

    public static void SetAlpha(float a)
    {
        if (group != null) group.alpha = a;
    }

    public static void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    static void Ensure()
    {
        if (root != null) return;

        root = new GameObject("PosterOverlayUI");
        Object.DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;   // 어떤 화면 비율에서도 전체가 보이게
        group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // 어두운 배경
        var dim = NewRect("Dim", root.transform);
        Stretch(dim);
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, DimAlpha);

        // 이미지 영역(화면 비율 박스) → 그 안에서 원본 비율 유지
        var area = NewRect("ImageArea", root.transform);
        area.anchorMin = new Vector2((1f - MaxWidthRatio) * 0.5f, 1f - MaxHeightRatio - 0.05f);
        area.anchorMax = new Vector2(1f - (1f - MaxWidthRatio) * 0.5f, 0.95f);
        area.offsetMin = area.offsetMax = Vector2.zero;

        var imgRt = NewRect("Image", area);
        imgRt.anchorMin = imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        image = imgRt.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        fitter = imgRt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        var font = LoadFont();

        // 설명 문구
        var cap = NewRect("Caption", root.transform);
        cap.anchorMin = new Vector2(0.1f, 0.07f);
        cap.anchorMax = new Vector2(0.9f, 0.15f);
        cap.offsetMin = cap.offsetMax = Vector2.zero;
        captionText = cap.gameObject.AddComponent<Text>();
        captionText.font = font;
        captionText.fontSize = 34;
        captionText.alignment = TextAnchor.MiddleCenter;
        captionText.color = new Color(0.92f, 0.90f, 0.85f);
        captionText.raycastTarget = false;
        cap.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        // 닫기 안내
        var hint = NewRect("Hint", root.transform);
        hint.anchorMin = new Vector2(0.5f, 0f);
        hint.anchorMax = new Vector2(0.5f, 0f);
        hint.pivot = new Vector2(0.5f, 0f);
        hint.anchoredPosition = new Vector2(0f, 28f);
        hint.sizeDelta = new Vector2(800f, 40f);
        hintText = hint.gameObject.AddComponent<Text>();
        hintText.font = font;
        hintText.fontSize = 24;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = new Color(1f, 1f, 1f, 0.55f);
        hintText.text = "[E] / [RMB]  CLOSE";
        hintText.raycastTarget = false;

        root.SetActive(false);
    }

    static Font LoadFont()
    {
        Font f = null;
        try { f = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 34); }
        catch { /* OS 폰트를 못 찾으면 아래 기본 폰트 */ }
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return f;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
