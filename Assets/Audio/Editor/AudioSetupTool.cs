using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사운드 기반 세팅 도구. 메뉴: Tools > Dead Signal > Audio > ...
/// 여러 번 실행해도 안전하다: 이미 있는 Cue/믹서/프리팹은 덮어쓰지 않고 빠진 것만 만든다.
/// (팀원이 Cue에 넣은 클립과 볼륨은 그대로 유지된다.)
/// </summary>
public static class AudioSetupTool
{
    const string Root = "Assets/Audio";
    const string MixerPath = Root + "/Mixer/MainMixer.mixer";
    const string CuesRoot = Root + "/Cues";
    const string ClipsRoot = Root + "/Clips";
    const string PrefabPath = Root + "/Prefabs/AudioSystem.prefab";

    static readonly string[] MixerGroups = { "BGM", "Ambience", "Player", "Enemy", "World", "UI" };
    static readonly string[] ClipFolders = { "Player", "Enemy1", "Enemy2", "Beacon", "Flashlight", "Door", "Item", "Ambience", "BGM", "UI" };

    // 사운드 목록 (계획서 2장과 같다). folder / name / group / 3D / min / max / volume / pitch / interval / note
    struct Def
    {
        public string folder, name, group, note;
        public bool is3D;
        public float min, max, vol, pitchLo, pitchHi, interval;
    }

    static Def D(string folder, string name, string group, bool is3D, float min, float max, string note,
                 float vol = 1f, float pitchLo = 0.95f, float pitchHi = 1.05f, float interval = 0.05f)
        => new Def { folder = folder, name = name, group = group, is3D = is3D, min = min, max = max, note = note, vol = vol, pitchLo = pitchLo, pitchHi = pitchHi, interval = interval };

