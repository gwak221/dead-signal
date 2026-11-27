using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴 Tools > Dead Signal > Build Document Prefabs
/// Assets/obj_document/models 의 FBX 마다 Prefabs/ 에 프리팹(모델 Variant)을 만든다.
///  - 모든 프리팹: 메시 크기에 맞는 BoxCollider
///  - 읽을 수 있는 서류(Clipboard, Envelope, Folder, Notebook, PaperPile): DocumentInspect 추가
///  - ArchiveBox, FacilityPlan, Recorder: 장식 (콜라이더만)
/// 이미 있는 프리팹은 건드리지 않는다 (팀원이 입력한 서류 내용을 덮어쓰지 않기 위해).
/// </summary>
public static class DocumentPrefabBuilder
{
    const string ModelDir = "Assets/obj_document/models";
    const string PrefabDir = "Assets/obj_document/Prefabs";
    static readonly string[] Readable = { "Clipboard", "Envelope", "Folder", "Notebook", "PaperPile" };

    [MenuItem("Tools/Dead Signal/Build Document Prefabs")]
    static void Build()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets/obj_document", "Prefabs");

        int made = 0, skipped = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            string outPath = $"{PrefabDir}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(outPath) != null) { skipped++; continue; }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                DocumentInspect.EnsureCollider(go);
                if (Readable.Any(k => name.Contains(k))) go.AddComponent<DocumentInspect>();
                PrefabUtility.SaveAsPrefabAsset(go, outPath);
                made++;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[DocumentPrefabBuilder] 생성 {made}개, 이미 있어서 건너뜀 {skipped}개 → {PrefabDir}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(PrefabDir);
    }
}
