using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 공포 게임용 조명/음영 프리셋 (에셋 1개에 모든 값을 저장).
///  - 씬 파일(RenderSettings)이 아니라 이 에셋에 값을 두므로, 씬 병합 충돌 없이
///    프로젝트를 새로 임포트/클론해도 같은 값이 유지된다.
///  - HorrorLightingApplier 가 씬 시작 시(에디터 포함) 이 값을 RenderSettings 와 런타임 Volume 에 적용한다.
///  - URP 그림자 설정은 메뉴 Dead Signal/Lighting/Apply Shadow Settings To URP Asset 으로 1회 적용한다.
/// 기본값 근거: 손전등이 주 광원이 되도록 환경광을 거의 0에 가깝게, 짙은 청록 안개로 원거리 시야를 끊고,
/// 저채도·고대비·비네트·필름 그레인으로 '오래된 CCTV' 같은 질감을 낸다.
/// </summary>
[CreateAssetMenu(menuName = "Dead Signal/Horror Lighting Preset", fileName = "HorrorLightingPreset")]
public class HorrorLightingPreset : ScriptableObject
{
    /// <summary>인스펙터에서 값이 바뀌면 호출된다. HorrorLightingApplier 가 구독해 플레이 중에도 즉시 다시 적용한다.</summary>
    public static event System.Action<HorrorLightingPreset> Changed;

#if UNITY_EDITOR
    void OnValidate()
    {
        // OnValidate 안에서 바로 적용하면 직렬화 도중이라 경고가 날 수 있어 한 프레임 미룬다
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Changed?.Invoke(this); };
    }
#endif

    [Tooltip("켜면 씬에 HorrorLightingApplier 가 없어도 플레이 시작/씬 로드 때 이 프리셋이 씬의 Fog/Ambient/Volume 을 덮어쓴다.\n" +
             "기본은 꺼짐: 메인 씬의 저장된 조명 분위기를 보존한다. 비교/조정은 Dead Signal/Lighting/Preview 메뉴(Play Mode)를 쓴다.")]
    public bool autoApplyOnPlay = false;

    [Header("Fog (시야 차단)")]
    public bool fog = true;
    public FogMode fogMode = FogMode.ExponentialSquared;
    public Color fogColor = new Color(0.018f, 0.024f, 0.026f);
    [Tooltip("ExponentialSquared 기준 0.06 ≈ 15m 부근에서 거의 보이지 않음")]
    [Range(0f, 0.3f)] public float fogDensity = 0.06f;

    [Header("Ambient (환경광) - 손전등이 주 광원이 되도록 매우 어둡게")]
    public AmbientMode ambientMode = AmbientMode.Trilight;
    public Color ambientSky = new Color(0.016f, 0.020f, 0.026f);
    public Color ambientEquator = new Color(0.022f, 0.026f, 0.028f);
    public Color ambientGround = new Color(0.008f, 0.007f, 0.006f);
    [Range(0f, 1f)] public float reflectionIntensity = 0.08f;
    public Color subtractiveShadowColor = new Color(0.10f, 0.12f, 0.15f);

    [Header("Post Processing (런타임 Volume, priority 아래 값)")]
    public bool applyPostProcessing = true;
    [Tooltip("기존 Global Volume(0)보다 위, EyeDamageEffect(50)보다 아래")]
    public float volumePriority = 10f;

    [Header("Tonemapping / Color")]
    public TonemappingMode tonemapping = TonemappingMode.ACES;
    public float postExposure = -0.35f;
    [Range(-100f, 100f)] public float contrast = 22f;
    [Range(-100f, 100f)] public float saturation = -38f;
    public Color colorFilter = new Color(0.86f, 0.97f, 0.95f);
    [Range(-100f, 100f)] public float whiteBalanceTemperature = -12f;
    [Range(-100f, 100f)] public float whiteBalanceTint = 6f;
    [Tooltip("어두운 영역을 청록으로 (Shadows/Midtones/Highlights 의 Shadows)")]
    public Vector4 shadowsTint = new Vector4(0.92f, 1.0f, 1.04f, -0.06f);

    [Header("Bloom (형광등/비상등 번짐)")]
    public float bloomThreshold = 0.95f;
    public float bloomIntensity = 0.6f;
    [Range(0f, 1f)] public float bloomScatter = 0.72f;

    [Header("Lens")]
    [Range(0f, 1f)] public float vignetteIntensity = 0.42f;
    [Range(0.01f, 1f)] public float vignetteSmoothness = 0.5f;
    [Range(0f, 1f)] public float chromaticAberration = 0.12f;
    public FilmGrainLookup filmGrainType = FilmGrainLookup.Medium3;
    [Range(0f, 1f)] public float filmGrainIntensity = 0.38f;
    [Range(0f, 1f)] public float filmGrainResponse = 0.7f;
    [Range(-1f, 1f)] public float lensDistortion = -0.08f;

    [Header("URP Shadows (메뉴로 URP 에셋에 적용)")]
    [Tooltip("실내 맵이라 짧게 잡아 해상도를 가까운 곳에 몰아준다")]
    public float shadowDistance = 28f;
    [Range(1, 4)] public int shadowCascadeCount = 2;
    public int mainLightShadowResolution = 2048;
    [Tooltip("손전등(Spot) 그림자가 핵심이므로 추가 광원 그림자 아틀라스를 크게")]
    public int additionalLightShadowResolution = 4096;
    [Range(1, 8)] public int additionalLightsPerObject = 6;
    public bool softShadows = true;
    public float shadowDepthBias = 0.6f;
    public float shadowNormalBias = 0.4f;
}
