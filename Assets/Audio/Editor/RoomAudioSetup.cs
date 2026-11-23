using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 방마다 다른 소리 세팅. 메뉴: Tools > Dead Signal > Audio > Rooms > ...
///   1. Setup Room Audio : 프로필/방 전용 Cue 생성, AudioSystem 프리팹에 RoomAudioController 추가,
///                         AudioSystem 프리팹 안 RoomAudioZones 아래에 방 박스들 생성. 여러 번 실행해도 안전 (있는 건 건드리지 않음)
///   2. Add Zone Here    : Scene 뷰 중심 위치에 Zone 하나 추가 + 벽에 맞춤 → 바로 프리팹에 저장
///   3. Move Scene Zones Into Prefab : 예전 방식(씬 루트의 RoomAudioZones)으로 만든 Zone을 프리팹으로 옮긴다
/// Zone 인스펙터: "벽에 맞추기", "프리팹에 저장" 버튼, Scene 뷰에서 박스 핸들로 크기 조절.
///
/// Zone은 씬이 아니라 AudioSystem 프리팹에 들어 있다. 씬 파일(바이너리, 맵 작업과 충돌)을 커밋하지 않아도 되도록.
/// 씬에서 Zone을 고쳤으면 "프리팹에 저장"을 누르고, 씬 파일 변경은 커밋하지 않는다.
/// (AudioSystem 인스턴스는 씬에서 위치 (0,0,0), 회전 0, 크기 1이어야 Zone이 맵과 맞는다)
/// </summary>
public static class RoomAudioSetup
{
    const string ProfilesFolder = "Assets/Audio/RoomProfiles";
    const string CuesFolder = "Assets/Audio/Cues/Rooms";
    const string PrefabPath = "Assets/Audio/Prefabs/AudioSystem.prefab";
    const string ZonesRootName = "RoomAudioZones";
    const float ZoneHeight = 3.4f;

    struct ProfileDef
    {
        public string name; public RoomReverbPreset preset; public float offset, drone; public string ambience, note;
    }

    static readonly ProfileDef[] Profiles =
    {
        new ProfileDef { name = "Hallway",         preset = RoomReverbPreset.Hallway,       offset = -300, drone = 1.0f, note = "복도 (어떤 방에도 속하지 않을 때). 길고 얇게 울림" },
        new ProfileDef { name = "Room",            preset = RoomReverbPreset.Room,          offset = 0,    drone = 0.9f, note = "평범한 작은 방" },
        new ProfileDef { name = "LargeRoom",       preset = RoomReverbPreset.Generic,       offset = -400, drone = 0.9f, note = "큰 방" },
        new ProfileDef { name = "Ward",            preset = RoomReverbPreset.Room,          offset = -200, drone = 0.85f, note = "병실. 커튼/침대가 소리를 먹어서 짧고 건조하게" },
        new ProfileDef { name = "Consultation",    preset = RoomReverbPreset.LivingRoom,    offset = 0,    drone = 0.8f, note = "진료실. 작고 먹먹하게" },
        new ProfileDef { name = "PhysicalTherapy", preset = RoomReverbPreset.LivingRoom,    offset = 400,  drone = 0.85f, note = "물리치료실. 매트가 많아 부드럽게" },
        new ProfileDef { name = "Lobby",           preset = RoomReverbPreset.Auditorium,    offset = -700, drone = 1.0f, note = "접수/대기실. 넓고 길게 울림" },
        new ProfileDef { name = "Toilet",          preset = RoomReverbPreset.Bathroom,      offset = -200, drone = 0.9f, note = "화장실. 타일 때문에 밝고 크게 울림" },
        new ProfileDef { name = "PharmacyStorage", preset = RoomReverbPreset.StoneRoom,     offset = -300, drone = 0.6f, ambience = "room_cold_hum", note = "약제/냉장 창고. 선반 사이로 울림 + 냉장 웅웅" },
        new ProfileDef { name = "OperatingRoom",   preset = RoomReverbPreset.Room,          offset = 200,  drone = 0.7f, ambience = "room_or_hum", note = "수술실. 타일 + 장비 웅웅" },
        new ProfileDef { name = "WasteStorage",    preset = RoomReverbPreset.Cave,          offset = -200, drone = 0.5f, ambience = "room_waste_drone", note = "폐기물 창고. 콘크리트, 낮게 깔리는 소리" },
    };

