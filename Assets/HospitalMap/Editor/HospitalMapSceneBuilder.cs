using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > HospitalMap > Build Scene
// HospitalMap.fbx 를 씬에 풀어서(Unpack) 넣고, Objects 아래 오브젝트를 Objects/<종류>.fbx 프리팹 인스턴스로 바꿔 끼운 뒤
// 같은 폴더에 HospitalMap.unity 로 저장. 이후 오브젝트는 씬에서 옮기기·지우기, Objects 폴더의 FBX 를 끌어다 추가하면 됨.
public static class HospitalMapSceneBuilder
{
    [MenuItem("Tools/HospitalMap/Build Scene")]
    public static void Build()
    {
        string mapPath = null;
        foreach (var guid in AssetDatabase.FindAssets("HospitalMap t:Model"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith("/HospitalMap/HospitalMap.fbx")) { mapPath = p; break; }
        }
        if (mapPath == null) { Debug.LogError("HospitalMap/HospitalMap.fbx 를 찾지 못했습니다."); return; }
        string dir = Path.GetDirectoryName(mapPath).Replace('\\', '/');

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var mapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(mapPath);
        var map = (GameObject)PrefabUtility.InstantiatePrefab(mapAsset);
        PrefabUtility.UnpackPrefabInstance(map, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        map.name = "HospitalMap";

        int swapped = 0, missing = 0;
        var objects = map.transform.Find("Objects");
        if (objects != null)
        {
            foreach (Transform group in objects)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/Objects/" + group.name + ".fbx");
                if (prefab == null) { missing++; continue; }
                var items = new List<Transform>();
                foreach (Transform c in group) items.Add(c);
                foreach (var c in items)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    inst.name = c.name;
                    inst.transform.localPosition = c.localPosition;
                    inst.transform.localRotation = c.localRotation;
                    inst.transform.localScale = c.localScale;
                    inst.transform.SetSiblingIndex(c.GetSiblingIndex());
                    Object.DestroyImmediate(c.gameObject);
                    swapped++;
                }
            }
        }
        string scenePath = dir + "/HospitalMap.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"HospitalMap 씬 저장: {scenePath} (오브젝트 {swapped}개를 프리팹으로 연결, 단독 FBX 없는 종류 {missing})");
    }
}
