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

    void OnDisable()
    {
        HorrorLightingPreset.Changed -= OnPresetChanged;
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
        RenderSettings.ambientLight = p.ambientEquator;
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
