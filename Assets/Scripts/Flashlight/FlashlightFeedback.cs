using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손전등 피드백 연출 (런타임 자동 생성, 씬 수정 불필요).
///  - ScreenFlash : 스턴 성공 시 화면 전체가 아주 짧게 하얗게 번쩍 (HUD는 가리지 않음)
///  - FaceBurst   : 몬스터 얼굴 위치에서 퍼지는 흰 빛 (UI로 그려서 셰이더 의존 없음)
///  - World Light : 몬스터 얼굴 위치에 순간 Point Light → 주변 벽까지 번쩍
///  - Exposure Bar: 크로스헤어 아래 눈부심 누적 게이지
/// 피 묻은/커스텀 그래픽으로 바꾸고 싶으면 faceBurstSprite 등에 연결하면 된다.
/// </summary>
public class FlashlightFeedback : MonoBehaviour
{
    [Header("Target Canvas (auto if empty)")]
    public Canvas canvas;

    [Header("Screen Flash")]
    [Range(0f, 1f)] public float screenFlashAlpha = 0.45f;
    [Tooltip("화면 번쩍임 유지 시간(초). 플레이 방해 방지를 위해 짧게.")]
    [Range(0.03f, 0.5f)] public float screenFlashDuration = 0.14f;

    [Header("Face Burst")]
    public Sprite faceBurstSprite;
    public float faceBurstStartSize = 80f;
    public float faceBurstEndSize = 420f;
    [Range(0.05f, 1f)] public float faceBurstDuration = 0.28f;

    [Header("World Light Pulse")]
    public bool useWorldLight = true;
    public float worldLightIntensity = 12f;
    public float worldLightRange = 7f;
    [Range(0.05f, 1f)] public float worldLightDuration = 0.3f;

    [Header("Exposure Bar")]
    public bool showExposureBar = true;
    public Vector2 exposureBarSize = new Vector2(70f, 5f);
    public float exposureBarOffsetY = -28f;

    Image screenFlash;
    Image faceBurst;
    RectTransform faceBurstRect;
    RectTransform canvasRect;
    GameObject barRoot;
    Image barFill;
    Light worldLight;

    float screenFlashT = -1f;
    float faceBurstT = -1f;
    float worldLightT = -1f;
    Vector3 faceWorldPos;
    Camera cam;

