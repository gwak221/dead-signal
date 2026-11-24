using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// HorrorLightingPreset 값을 씬에 적용한다. 씬에 이 컴포넌트가 붙은 오브젝트 하나만 두면 된다.
///  - RenderSettings(안개/환경광/반사)는 씬 저장값을 덮어쓰므로, 팀원이 씬 조명값을 바꿔도 프리셋이 기준이 된다.
///  - 포스트 프로세싱은 런타임 VolumeProfile 을 만들어 적용한다(에셋/씬 수정 없음).
///  - [ExecuteAlways] 라서 에디터 Scene 뷰에서도 같은 화면을 볼 수 있다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class HorrorLightingApplier : MonoBehaviour
{
    public HorrorLightingPreset preset;

    [Tooltip("켜두면 인스펙터에서 프리셋 값을 바꿀 때 바로 반영")]
    public bool liveUpdate = true;

    Volume _volume;
    VolumeProfile _profile;

    void OnEnable()
    {
        if (preset == null) preset = LoadDefaultPreset();
        HorrorLightingPreset.Changed += OnPresetChanged;
        Apply();
    }

    // 프리셋 에셋 값을 바꾸면(플레이 중 포함) 바로 다시 적용
    void OnPresetChanged(HorrorLightingPreset p)
    {
        if (liveUpdate && p == preset && this != null && isActiveAndEnabled) Apply();
    }

    // ── 씬 원본 RenderSettings 스냅샷: 이 Applier 가 처음 덮어쓰기 직전 값을 저장해 두었다가, 꺼질 때 복원한다 ──
    struct RsSnapshot
    {
        public bool valid; public int sceneHandle;
        public bool fog; public FogMode fogMode; public Color fogColor; public float fogDensity, fogStart, fogEnd;
        public AmbientMode ambientMode; public Color sky, equator, ground, light; public float ambientIntensity, reflection; public Color subtractive;
    }
    RsSnapshot _snap;

    void CaptureSnapshot()
    {
        if (_snap.valid) return;
        _snap = new RsSnapshot
        {
            valid = true, sceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle,
            fog = RenderSettings.fog, fogMode = RenderSettings.fogMode, fogColor = RenderSettings.fogColor, fogDensity = RenderSettings.fogDensity,
            fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
            ambientMode = RenderSettings.ambientMode, sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
            ground = RenderSettings.ambientGroundColor, light = RenderSettings.ambientLight, ambientIntensity = RenderSettings.ambientIntensity,
            reflection = RenderSettings.reflectionIntensity, subtractive = RenderSettings.subtractiveShadowColor,
        };
    }

    void RestoreSnapshot()
    {
        if (!_snap.valid) return;
        if (_snap.sceneHandle == UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle)   // 다른 씬으로 넘어갔다면 복원하지 않는다
        {
            RenderSettings.fog = _snap.fog; RenderSettings.fogMode = _snap.fogMode; RenderSettings.fogColor = _snap.fogColor;
            RenderSettings.fogDensity = _snap.fogDensity; RenderSettings.fogStartDistance = _snap.fogStart; RenderSettings.fogEndDistance = _snap.fogEnd;
            RenderSettings.ambientMode = _snap.ambientMode; RenderSettings.ambientSkyColor = _snap.sky; RenderSettings.ambientEquatorColor = _snap.equator;
            RenderSettings.ambientGroundColor = _snap.ground; RenderSettings.ambientLight = _snap.light; RenderSettings.ambientIntensity = _snap.ambientIntensity;
            RenderSettings.reflectionIntensity = _snap.reflection; RenderSettings.subtractiveShadowColor = _snap.subtractive;
        }
        _snap = default;
    }

    // ── 미리보기(Preview): 씬 파일/에셋을 건드리지 않고 Play Mode 에서만 프리셋을 임시 적용/해제한다 (Dead Signal/Lighting/Preview 메뉴) ──
    const string PreviewName = "HorrorLighting (Preview)";
    static HorrorLightingApplier _preview;   // HideFlags.DontSave 오브젝트는 FindObjects 에 잡히지 않으므로 직접 참조한다

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _preview = null;

    public static void PreviewPreset(HorrorLightingPreset p)
    {
        if (!Application.isPlaying || p == null) return;
        if (_preview == null)
        {
            var go = new GameObject(PreviewName) { hideFlags = HideFlags.DontSave };
            go.SetActive(false);                              // OnEnable 이 기본 프리셋을 먼저 적용하지 않도록 비활성 상태로 만든다
            _preview = go.AddComponent<HorrorLightingApplier>();
            _preview.preset = p;
            go.SetActive(true);                               // OnEnable → 원본 스냅샷 저장 + p 적용
            return;
        }
        _preview.preset = p;
        _preview.Apply();
    }

    public static void ClearPreview()
    {
        if (_preview != null) Destroy(_preview.gameObject);   // OnDisable 에서 씬 원본 RenderSettings/Volume 복원
        _preview = null;
    }

    void OnDisable()
    {
        HorrorLightingPreset.Changed -= OnPresetChanged;
        RestoreSnapshot();
        if (_volume != null)
        {
            if (Application.isPlaying) Destroy(_volume.gameObject); else DestroyImmediate(_volume.gameObject);
        }
        if (_profile != null)
        {
            if (Application.isPlaying) Destroy(_profile); else DestroyImmediate(_profile);
        }
        _volume = null;
        _profile = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!liveUpdate || !isActiveAndEnabled) return;
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Apply(); };
    }
