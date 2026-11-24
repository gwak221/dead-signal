using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임(씬) 시작 시 '랜덤 대상' 아이템을 ItemSpawnPoint 후보 지점에 무작위로 배치한다. 씬을 다시 불러오면(R/메뉴 재시작) 새로 뽑고, 플레이 중에는 바꾸지 않는다.
/// - 종류별 수량은 보존: 대상 아이템 오브젝트를 그대로 옮길 뿐 만들거나 지우지 않는다. 지점이 모자라면 원래 자리(home)로 돌아가 누락시키지 않는다.
/// - 한 지점/한 개폐부에는 하나만. 구역(zone)별로 고르게 퍼지도록 가장 적게 채워진 구역을 우선한다.
/// - 수납장 밖(openSurface) 지점은 전체 중 maxOpenSurface 개까지만.
/// - Awake 에서 실행 → 이후 LootQteBootstrap 이 실제 위치 기준으로 아이템이 든 개폐부에만 QTE 를 붙인다.
/// - 비콘 배터리, 고정 아이템, 플레이어가 버린 아이템은 대상이 아니다 (G 로 버린 것은 이 시스템이 다시 건드리지 않음).
/// </summary>
[DefaultExecutionOrder(-200)]
public class RandomItemSpawner : MonoBehaviour
{
    public List<FlashlightBatteryPickup> randomBatteries = new List<FlashlightBatteryPickup>();
    public List<MedkitPickup> randomMedkits = new List<MedkitPickup>();
    public List<SyringePickup> randomSyringes = new List<SyringePickup>();
    [Min(0)] public int maxOpenSurface = 3;
    [Tooltip("같은 방/구역(후보 지점 간 거리 clusterRadius 이내로 이어진 무리)에 배정할 수 있는 최대 개수")]
    [Min(1)] public int maxPerZone = 2;
    [Tooltip("-1 이면 매번 무작위")] public int seed = -1;
    public float clusterRadius = 6f;
    public bool logResult = true;

    public string report = "";
    struct Job { public Component item; public SpawnAllow kind; }

