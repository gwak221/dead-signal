using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EscapeSceneBuilder
{
    const string ScenePath = "Assets/Scenes/EscapePrototype.unity";

    [MenuItem("Dead Signal/Build Escape Prototype Scene")]
    public static void BuildScene()
    {
        EnsureFolder("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(4f, 1f, 4f);
        floor.isStatic = true;

        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1f, -12f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.radius = 0.35f;
        player.AddComponent<FirstPersonController>();

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        camGo.transform.localRotation = Quaternion.identity;
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        camGo.AddComponent<AudioListener>();

        var fpc = player.GetComponent<FirstPersonController>();
        SerializedObject soFpc = new SerializedObject(fpc);
        soFpc.FindProperty("cameraRoot").objectReferenceValue = camGo.transform;
        soFpc.ApplyModifiedPropertiesWithoutUndo();

        var interact = player.AddComponent<PlayerInteraction>();
        SerializedObject soPi = new SerializedObject(interact);
        soPi.FindProperty("viewCamera").objectReferenceValue = cam;
        soPi.ApplyModifiedPropertiesWithoutUndo();

        var exitFrame = new GameObject("EscapeExit");
        exitFrame.transform.position = new Vector3(0f, 3f, 15f);

        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "ExitDoor";
        door.transform.SetParent(exitFrame.transform);
        door.transform.localPosition = Vector3.zero;
        door.transform.localScale = new Vector3(4f, 0.4f, 0.3f);
        var doorEsc = door.AddComponent<EscapeDoor>();

        var exitSign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exitSign.name = "ExitFrame";
        exitSign.transform.SetParent(exitFrame.transform);
        exitSign.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        exitSign.transform.localScale = new Vector3(4.5f, 0.2f, 0.5f);
        Object.DestroyImmediate(exitSign.GetComponent<BoxCollider>());

        var devicePositions = new[]
        {
            new Vector3(-14f, 0.5f, -14f),
            new Vector3(14f, 0.5f, -14f),
            new Vector3(-14f, 0.5f, 14f),
            new Vector3(14f, 0.5f, 14f),
        };

        var devices = new HoldInteractable[4];
        for (int i = 0; i < 4; i++)
        {
            var devRoot = new GameObject($"Device_{i + 1}");
            devRoot.transform.position = devicePositions[i];

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(devRoot.transform);
            pedestal.transform.localPosition = Vector3.zero;
            pedestal.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);

            var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = "Indicator";
            lamp.transform.SetParent(devRoot.transform);
            lamp.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            lamp.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            var hold = devRoot.AddComponent<HoldInteractable>();
            SerializedObject soHold = new SerializedObject(hold);
            soHold.FindProperty("displayName").stringValue = $"탈출 장치 {i + 1}";
            soHold.FindProperty("holdDuration").floatValue = 3f;
            soHold.FindProperty("indicatorRenderer").objectReferenceValue = lamp.GetComponent<Renderer>();
            soHold.ApplyModifiedPropertiesWithoutUndo();

            devices[i] = hold;
        }

        var systemGo = new GameObject("EscapeSystem");
        var system = systemGo.AddComponent<EscapeSystem>();
        SerializedObject soSys = new SerializedObject(system);
        soSys.FindProperty("devices").arraySize = 4;
        for (int i = 0; i < 4; i++)
            soSys.FindProperty("devices").GetArrayElementAtIndex(i).objectReferenceValue = devices[i];
        soSys.FindProperty("escapeDoor").objectReferenceValue = doorEsc;
        soSys.FindProperty("requiredCount").intValue = 4;
        soSys.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"Escape prototype scene saved: {ScenePath}");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