    static readonly Def[] Defs =
    {
        // 플레이어
        D("Player", "plr_step_walk",     "Player", false, 1, 10, "걷기 발소리 (타일 바닥). 클립 4~6개. footstep tile shoe walk", 0.6f, 0.9f, 1.1f),
        D("Player", "plr_step_run",      "Player", false, 1, 10, "뛰기 발소리. 클립 4~6개. footstep run tile", 0.75f, 0.9f, 1.1f),
        D("Player", "plr_breath_idle",   "Player", false, 1, 10, "대기 숨소리 루프. breathing calm loop", 0.35f, 1f, 1f, 0f),
        D("Player", "plr_breath_tense",  "Player", false, 1, 10, "긴장 숨소리 루프 (120bpm 이상). heavy breathing scared panting loop", 0.6f, 1f, 1f, 0f),
        D("Player", "plr_heartbeat",     "Player", false, 1, 10, "심장박동 한 번 (BPM에 맞춰 반복). heartbeat single thump", 0.7f, 1f, 1f, 0.15f),
        D("Player", "plr_interact",      "Player", false, 1, 10, "상호작용. interact grab click"),
        D("Player", "plr_drop",          "World",  true,  1, 10, "물건 버리기 (바닥에 떨어짐). item drop floor"),
        D("Player", "plr_slot_select",   "UI",     false, 1, 10, "아이템 슬롯 선택. inventory select", 0.7f),
        D("Player", "plr_slot_deselect", "UI",     false, 1, 10, "아이템 슬롯 해제. inventory deselect", 0.7f),
        D("Player", "plr_hurt",          "Player", false, 1, 10, "피격 (다리/눈 파손). male hurt grunt pain"),
        D("Player", "plr_caught_bite",   "Player", false, 1, 10, "잡힘 (물리기). bite flesh gore", 1f, 1f, 1f),
        D("Player", "plr_hide_enter",    "Player", false, 1, 10, "캐비닛 숨기. metal locker enter cloth"),
        D("Player", "plr_hide_exit",     "Player", false, 1, 10, "캐비닛 나오기. metal locker exit"),

        // 추격 공통
        D("BGM", "bgm_chase",         "BGM", false, 1, 10, "쫓김 BGM 루프 (적 추격 상태 공통). horror chase music loop", 0.8f, 1f, 1f, 0f),
        D("BGM", "sfx_chase_escaped", "BGM", false, 1, 10, "따돌림 (추격 해제). horror relief sting", 0.8f, 1f, 1f, 1f),

        // 적 1
        D("Enemy1", "en1_step_walk", "Enemy", true, 2, 25, "적1 걷기 발소리. 클립 4개. monster footstep heavy slow", 0.8f, 0.9f, 1.1f),
        D("Enemy1", "en1_step_run",  "Enemy", true, 2, 30, "적1 뛰기 발소리. 클립 4개. monster footstep run heavy", 0.9f, 0.9f, 1.1f),
        D("Enemy1", "en1_idle",      "Enemy", true, 2, 25, "적1 대기 울음 (Mm_random). 클립 3~5개. creature groan moan low", 0.8f, 0.9f, 1.05f),
        D("Enemy1", "en1_hear",      "Enemy", true, 2, 25, "적1 소리 감지. creature curious sniff alert", 0.9f, 0.95f, 1.05f, 1f),
        D("Enemy1", "en1_spot",      "Enemy", true, 2, 35, "적1 플레이어 감지. monster scream roar detect", 1f, 0.95f, 1.05f, 1f),
        D("Enemy1", "en1_chase",     "Enemy", true, 2, 30, "적1 추격 울음 (Mm_random). 클립 3~5개. monster aggressive growl", 0.9f, 0.9f, 1.05f),
        D("Enemy1", "en1_stun",      "Enemy", true, 2, 30, "적1 기절 (눈뽕 맞음). monster shriek pain", 1f, 0.95f, 1.05f, 0.5f),
        D("Enemy1", "en1_attack",    "Enemy", true, 1, 15, "적1 공격. monster attack lunge"),

        // 적 2
        D("Enemy2", "en2_step_walk",    "Enemy", true, 2, 25, "적2 걷기 발소리. 클립 4개. creature footstep drag", 0.8f, 0.9f, 1.1f),
        D("Enemy2", "en2_idle",         "Enemy", true, 2, 25, "적2 대기 울음. 클립 3개. creature breathing wheeze", 0.8f, 0.9f, 1.05f),
        D("Enemy2", "en2_spot",         "Enemy", true, 2, 35, "적2 플레이어 감지. creature screech", 1f, 0.95f, 1.05f, 1f),
        D("Enemy2", "en2_chase",        "Enemy", true, 2, 30, "적2 추격 울음 (Mm_random). 클립 3개. creature snarl", 0.9f, 0.9f, 1.05f),
        D("Enemy2", "en2_charge_ready", "Enemy", true, 2, 35, "적2 돌진 준비. monster charge windup growl build", 1f, 1f, 1f, 0.5f),
        D("Enemy2", "en2_charge",       "Enemy", true, 2, 35, "적2 돌진. monster charge rush whoosh", 1f, 1f, 1f, 0.5f),
        D("Enemy2", "en2_charge_hit",   "Enemy", true, 2, 30, "적2 돌진 후 벽 충돌. body impact wall heavy"),

        // 비콘
        D("Beacon", "bcn_hum_off",        "World", true,  1, 8,  "비활성화 전기 기계음 루프. electrical buzz faulty machine loop", 0.5f, 1f, 1f, 0f),
        D("Beacon", "bcn_battery_insert", "World", true,  1, 12, "배터리 장착. battery insert click"),
        D("Beacon", "bcn_qte_success",    "UI",    false, 1, 10, "QTE 성공. ui success beep", 0.8f, 1f, 1f),
        D("Beacon", "bcn_qte_fail",       "UI",    false, 1, 10, "QTE 실패. electric spark fail buzz", 0.8f, 1f, 1f),
        D("Beacon", "bcn_activate",       "World", true,  1, 20, "비콘 활성화. power up machine start", 1f, 1f, 1f, 0.5f),
        D("Beacon", "bcn_hum_on",         "World", true,  1, 10, "활성화 전기 기계음 루프. machine hum stable loop", 0.5f, 1f, 1f, 0f),
        D("Beacon", "bcn_all_done",       "BGM",   false, 1, 10, "마지막 비콘 활성화 → 탈출 안내. alarm distant siren unlock", 0.9f, 1f, 1f, 1f),

        // 손전등
        D("Flashlight", "fl_on",     "Player", false, 1, 10, "손전등 켜기. flashlight click switch", 0.7f),
        D("Flashlight", "fl_off",    "Player", false, 1, 10, "손전등 끄기. flashlight click switch", 0.7f),
        D("Flashlight", "fl_charge", "Player", false, 1, 10, "눈뽕 충전. charge up whine electric", 0.8f, 1f, 1f),
        D("Flashlight", "fl_flash",  "Player", false, 1, 10, "눈뽕 (강한 빛). camera flash burst", 1f, 0.97f, 1.03f),
        D("Flashlight", "fl_reload", "Player", false, 1, 10, "배터리 교체. battery replace compartment", 0.8f),
        D("Flashlight", "fl_empty",  "Player", false, 1, 10, "배터리 없음 (빈 딸깍). dry click empty", 0.7f),

        // 문, 상자
        D("Door", "door_open",         "World", true, 1, 15, "문 열기. 클립 2개. door open creak hospital"),
        D("Door", "door_close",        "World", true, 1, 15, "문 닫기. 클립 2개. door close shut"),
        D("Door", "door_open_monster", "World", true, 2, 25, "적이 문 열기. door slam bang open", 1f, 0.85f, 0.95f),
        D("Door", "door_locked",       "World", true, 1, 12, "잠긴 문 덜컹. door locked rattle handle", 1f, 0.95f, 1.05f, 0.4f),
        D("Door", "box_open",          "World", true, 1, 12, "상자/가구 열기. drawer cabinet open"),
        D("Door", "box_close",         "World", true, 1, 12, "상자/가구 닫기. drawer cabinet close"),

        // 아이템, 퍼즐
        D("Item", "item_pickup_battery", "Player", false, 1, 10, "배터리 줍기. pickup item small", 0.8f),
        D("Item", "item_pickup_key",     "Player", false, 1, 10, "열쇠 줍기. keys jingle pickup", 0.8f),
        D("Item", "pz_keypad_press",     "UI",     false, 1, 10, "비밀번호 버튼. keypad button beep", 0.6f, 0.98f, 1.02f, 0.03f),
        D("Item", "pz_keypad_ok",        "UI",     false, 1, 10, "비밀번호 정답. access granted beep", 0.8f, 1f, 1f),
        D("Item", "pz_keypad_ng",        "UI",     false, 1, 10, "비밀번호 오답. access denied beep", 0.8f, 1f, 1f),
        D("Item", "pz_safe_open",        "World",  true,  1, 12, "금고 문 열림. safe door unlock heavy"),

        // 환경음
        D("Ambience", "amb_drone",       "Ambience", false, 1, 10, "병원 전체 저음 드론 루프 (2D, 스테레오 가능). horror ambience drone dark", 0.5f, 1f, 1f, 0f),
        D("Ambience", "amb_fluorescent", "Ambience", true,  0.5f, 5, "형광등 웅웅 루프 (구역마다 몇 개만). fluorescent light hum buzz loop", 0.3f, 1f, 1f, 0f),
        D("Ambience", "amb_emergency",   "Ambience", true,  0.5f, 6, "비상등 지직 루프. electrical crackle loop", 0.3f, 1f, 1f, 0f),
        D("Ambience", "amb_random",      "Ambience", true,  3, 30, "멀리서 나는 소리 (쿵, 끼익, 물방울). 클립 5~8개. distant bang creak drip", 0.6f, 0.85f, 1.1f, 2f),

        // 진행, UI
        D("BGM", "esc_door_open", "World", true,  2, 25, "탈출 문 열림. heavy metal gate open"),
        D("BGM", "bgm_gameover",  "BGM",   false, 1, 10, "게임 오버. horror game over sting", 1f, 1f, 1f, 1f),
        D("BGM", "bgm_escape",    "BGM",   false, 1, 10, "탈출 성공. relief victory dark ambient", 1f, 1f, 1f, 1f),
        D("BGM", "bgm_menu",      "BGM",   false, 1, 10, "메뉴 배경음 루프. horror menu ambient", 0.7f, 1f, 1f, 0f),
        D("UI",  "ui_hover",      "UI",    false, 1, 10, "메뉴 이동. ui click subtle", 0.5f, 1f, 1f, 0.03f),
        D("UI",  "ui_click",      "UI",    false, 1, 10, "메뉴 클릭. ui click subtle", 0.7f, 1f, 1f, 0.03f),
    };

