using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 눈 파손 화면 효과 ("한쪽 눈이 다쳐 시야가 저하된 느낌").
/// 화면 절반을 가리던 검은 패널 대신:
///  1) UI 레이어 (항상 동작)
///     - 다친 눈 쪽 화면 가장자리(기본 폭 22%)만 부드럽게 어두워지는 그라데이션
///     - 같은 쪽 가장자리에 아주 약한 붉은 번짐
///     - 화면 네 귀퉁이만 살짝 어두워지는 약한 비네팅
///  2) URP 후처리 (Main Camera Post Processing이 켜져 있을 때)
///     - Vignette 중심을 반대쪽으로 옮겨 다친 쪽이 더 어둡게
///     - 약한 Gaussian Depth of Field로 먼 곳이 살짝 흐려짐 (가까운 곳/화면 중앙 플레이는 정상)
/// 화면 왜곡/카메라 흔들림은 없다. 켜고 끌 때 fadeSpeed로 부드럽게 전환.
/// 모든 UI는 Canvas 첫 번째 자식으로 들어가 HUD/비밀번호 UI보다 아래에 그려진다.
/// </summary>
[DisallowMultipleComponent]
public class EyeDamageEffect : MonoBehaviour
{
    [Header("Side")]
    [Tooltip("true = 오른쪽 눈 손상 (오른쪽 가장자리가 어두워짐). 첫 활성화 전에 설정.")]
    public bool damagedEyeRight = true;

    [Header("Edge Darkness (다친 쪽 가장자리)")]
    [Tooltip("어두워지는 영역 폭 (화면 너비 비율). 0.22 = 화면의 78%는 정상 시야.")]
    [Range(0.05f, 0.45f)] public float edgeCoverage = 0.22f;
    [Tooltip("가장자리 최대 어둡기 (0~1)")]
    [Range(0f, 1f)] public float edgeDarkness = 0.72f;
    [Tooltip("붉은 번짐 강도 (0 = 없음)")]
    [Range(0f, 0.5f)] public float redTint = 0.12f;

    [Header("Soft Vignette (UI)")]
    [Tooltip("네 귀퉁이 약한 비네팅 강도")]
    [Range(0f, 1f)] public float vignetteStrength = 0.25f;

    [Header("Post Processing (URP Volume, 선택)")]
    public bool usePostProcessing = true;
    [Range(0f, 0.6f)] public float ppVignetteIntensity = 0.32f;
    [Tooltip("Vignette 중심을 다친 쪽 반대로 얼마나 옮길지")]
    [Range(0f, 0.2f)] public float ppVignetteShift = 0.1f;
    [Tooltip("먼 곳 흐림 (Gaussian DoF). 이 거리(m)부터 흐려지기 시작")]
    public float blurStartDistance = 6f;
    public float blurEndDistance = 22f;
    [Range(0.5f, 1.5f)] public float blurMaxRadius = 0.7f;

    [Header("Transition")]
    [Tooltip("켜고 끌 때 전환 속도 (1/초)")]
    [Range(0.5f, 10f)] public float fadeSpeed = 2.5f;
    [Tooltip("가장자리 어둠이 아주 천천히 숨쉬듯 변하는 정도 (0 = 고정)")]
    [Range(0f, 0.15f)] public float pulseAmount = 0.04f;

    bool active;
    float weight;
    bool built;

    GameObject root;
    Image edgeImage;
    Image tintImage;
    Image vignetteImage;
    Volume volume;

    public bool IsActive => active;

    public void SetActive(bool on)
    {
        active = on;
        if (on) EnsureBuilt();
    }

    void Update()
    {
        weight = Mathf.MoveTowards(weight, active ? 1f : 0f, fadeSpeed * Time.deltaTime);

        if (!built) return;

        bool visible = weight > 0.001f;
        if (root != null && root.activeSelf != visible) root.SetActive(visible);

        float pulse = 1f + Mathf.Sin(Time.time * 1.3f) * pulseAmount;
        if (edgeImage != null) edgeImage.color = new Color(1f, 1f, 1f, weight * pulse);
        if (tintImage != null) tintImage.color = new Color(1f, 1f, 1f, weight);
        if (vignetteImage != null) vignetteImage.color = new Color(1f, 1f, 1f, weight);
        if (volume != null) volume.weight = usePostProcessing ? weight : 0f;

    }

