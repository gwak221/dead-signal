using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 팀원 씬(맵/오브젝트 배치)을 베이스로, 우리 메인 씬에만 있는 기능 요소를 옮겨 합친 새 씬을 만든다.
///   SOURCE (기능 원본, 읽기만 함)  : Assets/Scenes/DeadSignal_Main.unity
///   TARGET (배치 원본, 읽기만 함)  : Assets/Scenes/DeadSignal_Main_teayoon.unity
///   OUTPUT (결과)                  : Assets/Scenes/DeadSignal_Main_merged.unity
///
/// 순서
///  1. TARGET 을 OUTPUT 으로 복사해서 열고, SOURCE 를 Additive 로 연다.
///  2. OUTPUT 의 원본 가구 FBX 를 Openables 프리팹으로 교체 (OpenablesSceneSwapper 재사용).
///  3. SOURCE 에만 있는 오브젝트 중 '기능 요소'(사운드, 배터리, 탈출 컷씬, Rooms/Layouts 프리팹 등)가 들어 있는 것을
///     같은 계층 위치로 OUTPUT 에 옮긴다. 프리팹 연결/오버라이드는 그대로 유지된다.
///  4. 양쪽에 다 있는 오브젝트에 기능 컴포넌트가 OUTPUT 쪽에만 없으면 값까지 복사해 붙인다.
///  5. 옮긴/복사한 컴포넌트가 SOURCE 씬 오브젝트를 가리키면 OUTPUT 의 같은 경로 오브젝트로 다시 연결한다.
///  6. 양쪽에 다 있는 스크립트 컴포넌트에서 OUTPUT 쪽 참조 필드가 비어 있고 SOURCE 쪽은 채워져 있으면 채운다
///     (최신 스크립트에서 새로 생긴 필드 등).
///  7. Rooms/Layouts 프리팹 영역 안에 있는 팀원 오브젝트를 '겹침 후보'로 보고한다 (삭제는 하지 않음).
///  8. OUTPUT 저장, SOURCE 는 저장하지 않고 닫는다. 보고서: Logs/SceneMerge_Report.txt
///
/// 오브젝트 짝 찾기는 '계층 경로 + 같은 이름 형제 중 순번'으로 한다.
/// </summary>
public static class SceneMergeTool
{
    public const string SourcePath = "Assets/Scenes/DeadSignal_Main.unity";
    public const string TargetPath = "Assets/Scenes/DeadSignal_Main_teayoon.unity";
    public const string OutputPath = "Assets/Scenes/DeadSignal_Main_merged.unity";
    const string ReportFile = "Logs/SceneMerge_Report.txt";

    // 옮길 대상으로 보는 컴포넌트 (타입 이름)
    static readonly HashSet<string> FeatureTypes = new HashSet<string>
    {
        "ExitCutsceneController", "FlashlightBatteryPickup", "FlashlightBatteryStock", "FlashlightBatteryCountUI",
        "MonsterDoorOpener",
    };

    // 옮길 대상으로 보는 프리팹 (에셋 경로 접두어)
    static readonly string[] FeaturePrefabPrefixes =
    {
        "Assets/HospitalMap/Rooms/Layouts/",
        "Assets/Prefabs/FlashlightBattery_AA.prefab",
    };

    const string LayoutPrefix = "Assets/HospitalMap/Rooms/Layouts/";

    // 튜토리얼 비콘 (시작 방) 과 그 비콘이 잠금 해제하는 시작 방 출구 문
    const string TutorialBeaconKey = "HospitalMap[0]/Objects[0]/control_panel[0]/control_panel_001 (1)[0]";
    const string TutorialDoorKey = "HospitalMap[0]/Walls[0]/Room_13[0]/WallDoor_02[0]/DoorFrame.002[0]";

    // ───────── menu ─────────

