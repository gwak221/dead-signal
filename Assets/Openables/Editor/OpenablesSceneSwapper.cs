using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 열린 씬의 원본 가구 인스턴스(예: HospitalMap/Objects/filing.fbx)를 Openables 프리팹으로 한꺼번에 교체.
///   Tools/Openables/Report Replaceable In Open Scenes : 교체 대상 개수만 보고 (아무것도 바꾸지 않음)
///   Tools/Openables/Replace In Open Scenes...          : 실제 교체 (Undo 한 번으로 되돌리기 가능, 씬 저장은 직접)
/// 대응 관계: Prefabs 폴더 프리팹의 OpenableFurniture.replacesModel == 원본 에셋 파일 이름.
/// 유지되는 것: 부모, 형제 순서, 이름, 위치/회전/스케일(월드 자세 동일), 활성 상태, 레이어, Static 플래그.
/// 원본에 HideSpot 이 있으면 HidePoint/HideViewPoint/ExitPoint 의 월드 위치를 새 프리팹으로 옮긴다.
/// 다른 오브젝트가 원본을 참조하고 있으면 교체 전에 경고한다(참조는 끊어진다).
/// </summary>
public static class OpenablesSceneSwapper
{
    class Candidate
    {
        public GameObject instance;
        public GameObject sourceAsset;
        public GameObject replacement;
        public Scene scene;
    }

    [MenuItem("Tools/Openables/Report Replaceable In Open Scenes")]
    static void Report()
    {
        var c = FindCandidates();
        Debug.Log("[Openables] 교체 가능 인스턴스\n" + Summary(c) + ReferenceWarnings(c));
    }

    [MenuItem("Tools/Openables/Replace In Open Scenes...")]
    static void ReplaceMenu()
    {
        var c = FindCandidates();
        if (c.Count == 0) { EditorUtility.DisplayDialog("Openables", "교체할 인스턴스가 없습니다.", "확인"); return; }
        string refs = ReferenceWarnings(c);
        string msg = Summary(c) + (refs.Length > 0 ? "\n⚠ 아래 참조가 끊어집니다 (Console 참고):\n" + refs : "") +
                     "\n교체 후 씬은 저장되지 않습니다. 결과가 마음에 들지 않으면 Ctrl+Z.";
        if (refs.Length > 0) Debug.LogWarning("[Openables] 교체 시 끊어질 참조\n" + refs);
        if (!EditorUtility.DisplayDialog("Openables: 씬 교체", msg, "교체", "취소")) return;
        int n = Replace(c);
        Debug.Log($"[Openables] {n}개 교체 완료. 씬을 확인한 뒤 저장하세요.");
    }

    // ───────── public API (다른 에디터 스크립트/자동화용, 확인 창 없음) ─────────

    /// <summary>교체 대상 요약 + 끊어질 참조 목록. 아무것도 바꾸지 않는다.</summary>
    public static string ReportInOpenScenes()
    {
        var c = FindCandidates();
        return Summary(c) + ReferenceWarnings(c);
    }

    /// <summary>확인 창 없이 열린 씬의 대상 전부 교체. 교체 개수 반환. Undo 한 번으로 되돌릴 수 있고 저장은 하지 않는다.</summary>
    public static int ReplaceAllInOpenScenes() => Replace(FindCandidates());

    /// <summary>지정한 씬 하나만 교체 (여러 씬이 열려 있을 때).</summary>
    public static int ReplaceAllInScene(Scene scene) => Replace(FindCandidates(scene));

    // ───────── find ─────────

