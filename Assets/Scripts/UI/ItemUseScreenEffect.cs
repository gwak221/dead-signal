using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 사용 시 화면 가장자리에 은은한 색 비네트를 띄운다.
/// - 씬의 빈 GameObject 에 붙여 두면 그 인스펙터 값을 쓴다. 없으면 처음 호출될 때 기본값으로 스스로 만든다.
/// - Pulse(color): 즉시 효과 (구급상자). 천천히 차올랐다가 천천히 사라진다.
/// - Sustain(color, seconds): 지속 효과 (주사기). 차오른 뒤 옅게 유지되고, 끝날 때 천천히 사라진다.
/// 두 효과는 별도 레이어라 동시에 걸려도 서로 끊지 않는다. Time.time 기준이라 일시정지 중엔 멈춘다.
/// HUD 보다 아래(sortingOrder -100)에 그려 슬롯/텍스트를 흐리지 않는다.
/// 플레이 중 인스펙터 값을 바꾸면 즉시 반영된다. 컴포넌트 ⋮ 메뉴의 Preview Pulse / Preview Sustain 으로 미리 볼 수 있다.
/// </summary>
public class ItemUseScreenEffect : MonoBehaviour
{
    public static readonly Color MedkitColor = new Color(1f, 0.32f, 0.22f, 1f);

    [Header("가장자리 범위 (화면 중심 0 ~ 화면 테두리 1)")]
    [Tooltip("효과가 시작되는 거리. 이보다 안쪽은 투명. 줄이면 안쪽까지 넓게 퍼진다.")]
    [Range(0f, 1f)] public float innerEdge = 0.6f;
    [Tooltip("효과가 가장 진해지는 거리. 1보다 크면 테두리까지 부드럽게, 1 이하면 테두리가 꽉 찬 색.")]
    [Range(0.5f, 1.5f)] public float outerEdge = 1.08f;
    [Tooltip("모서리 모양. 2 = 타원(모서리가 더 진함), 4 = 둥근 사각형, 클수록 화면 사각형에 가까움.")]
    [Range(2f, 12f)] public float cornerShape = 4f;

    [Header("즉시 효과 (구급상자)")]
    [Range(0f, 1f)] public float pulsePeak = 0.55f;
    [Min(0.01f)] public float pulseFadeIn = 0.5f;
    [Min(0.01f)] public float pulseFadeOut = 1.2f;

    [Header("지속 효과 (주사기)")]
    [Range(0f, 1f)] public float sustainPeak = 0.5f;
    [Tooltip("유지 중 진하기")]
    [Range(0f, 1f)] public float sustainLevel = 0.28f;
    [Min(0.01f)] public float sustainFadeIn = 0.5f;
    [Tooltip("최고점에서 유지 진하기로 내려가는 시간")]
    [Min(0.01f)] public float sustainSettle = 1.0f;
    [Min(0.01f)] public float sustainFadeOut = 1.5f;
    [Tooltip("유지 중 숨쉬듯 흔들리는 폭 (0 = 일정)")]
    [Range(0f, 0.5f)] public float sustainBreath = 0.15f;

    [Header("미리보기 (⋮ 메뉴 Preview)")]
    public Color previewColor = new Color(0.25f, 0.95f, 0.85f, 1f);
    [Min(0.1f)] public float previewSeconds = 5f;

    static ItemUseScreenEffect instance;