    struct CueDef { public string name, clip, note; public float vol, pitch; }

    static readonly CueDef[] Cues =
    {
        new CueDef { name = "room_cold_hum",    clip = "Assets/Audio/Clips/Ambience/amb_fluorescent_01.wav", vol = 0.35f, pitch = 0.55f, note = "냉장 창고 웅웅 (형광등 소리를 낮게 늘림). 전용 클립이 생기면 바꿔 주세요" },
        new CueDef { name = "room_or_hum",      clip = "Assets/Audio/Clips/Ambience/amb_fluorescent_01.wav", vol = 0.2f,  pitch = 1.25f, note = "수술실 장비 웅웅 (임시). 전용 클립이 생기면 바꿔 주세요" },
        new CueDef { name = "room_waste_drone", clip = "Assets/Audio/Clips/Ambience/amb_drone_01.wav",       vol = 0.4f,  pitch = 0.75f, note = "폐기물 창고 저음 (드론을 낮춤, 임시)" },
    };

    // 방 박스 (월드 X 범위, Z 범위). HospitalMap 벽을 레이캐스트해서 잡은 값. 다르면 Scene 뷰에서 고치면 된다.
    struct ZoneDef { public string name, profile; public float x0, x1, z0, z1; }

    static readonly ZoneDef[] Zones =
    {
        new ZoneDef { name = "Zone_StartRoom",       profile = "Room",            x0 = -4.6f, x1 = 12.4f, z0 = -15.2f, z1 = 7.8f },
        new ZoneDef { name = "Zone_Ward_North",      profile = "Ward",            x0 = 7.3f,  x1 = 26.7f, z0 = 37.2f,  z1 = 58.0f },
        new ZoneDef { name = "Zone_Ward_Middle",     profile = "Ward",            x0 = 8.5f,  x1 = 27.3f, z0 = 20.0f,  z1 = 32.8f },
        new ZoneDef { name = "Zone_Consultation",    profile = "Consultation",    x0 = 16.5f, x1 = 29.4f, z0 = 3.3f,   z1 = 15.2f },
        new ZoneDef { name = "Zone_PhysicalTherapy", profile = "PhysicalTherapy", x0 = 36.4f, x1 = 55.2f, z0 = 5.0f,   z1 = 15.1f },
        new ZoneDef { name = "Zone_Lobby",           profile = "Lobby",           x0 = 35.1f, x1 = 51.2f, z0 = 39.4f,  z1 = 53.8f },
        new ZoneDef { name = "Zone_Room03",          profile = "Room",            x0 = 53.4f, x1 = 66.7f, z0 = 39.4f,  z1 = 49.2f },
        new ZoneDef { name = "Zone_Room04",          profile = "LargeRoom",       x0 = 68.4f, x1 = 98.0f, z0 = 36.1f,  z1 = 48.7f },
        new ZoneDef { name = "Zone_Room07",          profile = "LargeRoom",       x0 = 42.8f, x1 = 64.6f, z0 = 19.8f,  z1 = 32.3f },
        new ZoneDef { name = "Zone_PharmacyStorage", profile = "PharmacyStorage", x0 = 76.0f, x1 = 91.4f, z0 = 24.0f,  z1 = 31.4f },
        new ZoneDef { name = "Zone_Room09",          profile = "Room",            x0 = 76.6f, x1 = 86.0f, z0 = 2.7f,   z1 = 17.5f },
        new ZoneDef { name = "Zone_Toilet",          profile = "Toilet",          x0 = 86.2f, x1 = 96.6f, z0 = 2.7f,   z1 = 17.5f },
        new ZoneDef { name = "Zone_Room12",          profile = "Room",            x0 = 101.9f, x1 = 112.6f, z0 = 3.5f, z1 = 9.7f },
        new ZoneDef { name = "Zone_OperatingRoom",   profile = "OperatingRoom",   x0 = 76.6f, x1 = 91.0f, z0 = -17.6f, z1 = -4.2f },
        new ZoneDef { name = "Zone_WasteStorage",    profile = "WasteStorage",    x0 = 100.6f, x1 = 118.4f, z0 = 38.0f, z1 = 56.9f },
    };