    static Dictionary<string, GameObject> ReplacementMap()
    {
        var map = new Dictionary<string, GameObject>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { OpenablesPrefabBuilder.PrefabsFolder }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var f = go != null ? go.GetComponent<OpenableFurniture>() : null;
            if (f != null && !string.IsNullOrEmpty(f.replacesModel)) map[f.replacesModel] = go;
        }
        return map;
    }

    static List<Candidate> FindCandidates(Scene? only = null)
    {
        var map = ReplacementMap();
        var list = new List<Candidate>();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            if (only.HasValue && scene != only.Value) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                    var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                    if (src == null) continue;
                    string path = AssetDatabase.GetAssetPath(src).Replace('\\', '/');
                    if (path.StartsWith(OpenablesPrefabBuilder.RootFolder + "/")) continue; // 이미 Openables
                    if (!map.TryGetValue(Path.GetFileNameWithoutExtension(path), out var rep)) continue;
                    list.Add(new Candidate { instance = go, sourceAsset = src, replacement = rep, scene = scene });
                }
        }
        return list;
    }

    static string Summary(List<Candidate> c)
    {
        var counts = new SortedDictionary<string, int>();
        foreach (var x in c)
        {
            string k = $"{AssetDatabase.GetAssetPath(x.sourceAsset)} → {x.replacement.name}.prefab";
            counts.TryGetValue(k, out int n); counts[k] = n + 1;
        }
        var sb = new StringBuilder();
        foreach (var kv in counts) sb.AppendLine($"  {kv.Value,3} × {kv.Key}");
        sb.AppendLine($"  합계 {c.Count}개");
        return sb.ToString();
    }

    /// <summary>교체 대상(및 자식) 바깥에서 그것들을 참조하는 직렬화 필드 목록.</summary>
    static string ReferenceWarnings(List<Candidate> c)
    {
        var targets = new HashSet<Object>();
        var targetTransforms = new HashSet<Transform>();
        foreach (var x in c)
            foreach (var t in x.instance.GetComponentsInChildren<Transform>(true))
            {
                targetTransforms.Add(t);
                targets.Add(t.gameObject);
                foreach (var comp in t.GetComponents<Component>()) if (comp != null) targets.Add(comp);
            }

        var sb = new StringBuilder();
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null || comp is Transform || targetTransforms.Contains(comp.transform)) continue;
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var r = it.objectReferenceValue;
                        if (r != null && targets.Contains(r))
                            sb.AppendLine($"  {GetPath(comp.transform)} ({comp.GetType().Name}.{it.propertyPath}) → {r.name}");
                    }
                }
        }
        return sb.ToString();
    }

    static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;

    // ───────── replace ─────────

    static int Replace(List<Candidate> list)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace with Openables");
        int done = 0;

        foreach (var c in list)
        {
            var old = c.instance;
            if (old == null) continue;
            var ot = old.transform;

            var furniture = c.replacement.GetComponent<OpenableFurniture>();
            Quaternion srcBase = c.sourceAsset != null
                ? c.sourceAsset.transform.localRotation
                : Quaternion.Euler(furniture.sourceRootEuler);
            Vector3 srcScale = c.sourceAsset != null ? c.sourceAsset.transform.localScale : Vector3.one;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(c.replacement, old.scene);
            Undo.RegisterCreatedObjectUndo(go, "Replace with Openables");
            var nt = go.transform;
            nt.SetParent(ot.parent, false);
            nt.SetSiblingIndex(ot.GetSiblingIndex());
            nt.SetPositionAndRotation(ot.position, ot.rotation * Quaternion.Inverse(srcBase));
            nt.localScale = new Vector3(
                ot.localScale.x / NonZero(srcScale.x), ot.localScale.y / NonZero(srcScale.y), ot.localScale.z / NonZero(srcScale.z));
            go.name = old.name;
            go.layer = old.layer;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(old));
            go.SetActive(old.activeSelf);

            CopyHidePoints(old, go);

            Undo.DestroyObjectImmediate(old);
            done++;
        }

        Undo.CollapseUndoOperations(group);
        if (!EditorApplication.isPlaying)
        {
            var touched = new HashSet<Scene>();
            foreach (var c in list) if (c.scene.IsValid()) touched.Add(c.scene);
            foreach (var s in touched) EditorSceneManager.MarkSceneDirty(s);
        }
        return done;
    }

    static float NonZero(float v) => Mathf.Abs(v) < 1e-6f ? 1f : v;

    static void CopyHidePoints(GameObject from, GameObject to)
    {
        var a = from.GetComponent<HideSpot>();
        var b = to.GetComponent<HideSpot>();
        if (a == null || b == null) return;
        CopyPose(a.hidePoint, b.hidePoint);
        CopyPose(a.viewPoint, b.viewPoint);
        CopyPose(a.exitPoint, b.exitPoint);
    }

    static void CopyPose(Transform src, Transform dst)
    {
        if (src == null || dst == null) return;
        Undo.RecordObject(dst, "Replace with Openables");
        dst.position = src.position;
    }
}
