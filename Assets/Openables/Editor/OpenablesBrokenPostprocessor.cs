using UnityEditor;
using UnityEngine;

/// <summary>
/// Openables/Broken/Models 의 파손 모델(정적) 임포트 설정.
/// 원본 FBX 를 Blender(Broken/Source~/build_broken.py)로 부숴서 내보낸 것이라 원본과 같은 임포트 설정을 쓴다.
/// 여닫이 파츠 처리는 하지 않고, 메쉬마다 MeshCollider 를 붙인다.
/// 이름이 _debris / _glass 로 끝나는 오브젝트(바닥 파편·유리 조각)는 콜라이더 없이 둔다.
/// </summary>
public class OpenablesBrokenPostprocessor : AssetPostprocessor
{
    public const string BrokenModelsFolder = "Assets/Openables/Broken/Models/";

    bool Ours => assetPath.Replace('\\', '/').StartsWith(BrokenModelsFolder);

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
        mi.bakeAxisConversion = false;
        mi.preserveHierarchy = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
    }

    void OnPostprocessModel(GameObject root)
    {
        if (!Ours) return;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            string n = mf.gameObject.name;
            if (n.EndsWith("_debris") || n.EndsWith("_glass")) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
    }
}