    // ───────────────────────── 메뉴 ─────────────────────────

    [MenuItem("Tools/Dead Signal/Audio/1. Setup All (Folders, Mixer, Cues, Prefab, Scene)")]
    public static void SetupAll()
    {
        EnsureFolders();
        var mixer = EnsureMixer();
        int created = EnsureCues(mixer);
        var prefab = EnsureAudioSystemPrefab();
        int openables = AssignOpenableCues();
        bool placed = PlaceAudioSystemInOpenScene(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AudioSetup] 완료: 새 Cue {created}개, Openable 프리팹 {openables}개 연결, 씬 배치 {(placed ? "추가함" : "이미 있음")}.");
    }

    [MenuItem("Tools/Dead Signal/Audio/Step 2. Setup Player Audio (open scene)")]
    public static void SetupPlayerAudio()
    {
        var fpc = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
        if (fpc == null) { Debug.LogWarning("[AudioSetup] 씬에서 FirstPersonController를 찾지 못했습니다."); return; }

        var t = fpc.transform.Find("Audio");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("Audio");
            Undo.RegisterCreatedObjectUndo(go, "Player Audio");
            go.transform.SetParent(fpc.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 0f); // 발 근처 (2D라 위치 영향은 거의 없음)
        }
        else go = t.gameObject;

