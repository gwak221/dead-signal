using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Openables/Models 폴더의 FBX 임포트 설정 + 여닫이 파츠 자동 구성.
///
/// Blender 규칙 (Source~/*.blend, 빌드 스크립트 Source~/build_openables.py):
///   - 움직이는 파츠 오브젝트: 커스텀 속성 open_type = "hinge" | "slide",
///     선택 open_group(같은 값끼리 같이 여닫힘), open_layer("top" 먼저 열리고 나중에 닫힘 / "side" 반대)
///   - 열린 자세: 같은 부모 아래 "&lt;파츠이름&gt;__open" 빈 오브젝트 (위치·회전이 열린 자세)
/// 임포트되면 파츠에 OpenablePart + BoxCollider, 본체 메쉬에 MeshCollider 를 붙이고 __open 빈 오브젝트는 지운다.
/// 축 변환을 추측하지 않도록 열린 자세는 각도 값이 아니라 빈 오브젝트의 실제 변환에서 가져온다.
/// </summary>
public class OpenablesModelPostprocessor : AssetPostprocessor
{
    public const string ModelsFolder = "Assets/Openables/Models/";
    const string OpenSuffix = "__open";

    readonly Dictionary<string, Dictionary<string, object>> props = new Dictionary<string, Dictionary<string, object>>();

    bool Ours => assetPath.Replace('\\', '/').StartsWith(ModelsFolder);

    void OnPreprocessModel()
    {
        if (!Ours) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.importLights = false;
        mi.importCameras = false;
        mi.isReadable = false;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        // 축 변환을 계층에 굽지 않는다: 굽으면 자식 회전이 메쉬로 들어가 경첩 피벗 회전이 사라진다.
        mi.bakeAxisConversion = false;
        mi.preserveHierarchy = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }

    void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] names, object[] values)
    {
        if (!Ours) return;
        var d = new Dictionary<string, object>();
        for (int i = 0; i < names.Length; i++) d[names[i]] = values[i];
        props[go.name] = d;
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!Ours) return;

        var all = root.GetComponentsInChildren<Transform>(true);
        var parts = new List<OpenablePart>();
        var groups = new Dictionary<string, List<OpenablePart>>();

        foreach (var t in all)
        {
            if (!props.TryGetValue(t.name, out var p) || !p.ContainsKey("open_type")) continue;

            var openT = t.parent != null ? t.parent.Find(t.name + OpenSuffix) : null;
            if (openT == null)
            {
                Debug.LogWarning($"[Openables] {assetPath}: '{t.name}' 에 '{t.name}{OpenSuffix}' 빈 오브젝트가 없어 건너뜀");
                continue;
            }

            var part = t.gameObject.AddComponent<OpenablePart>();
            string type = (p["open_type"] as string ?? "hinge").ToLowerInvariant();
            part.kind = type == "slide" ? OpenableKind.Slide : OpenableKind.Hinge;
            part.closedLocalPosition = t.localPosition;
            part.closedLocalRotation = t.localRotation;
            part.openLocalPosition = openT.localPosition;
            part.openLocalRotation = openT.localRotation;

            if (part.kind == OpenableKind.Slide)
            {
                part.openSeconds = 0.35f;
                part.openText = "E : Open Drawer";
                part.closeText = "E : Close Drawer";
            }
            else
            {
                part.openSeconds = 0.5f;
            }

            string layer = p.TryGetValue("open_layer", out var l) ? l as string : null;
            if (layer == "top") { part.openSeconds = 0.35f; part.closeDelay = 0.25f; }
            else if (layer == "side") { part.openSeconds = 0.35f; part.openDelay = 0.2f; }

            if (t.GetComponent<Collider>() == null)
            {
                var mf = t.GetComponent<MeshFilter>();
                var bc = t.gameObject.AddComponent<BoxCollider>();
                if (mf != null && mf.sharedMesh != null)
                {
                    // 서랍은 앞판만 잡히도록 하지 않고 전체 박스 — 닫힌 상태에선 본체 안에 숨어 있어 문제없다.
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = Vector3.Max(mf.sharedMesh.bounds.size, Vector3.one * 0.01f);
                }
            }

            parts.Add(part);
            if (p.TryGetValue("open_group", out var g) && g is string gs && gs.Length > 0)
            {
                if (!groups.TryGetValue(gs, out var list)) groups[gs] = list = new List<OpenablePart>();
                list.Add(part);
            }
        }

        foreach (var list in groups.Values)
            foreach (var part in list)
                part.linked = list.FindAll(x => x != part).ToArray();

        // __open 빈 오브젝트 제거
        foreach (var t in all)
            if (t != null && t.name.EndsWith(OpenSuffix))
                Object.DestroyImmediate(t.gameObject);

        // 본체(움직이지 않는 메쉬)에 MeshCollider — 속이 빈 메쉬라 서랍/문 칸을 막지 않는다.
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.GetComponentInParent<OpenablePart>() != null) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        if (parts.Count == 0)
            Debug.LogWarning($"[Openables] {assetPath}: open_type 파츠가 없습니다.");
    }
}