    [MenuItem("Tools/Dead Signal/Audio/Rooms/1. Setup Room Audio (Profiles, Prefab, Zones)")]
    public static void SetupAll()
    {
        EnsureFolder("Assets/Audio", "RoomProfiles");
        EnsureFolder("Assets/Audio/Cues", "Rooms");
        int cues = EnsureCues();
        int profiles = EnsureProfiles();
        AssetDatabase.SaveAssets();
        bool prefab = EnsureController();
        int zones = EnsureZones();
        Debug.Log($"[RoomAudio] Cue {cues}개, 프로필 {profiles}개 생성 / 프리팹 컨트롤러 {(prefab ? "추가" : "이미 있음")} / Zone {zones}개 생성");
    }

    [MenuItem("Tools/Dead Signal/Audio/Rooms/2. Add Zone Here (Scene view center)")]
    public static void AddZoneHere()
    {
        var root = SceneZonesRoot();
        if (root == null)
        {
            Debug.LogWarning("[RoomAudio] 열린 씬에 AudioSystem 프리팹 인스턴스가 없습니다. 먼저 AudioSystem을 씬에 두세요.");
            return;
        }
        var sv = SceneView.lastActiveSceneView;
        Vector3 p = sv != null ? sv.pivot : Vector3.zero;
        p.y = ZoneHeight * 0.5f - 0.1f;
        var go = new GameObject("Zone_New");
        Undo.RegisterCreatedObjectUndo(go, "Add Room Audio Zone");
        go.transform.SetParent(root, false);
        go.transform.position = p;
        var z = go.AddComponent<RoomAudioZone>();
        z.profile = LoadProfile("Room");
        z.size = new Vector3(6f, ZoneHeight, 6f);
        FitToWalls(z);
        PrefabUtility.ApplyAddedGameObject(go, PrefabPath, InteractionMode.UserAction);
        Selection.activeGameObject = go;
        Debug.Log("[RoomAudio] Zone_New를 AudioSystem 프리팹에 추가했습니다. 이름/프로필을 바꾼 뒤 '프리팹에 저장'을 누르세요.");
    }