    // ───────────────────────── 생성 ─────────────────────────

    void EnsureBuilt()
    {
        if (built) return;
        built = true;

        Canvas canvas = FindHudCanvas();
        if (canvas != null)
            BuildUI((RectTransform)canvas.transform);
        else
            Debug.LogWarning("[EyeDamageEffect] HUD Canvas를 찾지 못해 UI 효과를 생략합니다.", this);

        if (usePostProcessing)
            BuildVolume();

    }

    static Canvas FindHudCanvas()
    {
        Canvas best = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (c.transform.Find("SafeArea") != null) return c;
            if (best == null) best = c;
        }
        return best;
    }

    void BuildUI(RectTransform canvasRect)
    {
        root = new GameObject("EyeDamageOverlay", typeof(RectTransform));
        root.layer = 5;
        var rrt = (RectTransform)root.transform;
        rrt.SetParent(canvasRect, false);
        Stretch(rrt);
        rrt.SetAsFirstSibling(); // HUD / 비밀번호 UI 아래

        vignetteImage = NewLayer("SoftVignette", rrt, CreateVignetteSprite());
        tintImage = NewLayer("RedTint", rrt, CreateEdgeSprite(Mathf.Min(0.45f, edgeCoverage * 1.3f), redTint, new Color(0.55f, 0.02f, 0.02f), 1.2f));
        edgeImage = NewLayer("EdgeDarkness", rrt, CreateEdgeSprite(edgeCoverage, edgeDarkness, new Color(0.02f, 0f, 0f), 1.6f));

        root.SetActive(false);
    }

    static Image NewLayer(string name, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Stretch(rt);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    /// <summary>다친 쪽 가장자리에서 안쪽으로 coverage 폭만큼 부드럽게 사라지는 그라데이션.</summary>
    Sprite CreateEdgeSprite(float coverage, float maxAlpha, Color color, float power)
    {
        const int w = 256, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        for (int x = 0; x < w; x++)
        {
            float u = (x + 0.5f) / w;
            float fromEdge = damagedEyeRight ? 1f - u : u;     // 0 = 다친 쪽 끝
            float k = Mathf.Clamp01(1f - fromEdge / coverage);  // 1 = 끝, 0 = 안쪽 경계
            k = k * k * (3f - 2f * k);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float vy = Mathf.Abs(v - 0.5f) * 2f;            // 위/아래 귀퉁이 쪽으로 약간 더 넓게
                float a = Mathf.Pow(k, power) * (0.85f + 0.15f * vy);
                px[y * w + x] = new Color(color.r, color.g, color.b, Mathf.Clamp01(a * maxAlpha));
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>화면 중앙은 완전히 투명, 귀퉁이만 약하게 어두운 비네팅.</summary>
    Sprite CreateVignetteSprite()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = ((x + 0.5f) / s - 0.5f) * 2f;
            float dy = ((y + 0.5f) / s - 0.5f) * 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;   // 0 중앙 ~ 1 귀퉁이
            float k = Mathf.Clamp01((d - 0.55f) / 0.45f);       // 중앙 55%는 영향 없음
            k = k * k * (3f - 2f * k);
            px[y * s + x] = new Color(0f, 0f, 0f, k * vignetteStrength);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    void BuildVolume()
    {
        var go = new GameObject("EyeDamageVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f; // 기존 Global Volume(0)보다 위에서 블렌드
        volume.weight = 0f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "EyeDamageProfile(Runtime)";

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(ppVignetteIntensity);
        vig.smoothness.Override(0.45f);
        vig.color.Override(new Color(0.08f, 0f, 0f));
        float cx = 0.5f + (damagedEyeRight ? -ppVignetteShift : ppVignetteShift);
        vig.center.Override(new Vector2(cx, 0.5f));

        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(blurStartDistance);
        dof.gaussianEnd.Override(blurEndDistance);
        dof.gaussianMaxRadius.Override(blurMaxRadius);
        dof.highQualitySampling.Override(false);

        volume.sharedProfile = profile;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    void OnDisable()
    {
        if (volume != null) volume.weight = 0f;
        if (root != null) root.SetActive(false);
        weight = 0f;
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root);
        if (volume != null && volume.sharedProfile != null) Destroy(volume.sharedProfile);
    }
}
