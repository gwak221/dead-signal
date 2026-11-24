using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 1인칭 포획/사망 혈흔 연출.
/// PlayerCaughtSequence 의 Impact / Fall / Respawn / GameOver 흐름에 맞춰 동작하며,
/// 붉은 충격 플래시, 화면 가장자리 혈흔 튐(Splatter), 흘러내리는 핏줄기(Drips), URP 붉은 시야/흐림을 제어한다.
/// 기존 검은 눈꺼풀(EyelidCanvas, order 150)과 GAME OVER UI(order 200)는 건드리지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBloodDeathEffect : MonoBehaviour
{
    [Header("Overlay Layering")]
    [Tooltip("EyelidCanvas(150) 아래에서 눈꺼풀 안쪽 시야에 혈흔/핏줄기를 그린다.")]
    public int vignetteSortingOrder = 147;
    public int splatterSortingOrder = 149;
    [Tooltip("눈이 감길 때도 가장자리에 은은히 스며드는 붉은 잔광 (150 위, GameOver 200 아래).")]
    public int overBleedSortingOrder = 151;

    [Header("Impact Flash")]
    [Range(0f, 1f)] public float firstHitFlashAlpha = 0.44f;
    [Range(0f, 1f)] public float fatalFlashAlpha = 0.68f;
    public float flashDecayDuration = 0.30f;

    [Header("Blood Vignette")]
    [Range(0f, 1f)] public float firstHitVignetteAlpha = 0.52f;
    [Range(0f, 1f)] public float fatalVignetteAlpha = 0.74f;

    [Header("Blood Splatter & Drips")]
    [Range(0f, 1f)] public float firstHitSplatterAlpha = 0.84f;
    [Range(0f, 1f)] public float fatalSplatterAlpha = 0.98f;
    public int firstHitDripCount = 7;
    public int fatalDripCount = 12;

    [Header("Post Processing (URP Volume)")]
    public bool usePostProcessing = true;
    public float volumePriority = 80f;
    public Color firstHitColorFilter = new Color(1f, 0.38f, 0.35f, 1f);
    public Color fatalColorFilter = new Color(1f, 0.18f, 0.18f, 1f);
    [Tooltip("HospitalMapProfile 기본 검은 비네트(0.431)를 대신 덮어쓰므로 낮게 유지해 가장자리 검은 조임을 없앤다.")]
    [Range(0f, 0.85f)] public float firstHitPpVignette = 0.18f;
    [Range(0f, 0.85f)] public float fatalPpVignette = 0.25f;

    struct DripState
    {
        public RectTransform rect;
        public Image image;
        public float xNorm;
        public float targetLen;
        public float width;
        public float delay;
        public float duration;
        public bool fatalOnly;
    }

    enum Phase { Idle, Active, Recovering, Dead }

    Phase phase = Phase.Idle;
    bool built;
    bool currentFatal;
    float elapsedSinceHit;
    float fallAmount;
    float lidClosed;
    float recoverElapsed;
    float recoverDuration = 2.2f;
    float recoverStartAlpha = 1f;

    GameObject vignetteCanvasGo;
    GameObject detailCanvasGo;
    GameObject overBleedCanvasGo;
    Image flashImage;
    Image vignetteImage;
    Image splatterImage;
    Image overBleedImage;
    RectTransform splatterRect;

    readonly List<DripState> drips = new List<DripState>();
    readonly List<Texture2D> ownedTextures = new List<Texture2D>();

    Volume volume;
    Vignette ppVignette;
    ColorAdjustments ppColor;
    ChromaticAberration ppChromatic;
    DepthOfField ppDof;

    // (xNorm, targetLen): 화면 상단(y=1.0)에서 출발해 중력으로 흘러내리는 핏줄기
    static readonly Vector2[] DripPresets =
    {
        new Vector2(0.07f, 0.56f),
        new Vector2(0.92f, 0.62f),
        new Vector2(0.16f, 0.48f),
        new Vector2(0.83f, 0.52f),
        new Vector2(0.28f, 0.40f),
        new Vector2(0.71f, 0.43f),
        new Vector2(0.41f, 0.34f),
        // fatal-only extras
        new Vector2(0.57f, 0.46f),
        new Vector2(0.12f, 0.66f),
        new Vector2(0.87f, 0.68f),
        new Vector2(0.48f, 0.52f),
        new Vector2(0.64f, 0.39f),
    };

    void Awake()
    {
        // 첫 피격 순간 히치 없이 즉시 플래시가 터지도록 씬 시작 시 미리 텍스처 생성
        EnsureBuilt();
    }

    /// <summary>물림 충격 순간 호출: 즉시 붉은 플래시·혈흔 튀김·핏줄기 낙하를 시작한다.</summary>
    public void TriggerHit(bool firstHit)
    {
        EnsureBuilt();
        currentFatal = !firstHit;
        phase = Phase.Active;
        elapsedSinceHit = 0f;
        fallAmount = 0f;
        lidClosed = 0f;

        for (int i = 0; i < drips.Count; i++)
        {
            var d = drips[i];
            d.xNorm = Mathf.Clamp(DripPresets[i].x + Random.Range(-0.02f, 0.02f), 0.03f, 0.97f);
            d.targetLen = DripPresets[i].y * Random.Range(0.88f, 1.16f);
            d.delay = i < 4 ? Random.Range(0f, 0.05f) : Random.Range(0.05f, 0.28f);
            d.duration = Random.Range(0.60f, 1.15f);
            drips[i] = d;
        }

        if (splatterRect != null)
            splatterRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-3.5f, 3.5f));

        SetCanvasesActive(true);
        ApplyVisuals();
    }

    /// <summary>쓰러짐(fall 0..1) 및 눈꺼풀 닫힘(lid 0..1) 진행도를 반영한다.</summary>
    public void SetFallProgress(float fall, float lid)
    {
        if (!built) return;
        fallAmount = Mathf.Clamp01(fall);
        lidClosed = Mathf.Clamp01(lid);
        ApplyVisuals();
    }

    /// <summary>1차 피격 부활 직전 호출: 암전을 풀며 남은 혈흔이 서서히 가라앉게 한다.</summary>
    public void BeginRecovery(float duration)
    {
        if (!built) return;
        phase = Phase.Recovering;
        recoverElapsed = 0f;
        recoverDuration = Mathf.Max(0.4f, duration);
        recoverStartAlpha = 0.85f;
        lidClosed = 0f;
        fallAmount = 0.2f;
        ApplyVisuals();
    }

    /// <summary>2차 피격 게임 오버 확정 시 호출.</summary>
    public void LockFatal()
    {
        if (!built) return;
        phase = Phase.Dead;
        currentFatal = true;
        fallAmount = 1f;
        lidClosed = 1f;
        ApplyVisuals();
    }

    /// <summary>즉시 모든 혈흔/후처리 효과를 숨긴다.</summary>
    public void ResetImmediate()
    {
        phase = Phase.Idle;
        elapsedSinceHit = 0f;
        fallAmount = 0f;
        lidClosed = 0f;
        if (!built) return;
        SetCanvasesActive(false);
        if (volume != null) volume.weight = 0f;
    }

    void Update()
    {
        if (!built || phase == Phase.Idle) return;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        elapsedSinceHit += dt;

        if (phase == Phase.Recovering)
        {
            recoverElapsed += dt;
            if (recoverElapsed >= recoverDuration)
            {
                ResetImmediate();
                return;
            }
        }

        ApplyVisuals();
    }

    void ApplyVisuals()
    {
        float masterAlpha = 1f;
        if (phase == Phase.Recovering)
        {
            float k = Mathf.Clamp01(recoverElapsed / recoverDuration);
            masterAlpha = (1f - k * k) * recoverStartAlpha;
        }

        float pulse = 1f + 0.06f * Mathf.Sin(elapsedSinceHit * (currentFatal ? 8f : 11f));
        float buildUp = Mathf.Lerp(0.68f, 1f, fallAmount);

        if (flashImage != null)
        {
            float flashPeak = currentFatal ? fatalFlashAlpha : firstHitFlashAlpha;
            float flashT = Mathf.Clamp01(elapsedSinceHit / Mathf.Max(0.05f, flashDecayDuration));
            float flashA = flashPeak * Mathf.Pow(1f - flashT, 2.0f) * masterAlpha;
            flashImage.color = new Color(0.75f, 0.015f, 0.02f, flashA);
        }

        if (vignetteImage != null)
        {
            float targetVig = (currentFatal ? fatalVignetteAlpha : firstHitVignetteAlpha) * buildUp * pulse;
            vignetteImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(targetVig * masterAlpha));
        }

        if (overBleedImage != null)
            overBleedImage.color = Color.clear;

        if (splatterImage != null && splatterRect != null)
        {
            float punch = Mathf.Clamp01(elapsedSinceHit / 0.13f);
            float scale = Mathf.Lerp(0.94f, 1.02f, Mathf.Sin(punch * Mathf.PI * 0.5f));
            splatterRect.localScale = new Vector3(scale, scale, 1f);

            float targetSplatter = (currentFatal ? fatalSplatterAlpha : firstHitSplatterAlpha) * Mathf.Lerp(0.88f, 1f, fallAmount);
            splatterImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(targetSplatter * masterAlpha));
        }

        int activeDrips = currentFatal ? fatalDripCount : firstHitDripCount;
        for (int i = 0; i < drips.Count; i++)
        {
            var d = drips[i];
            if (d.image == null || d.rect == null) continue;
            bool allow = i < activeDrips && (!d.fatalOnly || currentFatal);
            if (!allow)
            {
                d.image.color = new Color(1f, 1f, 1f, 0f);
                continue;
            }

            float localT = Mathf.Clamp01((elapsedSinceHit - d.delay) / Mathf.Max(0.1f, d.duration));
            float flow = 1f - Mathf.Pow(1f - localT, 2.2f);
            float curLen = Mathf.Max(0.02f, d.targetLen * flow);

            d.rect.anchorMin = new Vector2(d.xNorm, Mathf.Max(0f, 1f - curLen));
            d.rect.anchorMax = new Vector2(d.xNorm, 1f);
            d.rect.anchoredPosition = Vector2.zero;
            d.rect.sizeDelta = new Vector2(d.width, 0f);

            float appear = Mathf.Clamp01(localT * 6f);
            float dripAlpha = appear * (currentFatal ? 0.98f : 0.92f) * masterAlpha;
            d.image.color = new Color(1f, 1f, 1f, dripAlpha);
        }

        if (volume != null)
        {
            float ppWeight = usePostProcessing ? Mathf.Clamp01(masterAlpha * (phase == Phase.Dead ? 1f : Mathf.Lerp(1f, 0.7f, lidClosed))) : 0f;
            volume.weight = ppWeight;

            float trauma = Mathf.Lerp(0.55f, 1f, fallAmount);
            if (ppVignette != null)
                ppVignette.intensity.value = (currentFatal ? fatalPpVignette : firstHitPpVignette) * trauma;
            if (ppColor != null)
                ppColor.colorFilter.value = Color.Lerp(Color.white, currentFatal ? fatalColorFilter : firstHitColorFilter, trauma);
            if (ppChromatic != null)
            {
                float chromDecay = Mathf.Clamp01(elapsedSinceHit / 0.35f);
                ppChromatic.intensity.value = Mathf.Lerp(currentFatal ? 0.65f : 0.45f, 0.16f, chromDecay);
            }
            if (ppDof != null)
                ppDof.gaussianMaxRadius.value = Mathf.Lerp(0.5f, currentFatal ? 1.15f : 0.90f, fallAmount);
        }
    }

    void SetCanvasesActive(bool active)
    {
        if (vignetteCanvasGo != null && vignetteCanvasGo.activeSelf != active) vignetteCanvasGo.SetActive(active);
        if (detailCanvasGo != null && detailCanvasGo.activeSelf != active) detailCanvasGo.SetActive(active);
        if (overBleedCanvasGo != null && overBleedCanvasGo.activeSelf != active) overBleedCanvasGo.SetActive(active);
    }

    // ───────────────────────── 생성 ─────────────────────────

    void EnsureBuilt()
    {
        if (built) return;
        built = true;

        vignetteCanvasGo = CreateOverlayCanvas("PlayerBloodVignetteCanvas", vignetteSortingOrder);
        detailCanvasGo = CreateOverlayCanvas("PlayerBloodDetailCanvas", splatterSortingOrder);
        overBleedCanvasGo = CreateOverlayCanvas("PlayerBloodOverBleedCanvas", overBleedSortingOrder);

        var vigSprite = CreateBloodVignetteSprite(0.42f);
        vignetteImage = AddFullscreenImage(vignetteCanvasGo.transform, "BloodVignette", vigSprite);

        var flashGo = new GameObject("BloodFlash", typeof(RectTransform), typeof(Image));
        flashGo.transform.SetParent(vignetteCanvasGo.transform, false);
        Stretch((RectTransform)flashGo.transform);
        flashImage = flashGo.GetComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.color = new Color(0.75f, 0.015f, 0.02f, 0f);

        var splatSprite = CreateBloodSplatterSprite();
        splatterImage = AddFullscreenImage(detailCanvasGo.transform, "BloodSplatter", splatSprite);
        splatterRect = splatterImage.rectTransform;

        var dripSprite = CreateBloodDripSprite();
        for (int i = 0; i < DripPresets.Length; i++)
        {
            var go = new GameObject("BloodDrip_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(detailCanvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 1f);
            var img = go.GetComponent<Image>();
            img.sprite = dripSprite;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);

            drips.Add(new DripState
            {
                rect = rt,
                image = img,
                xNorm = DripPresets[i].x,
                targetLen = DripPresets[i].y,
                width = Random.Range(38f, 68f),
                delay = 0f,
                duration = 0.8f,
                fatalOnly = i >= 7
            });
        }

        var overSprite = CreateBloodVignetteSprite(0.58f);
        overBleedImage = AddFullscreenImage(overBleedCanvasGo.transform, "OverBloodBleed", overSprite);

        if (usePostProcessing) BuildVolume();
        SetCanvasesActive(false);
    }

    GameObject CreateOverlayCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        return go;
    }

    static Image AddFullscreenImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = false;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>HLSL smoothstep(edge0, edge1, x): edge0 쪽이 0, edge1 쪽이 1.</summary>
    static float SmoothStep01(float edge0, float edge1, float x)
    {
        float denom = edge1 - edge0;
        if (Mathf.Abs(denom) < 1e-6f) return x >= edge1 ? 1f : 0f;
        float t = Mathf.Clamp01((x - edge0) / denom);
        return t * t * (3f - 2f * t);
    }

    Sprite CreateBloodVignetteSprite(float clearRadius)
    {
        const int w = 256, h = 144;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        ownedTextures.Add(tex);
        var px = new Color[w * h];

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x + 0.5f) / w * 2f - 1f;
            float ny = (y + 0.5f) / h * 2f - 1f;
            float ang = Mathf.Atan2(ny, nx);
            float organic = 0.055f * Mathf.Sin(ang * 3f + 0.4f)
                          + 0.035f * Mathf.Cos(ang * 7f - 1.1f);
            float dist = Mathf.Sqrt(nx * nx * 0.78f + ny * ny * 1.05f) + organic;
            float k = SmoothStep01(clearRadius, 1.18f, dist);

            Color col = Color.Lerp(new Color(0.62f, 0.018f, 0.022f, 1f), new Color(0.38f, 0.006f, 0.008f, 1f), Mathf.Clamp01((dist - 0.78f) / 0.52f));
            col.a = Mathf.Clamp01(k * 0.85f);
            px[y * w + x] = col;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite CreateBloodSplatterSprite()
    {
        const int w = 640, h = 360;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        ownedTextures.Add(tex);
        var alphaBuf = new float[w * h];
        var depthBuf = new float[w * h];

        var state = Random.state;
        Random.InitState(48271);

        var spots = new List<(Vector2 pos, Vector2 dir, float rx, float ry, float phase)>();
        Vector2[] clusters =
        {
            new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.86f),
            new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.20f),
            new Vector2(0.20f, 0.92f), new Vector2(0.79f, 0.91f),
            new Vector2(0.16f, 0.11f), new Vector2(0.84f, 0.12f),
            new Vector2(0.03f, 0.54f), new Vector2(0.97f, 0.52f),
        };
        Vector2 center = new Vector2(0.5f, 0.5f);

        foreach (var cl in clusters)
        {
            Vector2 outDir = (cl - center).normalized;
            // 가장자리 불규칙 혈흔 뭉치 (다수의 비대칭 소형 얼룩 결합)
            for (int l = 0; l < 6; l++)
            {
                Vector2 j = Random.insideUnitCircle * 0.032f;
                float r = Random.Range(0.016f, 0.042f);
                Vector2 dir = (outDir + Random.insideUnitCircle * 0.55f).normalized;
                spots.Add((cl + new Vector2(j.x, j.y * 1.3f), dir, r * Random.Range(1.0f, 1.55f), r * Random.Range(0.55f, 0.95f), Random.Range(0f, 6.28f)));
            }
            // 방사형으로 길게 튄 피 방울/줄기
            for (int s = 0; s < 16; s++)
            {
                Vector2 sprayDir = (outDir + Random.insideUnitCircle * 0.5f).normalized;
                float dist = Random.Range(0.02f, 0.16f);
                Vector2 sp = cl + new Vector2(sprayDir.x * dist * 0.85f, sprayDir.y * dist * 1.25f);
                // 중앙 42% 영역은 가리지 않도록 억제
                if (Vector2.Distance(sp, center) < 0.36f) continue;
                float r = Random.Range(0.0035f, 0.011f);
                spots.Add((sp, sprayDir, r * Random.Range(1.6f, 3.8f), r * Random.Range(0.4f, 0.8f), Random.Range(0f, 6.28f)));
            }
        }
        Random.state = state;

        // 각 spot의 바운딩 박스 픽셀만 순회 (O(spots * box) 로 생성 시간 수 ms 수준)
        float aspect = (float)w / h;
        for (int i = 0; i < spots.Count; i++)
        {
            var sp = spots[i];
            float maxR = Mathf.Max(sp.rx, sp.ry) * 1.30f;
            int minX = Mathf.Clamp(Mathf.FloorToInt((sp.pos.x - maxR / aspect) * w), 0, w - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((sp.pos.x + maxR / aspect) * w), 0, w - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt((sp.pos.y - maxR) * h), 0, h - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((sp.pos.y + maxR) * h), 0, h - 1);

            for (int y = minY; y <= maxY; y++)
            {
                float v = (y + 0.5f) / h;
                float dy = v - sp.pos.y;
                for (int x = minX; x <= maxX; x++)
                {
                    float u = (x + 0.5f) / w;
                    float dx = (u - sp.pos.x) * aspect;
                    float proj = dx * sp.dir.x + dy * sp.dir.y;
                    float perp = -dx * sp.dir.y + dy * sp.dir.x;
                    float ang = Mathf.Atan2(perp, proj);
                    float noise = 1f + 0.14f * Mathf.Sin(ang * 3f + sp.phase) + 0.09f * Mathf.Cos(ang * 7f - sp.phase);
                    float nd = Mathf.Sqrt((proj * proj) / (sp.rx * sp.rx) + (perp * perp) / (sp.ry * sp.ry)) / noise;
                    if (nd < 1.14f)
                    {
                        float edge = SmoothStep01(1.12f, 0.42f, nd);
                        float d = SmoothStep01(0.92f, 0.12f, nd);
                        int idx = y * w + x;
                        if (edge > alphaBuf[idx]) alphaBuf[idx] = edge;
                        if (d > depthBuf[idx]) depthBuf[idx] = d;
                    }
                }
            }
        }

        var px = new Color[w * h];
        Color rim = new Color(0.34f, 0.008f, 0.010f, 1f);
        Color clot = new Color(0.20f, 0.004f, 0.006f, 1f);
        for (int i = 0; i < px.Length; i++)
        {
            if (alphaBuf[i] > 0.01f)
            {
                Color c = Color.Lerp(rim, clot, depthBuf[i] * 0.65f);
                c.a = Mathf.Clamp01(alphaBuf[i] * 0.85f);
                px[i] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite CreateBloodDripSprite()
    {
        const int w = 64, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        ownedTextures.Add(tex);
        var px = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h; // 0 = 아래쪽 핏방울 끝, 1 = 화면 상단
            float wobble = 0.09f * Mathf.Sin(v * 11f) + 0.045f * Mathf.Cos(v * 19f);

            // 아랫쪽 둥근 핏방울(v=0.07) + 가늘고 유기적으로 구불구불한 피 줄기 + 상단 끝 자연 페이드
            float bulb = Mathf.Exp(-Mathf.Pow((v - 0.065f) / 0.045f, 2f)) * 0.24f;
            float baseHalfW = v < 0.065f
                ? Mathf.Lerp(0.02f, 0.34f, SmoothStep01(0.012f, 0.065f, v))
                : Mathf.Lerp(0.11f, 0.19f, v) + bulb;

            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float d = Mathf.Abs(u - wobble);
                float edge = SmoothStep01(baseHalfW + 0.07f, Mathf.Max(0f, baseHalfW - 0.04f), d);
                if (edge <= 0.005f) continue;

                float topFade = SmoothStep01(1.0f, 0.80f, v) * 0.45f + 0.55f;
                float ridge = Mathf.Clamp01(1f - d / Mathf.Max(0.04f, baseHalfW));
                float spec = Mathf.Exp(-Mathf.Pow((u - wobble + 0.04f) / 0.045f, 2f)) * 0.24f;

                Color c = Color.Lerp(new Color(0.58f, 0.014f, 0.018f), new Color(0.20f, 0.004f, 0.006f), ridge * 0.72f);
                c.r = Mathf.Clamp01(c.r + spec);
                c.g = Mathf.Clamp01(c.g + spec * 0.06f);
                c.b = Mathf.Clamp01(c.b + spec * 0.06f);
                c.a = edge * topFade * 0.95f;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f));
    }

    void BuildVolume()
    {
        var go = new GameObject("PlayerBloodVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = volumePriority;
        volume.weight = 0f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "PlayerBloodProfile(Runtime)";

        ppVignette = profile.Add<Vignette>(true);
        ppVignette.intensity.Override(firstHitPpVignette);
        ppVignette.smoothness.Override(0.42f);
        ppVignette.color.Override(new Color(0.50f, 0.012f, 0.016f));

        ppColor = profile.Add<ColorAdjustments>(true);
        ppColor.colorFilter.Override(Color.white);
        ppColor.contrast.Override(14f);

        ppChromatic = profile.Add<ChromaticAberration>(true);
        ppChromatic.intensity.Override(0.2f);

        ppDof = profile.Add<DepthOfField>(true);
        ppDof.mode.Override(DepthOfFieldMode.Gaussian);
        ppDof.gaussianStart.Override(0.4f);
        ppDof.gaussianEnd.Override(3.2f);
        ppDof.gaussianMaxRadius.Override(1.2f);
        ppDof.highQualitySampling.Override(true);

        volume.sharedProfile = profile;
    }

    void OnDisable() => ResetImmediate();

    void OnDestroy()
    {
        if (vignetteCanvasGo != null) Destroy(vignetteCanvasGo);
        if (detailCanvasGo != null) Destroy(detailCanvasGo);
        if (overBleedCanvasGo != null) Destroy(overBleedCanvasGo);
        if (volume != null && volume.sharedProfile != null) Destroy(volume.sharedProfile);
        for (int i = 0; i < ownedTextures.Count; i++)
            if (ownedTextures[i] != null) Destroy(ownedTextures[i]);
        ownedTextures.Clear();
    }
}