    Image pulseImage, sustainImage;
    Texture2D tex;
    Sprite sprite;
    bool textureDirty = true;
    float pulseStart = -999f;
    float sustainStart = -999f, sustainEnd = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    static ItemUseScreenEffect Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<ItemUseScreenEffect>();
            if (instance == null) instance = new GameObject("ItemUseScreenEffect").AddComponent<ItemUseScreenEffect>();
            return instance;
        }
    }

    /// <summary>즉시 효과: 은은하게 나타났다가 사라진다.</summary>
    public static void Pulse(Color color)
    {
        var fx = Instance;
        fx.EnsureLayers();
        fx.pulseImage.color = WithAlpha(color, 0f);
        fx.pulseStart = Time.time;
    }

    /// <summary>지속 효과: seconds 동안 가장자리에 옅게 남는다. 다시 호출하면 그 시점부터 갱신.</summary>
    public static void Sustain(Color color, float seconds)
    {
        var fx = Instance;
        fx.EnsureLayers();
        bool running = Time.time < fx.sustainEnd;
        fx.sustainImage.color = WithAlpha(color, fx.sustainImage.color.a);
        if (!running) fx.sustainStart = Time.time;   // 이미 켜져 있으면 다시 차오르지 않고 시간만 연장
        fx.sustainEnd = Time.time + Mathf.Max(fx.sustainFadeOut, seconds);
    }

    [ContextMenu("Preview Pulse")]
    void PreviewPulse() { if (Application.isPlaying) Pulse(previewColor); else Debug.Log("[ItemUseScreenEffect] 플레이 모드에서 실행하세요."); }

    [ContextMenu("Preview Sustain")]
    void PreviewSustain() { if (Application.isPlaying) Sustain(previewColor, previewSeconds); else Debug.Log("[ItemUseScreenEffect] 플레이 모드에서 실행하세요."); }

    void Awake()
    {
        if (instance == null) instance = this;
        EnsureLayers();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (sprite != null) Destroy(sprite);
        if (tex != null) Destroy(tex);
    }

    void OnValidate()
    {
        if (outerEdge < innerEdge + 0.01f) outerEdge = innerEdge + 0.01f;
        textureDirty = true;
    }

    void EnsureLayers()
    {
        if (pulseImage != null) return;
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -100;
        sustainImage = MakeLayer("Sustain");
        pulseImage = MakeLayer("Pulse");
    }

    Image MakeLayer(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        img.enabled = false;
        return img;
    }

    void Update()
    {
        if (pulseImage == null) return;
        if (textureDirty) RebuildTexture();

        float now = Time.time;

        // Pulse: 0 → peak (FadeIn) → 0 (FadeOut)
        float p = now - pulseStart, pa = 0f;
        if (p < pulseFadeIn) pa = pulsePeak * Mathf.SmoothStep(0f, 1f, p / pulseFadeIn);
        else if (p < pulseFadeIn + pulseFadeOut) pa = pulsePeak * (1f - Mathf.SmoothStep(0f, 1f, (p - pulseFadeIn) / pulseFadeOut));
        pulseImage.color = WithAlpha(pulseImage.color, pa);

        // Sustain: 0 → peak (FadeIn) → level (Settle) → 유지(느린 숨쉬기) → 0 (끝나기 전 FadeOut)
        float sa = 0f;
        if (now < sustainEnd)
        {
            float s = now - sustainStart;
            if (s < sustainFadeIn) sa = sustainPeak * Mathf.SmoothStep(0f, 1f, s / sustainFadeIn);
            else if (s < sustainFadeIn + sustainSettle) sa = Mathf.Lerp(sustainPeak, sustainLevel, Mathf.SmoothStep(0f, 1f, (s - sustainFadeIn) / sustainSettle));
            else sa = sustainLevel * (1f + sustainBreath * Mathf.Sin(now * 1.6f));
            float left = sustainEnd - now;
            if (left < sustainFadeOut) sa *= Mathf.SmoothStep(0f, 1f, left / sustainFadeOut);
        }
        sustainImage.color = WithAlpha(sustainImage.color, sa);

        pulseImage.enabled = pa > 0.001f;
        sustainImage.enabled = sa > 0.001f;
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    /// <summary>가운데는 투명, 가장자리로 갈수록 짙어지는 흰색 비네트 (색은 Image.color 로 입힌다). 인스펙터 값이 바뀌면 다시 만든다.</summary>
    void RebuildTexture()
    {
        textureDirty = false;
        const int n = 128;
        if (tex == null)
        {
            tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        var px = new Color32[n * n];
        float k = cornerShape;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), k) + Mathf.Pow(Mathf.Abs(v), k), 1f / k);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerEdge, outerEdge, r));
            px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        if (sprite == null)
        {
            sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
        }
        pulseImage.sprite = sprite;
        sustainImage.sprite = sprite;
    }
}