    [MenuItem("Tools/Dead Signal/Audio/Rooms/3. Move Scene Zones Into Prefab")]
    public static void MoveSceneZonesIntoPrefab()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == ZonesRootName);
        if (old == null) { Debug.Log("[RoomAudio] 씬 루트에 RoomAudioZones가 없습니다. 옮길 것이 없습니다."); return; }

        int moved = 0;
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var parent = PrefabZonesRoot(root);
            foreach (var src in old.GetComponentsInChildren<RoomAudioZone>(true))
            {
                var existing = parent.Find(src.name);
                var go = existing != null ? existing.gameObject : new GameObject(src.name);
                go.transform.SetParent(parent, false);
                // 월드 좌표 그대로 (프리팹 루트 = 원점 기준)
                go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
                go.transform.localScale = src.transform.lossyScale;
                var z = go.GetComponent<RoomAudioZone>() ?? go.AddComponent<RoomAudioZone>();
                EditorUtility.CopySerialized(src, z);
                moved++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        Undo.DestroyObjectImmediate(old);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[RoomAudio] Zone {moved}개를 AudioSystem 프리팹으로 옮기고 씬에서 지웠습니다. 씬 파일 변경은 커밋하지 마세요.");
    }

    // ───────────── 생성 ─────────────

    static int EnsureCues()
    {
        var ambienceGroup = AssetDatabase.LoadAssetAtPath<SoundCue>("Assets/Audio/Cues/Ambience/amb_drone.asset")?.mixerGroup;
        int n = 0;
        foreach (var d in Cues)
        {
            string path = $"{CuesFolder}/{d.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<SoundCue>(path) != null) continue;
            var cue = ScriptableObject.CreateInstance<SoundCue>();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(d.clip);
            cue.clips = clip != null ? new[] { clip } : new AudioClip[0];
            cue.volume = d.vol;
            cue.pitchRange = new Vector2(d.pitch, d.pitch);
            cue.spatial3D = false;
            cue.minInterval = 0f;
            cue.mixerGroup = ambienceGroup;
            cue.note = d.note;
            AssetDatabase.CreateAsset(cue, path);
            n++;
        }
        return n;
    }

    static int EnsureProfiles()
    {
        int n = 0;
        foreach (var d in Profiles)
        {
            string path = $"{ProfilesFolder}/{d.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<RoomAudioProfile>(path) != null) continue;
            var p = ScriptableObject.CreateInstance<RoomAudioProfile>();
            p.reverbPreset = d.preset;
            p.reverbLevelOffset = d.offset;
            p.custom = RoomReverb.FromPreset(d.preset == RoomReverbPreset.Custom ? RoomReverbPreset.Room : d.preset);
            p.droneWeight = d.drone;
            if (!string.IsNullOrEmpty(d.ambience)) p.ambienceLoop = AssetDatabase.LoadAssetAtPath<SoundCue>($"{CuesFolder}/{d.ambience}.asset");
            p.note = d.note;
            AssetDatabase.CreateAsset(p, path);
            n++;
        }
        return n;
    }


    static bool EnsureController()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<RoomAudioController>() != null) return false;
            var c = root.AddComponent<RoomAudioController>();
            c.defaultProfile = LoadProfile("Hallway");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static int EnsureZones()
    {
        // 예전 방식으로 씬에 만든 Zone이 있으면 그걸 옮기고 끝 (중복 생성 방지)
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Any(g => g.name == ZonesRootName))
        {
            MoveSceneZonesIntoPrefab();
            return 0;
        }

        int n = 0;
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var parent = PrefabZonesRoot(root);
            foreach (var d in Zones)
            {
                if (parent.Find(d.name) != null) continue;
                var go = new GameObject(d.name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3((d.x0 + d.x1) * 0.5f, ZoneHeight * 0.5f - 0.1f, (d.z0 + d.z1) * 0.5f);
                var z = go.AddComponent<RoomAudioZone>();
                z.size = new Vector3(Mathf.Abs(d.x1 - d.x0), ZoneHeight, Mathf.Abs(d.z1 - d.z0));
                z.profile = LoadProfile(d.profile);
                z.gizmoColor = Color.HSVToRGB((n * 0.137f) % 1f, 0.7f, 1f);
                n++;
            }
            if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return n;
    }

    /// <summary>프리팹 내용 안의 RoomAudioZones (없으면 만든다)</summary>
    static Transform PrefabZonesRoot(GameObject prefabRoot)
    {
        var t = prefabRoot.transform.Find(ZonesRootName);
        if (t != null) return t;
        // Zone이 이미 프리팹 루트 바로 아래에 있으면 그대로 쓴다
        if (prefabRoot.GetComponentsInChildren<RoomAudioZone>(true).Any(z => z.transform.parent == prefabRoot.transform))
            return prefabRoot.transform;
        var go = new GameObject(ZonesRootName);
        go.transform.SetParent(prefabRoot.transform, false);
        return go.transform;
    }

    /// <summary>열린 씬의 AudioSystem 인스턴스 안 RoomAudioZones</summary>
    static Transform SceneZonesRoot()
    {
        var c = Object.FindFirstObjectByType<RoomAudioController>(FindObjectsInactive.Include);
        if (c == null) return null;
        var t = c.transform.Find(ZonesRootName);
        if (t == null && c.GetComponentsInChildren<RoomAudioZone>(true).Any(z => z.transform.parent == c.transform))
            return c.transform; // Zone이 AudioSystem 바로 아래에 있는 구조
        if (t == null)
        {
            // 프리팹에 먼저 만들고 나면 인스턴스에도 생긴다
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { PrefabZonesRoot(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            t = c.transform.Find(ZonesRootName);
        }
        return t;
    }

    /// <summary>씬에서 고친 Zone(위치/크기/프로필)을 AudioSystem 프리팹에 반영</summary>
    public static bool ApplyZoneToPrefab(RoomAudioZone z)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(z)) return false;
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(z);
        PrefabUtility.ApplyObjectOverride(z, path, InteractionMode.UserAction);
        PrefabUtility.ApplyObjectOverride(z.transform, path, InteractionMode.UserAction);
        PrefabUtility.ApplyObjectOverride(z.gameObject, path, InteractionMode.UserAction);
        return true;
    }

    static RoomAudioProfile LoadProfile(string name) => AssetDatabase.LoadAssetAtPath<RoomAudioProfile>($"{ProfilesFolder}/{name}.asset");

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    // ───────────── 벽에 맞추기 ─────────────

    /// <summary>Zone 중심에서 4방향(로컬 ±X, ±Z)으로 레이를 쏴 가장 가까운 벽까지 박스를 넓히거나 줄인다.
    /// 가구에 걸리지 않게 천장 근처 높이에서 쏘고, 'Walls' 아래 콜라이더가 있으면 그것만 본다.</summary>
    public static void FitToWalls(RoomAudioZone z)
    {
        var t = z.transform;
        Vector3 o = t.position; o.y = 2.6f;
        float Cast(Vector3 dir, float fallback)
        {
            var hits = Physics.RaycastAll(o, dir, 60f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
            var wallHits = hits.Where(h => IsWall(h.transform)).ToArray();
            var use = wallHits.Length > 0 ? wallHits : hits;
            return use.Length > 0 ? use[0].distance : fallback;
        }
        float px = Cast(t.right, z.size.x * 0.5f), nx = Cast(-t.right, z.size.x * 0.5f);
        float pz = Cast(t.forward, z.size.z * 0.5f), nz = Cast(-t.forward, z.size.z * 0.5f);
        Undo.RecordObjects(new Object[] { z, t }, "Fit Zone To Walls");
        t.position += t.right * ((px - nx) * 0.5f) + t.forward * ((pz - nz) * 0.5f);
        z.size = new Vector3(px + nx, z.size.y, pz + nz);
        EditorUtility.SetDirty(z);
    }

    static bool IsWall(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name == "Walls") return true;
        return false;
    }
}

