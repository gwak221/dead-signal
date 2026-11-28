using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IntegratedWeek7CabinetBuilder
{
    const string ScenePath = "Assets/Scenes/Integrated_Week7.unity";

    [MenuItem("Tools/Integrated Week 7/Create Hiding Cabinet")]
    public static void CreateHidingCabinet()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var existing = GameObject.Find("HidingCabinet");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var cabinet = new GameObject("HidingCabinet");
        cabinet.transform.position = new Vector3(4f, 0f, -20f);
        cabinet.transform.rotation = Quaternion.identity;

        var wood = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        wood.name = "Cabinet Dark Wood";
        wood.color = new Color(0.22f, 0.13f, 0.08f);

        AddPanel(cabinet.transform, "Left Side", new Vector3(-0.94f, 1.3f, 0f), new Vector3(0.12f, 2.6f, 1.8f), wood);
        AddPanel(cabinet.transform, "Right Side", new Vector3(0.94f, 1.3f, 0f), new Vector3(0.12f, 2.6f, 1.8f), wood);
        AddPanel(cabinet.transform, "Back", new Vector3(0f, 1.3f, -0.84f), new Vector3(1.76f, 2.6f, 0.12f), wood);
        AddPanel(cabinet.transform, "Top", new Vector3(0f, 2.6f, 0f), new Vector3(2f, 0.12f, 1.8f), wood);
        AddPanel(cabinet.transform, "Front Left Trim", new Vector3(-0.78f, 1.3f, 0.84f), new Vector3(0.18f, 2.6f, 0.12f), wood);
        AddPanel(cabinet.transform, "Front Right Trim", new Vector3(0.78f, 1.3f, 0.84f), new Vector3(0.18f, 2.6f, 0.12f), wood);

        // The open front lets the player walk straight into the trigger volume.
        var interior = new GameObject("Hide Volume");
        interior.transform.SetParent(cabinet.transform, false);
        interior.transform.localPosition = new Vector3(0f, 1.15f, 0.02f);
        var trigger = interior.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.55f, 2.2f, 1.35f);
        interior.AddComponent<HidingCabinet>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Hiding cabinet created in Integrated_Week7 at (4, 0, -20).");
    }

    static void AddPanel(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;
        panel.transform.SetParent(parent, false);
        panel.transform.localPosition = position;
        panel.transform.localScale = size;
        panel.GetComponent<Renderer>().sharedMaterial = material;
    }
}