#endif

    // 씬에 Applier 가 없어도 프리셋의 autoApplyOnPlay 가 켜져 있으면 자동 생성 (씬 파일을 건드리지 않음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirstObjectByType<HorrorLightingApplier>() != null) return;
        var p = LoadDefaultPreset();
        if (p == null || !p.autoApplyOnPlay) return;
        var go = new GameObject("HorrorLighting (Auto)");
        DontDestroyOnLoad(go);
        go.AddComponent<HorrorLightingApplier>().preset = p;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) =>
        {
            var ap = FindFirstObjectByType<HorrorLightingApplier>();
            if (ap != null) ap.Apply();   // RenderSettings 는 씬마다 따로 저장되므로 다시 적용
        };
    }

    static HorrorLightingPreset LoadDefaultPreset()
    {
        // Resources 폴더에 둔 기본 프리셋 (빌드에도 포함)
        return Resources.Load<HorrorLightingPreset>("HorrorLightingPreset");
    }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (preset == null) return;
        CaptureSnapshot();
        ApplyRenderSettings(preset);
        if (preset.applyPostProcessing) ApplyVolume(); else if (_volume != null) _volume.enabled = false;
    }

    public static void ApplyRenderSettings(HorrorLightingPreset p)
    {
        RenderSettings.fog = p.fog;
        RenderSettings.fogMode = p.fogMode;
        RenderSettings.fogColor = p.fogColor;
        RenderSettings.fogDensity = p.fogDensity;
        RenderSettings.ambientMode = p.ambientMode;
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;
        // Flat 모드의 단색 환경광은 ambientLight(= Sky 색) 를 쓴다. Trilight 일 때만 Equator 로 맞춘다.
        RenderSettings.ambientLight = p.ambientMode == AmbientMode.Flat ? p.ambientSky : p.ambientEquator;
        RenderSettings.reflectionIntensity = p.reflectionIntensity;
        RenderSettings.subtractiveShadowColor = p.subtractiveShadowColor;
    }

    void ApplyVolume()
    {
        if (_volume == null)
        {
            var go = new GameObject("HorrorLightingVolume (Runtime)");
            go.hideFlags = HideFlags.HideAndDontSave;   // 씬에 저장되지 않음
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
        }
        if (_profile == null)
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "HorrorLightingProfile(Runtime)";
            _profile.hideFlags = HideFlags.HideAndDontSave;
        }
        _volume.enabled = true;
        _volume.priority = preset.volumePriority;
        _volume.sharedProfile = _profile;

        var p = preset;

        var tm = Get<Tonemapping>();
        tm.mode.Override(p.tonemapping);

        var ca = Get<ColorAdjustments>();
        ca.postExposure.Override(p.postExposure);
        ca.contrast.Override(p.contrast);
        ca.saturation.Override(p.saturation);
        ca.colorFilter.Override(p.colorFilter);

        var wb = Get<WhiteBalance>();
        wb.temperature.Override(p.whiteBalanceTemperature);
        wb.tint.Override(p.whiteBalanceTint);

        var smh = Get<ShadowsMidtonesHighlights>();
        smh.shadows.Override(p.shadowsTint);

        var bloom = Get<Bloom>();
        bloom.threshold.Override(p.bloomThreshold);
        bloom.intensity.Override(p.bloomIntensity);
        bloom.scatter.Override(p.bloomScatter);

        var vig = Get<Vignette>();
        vig.intensity.Override(p.vignetteIntensity);
        vig.smoothness.Override(p.vignetteSmoothness);

        var chr = Get<ChromaticAberration>();
        chr.intensity.Override(p.chromaticAberration);

        var fg = Get<FilmGrain>();
        fg.type.Override(p.filmGrainType);
        fg.intensity.Override(p.filmGrainIntensity);
        fg.response.Override(p.filmGrainResponse);

        var ld = Get<LensDistortion>();
        ld.intensity.Override(p.lensDistortion);
    }

    T Get<T>() where T : VolumeComponent
    {
        if (!_profile.TryGet(out T c)) c = _profile.Add<T>(true);
        c.active = true;
        return c;
    }
}