    void Awake()
    {
        report = "";
        var points = new List<ItemSpawnPoint>(FindObjectsByType<ItemSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        points.RemoveAll(p => p == null || p.reserved);
        BuildClusters(points);
        var rng = seed >= 0 ? new System.Random(seed) : new System.Random();

        var jobs = new List<Job>();
        foreach (var m in randomMedkits) if (m != null) jobs.Add(new Job { item = m, kind = SpawnAllow.Medkit });
        foreach (var s in randomSyringes) if (s != null) jobs.Add(new Job { item = s, kind = SpawnAllow.Syringe });
        foreach (var b in randomBatteries) if (b != null) jobs.Add(new Job { item = b, kind = SpawnAllow.FlashlightBattery });

        // 제약이 큰 종류(주사기 → 구급상자 → 배터리) 먼저
        var used = new HashSet<ItemSpawnPoint>();
        var usedParts = new HashSet<Transform>();
        var zoneCount = new Dictionary<string, int>();
        int open = 0, placed = 0, fallback = 0;
        var homes = new Dictionary<Component, ItemSpawnPoint>();
        foreach (var j in jobs) homes[j.item] = Nearest(points, j.item.transform.position);

        foreach (var j in jobs)
        {
            var cands = new List<ItemSpawnPoint>();
            foreach (var p in points)
            {
                if (used.Contains(p) || !p.Allows(j.kind)) continue;
                if (p.openSurface && open >= maxOpenSurface) continue;
                if (p.transform.parent != null && usedParts.Contains(p.transform.parent)) continue;   // 같은 서랍/문에 두 개 금지
                cands.Add(p);
            }
            // 한 구역에 몰리지 않게 구역당 상한(maxPerZone). 상한을 지키는 후보가 있으면 그 안에서만 고른다.
            var capped = cands.FindAll(p => Count(zoneCount, ZoneOf(p)) < Mathf.Max(1, maxPerZone <= 0 ? 2 : maxPerZone));
            if (capped.Count > 0) cands = capped;
            ItemSpawnPoint pick = null;
            if (cands.Count > 0)
            {
                // 가장 덜 채워진 구역 우선 (그 안에서 무작위). 수납장 지점을 우선하고 열린 면은 낮은 확률.
                int best = int.MaxValue;
                foreach (var p in cands) best = Mathf.Min(best, Count(zoneCount, ZoneOf(p)));
                var pool = cands.FindAll(p => Count(zoneCount, ZoneOf(p)) <= best + (rng.Next(3) == 0 ? 1 : 0));
                if (pool.Count == 0) pool = cands;
                var closed = pool.FindAll(p => !p.openSurface);
                if (closed.Count > 0 && rng.Next(100) < 90) pool = closed;
                pick = pool[rng.Next(pool.Count)];
            }
            else if (homes[j.item] != null && !used.Contains(homes[j.item]))
            {
                pick = homes[j.item]; fallback++;   // 후보 부족: 안전한 기존 위치
            }
            if (pick == null) continue;           // 원래 위치에 그대로 둔다 (누락 없음)

            report += j.item.name + "->" + ZoneOf(pick) + "(" + Count(zoneCount, ZoneOf(pick)) + "/" + cands.Count + ") ";
            if (logResult) Debug.Log("[RandomItemSpawner] " + j.item.name + " -> " + pick.name + " zone=" + ZoneOf(pick) + " cnt=" + Count(zoneCount, ZoneOf(pick)) + " cands=" + cands.Count);
            Place(j.item, pick);
            used.Add(pick);
            if (pick.transform.parent != null) usedParts.Add(pick.transform.parent);
            zoneCount[ZoneOf(pick) ?? ""] = Count(zoneCount, ZoneOf(pick)) + 1;
            if (pick.openSurface) open++;
            placed++;
        }
        if (logResult) Debug.Log("[RandomItemSpawner] placed=" + placed + "/" + jobs.Count + " (fallback " + fallback + ", open " + open + ") candidates=" + points.Count + " zones=" + zoneCount.Count);
    }

    // 구역 = 후보 지점끼리 clusterRadius 이내로 이어진 무리 (같은 방의 가구는 같은 구역). 저장된 zone 문자열(격자)은 쓰지 않는다.
    readonly Dictionary<ItemSpawnPoint, string> clusterOf = new Dictionary<ItemSpawnPoint, string>();
    string ZoneOf(ItemSpawnPoint p) => clusterOf.TryGetValue(p, out var z) ? z : "";

    void BuildClusters(List<ItemSpawnPoint> pts)
    {
        clusterOf.Clear();
        int n = pts.Count;
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        float r2 = clusterRadius * clusterRadius;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                Vector3 a = pts[i].transform.position, b = pts[j].transform.position;
                float dx = a.x - b.x, dz = a.z - b.z;
                if (dx * dx + dz * dz < r2) parent[Find(i)] = Find(j);
            }
        for (int i = 0; i < n; i++) clusterOf[pts[i]] = Find(i).ToString();
    }

    static int Count(Dictionary<string, int> d, string z) => d.TryGetValue(z ?? "", out int v) ? v : 0;

    static ItemSpawnPoint Nearest(List<ItemSpawnPoint> pts, Vector3 pos)
    {
        ItemSpawnPoint best = null; float bd = 0.6f;
        foreach (var p in pts) { float d = Vector3.Distance(p.transform.position, pos); if (d < bd) { bd = d; best = p; } }
        return best;
    }

    static void Place(Component item, ItemSpawnPoint p)
    {
        var t = item.transform;
        float yaw = p.transform.eulerAngles.y;
        Quaternion rot = item is MedkitPickup ? Quaternion.Euler(90f, yaw, 0f)
                       : item is SyringePickup ? Quaternion.Euler(85f, yaw, 180f)
                       : Quaternion.Euler(0f, yaw, 0f);
        t.SetParent(null, true);
        t.rotation = rot;
        t.position = p.transform.position + Vector3.up * 0.3f;
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            t.position += Vector3.up * (p.transform.position.y + 0.004f - b.min.y);
        }
        if (p.transform.parent != null) t.SetParent(p.transform.parent, true);   // 서랍/문과 함께 움직이고 QTE 대상이 된다
        if (item is FlashlightBatteryPickup fb) fb.cabinetDoor = p.cabinetDoor;
    }
}
