using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 신체 실루엣 주변을 열린 원으로 감싸는 심전도.
/// 파형 색은 안정 BPM에서 초록, 오를수록 주황, 최대 160에서 빨강.
/// </summary>
[DisallowMultipleComponent]
public class HeartRateEcgView : MonoBehaviour
{
    const int Tex = 256;
    const float BaseRadius = 102f;
    const float ArcStartDeg = 214f;
    const float ArcSweepDeg = 252f;
    const float PanelAlpha = 0.92f;

    HeartRateController heart;
    RawImage screen;
    TMP_Text danger;
    Texture2D tex;
    Color32[] pixels;
    float phase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var ui = GameObject.Find("HeartRateUI");
        if (ui == null || ui.GetComponent<HeartRateEcgView>() != null) return;
        ui.AddComponent<HeartRateEcgView>();
    }

    void Awake()
    {
        if (HeartRateController.Instance == null && GetComponent<HeartRateController>() == null)
            gameObject.AddComponent<HeartRateController>();

        HideLegacy("HeartIcon");
        HideLegacy("HeartRate_Text");
    }

    void HideLegacy(string childName)
    {
        var t = transform.Find(childName);
        if (t != null) t.gameObject.SetActive(false);
    }

    void Start()
    {
        heart = HeartRateController.Instance;
        HideLegacy("HeartIcon");
        HideLegacy("HeartRate_Text");

        var panel = (RectTransform)transform;
        panel.sizeDelta = new Vector2(186f, 186f);

        var group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = PanelAlpha;
        group.interactable = false;
        group.blocksRaycasts = false;

        var bg = transform.Find("HeartRateBackground");
        if (bg != null)
        {
            var outline = bg.GetComponent<Outline>();
            if (outline != null) Destroy(outline);
            var shadow = bg.GetComponent<Shadow>();
            if (shadow != null) Destroy(shadow);
            var img = bg.GetComponent<Image>();
            if (img != null) img.enabled = false;
        }

        tex = new Texture2D(Tex, Tex, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        pixels = new Color32[Tex * Tex];

        var go = new GameObject("EcgScreen", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        screen = go.AddComponent<RawImage>();
        screen.texture = tex;
        screen.raycastTarget = false;
        screen.color = Color.white;

        var dangerGo = new GameObject("Danger", typeof(RectTransform));
        dangerGo.layer = gameObject.layer;
        dangerGo.transform.SetParent(transform, false);
        danger = dangerGo.AddComponent<TextMeshProUGUI>();
        var legacyText = transform.Find("HeartRate_Text");
        var legacyFont = legacyText != null ? legacyText.GetComponent<TMP_Text>() : null;
        if (legacyFont != null && legacyFont.font != null) danger.font = legacyFont.font;
        danger.text = "DANGER";
        danger.fontSize = 13f;
        danger.fontStyle = FontStyles.Bold;
        danger.alignment = TextAlignmentOptions.Center;
        danger.raycastTarget = false;
        danger.textWrappingMode = TextWrappingModes.NoWrap;
        var dr = danger.rectTransform;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0f);
        dr.pivot = new Vector2(0.5f, 0f);
        dr.anchoredPosition = new Vector2(0f, 10f);
        dr.sizeDelta = new Vector2(120f, 20f);
    }

    void OnDestroy()
    {
        if (tex != null) Destroy(tex);
    }

    void Update()
    {
        if (heart == null) heart = HeartRateController.Instance;
        float bpm = heart != null ? heart.CurrentBpm : 70f;
        float max = heart != null ? heart.MaxBpm : 160f;
        float resting = heart != null ? heart.RestingBpm : 70f;
        Color trace = heart != null ? heart.TraceColor : new Color(0.22f, 0.86f, 0.34f, 1f);

        phase += Time.deltaTime * (bpm / 60f);
        Draw(trace);

        if (danger != null)
        {
            float stress = Mathf.Clamp01(Mathf.InverseLerp(resting + 30f, max, bpm));
            danger.color = new Color(trace.r, trace.g, trace.b, Mathf.Lerp(0f, 0.75f, stress));
        }
    }

    void Draw(Color trace)
    {
        var clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        var core = (Color32)trace;
        var glow = new Color32(core.r, core.g, core.b, 64);
        var rim = new Color32(226, 230, 232, 78);
        const int steps = 520;

        for (int i = 0; i <= steps; i++)
        {
            float u = i / (float)steps;
            float fade = EndFade(u);
            if (fade <= 0.01f) continue;

            var guide = RingPoint(u, BaseRadius + 12f);
            var dim = rim;
            dim.a = (byte)(rim.a * fade);
            Blob(guide.x, guide.y, dim, 0.85f);

            // 원 위에 심전도 여러 박을 깔고, 그 파형만 고리를 따라 흘러가게 한다.
            const float beatsOnArc = 3.4f;
            float along = u * beatsOnArc - phase;
            float wave = SampleWave(along);
            float head = Mathf.Repeat(phase / beatsOnArc, 1f);
            float gap = Mathf.Abs(Mathf.Repeat(u - head + 0.5f, 1f) - 0.5f);
            float sweep = Mathf.Clamp01(gap / 0.035f);
            fade *= sweep;
            if (fade <= 0.01f) continue;

            float radius = BaseRadius + (wave - 0.46f) * 26f;
            var p = RingPoint(u, radius);

            var soft = glow;
            soft.a = (byte)(glow.a * fade);
            Blob(p.x, p.y, soft, 3.4f);

            var line = core;
            line.a = (byte)(255f * fade);
            Blob(p.x, p.y, line, 1.25f);
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    static float EndFade(float u)
    {
        float e = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.07f);
        return e * e * (3f - 2f * e);
    }

    static Vector2 RingPoint(float u, float radius)
    {
        float deg = (ArcStartDeg - u * ArcSweepDeg) * Mathf.Deg2Rad;
        return new Vector2(
            Tex * 0.5f + Mathf.Cos(deg) * radius,
            Tex * 0.5f + Mathf.Sin(deg) * radius);
    }

    static float SampleWave(float beatPhase)
    {
        float p = beatPhase - Mathf.Floor(beatPhase);
        float y = 0.46f;
        y += 0.07f * Bell(p, 0.16f, 0.018f);
        y -= 0.10f * Bell(p, 0.30f, 0.008f);
        y += 0.46f * Bell(p, 0.335f, 0.010f);
        y -= 0.20f * Bell(p, 0.375f, 0.010f);
        y += 0.13f * Bell(p, 0.50f, 0.028f);
        return Mathf.Clamp(y, 0.08f, 0.96f);
    }

    static float Bell(float x, float center, float width)
    {
        float d = (x - center) / width;
        return Mathf.Exp(-d * d);
    }

    void Blob(float x, float y, Color32 c, float radius)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(x - radius));
        int x1 = Mathf.Min(Tex - 1, Mathf.CeilToInt(x + radius));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(y - radius));
        int y1 = Mathf.Min(Tex - 1, Mathf.CeilToInt(y + radius));
        float r2 = radius * radius;
        for (int yy = y0; yy <= y1; yy++)
        {
            for (int xx = x0; xx <= x1; xx++)
            {
                float dx = xx + 0.5f - x;
                float dy = yy + 0.5f - y;
                if (dx * dx + dy * dy > r2) continue;
                int i = yy * Tex + xx;
                if (c.a > pixels[i].a) pixels[i] = c;
            }
        }
    }
}
