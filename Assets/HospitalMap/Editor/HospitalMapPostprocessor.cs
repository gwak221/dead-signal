using UnityEditor;
using UnityEngine;

// HospitalMap 폴더에 들어온 FBX·텍스처 임포트 설정
public class HospitalMapPostprocessor : AssetPostprocessor
{
    bool Ours => assetPath.Replace('\\', '/').Contains("/HospitalMap/");

    void OnPreprocessTexture()
    {
        if (!Ours) return;
        var ti = (TextureImporter)assetImporter;
        ti.filterMode = FilterMode.Point;          // PS1풍 저해상도 텍스처
        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }

    void OnPreprocessModel()
    {
        if (!Ours) return;
        var mi = (ModelImporter)assetImporter;
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.importLights = true;
        mi.importCameras = false;
        mi.isReadable = false;
        mi.bakeAxisConversion = true;          // Blender Z-up -> Unity Y-up 을 계층 전체에 구움 (오브젝트에 -90° 회전이 안 남음)
    }

    // Blender 커스텀 속성 door_open_angle 이 붙은 문짝에 HingedDoor 추가
    void OnPostprocessGameObjectWithUserProperties(GameObject go, string[] names, object[] values)
    {
        if (!Ours) return;
        foreach (var n in names)
        {
            if (n != "door_open_angle") continue;
            var door = go.GetComponent<HingedDoor>();
            if (door == null) door = go.AddComponent<HingedDoor>();
            door.Init(go.transform.localRotation);
            if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
        }
    }

    // 벽·바닥·천장 구조물에 충돌체 (FloorCeiling/Floor, FloorCeiling/Ceiling, Rooms/*/*_Walls)
    void OnPostprocessModel(GameObject root)
    {
        if (!Ours) return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            // 맵 FBX 최상위의 Floor / Ceiling / Walls 그룹만 (오브젝트 단독 FBX 에는 없음)
            bool structural = (t.name == "Floor" || t.name == "Ceiling" || t.name == "Walls")
                              && t.parent != null && t.parent == root.transform;
            if (!structural) continue;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                if (mf.GetComponent<HingedDoor>() == null && mf.GetComponent<MeshCollider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
    }
}
