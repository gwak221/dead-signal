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
