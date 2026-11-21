using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템(배터리/AA/Medkit/Key 등 줍기 아이템)이 실제로 들어 있는 여닫이 상자(OpenableFurniture / 약장 DoorPair)의 잠금 QTE.
/// - 처음 E 로 열려고 하면 기존 SkillCheckManager(원형 스킬체크)를 실행한다.
///   성공 → 잠금 해제 + 그 파츠가 열림(이후 이 상자는 QTE 없이 자유롭게 열고 닫음).
///   실패 → 닫힌 상태 유지(기존 SkillCheckManager 의 실패 패널티 적용), E 로 다시 도전 가능.
/// - 닫혀 있거나 잠긴 상자 안 아이템은 F 로 주울 수 없다 (PlayerInteraction.FindPickup 이 IsSealed 로 거른다).
/// - 빈 상자에는 붙지 않는다 → 기존처럼 E 로 바로 열림. 은신 캐비넷(HideSpot)은 대상에서 제외.
/// 씬 시작 시 LootQteBootstrap 이 씬의 실제 pickup 을 검사해 자동으로 붙인다.
/// </summary>
public class LootQteGate : MonoBehaviour
{
    public bool locked = true;
    public string lockedText = "E : Unlock";

    /// <summary>이 상자 안에 들어 있는 줍기 아이템들(디버그/검증용).</summary>
    public readonly List<Component> contents = new List<Component>();

    static readonly Dictionary<Component, LootQteGate> map = new Dictionary<Component, LootQteGate>();
    static readonly List<LootQteGate> all = new List<LootQteGate>();
    bool qteRunning;

    /// <summary>(상자, 성공 여부) — 사운드/로그용.</summary>
    public static event Action<LootQteGate, bool> AnyResult;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { map.Clear(); all.Clear(); AnyResult = null; }

    public static IReadOnlyList<LootQteGate> All => all;

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDestroy() { all.Remove(this); }

    public static void Register(LootQteGate gate, Component pickup)
    {
        if (gate == null || pickup == null) return;
        map[pickup] = gate;
        if (!gate.contents.Contains(pickup)) gate.contents.Add(pickup);
    }

    /// <summary>이 pickup 이 잠겼거나 닫힌 상자 안에 있어 지금은 주울 수 없는가.</summary>
    public static bool IsSealed(Component pickup)
    {
        if (pickup == null) return false;
        if (!map.TryGetValue(pickup, out var gate) || gate == null) return false;
        return gate.Sealed;
    }

    /// <summary>잠겼거나, 열린 문/서랍이 하나도 없으면 봉인 상태.</summary>
    public bool Sealed => locked || !AnyOpen();

    bool AnyOpen()
    {
        foreach (var p in GetComponentsInChildren<OpenablePart>(true)) if (p.IsOpen) return true;
        foreach (var d in GetComponentsInChildren<DoorPair>(true)) if (d.IsOpen) return true;
        return false;
    }

    /// <summary>
    /// 열기 시도를 가로챈다. 잠겨 있으면 QTE 를 시작하고 true(이번 E 는 여기서 처리됨).
    /// 이미 해제됐으면 false → 호출한 쪽이 평소처럼 연다. onSuccess 는 QTE 성공 시 실제 열기 동작.
    /// </summary>
    public bool Intercept(Action onSuccess)
    {
        if (!locked) return false;
        if (qteRunning) return true;

        var sk = SkillCheckManager.Instance != null ? SkillCheckManager.Instance : FindFirstObjectByType<SkillCheckManager>();
        if (sk == null)
        {
            Debug.LogWarning("[LootQteGate] SkillCheckManager 가 씬에 없어 QTE 없이 엽니다: " + name);
            locked = false;
            return false;
        }

        qteRunning = sk.TriggerSkillCheck(
            () => { qteRunning = false; locked = false; AnyResult?.Invoke(this, true); onSuccess?.Invoke(); },
            () => { qteRunning = false; AnyResult?.Invoke(this, false); });   // 실패/취소: 닫힌 채 유지, 다시 E 가능
        return true;
    }
}

/// <summary>씬 시작 직후 실제 pickup 을 검사해 '아이템이 든 상자'에만 LootQteGate 를 붙인다.</summary>
public class LootQteBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<LootQteBootstrap>() != null) return;
        new GameObject("LootQteBootstrap").AddComponent<LootQteBootstrap>();
    }

    System.Collections.IEnumerator Start()
    {
        yield return null;   // 다른 시스템의 Awake/Start(아이템 배치 등) 이후
        Scan();
    }

    struct Container { public Transform root; public Bounds bounds; }

    void Scan()
    {
        var containers = new List<Container>();
        foreach (var f in FindObjectsByType<OpenableFurniture>(FindObjectsSortMode.None))
            if (f.GetComponent<HideSpot>() == null && !DecorativeOpenable.IsDecorative(f)) AddContainer(containers, f.transform);
        foreach (var d in FindObjectsByType<DoorPair>(FindObjectsSortMode.None))
            if (!d.isPassableDoor && !DecorativeOpenable.IsDecorative(d)) AddContainer(containers, d.transform);

        int gated = 0, pickups = 0;
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb == null || !(mb is IPickupable || mb is BatteryPickup)) continue;
            if (mb.GetComponentInParent<PlayerInventory>() != null) continue;   // 이미 플레이어가 든 것
            pickups++;
            Transform root = FindContainer(mb, containers);
            if (root == null) continue;

            var gate = root.GetComponent<LootQteGate>();
            if (gate == null) { gate = root.gameObject.AddComponent<LootQteGate>(); gated++; }
            LootQteGate.Register(gate, mb);
        }
        Debug.Log("[LootQteGate] pickups=" + pickups + " gatedContainers=" + gated);
    }

    static void AddContainer(List<Container> list, Transform root)
    {
        foreach (var c in list) if (c.root == root) return;
        Bounds b = default; bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (any) list.Add(new Container { root = root, bounds = b });
    }

    static Transform FindContainer(Component pickup, List<Container> containers)
    {
        // 0) 약장 AA 처럼 '이 약장에 들어 있다'고 명시한 경우(cabinetDoor)는 그 약장
        if (pickup is FlashlightBatteryPickup fb && fb.cabinetDoor != null)
            foreach (var c in containers) if (c.root == fb.cabinetDoor.transform) return c.root;

        // 1) 계층상 자식이면 그 상자
        for (Transform t = pickup.transform; t != null; t = t.parent)
            foreach (var c in containers) if (c.root == t) return t;

        // 2) 위치가 상자 외형(Renderer bounds) 안이면 그 상자 (가장 작은 것)
        Vector3 p = pickup.transform.position;
        var col = pickup.GetComponentInChildren<Collider>();
        if (col != null) p = col.bounds.center;
        else { var r = pickup.GetComponentInChildren<Renderer>(); if (r != null) p = r.bounds.center; }

        Transform best = null; float bestVol = float.MaxValue;
        foreach (var c in containers)
        {
            var b = c.bounds; b.Expand(0.04f);
            if (!b.Contains(p)) continue;
            float vol = b.size.x * b.size.y * b.size.z;
            if (vol < bestVol) { bestVol = vol; best = c.root; }
        }
        return best;
    }
}