        T Need<T>() where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        Need<SoundEmitter>();

        var steps = Need<FootstepEmitter>();
        if (steps.walkCue == null) steps.walkCue = LoadCue("plr_step_walk");
        if (steps.runCue == null) steps.runCue = LoadCue("plr_step_run");
        steps.walkStride = 2.1f;   // 걷기 5 m/s ≈ 초당 2.4걸음
        steps.runStride = 3.0f;    // 뛰기 9 m/s ≈ 초당 3걸음
        steps.minSpeed = 0.5f;

        var breath = Need<BreathingAudio>();
        if (breath.idleBreathCue == null) breath.idleBreathCue = LoadCue("plr_breath_idle");
        if (breath.tenseBreathCue == null) breath.tenseBreathCue = LoadCue("plr_breath_tense");

        var pa = Need<PlayerAudio>();
        if (pa.interactCue == null) pa.interactCue = LoadCue("plr_interact");
        if (pa.slotSelectCue == null) pa.slotSelectCue = LoadCue("plr_slot_select");
        if (pa.slotDeselectCue == null) pa.slotDeselectCue = LoadCue("plr_slot_deselect");
        if (pa.dropCue == null) pa.dropCue = LoadCue("plr_drop");
        if (pa.pickupBatteryCue == null) pa.pickupBatteryCue = LoadCue("item_pickup_battery");
        if (pa.pickupKeyCue == null) pa.pickupKeyCue = LoadCue("item_pickup_key");
        if (pa.heartbeatCue == null) pa.heartbeatCue = LoadCue("plr_heartbeat");
        if (pa.hurtCue == null) pa.hurtCue = LoadCue("plr_hurt");
        if (pa.caughtBiteCue == null) pa.caughtBiteCue = LoadCue("plr_caught_bite");
        if (pa.hideEnterCue == null) pa.hideEnterCue = LoadCue("plr_hide_enter");
        if (pa.hideExitCue == null) pa.hideExitCue = LoadCue("plr_hide_exit");