    [MenuItem("Tools/Dead Signal/Scene Merge/1. Dry Run (report only)")]
    static void DryRunMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string r = Run(dryRun: true);
        Debug.Log("[SceneMerge] Dry run 완료. 보고서: " + ReportFile + "\n" + Head(r));
    }

    [MenuItem("Tools/Dead Signal/Scene Merge/2. Build Merged Scene")]
    static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(OutputPath) &&
            !EditorUtility.DisplayDialog("Scene Merge", OutputPath + " 가 이미 있습니다. 새로 만들까요?", "덮어쓰기", "취소"))
            return;
        string r = Run(dryRun: false);
        Debug.Log("[SceneMerge] 완료 → " + OutputPath + ". 보고서: " + ReportFile + "\n" + Head(r));
    }

    [MenuItem("Tools/Dead Signal/Scene Merge/3. Replace Main Scene With Merged...")]
    static void ReplaceMenu()
    {
        if (!File.Exists(OutputPath)) { EditorUtility.DisplayDialog("Scene Merge", "먼저 2번으로 merged 씬을 만드세요.", "확인"); return; }
        if (!EditorUtility.DisplayDialog("Scene Merge",
                SourcePath + " 의 내용을 merged 씬으로 바꿉니다.\n(파일 GUID 는 유지 → Build Settings 그대로)\n원본은 git 으로 되돌릴 수 있습니다.",
                "교체", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ReplaceMainWithMerged();
    }

    [MenuItem("Tools/Dead Signal/Scene Merge/2b. Bake NavMesh (merged scene)")]
    static void BakeMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("[SceneMerge] " + BakeNavMeshForMerged());
    }

    /// <summary>
    /// merged 씬만 열고 NavMeshSurface 를 Bake 해서 merged 씬 전용 폴더에 저장한다
    /// (메인 씬의 Scenes/DeadSignal_Main/NavMesh-NavMesh.asset 은 건드리지 않음). 검증 결과 문자열 반환.
    /// </summary>
    public static string BakeNavMeshForMerged()
    {
        var sb = new StringBuilder();
        var s = EditorSceneManager.OpenScene(OutputPath, OpenSceneMode.Single);
        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include);
        if (surface == null) return "NavMeshSurface 없음";

        double t0 = EditorApplication.timeSinceStartup;
        surface.BuildNavMesh();
        var data = surface.navMeshData;
        if (data == null) return "Bake 결과 없음";

        string folder = Path.ChangeExtension(OutputPath, null).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(OutputPath).Replace('\\', '/'), Path.GetFileName(folder));
        string path = folder + "/NavMesh-" + surface.name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(data, path);
        surface.RemoveData();
        surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
        surface.AddData();
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
        AssetDatabase.SaveAssets();
        sb.AppendLine($"Bake {(EditorApplication.timeSinceStartup - t0) * 1000:0}ms → {path}");

        var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
        sb.AppendLine($"verts={tri.vertices.Length} tris={tri.indices.Length / 3}");

        sb.Append(NavMeshReachability());
        return sb.ToString();
    }

    /// <summary>현재 로드된 NavMesh 에서 Player/Monster → 각 비콘(바닥 투영 2.5m 이내) 경로가 끝까지 이어지는지.</summary>
    public static string NavMeshReachability()
    {
        var sb = new StringBuilder();
        var all = UnityEngine.AI.NavMesh.AllAreas;
        var player = GameObject.Find("Player");
        var mon = GameObject.Find("Monster");
        UnityEngine.AI.NavMeshHit ph = default, mh = default;
        bool hasP = player && UnityEngine.AI.NavMesh.SamplePosition(player.transform.position, out ph, 2f, all);
        bool hasM = mon && UnityEngine.AI.NavMesh.SamplePosition(mon.transform.position, out mh, 2f, all);
        if (!hasP) sb.AppendLine("MISS Player 위치에 NavMesh 없음");
        if (!hasM) sb.AppendLine("MISS Monster 위치에 NavMesh 없음");

        string Path(bool ok, Vector3 from, Vector3 to)
        {
            if (!ok) return "-";
            var p = new UnityEngine.AI.NavMeshPath();
            UnityEngine.AI.NavMesh.CalculatePath(from, to, all, p);
            if (p.status == UnityEngine.AI.NavMeshPathStatus.PathComplete) return "OK";
            float gap = p.corners.Length > 0 ? Vector3.Distance(p.corners[p.corners.Length - 1], to) : -1f;
            return $"끊김({gap:0.0}m 부족)";
        }

        if (hasP && hasM) sb.AppendLine($"Player ↔ Monster: {Path(true, ph.position, mh.position)}");
        foreach (var b in Object.FindObjectsByType<Beacon>(FindObjectsSortMode.None).OrderBy(b => b.name))
        {
            var p = b.transform.position;
            if (!UnityEngine.AI.NavMesh.SamplePosition(new Vector3(p.x, p.y + 0.1f, p.z), out var bh, 2.5f, all))
            { sb.AppendLine($"MISS {b.name} {p}: 주변 2.5m 안에 NavMesh 없음"); continue; }
            sb.AppendLine($"{b.name} {p}: Player {Path(hasP, ph.position, bh.position)} / Monster {Path(hasM, mh.position, bh.position)}");
        }
        return sb.ToString();
    }

    static string Head(string s) => s.Length > 6000 ? s.Substring(0, 6000) + "\n... (전체는 보고서 파일)" : s;

    // ───────── public API (MCP/자동화용, 확인 창 없음) ─────────

    public static string Run(bool dryRun)
    {
        var rep = new StringBuilder();
        rep.AppendLine($"Scene Merge {(dryRun ? "(DRY RUN)" : "")}  {System.DateTime.Now}");
        rep.AppendLine($"SOURCE {SourcePath}\nTARGET {TargetPath}\nOUTPUT {OutputPath}\n");

        Scene output;
        if (dryRun)
            output = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
        else
        {
            if (File.Exists(OutputPath)) AssetDatabase.DeleteAsset(OutputPath);
            if (!AssetDatabase.CopyAsset(TargetPath, OutputPath)) { rep.AppendLine("ERROR: 복사 실패"); return Finish(rep); }
            output = EditorSceneManager.OpenScene(OutputPath, OpenSceneMode.Single);
        }
        var source = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(output);

        try
        {
            // 2. Openables
            rep.AppendLine("== 1. Openables 교체 (팀원 씬의 원본 가구 FBX → Openables 프리팹)");
            if (dryRun) rep.AppendLine(OpenablesSceneSwapper.ReportInOpenScenes());
            else rep.AppendLine($"  {OpenablesSceneSwapper.ReplaceAllInScene(output)}개 교체\n");

            var tIndex = BuildIndex(output);
            var sIndex = BuildIndex(source);

            // 3. 옮길 오브젝트 찾기
            var toMove = new List<GameObject>();
            var notMoved = new List<string>();
            foreach (var root in source.GetRootGameObjects())
                CollectMissing(root.transform, sIndex, tIndex, toMove, notMoved);
            var pulled = AddReferencedMissing(toMove, notMoved, tIndex);

            rep.AppendLine($"== 2. 메인 씬에서 옮기는 오브젝트 ({toMove.Count})");
            var blocked = new List<GameObject>();
            foreach (var go in toMove)
            {
                string key = KeyOf(go.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsOutermostPrefabInstanceRoot(go))
                {
                    blocked.Add(go);
                    rep.AppendLine($"  [수동] {key}  (프리팹 내부 오브젝트라 옮길 수 없음)");
                    continue;
                }
                string why = pulled.TryGetValue(go, out var by) ? "참조됨: " + by : FeatureSummary(go);
                rep.AppendLine($"  {key}{PrefabLabel(go)}  [{why}]");
            }
            toMove.RemoveAll(blocked.Contains);
            rep.AppendLine();

            rep.AppendLine($"== 3. 메인 씬에만 있지만 옮기지 않은 오브젝트 ({notMoved.Count}) — 기능 요소가 없음. 필요하면 수동으로");
            foreach (var g in notMoved.GroupBy(x => x).OrderBy(x => x.Key))
                rep.AppendLine("  " + g.Key);
            rep.AppendLine();

            // 4. 기능 컴포넌트 누락 찾기
            var compCopies = new List<(Component src, GameObject dst)>();
            foreach (var kv in sIndex)
            {
                if (!tIndex.TryGetValue(kv.Key, out var dstGo)) continue;
                foreach (var c in kv.Value.GetComponents<Component>())
                {
                    if (c == null || !FeatureTypes.Contains(c.GetType().Name)) continue;
                    int need = kv.Value.GetComponents(c.GetType()).Length;
                    int have = dstGo.GetComponents(c.GetType()).Length + compCopies.Count(x => x.dst == dstGo && x.src.GetType() == c.GetType());
                    if (have < need) compCopies.Add((c, dstGo));
                }
            }
            rep.AppendLine($"== 4. 기존 오브젝트에 붙이는 기능 컴포넌트 ({compCopies.Count})");
            foreach (var x in compCopies) rep.AppendLine($"  {KeyOf(x.dst.transform)} + {x.src.GetType().Name}");
            rep.AppendLine();

            if (dryRun)
            {
                rep.AppendLine("(dry run: 실제 변경 없음. 참조 재연결/겹침 검사는 2번 실행 때 보고)");
                return Finish(rep);
            }

            // 3 실행: 옮기기
            foreach (var go in toMove)
            {
                string parentKey = ParentKey(KeyOf(go.transform));
                int sib = go.transform.GetSiblingIndex();
                if (parentKey == null)
                {
                    go.transform.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(go, output);
                }
                else go.transform.SetParent(tIndex[parentKey].transform, false); // 부모 기준 로컬 자세 유지 (팀원이 부모를 옮겼으면 같이 따라감)
                go.transform.SetSiblingIndex(Mathf.Min(sib, go.transform.parent != null
                    ? go.transform.parent.childCount - 1
                    : output.rootCount - 1));
            }

            // 4 실행: 컴포넌트 복사
            foreach (var x in compCopies)
            {
                if (ComponentUtility.CopyComponent(x.src) && ComponentUtility.PasteComponentAsNew(x.dst)) continue;
                rep.AppendLine($"  [실패] {KeyOf(x.dst.transform)} + {x.src.GetType().Name}");
            }

            // 5. SOURCE 씬을 가리키는 참조 재연결
            tIndex = BuildIndex(output);
            rep.AppendLine("== 5. 메인 씬을 가리키던 참조 재연결");
            int fixedRefs = 0;
            var unresolved = new List<string>();
            foreach (var root in output.GetRootGameObjects())
                foreach (var comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var v = it.objectReferenceValue;
                        if (v == null || SceneOf(v) != source) continue;
                        var mapped = Counterpart(v, tIndex);
                        if (mapped != null) { it.objectReferenceValue = mapped; fixedRefs++; }
                        else { it.objectReferenceValue = null; unresolved.Add($"  {KeyOf(comp.transform)} ({comp.GetType().Name}.{it.propertyPath}) → {Describe(v)}"); }
                        changed = true;
                    }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }
            rep.AppendLine($"  재연결 {fixedRefs}개, 짝을 못 찾아 비운 참조 {unresolved.Count}개 (수동 연결 필요)");
            foreach (var u in unresolved) rep.AppendLine(u);
            rep.AppendLine();

            // 6. 양쪽에 있는 스크립트의 빈 참조 채우기 + 값 차이 보고
            rep.AppendLine("== 6. 스크립트 필드: 팀원 씬에서 비어 있던 참조 채움");
            var diffs = new List<string>();
            int filled = 0;
            foreach (var kv in BuildIndex(source))
            {
                if (!tIndex.TryGetValue(kv.Key, out var dstGo)) continue;
                foreach (var sc in kv.Value.GetComponents<MonoBehaviour>())
                {
                    if (sc == null) continue;
                    var dc = SameComponent(sc, dstGo);
                    if (dc == null) continue;
                    var sso = new SerializedObject(sc);
                    var dso = new SerializedObject(dc);
                    bool changed = false;
                    var it = sso.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.propertyPath.StartsWith("m_")) continue;
                        var sv = it.objectReferenceValue;
                        if (sv == null) continue;
                        var dp = dso.FindProperty(it.propertyPath);
                        if (dp == null || dp.propertyType != SerializedPropertyType.ObjectReference || dp.objectReferenceValue != null) continue;
                        var nv = EditorUtility.IsPersistent(sv) || SceneOf(sv) == output ? sv : Counterpart(sv, tIndex);
                        if (nv == null) continue;
                        dp.objectReferenceValue = nv;
                        changed = true; filled++;
                        rep.AppendLine($"  {kv.Key} ({sc.GetType().Name}.{it.propertyPath}) = {Describe(nv)}");
                    }
                    if (changed) dso.ApplyModifiedPropertiesWithoutUndo();

                    // 값 차이 (보고만)
                    var fields = new List<string>();
                    var top = new SerializedObject(sc).GetIterator();
                    var dso2 = new SerializedObject(dc);
                    if (top.NextVisible(true))
                        do
                        {
                            if (top.propertyPath == "m_Script" || top.propertyType == SerializedPropertyType.ObjectReference) continue;
                            // 참조 배열은 씬이 달라 항상 '다름'으로 나오므로 비교하지 않는다 (빈 참조는 위에서 채움)
                            if (top.isArray && top.arrayElementType.StartsWith("PPtr")) continue;
                            var dp = dso2.FindProperty(top.propertyPath);
                            if (dp != null && !SerializedProperty.DataEquals(top, dp)) fields.Add(top.propertyPath);
                        } while (top.NextVisible(false));
                    if (fields.Count > 0) diffs.Add($"  {kv.Key} ({sc.GetType().Name}): {string.Join(", ", fields)}");
                }
            }
            rep.AppendLine($"  합계 {filled}개\n");
            rep.AppendLine($"== 7. 스크립트 값이 서로 다른 곳 ({diffs.Count}) — 팀원 값 유지됨. 메인 값이 맞으면 수동 수정");
            foreach (var d in diffs) rep.AppendLine(d);
            rep.AppendLine();

            // 7. Layout 프리팹 영역 겹침
            rep.AppendLine("== 8. Rooms/Layouts 프리팹 영역 안의 팀원 오브젝트 (겹침 후보, 삭제 안 함)");
            foreach (var go in toMove)
            {
                if (!PrefabPathOf(go).StartsWith(LayoutPrefix)) continue;
                if (!TryBounds(go, out var room)) continue;
                rep.AppendLine($"  [{go.name}] center {room.center} size {room.size}");
                var inside = new SortedSet<string>();
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (r.gameObject.scene != output || r.transform.IsChildOf(go.transform)) continue;
                    var b = r.bounds;
                    if (!room.Contains(b.center)) continue;
                    if (b.size.x > room.size.x * 0.6f || b.size.z > room.size.z * 0.6f) continue; // 벽/바닥/천장 제외
                    var owner = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject) ?? r.gameObject;
                    inside.Add(KeyOf(owner.transform) + PrefabLabel(owner));
                }
                foreach (var s in inside) rep.AppendLine("    " + s);
                if (inside.Count == 0) rep.AppendLine("    (없음)");
            }
            rep.AppendLine();
            // 9. 수납장 DoorPair 는 몬스터 통행 문이 아님 (새 필드라 팀원 씬엔 기본값 true)
            int cab = 0;
            foreach (var d in Object.FindObjectsByType<DoorPair>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (d.gameObject.scene == output && d.isPassableDoor && d.name.ToLowerInvariant().Contains("cabinet"))
                { d.isPassableDoor = false; EditorUtility.SetDirty(d); cab++; }
            rep.AppendLine($"== 9. cabinet DoorPair isPassableDoor=false: {cab}개\n");

            // 9b. 통행용 문 프레임은 NavMesh Bake 에서 제외 (메인 씬 규칙: 닫힌 문이 길을 끊지 않고, 몬스터는 MonsterDoorOpener 로 열고 지나감)
            rep.AppendLine("== 9b. 통행용 문 프레임에 NavMeshModifier(Ignore From Build) 추가");
            int mods = 0;
            foreach (var t in output.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
            {
                if (!t.name.StartsWith("DoorFrame")) continue;
                var pair = t.GetComponentInChildren<DoorPair>(true) ?? t.GetComponentInParent<DoorPair>(true);
                if (pair == null || !pair.isPassableDoor) continue;
                if (t.GetComponent<Unity.AI.Navigation.NavMeshModifier>() != null) continue;
                var m = t.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
                m.ignoreFromBuild = true;
                m.applyToChildren = true;
                EditorUtility.SetDirty(t.gameObject);
                rep.AppendLine("  " + KeyOf(t));
                mods++;
            }
            rep.AppendLine($"  합계 {mods}개\n");

            // 9c. 튜토리얼 비콘: 시작 방의 비콘 → 시작 방 출구 문 잠금 해제 (탈출 진행도에는 포함 안 함)
            rep.AppendLine("== 9c. 튜토리얼 비콘 연결");
            var tIdx = BuildIndex(output);
            if (tIdx.TryGetValue(TutorialBeaconKey, out var tutGo) && tIdx.TryGetValue(TutorialDoorKey, out var doorGo)
                && tutGo.GetComponent<Beacon>() is Beacon tut && doorGo.GetComponent<DoorPair>() is DoorPair door)
            {
                tut.isTutorial = true;
                tut.unlockDoor = door;
                tut.openDoorOnUnlock = false; // 잠금만 해제, 문은 플레이어가 직접 연다
                EditorUtility.SetDirty(tut);
                rep.AppendLine($"  {TutorialBeaconKey} → {TutorialDoorKey} (잠금 해제만)\n");
            }
            else rep.AppendLine($"  [수동] 튜토리얼 비콘/문을 찾지 못함: {TutorialBeaconKey}, {TutorialDoorKey}\n");

            EditorSceneManager.MarkSceneDirty(output);
            EditorSceneManager.SaveScene(output);
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true); // 저장하지 않고 닫음
        }

        if (!dryRun)
        {
            // (기존 10단계 "오디오 없는 비콘 보충"은 오디오 시스템 교체로 제거됨. 새 AudioManager는 씬 배치가 필요 없다.)
            rep.AppendLine("남은 작업: NavMesh 다시 Bake → 플레이 테스트 → 3번 메뉴로 메인 씬 교체");
            EditorSceneManager.SaveScene(output);
        }
        return Finish(rep);
    }

    /// <summary>merged 씬 내용으로 메인 씬 파일을 덮어쓴다 (메인의 .meta/GUID 유지).</summary>
    public static void ReplaceMainWithMerged()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        File.Copy(OutputPath, SourcePath, true);
        AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceUpdate);

        // merged 전용 폴더의 NavMesh 를 메인 씬 폴더로 옮긴다 (GUID 유지 → 씬 참조 그대로). 예전 메인 NavMesh 는 교체된 씬이 더 이상 쓰지 않음.
        string mergedNav = Path.ChangeExtension(OutputPath, null).Replace('\\', '/') + "/NavMesh-NavMesh.asset";
        string mainNav = Path.ChangeExtension(SourcePath, null).Replace('\\', '/') + "/NavMesh-NavMesh.asset";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(mergedNav) != null)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(mainNav) != null) AssetDatabase.DeleteAsset(mainNav);
            string err = AssetDatabase.MoveAsset(mergedNav, mainNav);
            if (!string.IsNullOrEmpty(err)) Debug.LogError("[SceneMerge] NavMesh 이동 실패: " + err);
            else AssetDatabase.DeleteAsset(Path.GetDirectoryName(mergedNav).Replace('\\', '/'));
        }

        EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
        Debug.Log("[SceneMerge] 메인 씬을 merged 내용으로 교체했습니다: " + SourcePath);
    }

    static string Finish(StringBuilder rep)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ReportFile, rep.ToString(), new UTF8Encoding(false));
        return rep.ToString();
    }

    // ───────── 경로 인덱스 ─────────

    static Dictionary<string, GameObject> BuildIndex(Scene scene)
    {
        var map = new Dictionary<string, GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                map[KeyOf(t)] = t.gameObject;
        return map;
    }

    /// <summary>"Parent[0]/Child[1]" — 이름 + 같은 이름 형제 중 순번.</summary>
    static string KeyOf(Transform t)
    {
        string seg = t.name + "[" + SameNameIndex(t) + "]";
        return t.parent == null ? seg : KeyOf(t.parent) + "/" + seg;
    }

    static int SameNameIndex(Transform t)
    {
        int n = 0;
        if (t.parent != null)
        {
            for (int i = 0; i < t.GetSiblingIndex(); i++)
                if (t.parent.GetChild(i).name == t.name) n++;
        }
        else
        {
            foreach (var r in t.gameObject.scene.GetRootGameObjects())
            {
                if (r.transform == t) break;
                if (r.name == t.name) n++;
            }
        }
        return n;
    }

    static string ParentKey(string key)
    {
        int i = key.LastIndexOf('/');
        return i < 0 ? null : key.Substring(0, i);
    }

    static void CollectMissing(Transform t, Dictionary<string, GameObject> sIndex, Dictionary<string, GameObject> tIndex,
                               List<GameObject> toMove, List<string> notMoved)
    {
        string key = KeyOf(t);
        if (!tIndex.ContainsKey(key))
        {
            if (HasFeature(t.gameObject)) toMove.Add(t.gameObject);
            else notMoved.Add(key + PrefabLabel(t.gameObject));
            return; // 하위는 통째로 처리
        }
        for (int i = 0; i < t.childCount; i++) CollectMissing(t.GetChild(i), sIndex, tIndex, toMove, notMoved);
    }

    /// <summary>
    /// 옮길 오브젝트(하위 포함)의 컴포넌트가 참조하는데 팀원 씬에 없는 SOURCE 오브젝트를 같이 옮길 목록에 넣는다 (예: ExitCutscene → ExitCutsceneCamera).
    /// 반환: 추가된 오브젝트 → 참조한 쪽 설명.
    /// </summary>
    static Dictionary<GameObject, string> AddReferencedMissing(List<GameObject> toMove, List<string> notMoved,
                                                               Dictionary<string, GameObject> tIndex)
    {
        var added = new Dictionary<GameObject, string>();
        for (int i = 0; i < toMove.Count; i++) // toMove 가 늘어나면 새로 들어온 것도 검사
            foreach (var comp in toMove[i].GetComponentsInChildren<Component>(true))
            {
                if (comp == null || comp is Transform) continue;
                var it = new SerializedObject(comp).GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var v = it.objectReferenceValue;
                    GameObject g = v is GameObject vg ? vg : v is Component vc ? vc.gameObject : null;
                    if (g == null || g.scene != toMove[i].scene) continue;
                    if (tIndex.ContainsKey(KeyOf(g.transform))) continue;
                    // 팀원 씬에 없는 가장 위 조상
                    var top = g.transform;
                    while (top.parent != null && !tIndex.ContainsKey(KeyOf(top.parent))) top = top.parent;
                    var topGo = top.gameObject;
                    if (toMove.Any(m => top.IsChildOf(m.transform))) continue;
                    toMove.Add(topGo);
                    notMoved.RemoveAll(s => s == KeyOf(top) + PrefabLabel(topGo));
                    added[topGo] = $"{KeyOf(comp.transform)} ({comp.GetType().Name}.{it.propertyPath})";
                }
            }
        return added;
    }

    static bool HasFeature(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            foreach (var c in t.GetComponents<Component>())
                if (c != null && FeatureTypes.Contains(c.GetType().Name)) return true;
            string p = PrefabPathOf(t.gameObject);
            if (p.Length > 0 && FeaturePrefabPrefixes.Any(p.StartsWith)) return true;
        }
        return false;
    }

    static string FeatureSummary(GameObject go)
    {
        var names = new SortedSet<string>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            foreach (var c in t.GetComponents<Component>())
                if (c != null && FeatureTypes.Contains(c.GetType().Name)) names.Add(c.GetType().Name);
            string p = PrefabPathOf(t.gameObject);
            if (p.Length > 0 && FeaturePrefabPrefixes.Any(p.StartsWith)) names.Add(Path.GetFileName(p));
        }
        return string.Join(", ", names);
    }

    static string PrefabPathOf(GameObject go)
    {
        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go)) return "";
        var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
        return src != null ? AssetDatabase.GetAssetPath(src).Replace('\\', '/') : "";
    }

    static string PrefabLabel(GameObject go)
    {
        string p = PrefabPathOf(go);
        return p.Length > 0 ? "  <" + p + ">" : "";
    }

    // ───────── 참조 매핑 ─────────

    static Scene SceneOf(Object o)
    {
        if (o is GameObject g) return g.scene;
        if (o is Component c) return c.gameObject.scene;
        return default;
    }

    static Object Counterpart(Object v, Dictionary<string, GameObject> tIndex)
    {
        if (v is GameObject g)
            return tIndex.TryGetValue(KeyOf(g.transform), out var m) ? m : null;
        if (v is Component c)
            return tIndex.TryGetValue(KeyOf(c.transform), out var m) ? SameComponent(c, m) : null;
        return null;
    }

    static Component SameComponent(Component c, GameObject on)
    {
        var src = c.gameObject.GetComponents(c.GetType());
        int idx = System.Array.IndexOf(src, c);
        var dst = on.GetComponents(c.GetType());
        return idx >= 0 && idx < dst.Length ? dst[idx] : null;
    }

    static string Describe(Object v) =>
        v is Component c ? KeyOf(c.transform) + " (" + c.GetType().Name + ")"
        : v is GameObject g ? KeyOf(g.transform)
        : v.name + " (" + AssetDatabase.GetAssetPath(v) + ")";

    static bool TryBounds(GameObject go, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any;
    }
}
