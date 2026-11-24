using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 공포 조명 프리셋 관련 메뉴.
///  - 프로젝트를 처음 열면(클론/임포트 직후 포함) 기본 프리셋 에셋이 없을 때 자동 생성한다.
///  - Add To Open Scene: 열린 씬에 HorrorLighting 오브젝트를 하나 추가.
///  - Apply Shadow Settings To URP Asset: 현재 사용 중인 URP 에셋에 그림자 값을 기록.
/// </summary>
[InitializeOnLoad]
public static class HorrorLightingMenu
{
    public const string PresetPath = "Assets/Scripts/Environment/Resources/HorrorLightingPreset.asset";

    static HorrorLightingMenu()
    {
        EditorApplication.delayCall += () => EnsurePreset();
    }

    public static HorrorLightingPreset EnsurePreset()
    {
        var preset = AssetDatabase.LoadAssetAtPath<HorrorLightingPreset>(PresetPath);
        if (preset != null) return preset;

        var dir = System.IO.Path.GetDirectoryName(PresetPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
        preset = ScriptableObject.CreateInstance<HorrorLightingPreset>();
        AssetDatabase.CreateAsset(preset, PresetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[HorrorLighting] 기본 프리셋 생성: " + PresetPath);
        return preset;
    }

    [MenuItem("Dead Signal/Lighting/Select Horror Preset", priority = 0)]
    static void SelectPreset()
    {
        Selection.activeObject = EnsurePreset();
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    [MenuItem("Dead Signal/Lighting/Add Horror Lighting To Open Scene", priority = 1)]
    static void AddToScene()
    {
        var existing = Object.FindFirstObjectByType<HorrorLightingApplier>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            Debug.Log("[HorrorLighting] 이미 씬에 있습니다: " + existing.name);
            return;
        }
        var go = new GameObject("HorrorLighting");
        Undo.RegisterCreatedObjectUndo(go, "Add Horror Lighting");
        var ap = go.AddComponent<HorrorLightingApplier>();
        ap.preset = EnsurePreset();
        ap.Apply();
        Selection.activeObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    [MenuItem("Dead Signal/Lighting/Apply Shadow Settings To URP Asset", priority = 2)]
    static void ApplyShadows()
    {
        var preset = EnsurePreset();
        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (rp == null) { Debug.LogWarning("[HorrorLighting] 현재 URP 에셋을 찾을 수 없습니다."); return; }

        if (!EditorUtility.DisplayDialog("URP 그림자 설정",
                $"{AssetDatabase.GetAssetPath(rp)} 의 그림자 값을 프리셋 값으로 바꿉니다.\n(공용 설정 파일이므로 커밋 전에 팀과 공유하세요)",
                "적용", "취소")) return;

        var so = new SerializedObject(rp);
        Set(so, "m_ShadowDistance", preset.shadowDistance);
        Set(so, "m_ShadowCascadeCount", preset.shadowCascadeCount);
        Set(so, "m_MainLightShadowmapResolution", preset.mainLightShadowResolution);
        Set(so, "m_AdditionalLightShadowsSupported", true);
        Set(so, "m_AdditionalLightsShadowmapResolution", preset.additionalLightShadowResolution);
        Set(so, "m_AdditionalLightsPerObjectLimit", preset.additionalLightsPerObject);
        Set(so, "m_SoftShadowsSupported", preset.softShadows);
        Set(so, "m_ShadowDepthBias", preset.shadowDepthBias);
        Set(so, "m_ShadowNormalBias", preset.shadowNormalBias);
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[HorrorLighting] URP 그림자 설정 적용: " + AssetDatabase.GetAssetPath(rp));
    }

    // ───────────── 프리셋 비교 / 전환 (씬·에셋을 수정하지 않는다) ─────────────
    public const string OriginalPresetPath = "Assets/Scripts/Environment/Presets/DeadSignal_OriginalLighting.asset";

    [MenuItem("Dead Signal/Lighting/Preview/Scene Original (revert)", priority = 20)]
    static void PreviewOriginal() => HorrorLightingApplier.ClearPreview();

    [MenuItem("Dead Signal/Lighting/Preview/Horror Preset (PR #13 candidate)", priority = 21)]
    static void PreviewHorror() => HorrorLightingApplier.PreviewPreset(EnsurePreset());

    [MenuItem("Dead Signal/Lighting/Preview/Original Preset Asset", priority = 22)]
    static void PreviewOriginalAsset()
    {
        var p = AssetDatabase.LoadAssetAtPath<HorrorLightingPreset>(OriginalPresetPath);
        if (p == null) { Debug.LogWarning("[HorrorLighting] 원본 프리셋 에셋이 없습니다: " + OriginalPresetPath); return; }
        HorrorLightingApplier.PreviewPreset(p);
    }

    [MenuItem("Dead Signal/Lighting/Preview/Scene Original (revert)", true)]
    [MenuItem("Dead Signal/Lighting/Preview/Horror Preset (PR #13 candidate)", true)]
    [MenuItem("Dead Signal/Lighting/Preview/Original Preset Asset", true)]
    static bool PreviewValidate() => Application.isPlaying;

    /// <summary>열린 씬(Edit Mode)의 RenderSettings 와 Global Volume 프로필 값을 읽어 '원본 분위기' 프리셋 에셋을 만든다. 이미 있으면 덮어쓰지 않는다.</summary>
    [MenuItem("Dead Signal/Lighting/Create Original Preset From Open Scene", priority = 10)]
    public static void CreateOriginalFromScene()
    {
        if (Application.isPlaying) { Debug.LogWarning("[HorrorLighting] Edit Mode 에서만 실행하세요 (씬 저장값을 읽어야 합니다)."); return; }
        if (AssetDatabase.LoadAssetAtPath<HorrorLightingPreset>(OriginalPresetPath) != null) { Debug.Log("[HorrorLighting] 원본 프리셋이 이미 있습니다: " + OriginalPresetPath); return; }
        if (Object.FindFirstObjectByType<HorrorLightingApplier>(FindObjectsInactive.Include) != null) { Debug.LogWarning("[HorrorLighting] 씬에 Applier 가 있어 RenderSettings 가 이미 덮어써졌을 수 있습니다. 중단."); return; }

        var p = ScriptableObject.CreateInstance<HorrorLightingPreset>();
        p.autoApplyOnPlay = false;
        p.fog = RenderSettings.fog; p.fogMode = RenderSettings.fogMode; p.fogColor = RenderSettings.fogColor; p.fogDensity = RenderSettings.fogDensity;
        p.ambientMode = RenderSettings.ambientMode; p.ambientSky = RenderSettings.ambientSkyColor; p.ambientEquator = RenderSettings.ambientEquatorColor;
        p.ambientGround = RenderSettings.ambientGroundColor; p.reflectionIntensity = RenderSettings.reflectionIntensity; p.subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
        // 포스트 프로세싱은 씬의 Global Volume 이 이미 담당하므로 이 프리셋은 적용하지 않는다(값은 참고용으로만 기록).
        p.applyPostProcessing = false;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!v.isGlobal || v.sharedProfile == null || v.priority != 0) continue;
            var prof = v.sharedProfile;
            if (prof.TryGet(out Tonemapping tm) && tm.mode.overrideState) p.tonemapping = tm.mode.value;
            if (prof.TryGet(out ColorAdjustments ca)) { p.postExposure = ca.postExposure.value; p.contrast = ca.contrast.value; p.saturation = ca.saturation.value; p.colorFilter = ca.colorFilter.value; }
            if (prof.TryGet(out Bloom b)) { p.bloomThreshold = b.threshold.value; p.bloomIntensity = b.intensity.value; p.bloomScatter = b.scatter.value; }
            if (prof.TryGet(out Vignette vg)) { p.vignetteIntensity = vg.intensity.value; p.vignetteSmoothness = vg.smoothness.value; }
            if (prof.TryGet(out ChromaticAberration c)) p.chromaticAberration = c.intensity.value; else p.chromaticAberration = 0f;
            if (prof.TryGet(out FilmGrain fg)) { p.filmGrainType = fg.type.value; p.filmGrainIntensity = fg.intensity.value; p.filmGrainResponse = fg.response.value; }
            p.whiteBalanceTemperature = 0f; p.whiteBalanceTint = 0f; p.shadowsTint = new Vector4(1f, 1f, 1f, 0f); p.lensDistortion = 0f;
            break;
        }
        // URP 그림자 값(기록용): PR #13 이전 PC_RPAsset 값. 에셋에 자동 적용되지 않는다.
        p.shadowDistance = 50f; p.shadowCascadeCount = 4; p.mainLightShadowResolution = 2048; p.additionalLightShadowResolution = 2048;
        p.additionalLightsPerObject = 4; p.softShadows = true; p.shadowDepthBias = 0.1f; p.shadowNormalBias = 0.5f;

        var dir = System.IO.Path.GetDirectoryName(OriginalPresetPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir)) { System.IO.Directory.CreateDirectory(dir); AssetDatabase.Refresh(); }
        AssetDatabase.CreateAsset(p, OriginalPresetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[HorrorLighting] 원본 프리셋 생성: " + OriginalPresetPath);
    }

    static void Set(SerializedObject so, string path, float v)
    {
        var p = so.FindProperty(path);
        if (p == null) { Debug.LogWarning("[HorrorLighting] 필드 없음: " + path); return; }
        if (p.propertyType == SerializedPropertyType.Integer || p.propertyType == SerializedPropertyType.Enum) p.intValue = Mathf.RoundToInt(v);
        else p.floatValue = v;
    }

    static void Set(SerializedObject so, string path, bool v)
    {
        var p = so.FindProperty(path);
        if (p == null) { Debug.LogWarning("[HorrorLighting] 필드 없음: " + path); return; }
        p.boolValue = v;
    }
}