        foreach (var c in go.GetComponents<Component>()) EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Debug.Log("[AudioSetup] 플레이어 오디오 설정 완료: " + fpc.name + "/Audio");
    }

    static GameObject EnsureAudioChild(Transform parent)
    {
        var t = parent.Find("Audio");
        if (t != null) return t.gameObject;
        var go = new GameObject("Audio");
        Undo.RegisterCreatedObjectUndo(go, "Audio child");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1f, 0f);
        return go;
    }

    static T Need<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static void SaveScene(GameObject any)
    {
        foreach (var c in any.GetComponentsInChildren<Component>(true)) if (c != null) EditorUtility.SetDirty(c);
        EditorSceneManager.MarkSceneDirty(any.scene);
        EditorSceneManager.SaveScene(any.scene);
    }

    [MenuItem("Tools/Dead Signal/Audio/Step 3. Setup Enemy Audio (open scene)")]
    public static void SetupEnemyAudio()
    {
        var monsters = Object.FindObjectsByType<MonsterChase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in monsters)
        {
            var go = EnsureAudioChild(m.transform);
            Need<SoundEmitter>(go);

            var steps = Need<FootstepEmitter>(go);
            if (steps.walkCue == null) steps.walkCue = LoadCue("en1_step_walk");
            if (steps.runCue == null) steps.runCue = LoadCue("en1_step_run");
            steps.walkStride = 1.4f;   // 순찰 2.5 m/s ≈ 초당 1.8걸음
            steps.runStride = 2.4f;    // 추격 6.5 m/s ≈ 초당 2.7걸음
            steps.requireGrounded = false;

            var voc = Need<RandomVocalizer>(go);
            if (voc.idleCue == null) voc.idleCue = LoadCue("en1_idle");
            if (voc.activeCue == null) voc.activeCue = LoadCue("en1_chase");
            voc.idleInterval = new Vector2(5f, 11f);
            voc.activeInterval = new Vector2(1.8f, 4f);

            var ma = Need<MonsterAudio>(go);
            if (ma.spotCue == null) ma.spotCue = LoadCue("en1_spot");
            if (ma.hearCue == null) ma.hearCue = LoadCue("en1_hear");
            if (ma.stunCue == null) ma.stunCue = LoadCue("en1_stun");
            if (ma.attackCue == null) ma.attackCue = LoadCue("en1_attack");
            SaveScene(go);
        }
        Debug.Log($"[AudioSetup] 적 오디오 설정: {monsters.Length}개");
    }

    [MenuItem("Tools/Dead Signal/Audio/Step 4. Setup Beacon + World Audio (open scene)")]
    public static void SetupBeaconAndWorldAudio()
    {
        var beacons = Object.FindObjectsByType<Beacon>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in beacons)
        {
            var go = EnsureAudioChild(b.transform);
            go.transform.localPosition = Vector3.zero;
            Need<SoundEmitter>(go);
            var ba = Need<BeaconAudio>(go);
            if (ba.humOffCue == null) ba.humOffCue = LoadCue("bcn_hum_off");
            if (ba.humOnCue == null) ba.humOnCue = LoadCue("bcn_hum_on");
            if (ba.qteSuccessCue == null) ba.qteSuccessCue = LoadCue("bcn_qte_success");
            if (ba.qteFailCue == null) ba.qteFailCue = LoadCue("bcn_qte_fail");
            if (ba.batteryInsertCue == null) ba.batteryInsertCue = LoadCue("bcn_battery_insert");
            if (ba.activateCue == null) ba.activateCue = LoadCue("bcn_activate");
            SaveScene(go);
        }

        // AudioSystem 프리팹에 WorldAudio + 랜덤 환경음 추가 (씬이 아니라 프리팹만 바뀐다)
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var wa = root.GetComponent<WorldAudio>();
            if (wa == null) wa = root.AddComponent<WorldAudio>();
            void Set(ref SoundCue f, string n) { if (f == null) f = LoadCue(n); }
            Set(ref wa.doorOpenCue, "door_open");
            Set(ref wa.doorCloseCue, "door_close");
            Set(ref wa.doorOpenMonsterCue, "door_open_monster");
            Set(ref wa.doorLockedCue, "door_locked");
            Set(ref wa.safeOpenCue, "pz_safe_open");
            Set(ref wa.escapeDoorOpenCue, "esc_door_open");
            Set(ref wa.flashlightOnCue, "fl_on");
            Set(ref wa.flashlightOffCue, "fl_off");
            Set(ref wa.flashChargeCue, "fl_charge");
            Set(ref wa.flashCue, "fl_flash");
            Set(ref wa.flashlightReloadCue, "fl_reload");
            Set(ref wa.flashlightEmptyCue, "fl_empty");
            Set(ref wa.keypadPressCue, "pz_keypad_press");
            Set(ref wa.keypadOkCue, "pz_keypad_ok");
            Set(ref wa.keypadNgCue, "pz_keypad_ng");
            Set(ref wa.allBeaconsCue, "bcn_all_done");
            Set(ref wa.gameOverCue, "bgm_gameover");
            Set(ref wa.escapedCue, "bgm_escape");
            Set(ref wa.fluorescentCue, "amb_fluorescent");
            Set(ref wa.emergencyCue, "amb_emergency");

            var droneT = root.transform.Find("AmbienceDrone");
            if (wa.drone == null && droneT != null) wa.drone = droneT.GetComponent<LoopSoundEmitter>();

            var randT = root.transform.Find("AmbienceRandom");
            if (randT == null)
            {
                randT = new GameObject("AmbienceRandom").transform;
                randT.SetParent(root.transform, false);
            }
            var rv = randT.GetComponent<RandomVocalizer>();
            if (rv == null)
            {
                rv = randT.gameObject.AddComponent<RandomVocalizer>();
                rv.idleCue = LoadCue("amb_random");
                rv.idleInterval = new Vector2(12f, 30f);
                rv.randomOffset = 18f;
            }
            if (wa.randomAmbience == null) wa.randomAmbience = rv;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"[AudioSetup] 비콘 오디오 {beacons.Length}개, WorldAudio 설정 완료");
    }

    // 메뉴/일시정지 중에도 들려야 하는 Cue
    static readonly HashSet<string> PauseIgnoringCues = new HashSet<string> { "ui_hover", "ui_click", "bgm_menu" };

    [MenuItem("Tools/Dead Signal/Audio/Step 5. Setup Menu Audio")]
    public static void SetupMenuAudio()
    {
        foreach (var n in PauseIgnoringCues)
        {
            var c = LoadCue(n);
            if (c == null || c.ignoreListenerPause) continue;
            c.ignoreListenerPause = true;
            EditorUtility.SetDirty(c);
        }
        AssetDatabase.SaveAssets();

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var ma = root.GetComponent<MenuAudio>();
            if (ma == null) ma = root.AddComponent<MenuAudio>();
            if (ma.hoverCue == null) ma.hoverCue = LoadCue("ui_hover");
            if (ma.clickCue == null) ma.clickCue = LoadCue("ui_click");
            if (ma.menuMusicCue == null) ma.menuMusicCue = LoadCue("bgm_menu");
            if (ma.pauseOpenCue == null) ma.pauseOpenCue = LoadCue("ui_click");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[AudioSetup] 메뉴 오디오 설정 완료");
    }

    [MenuItem("Tools/Dead Signal/Audio/2. Create Missing Cues Only")]
    public static void CreateMissingCuesOnly()
    {
        EnsureFolders();
        int created = EnsureCues(AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
        AssetDatabase.SaveAssets();
        Debug.Log($"[AudioSetup] 새 Cue {created}개.");
    }

    [MenuItem("Tools/Dead Signal/Audio/3. Report Cues Without Clips")]
    public static void ReportEmptyCues()
    {
        var empty = AssetDatabase.FindAssets("t:SoundCue", new[] { CuesRoot })
            .Select(g => AssetDatabase.LoadAssetAtPath<SoundCue>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && !c.HasClips).Select(c => c.name).OrderBy(n => n).ToList();
        Debug.Log($"[AudioSetup] 클립이 없는 Cue {empty.Count}개:\n" + string.Join("\n", empty));
    }

    // ───────────────────────── 단계 ─────────────────────────

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets", "Audio");
        foreach (var f in new[] { "Clips", "Cues", "Mixer", "Prefabs" }) EnsureFolder(Root, f);
        foreach (var f in ClipFolders) EnsureFolder(ClipsRoot, f);
        foreach (var f in Defs.Select(d => d.folder).Distinct()) EnsureFolder(CuesRoot, f);
    }

    static AudioMixer EnsureMixer()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var ctrlType = typeof(EditorWindow).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
        const BindingFlags all = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (ctrlType == null) { Debug.LogWarning("[AudioSetup] AudioMixerController를 찾지 못했습니다. 믹서는 직접 만들어 주세요."); return mixer; }

        if (mixer == null)
        {
            ctrlType.GetMethod("CreateMixerControllerAtPath", all)?.Invoke(null, new object[] { MixerPath });
            AssetDatabase.ImportAsset(MixerPath);
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) { Debug.LogWarning("[AudioSetup] 믹서 생성 실패. 직접 만들어 주세요: " + MixerPath); return null; }
        }

        var master = ctrlType.GetProperty("masterGroup", all)?.GetValue(mixer);
        var createGroup = ctrlType.GetMethod("CreateNewGroup", all);
        var addChild = ctrlType.GetMethod("AddChildToParent", all);
        var addToView = ctrlType.GetMethod("AddGroupToCurrentView", all);
        var existing = new HashSet<string>(mixer.FindMatchingGroups(string.Empty).Select(g => g.name));
        foreach (var name in MixerGroups)
        {
            if (existing.Contains(name) || master == null || createGroup == null || addChild == null) continue;
            var g = createGroup.Invoke(mixer, new object[] { name, false });
            addChild.Invoke(mixer, new object[] { g, master });
            try { addToView?.Invoke(mixer, new object[] { g }); }
            catch { /* 뷰가 없는 새 믹서에서는 실패할 수 있다. 그룹 자체는 만들어졌으므로 무시 */ }
        }
        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        return mixer;
    }

    static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        => mixer == null ? null : mixer.FindMatchingGroups(string.Empty).FirstOrDefault(g => g.name == name);

    static string CuePath(Def d) => $"{CuesRoot}/{d.folder}/{d.name}.asset";

    public static SoundCue LoadCue(string name)
    {
        var d = Defs.FirstOrDefault(x => x.name == name);
        return d.name == null ? null : AssetDatabase.LoadAssetAtPath<SoundCue>(CuePath(d));
    }

    static int EnsureCues(AudioMixer mixer)
    {
        int created = 0;
        foreach (var d in Defs)
        {
            string path = CuePath(d);
            var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(path);
            if (cue != null)
            {
                // 이미 있으면 설정은 건드리지 않고, 비어 있는 믹서 그룹만 채운다
                if (cue.mixerGroup == null && mixer != null) { cue.mixerGroup = FindGroup(mixer, d.group); EditorUtility.SetDirty(cue); }
                continue;
            }
            cue = ScriptableObject.CreateInstance<SoundCue>();
            cue.clips = new AudioClip[0];
            cue.volume = d.vol;
            cue.pitchRange = new Vector2(d.pitchLo, d.pitchHi);
            cue.spatial3D = d.is3D;
            cue.minDistance = d.min;
            cue.maxDistance = d.max;
            cue.rolloff = AudioRolloffMode.Linear;
            cue.minInterval = d.interval;
            cue.ignoreListenerPause = PauseIgnoringCues.Contains(d.name);
            cue.mixerGroup = FindGroup(mixer, d.group);
            cue.note = d.note + $"\n클립 폴더: {ClipsRoot}/{FolderForClips(d.folder)}/  파일 이름: {d.name}_01, {d.name}_02 ...";
            AssetDatabase.CreateAsset(cue, path);
            created++;
        }
        return created;
    }

    static string FolderForClips(string cueFolder) => ClipFolders.Contains(cueFolder) ? cueFolder : "BGM";

    static GameObject EnsureAudioSystemPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing;

        var root = new GameObject("AudioSystem");
        try
        {
            var chase = root.AddComponent<ChaseMusicController>();
            chase.chaseBgmCue = LoadCue("bgm_chase");
            chase.escapedCue = LoadCue("sfx_chase_escaped");

            var drone = new GameObject("AmbienceDrone");
            drone.transform.SetParent(root.transform, false);
            var loop = drone.AddComponent<LoopSoundEmitter>();
            loop.cue = LoadCue("amb_drone");
            loop.playOnStart = true;
            loop.fadeInTime = 3f;

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static int AssignOpenableCues()
    {
        var open = LoadCue("box_open");
        var close = LoadCue("box_close");
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Openables/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var s = go.GetComponent<OpenableSound>();
                if (s == null || (s.openCue != null && s.closeCue != null)) continue;
                if (s.openCue == null) s.openCue = open;
                if (s.closeCue == null) s.closeCue = close;
                PrefabUtility.SaveAsPrefabAsset(go, path);
                n++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
        }
        return n;
    }

    static bool PlaceAudioSystemInOpenScene(GameObject prefab)
    {
        if (prefab == null) return false;
        if (Object.FindFirstObjectByType<ChaseMusicController>(FindObjectsInactive.Include) != null) return false;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(inst, "Place AudioSystem");
        var scene = inst.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }
}