    public void Init(Camera playerCamera)
    {
        cam = playerCamera;
        if (canvas == null) canvas = FindHudCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[FlashlightFeedback] Screen Space Overlay Canvas를 찾지 못해 UI 연출을 생략합니다.", this);
            return;
        }
        canvasRect = (RectTransform)canvas.transform;
        BuildUI();
    }

    static Canvas FindHudCanvas()
    {
        Canvas best = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (c.transform.Find("SafeArea") != null) return c; // 현재 HUD Canvas
            if (best == null) best = c;
        }
        return best;
    }

    void BuildUI()
    {
        // 화면 번쩍임: EyePenalty 바로 위, HUD(SafeArea) 아래 → HUD/비밀번호 UI는 가리지 않음.
        var flashGo = NewUI("FlashlightScreenFlash", canvasRect);
        var frt = (RectTransform)flashGo.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        screenFlash = flashGo.AddComponent<Image>();
        screenFlash.color = new Color(1f, 1f, 1f, 0f);
        screenFlash.raycastTarget = false;
        Transform eye = canvasRect.Find("EyePenalty");
        flashGo.transform.SetSiblingIndex(eye != null ? eye.GetSiblingIndex() + 1 : 0);
        flashGo.SetActive(false);

        // 얼굴 플래시
        var burstGo = NewUI("FlashlightFaceBurst", canvasRect);
        faceBurstRect = (RectTransform)burstGo.transform;
        faceBurstRect.anchorMin = faceBurstRect.anchorMax = new Vector2(0.5f, 0.5f);
        faceBurst = burstGo.AddComponent<Image>();
        faceBurst.sprite = faceBurstSprite != null ? faceBurstSprite : CreateSoftCircleSprite(128);
        faceBurst.raycastTarget = false;
        burstGo.transform.SetSiblingIndex(flashGo.transform.GetSiblingIndex() + 1);
        burstGo.SetActive(false);

        // 누적 게이지 (크로스헤어 아래)
        if (showExposureBar)
        {
            barRoot = NewUI("FlashlightExposureBar", canvasRect);
            var brt = (RectTransform)barRoot.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = exposureBarSize;
            brt.anchoredPosition = new Vector2(0f, exposureBarOffsetY);
            var bg = barRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.5f);
            bg.raycastTarget = false;

            var fill = NewUI("Fill", brt);
            var lrt = (RectTransform)fill.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            barFill = fill.AddComponent<Image>();
            barFill.raycastTarget = false;
            barRoot.SetActive(false);
        }
    }

    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        go.transform.SetParent(parent, false);
        return go;
    }

    // ───────────────────────── 외부 호출 ─────────────────────────

    /// <summary>스턴 성공 연출.</summary>
    public void PlayStunFlash(Vector3 faceWorldPosition)
    {
        faceWorldPos = faceWorldPosition;
        screenFlashT = 0f;
        faceBurstT = 0f;
        worldLightT = 0f;

        if (useWorldLight)
        {
            if (worldLight == null)
            {
                var go = new GameObject("FlashlightStunLight");
                worldLight = go.AddComponent<Light>();
                worldLight.type = LightType.Point;
                worldLight.color = Color.white;
                worldLight.shadows = LightShadows.None;
            }
            worldLight.transform.position = faceWorldPosition;
            worldLight.range = worldLightRange;
            worldLight.enabled = true;
        }
    }

    /// <summary>누적 게이지 (0~1). state: 0 = 숨김, 1 = 누적 중, 2 = 쿨타임/스턴 중(회색)</summary>
    public void SetExposure(float normalized, int state)
    {
        if (barRoot == null) return;

        bool show = state != 0 && (normalized > 0.001f || state == 2);
        if (barRoot.activeSelf != show) barRoot.SetActive(show);
        if (!show) return;

        var rt = (RectTransform)barFill.transform;
        rt.anchorMax = new Vector2(state == 2 ? 1f : Mathf.Clamp01(normalized), 1f);
        barFill.color = state == 2 ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : new Color(1f, 0.97f, 0.85f, 0.95f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (screenFlash != null && screenFlashT >= 0f)
        {
            screenFlashT += dt;
            float a = 1f - screenFlashT / screenFlashDuration;
            if (a <= 0f) { screenFlashT = -1f; screenFlash.gameObject.SetActive(false); }
            else
            {
                if (!screenFlash.gameObject.activeSelf) screenFlash.gameObject.SetActive(true);
                screenFlash.color = new Color(1f, 1f, 1f, screenFlashAlpha * a * a);
            }
        }

        if (faceBurst != null && faceBurstT >= 0f)
        {
            faceBurstT += dt;
            float u = faceBurstT / faceBurstDuration;
            if (u >= 1f || !TryWorldToCanvas(faceWorldPos, out Vector2 local))
            {
                if (u >= 1f) faceBurstT = -1f;
                faceBurst.gameObject.SetActive(false);
            }
            else
            {
                if (!faceBurst.gameObject.activeSelf) faceBurst.gameObject.SetActive(true);
                float e = 1f - (1f - u) * (1f - u);
                float size = Mathf.Lerp(faceBurstStartSize, faceBurstEndSize, e);
                faceBurstRect.sizeDelta = new Vector2(size, size);
                faceBurstRect.anchoredPosition = local;
                faceBurst.color = new Color(1f, 1f, 1f, (1f - u));
            }
        }

        if (worldLight != null && worldLightT >= 0f)
        {
            worldLightT += dt;
            float u = worldLightT / worldLightDuration;
            if (u >= 1f) { worldLightT = -1f; worldLight.enabled = false; }
            else worldLight.intensity = worldLightIntensity * (1f - u) * (1f - u);
        }
    }

    bool TryWorldToCanvas(Vector3 world, out Vector2 local)
    {
        local = Vector2.zero;
        if (cam == null || canvasRect == null) return false;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return false;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out local);
    }

    static Sprite CreateSoftCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f)) / r;
            float a = Mathf.Clamp01(1f - d);
            px[y * size + x] = new Color(1f, 1f, 1f, a * a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void OnDestroy()
    {
        if (worldLight != null) Destroy(worldLight.gameObject);
    }
}
