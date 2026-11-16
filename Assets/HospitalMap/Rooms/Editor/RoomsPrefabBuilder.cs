using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools/Dead Signal/Rooms/Build Prefabs :
///   Rooms/&lt;공간&gt;/Models/&lt;name&gt;.fbx → Rooms/&lt;공간&gt;/Prefabs/&lt;name&gt;.prefab
/// 모델은 Rooms/Source~/build_rooms.py (Blender) 로 다시 만들 수 있다.
///
/// 프리팹 구조:  &lt;name&gt; (루트, +Z 앞)
///                 &lt;name&gt;_model  ← FBX 를 풀어서 넣은 계층
/// - 머티리얼은 Rooms/Common/Materials 로 추출해 모든 모델이 같은 .mat 을 공유한다.
/// - 콜라이더: 본체 MeshCollider, _leaf 는 BoxCollider, _debris / _glass / _cloth 는 없음.
/// - _leaf 에 HingedDoor 가 붙은 모델(ward_door)은 루트에 DoorPair 를 붙인다.
/// 프리팹은 항상 다시 만든다 (같은 경로에 저장하므로 GUID 유지 → 씬에 놓인 인스턴스는 그대로 따라온다).
/// </summary>
public static class RoomsPrefabBuilder
{
    public const string RoomsFolder = "Assets/HospitalMap/Rooms";
    public const string MaterialsFolder = RoomsFolder + "/Common/Materials";

    static readonly string[] NoCollider = { "_debris", "_glass", "_cloth" };

    [MenuItem("Tools/Dead Signal/Rooms/Build Prefabs")]
    public static void BuildMenu() => Build();

    /// <param name="only">비어 있으면 전부, 아니면 이 이름(확장자 제외)의 모델만.</param>
    public static string Build(params string[] only)
    {
        var filter = new HashSet<string>(only ?? new string[0]);
        if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder(RoomsFolder + "/Common", "Materials");

        var models = AssetDatabase.FindAssets("t:Model", new[] { RoomsFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileName(Path.GetDirectoryName(p)) == "Models")
            .Where(p => filter.Count == 0 || filter.Contains(Path.GetFileNameWithoutExtension(p)))
            .OrderBy(p => p).ToList();

        foreach (var m in models) ExtractMaterials(m);

        var log = new System.Text.StringBuilder();
        foreach (var m in models) log.AppendLine(BuildOne(m));
        AssetDatabase.SaveAssets();
        Debug.Log($"[Rooms] Build Prefabs ({models.Count})\n" + log);
        return log.ToString();
    }

    static void ExtractMaterials(string modelPath)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        bool changed = false;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            if (!(o is Material m)) continue;
            string matPath = $"{MaterialsFolder}/{m.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing == null)
            {
                string err = AssetDatabase.ExtractAsset(m, matPath);
                if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[Rooms] {m.name}: {err}");
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

    /// <summary>문이 열리는 각도 (build_rooms.py 의 door_open_angle 과 같게).</summary>
    const float DoorOpenAngle = 86f;

    /// <summary>
    /// Unity 는 FBX 오브젝트 회전을 버리고 HingedDoor 는 열린 회전을 identity 로 받는다.
    /// 경첩(문짝 피벗)을 지나는 세로축으로 돌렸을 때 문짝이 루트의 +Z(앞) 쪽으로 가는 방향을 골라 넣는다.
    /// </summary>
    static void SetupOpenRotation(Transform root, HingedDoor leaf, float angle)
    {
        var t = leaf.transform;
        var closed = t.localRotation;
        Vector3 Center()
        {
            var r = t.GetComponent<Renderer>();
            return root.InverseTransformPoint(r != null ? r.bounds.center : t.position);
        }
        Quaternion best = closed; float bestZ = float.MinValue;
        foreach (var sign in new[] { 1f, -1f })
        {
            var worldRot = Quaternion.AngleAxis(sign * angle, root.up) * t.rotation;
            var local = Quaternion.Inverse(t.parent.rotation) * worldRot;
            t.localRotation = local;
            float z = Center().z;
            if (z > bestZ) { bestZ = z; best = local; }
        }
        t.localRotation = closed;
        leaf.closedLocalRotation = closed;
        leaf.openLocalRotation = best;
    }

    static string BuildOne(string modelPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        string name = model.name;
        string roomDir = Path.GetDirectoryName(Path.GetDirectoryName(modelPath)).Replace('\\', '/');
        string prefabDir = roomDir + "/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabDir)) AssetDatabase.CreateFolder(roomDir, "Prefabs");

        var root = new GameObject(name);
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = name + "_model";
            inst.transform.SetParent(root.transform, false);

            HingedDoor leaf = null;
            int colliders = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                bool isLeaf = go.name.EndsWith("_leaf");
                if (isLeaf)
                {
                    leaf = go.GetComponent<HingedDoor>();
                    if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
                    colliders++;
                    continue;
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
                if (NoCollider.Any(s => go.name.EndsWith(s))) continue;
                if (go.GetComponent<Collider>() == null) go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                colliders++;
            }

            string extra = "";
            if (leaf != null)
            {
                SetupOpenRotation(root.transform, leaf, DoorOpenAngle);
                leaf.SetOpen(false);   // 닫힌 자세로 저장
                var pair = root.AddComponent<DoorPair>();
                pair.leafL = leaf;
                // isPassableDoor 는 기본값(true) 그대로 둔다. 이 필드가 없는 예전 DoorPair 에서도 컴파일되도록 직접 쓰지 않는다.
                extra = " +DoorPair";
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{prefabDir}/{name}.prefab", out bool ok);
            return (ok ? "built  " : "FAILED ") + $"{name}  colliders={colliders}{extra}";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