[CustomEditor(typeof(RoomAudioZone)), CanEditMultipleObjects]
public class RoomAudioZoneEditor : Editor
{
    readonly BoxBoundsHandle handle = new BoxBoundsHandle();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("벽에 맞추기 (중심에서 4방향 레이캐스트)"))
            foreach (var o in targets) RoomAudioSetup.FitToWalls((RoomAudioZone)o);

        var z = (RoomAudioZone)target;
        if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabInstance(z))
        {
            bool changed = targets.Any(o => PrefabUtility.HasPrefabInstanceAnyOverrides(PrefabUtility.GetNearestPrefabInstanceRoot(o), false));
            using (new EditorGUI.DisabledScope(!changed))
                if (GUILayout.Button("프리팹에 저장 (씬은 커밋하지 않아도 됨)"))
                    foreach (var o in targets) RoomAudioSetup.ApplyZoneToPrefab((RoomAudioZone)o);
        }
        else if (!Application.isPlaying && z.gameObject.scene.IsValid() && !PrefabUtility.IsPartOfPrefabAsset(z) && PrefabStageUtility.GetCurrentPrefabStage() == null)
        {
            EditorGUILayout.HelpBox("이 Zone은 씬에만 있습니다. 메뉴 Tools > Dead Signal > Audio > Rooms > 3. Move Scene Zones Into Prefab 으로 프리팹에 옮기세요.", MessageType.Warning);
        }
        if (Application.isPlaying && RoomAudioController.Instance != null)
        {
            var c = RoomAudioController.Instance;
            EditorGUILayout.HelpBox($"현재 방: {(c.CurrentZone ? c.CurrentZone.name : "(없음 → 기본)")} / 프로필: {(c.CurrentProfile ? c.CurrentProfile.name : "-")}", MessageType.Info);
            Repaint();
        }
        if (z.profile == null) EditorGUILayout.HelpBox("profile이 비어 있으면 이 Zone은 무시됩니다.", MessageType.Warning);
    }

    void OnSceneGUI()
    {
        var z = (RoomAudioZone)target;
        using (new Handles.DrawingScope(z.gizmoColor, z.transform.localToWorldMatrix))
        {
            handle.center = Vector3.zero;
            handle.size = z.size;
            EditorGUI.BeginChangeCheck();
            handle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new Object[] { z, z.transform }, "Resize Room Audio Zone");
                Vector3 worldCenter = z.transform.TransformPoint(handle.center);
                z.size = handle.size;
                z.transform.position = worldCenter;
            }
        }
        Handles.Label(z.transform.position + Vector3.up * (z.size.y * 0.5f), z.name + (z.profile ? $"\n[{z.profile.name}]" : ""));
    }
}
