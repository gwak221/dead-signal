using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools/Openables/Build Prefabs : Openables/Models/*.fbx 로 독립 프리팹(Variant 아님)을 만든다.
///   Prefabs/&lt;name&gt;.prefab
///     &lt;name&gt;            ← 루트: OpenableFurniture, OpenableSound(+AudioSource), (tall_cabinet: HideSpot)
///       &lt;name&gt;_model    ← FBX를 풀어서(Unpack) 넣은 계층. 파츠마다 OpenablePart + BoxCollider, 본체 MeshCollider
/// 머티리얼은 Materials/ 로 추출해 모든 모델이 같은 .mat 을 공유한다.
/// 이미 있는 프리팹은 기본적으로 건너뛴다(사운드 등 수동 조정 보존). 덮어쓰려면 Rebuild 메뉴.
/// </summary>
public static class OpenablesPrefabBuilder
{
    public const string RootFolder = "Assets/Openables";
    public const string ModelsFolder = RootFolder + "/Models";
    public const string PrefabsFolder = RootFolder + "/Prefabs";
    public const string MaterialsFolder = RootFolder + "/Materials";

    [MenuItem("Tools/Openables/Build Prefabs (keep existing)")]
    static void BuildKeep() => Build(false);

    [MenuItem("Tools/Openables/Rebuild Prefabs (overwrite)")]
    static void BuildOverwrite()
    {
        if (EditorUtility.DisplayDialog("Openables", "Prefabs 폴더의 프리팹을 모두 다시 만듭니다.\n프리팹에 직접 바꾼 값(사운드 클립 등)은 사라집니다.", "다시 만들기", "취소"))
            Build(true);
    }

    /// <param name="names">비어 있으면 Models 의 모든 모델, 아니면 이 이름(확장자 제외)의 모델만.</param>
    public static string Build(bool overwrite, params string[] names)
    {
        EnsureFolder(PrefabsFolder);
        EnsureFolder(MaterialsFolder);

        var only = new HashSet<string>(names ?? new string[0]);
        var models = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (only.Count == 0 || only.Contains(Path.GetFileNameWithoutExtension(path))) models.Add(path);
        }

        foreach (var m in models) ExtractMaterials(m);

        var log = new System.Text.StringBuilder();
        foreach (var m in models) log.AppendLine(BuildOne(m, overwrite));
        AssetDatabase.SaveAssets();
        Debug.Log("[Openables] Build Prefabs\n" + log);
        return log.ToString();
    }

    // ───────── materials ─────────

    static void ExtractMaterials(string modelPath)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        bool changed = false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            if (!(o is Material m)) continue;
            string matPath = $"{MaterialsFolder}/{Sanitize(m.name)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing == null)
            {
                string err = AssetDatabase.ExtractAsset(m, matPath);
                if (!string.IsNullOrEmpty(err)) { Debug.LogWarning($"[Openables] {m.name}: {err}"); continue; }
                // ExtractAsset 이 remap 을 등록한다.
            }
            else
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), existing);
                changed = true;
            }
        }
        if (changed) importer.SaveAndReimport();
        else AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    // ───────── prefab ─────────

    static string BuildOne(string modelPath, bool overwrite)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        string name = model.name;
        string prefabPath = $"{PrefabsFolder}/{name}.prefab";
        if (!overwrite && File.Exists(prefabPath)) return $"skip   {name} (이미 있음)";

        var root = new GameObject(name);
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = name + "_model";
            inst.transform.SetParent(root.transform, false);
            // 축 변환을 굽지 않은 임포트는 앞면이 -Z 로 온다. 프리팹 루트 기준 +Z 가 앞(원본 HospitalMap 모델과 같은 방향)이 되도록 돌린다.
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var furniture = root.AddComponent<OpenableFurniture>();
            furniture.replacesModel = name;

            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1f;
            src.maxDistance = 15f;
            root.AddComponent<OpenableSound>();

            var parts = root.GetComponentsInChildren<OpenablePart>(true);
            foreach (var p in parts) p.Apply(0f); // 닫힌 자세로 저장

            if (name == "tall_cabinet") SetupHideSpot(root, parts);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
            return (ok ? "built  " : "FAILED ") + $"{name}  parts={parts.Length}";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>숨는 캐비넷: E 는 문 대신 HideSpot 으로, 문은 숨기/나오기 연출로만 움직인다.
    /// 포인트 기본값은 씬의 기존 tall_cabinet 4개에서 측정한 값 (루트 기준, Y 위, +Z 앞).</summary>
    static void SetupHideSpot(GameObject root, OpenablePart[] parts)
    {
        var hs = root.AddComponent<HideSpot>();
        hs.hidePoint = Point(root, "HidePoint", new Vector3(-0.265f, 0.37f, 0.55f));
        hs.viewPoint = Point(root, "HideViewPoint", new Vector3(-0.265f, 1.97f, 0.55f));
        hs.exitPoint = Point(root, "ExitPoint", new Vector3(0f, 0.02f, 1.5f));
        var doors = new List<OpenablePart>();
        foreach (var p in parts)
        {
            p.interactable = false;
            p.openSeconds = 0.4f;
            doors.Add(p);
        }
        hs.doors = doors.ToArray();
    }

    static Transform Point(GameObject root, string name, Vector3 local)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root.transform, false);
        t.localPosition = local;
        return t;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
