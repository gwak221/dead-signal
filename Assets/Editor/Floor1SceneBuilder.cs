using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Floor1SceneBuilder
{
    const string ScenePath = "Assets/Scenes/Floor1.unity";

    [MenuItem("Dead Signal/Build Floor 1 Map Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var existing = Object.FindFirstObjectByType<Floor1Layout>();
        if (existing != null)
        {
            existing.Build();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Floor 1 walls rebuilt: " + ScenePath);
            return;
        }

        var map = new GameObject("Floor1Map");
        var layout = map.AddComponent<Floor1Layout>();
        layout.Build();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Floor 1 map added: " + ScenePath);
    }
}
