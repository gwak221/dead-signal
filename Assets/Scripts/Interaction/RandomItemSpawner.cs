using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임(씬) 시작 시 '랜덤 대상' 아이템을 ItemSpawnPoint 후보 지점에 무작위로 배치한다. 씬을 다시 불러오면(R/메뉴 재시작) 새로 뽑고, 플레이 중에는 바꾸지 않는다.
///
/// 배치는 '계획 먼저, 적용 나중' 이다: 모든 제약을 만족하는 전체 배치 계획을 백트래킹으로 먼저 확정한 뒤에만 아이템을 옮긴다.
/// 제약(전부 하드 제약 - 어기는 계획은 만들어지지 않는다):
///  - 아이템 종류별 허용 지점(ItemSpawnPoint.allow), 한 지점에 하나
///  - 가구 루트(OpenableFurniture / 약장 DoorPair)당 최대 1개, 고정 아이템이 든 가구에는 랜덤 아이템 금지
///  - 같은 서랍/문(OpenablePart 부모)에 하나
///  - 구역(후보 지점 간 clusterRadius 이내로 이어진 무리)당 최대 maxPerZone
///  - 장식용 가구 / cardboard / 소화전함 에는 배치 금지
/// 선호(완화 가능, 단 위 하드 제약은 절대 완화하지 않음): 수납장 밖(openSurface) 지점은 maxOpenSurface 개까지, 부족하면 단계적으로 늘린다.
/// 계획을 세우지 못하면 아이템을 옮기지 않고(삭제/상한 무시 없음) 오류를 남긴다 - 검증 결과 현재 후보로는 항상 계획이 나온다.
/// 비콘 배터리, 고정 아이템, 플레이어가 버린 아이템은 대상이 아니다. QTE 는 배치 후 LootQteBootstrap 이 실제 위치로 붙인다.
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
    [Min(1)] public int maxAttempts = 40;
    [Tooltip("항상 비어 있어야 하는 가구(랜덤 아이템 영구 제외). 개폐/외곽선/안내는 그대로.")]
    public List<Component> excludedRoots = new List<Component>();
    [Header("사전 검증된 안전 배치 (무작위 배치가 반복 실패할 때만 사용)")]
    public List<Component> safeItems = new List<Component>();
    public List<ItemSpawnPoint> safePoints = new List<ItemSpawnPoint>();

    [System.NonSerialized] public string report = "";
    [System.NonSerialized] public bool planOk;
    [System.NonSerialized] public int lastOpenCap;
    [System.NonSerialized] public int lastAttempts;

    public struct Job { public Component item; public SpawnAllow kind; }

    readonly List<Component> roots = new List<Component>();
    readonly Dictionary<ItemSpawnPoint, Component> rootCache = new Dictionary<ItemSpawnPoint, Component>();
    readonly Dictionary<ItemSpawnPoint, string> clusterOf = new Dictionary<ItemSpawnPoint, string>();
    readonly HashSet<Component> fixedRoots = new HashSet<Component>();

    // 계획 상태
    List<Job> jobs;
    ItemSpawnPoint[] plan;
    readonly HashSet<ItemSpawnPoint> usedPoints = new HashSet<ItemSpawnPoint>();
    readonly HashSet<Component> usedRoots = new HashSet<Component>();
    readonly HashSet<Transform> usedParts = new HashSet<Transform>();
    readonly Dictionary<string, int> zoneCount = new Dictionary<string, int>();
    int openUsed;
    System.Random rng;

    void Awake()
    {
        report = ""; planOk = false;
        Collect(out var eligible);
        BuildPlan(eligible);
        if (planOk) Apply();
    }

    /// <summary>씬에서 후보/가구/구역 정보를 모은다 (계획 전용, 아이템은 옮기지 않는다).</summary>
    public void Collect(out List<ItemSpawnPoint> eligible)
    {
        var allPoints = new List<ItemSpawnPoint>(FindObjectsByType<ItemSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        roots.Clear(); rootCache.Clear();
        foreach (var f in FindObjectsByType<OpenableFurniture>(FindObjectsInactive.Include, FindObjectsSortMode.None)) roots.Add(f);
        foreach (var d in FindObjectsByType<DoorPair>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!d.isPassableDoor) roots.Add(d);

        // 고정 아이템이 든 가구는 랜덤 금지
        fixedRoots.Clear();
        foreach (var rp in allPoints) if (rp != null && rp.reserved) { var rr = RootOf(rp); if (rr != null) fixedRoots.Add(rr); }

        var nonReserved = allPoints.FindAll(p => p != null && !p.reserved);
        BuildClusters(nonReserved);
        eligible = nonReserved.FindAll(p =>
        {
            var r = RootOf(p);
            if (r != null)
            {
                if (fixedRoots.Contains(r) || excludedRoots.Contains(r)) return false;
                if (DecorativeOpenable.IsDecorative(r)) return false;
                string n = r.name.ToLower();
                if (n.StartsWith("cardboard") || n.StartsWith("fire_cabinet")) return false;
            }
            return p.allow != SpawnAllow.None;
        });
    }

    /// <summary>
    /// 단순 무작위 배치 계획. 후보가 적은 구급상자/주사기를 먼저, AA 를 마지막에, 각자 유효한 후보 중 무작위로 고른다.
    /// 한 번 실패(후보 없음)하면 처음부터 다시(최대 maxAttempts 회, 무한 탐색 없음). 구역 상한은 2 를 우선하고 시도가 쌓이면 3 까지 허용.
    /// 끝내 실패하면 사전에 검증해 저장해 둔 안전 배치(safeItems/safePoints)를 쓴다. 반복 횟수가 정해져 있어 멈추지 않는다.
    /// </summary>
    public void BuildPlan(List<ItemSpawnPoint> eligible)
    {
        rng = seed >= 0 ? new System.Random(seed) : new System.Random();
        planOk = false;
        jobs = new List<Job>();
        foreach (var m in randomMedkits) if (m != null) jobs.Add(new Job { item = m, kind = SpawnAllow.Medkit });
        // 후보가 적은 주사기(4곳)를 먼저, AA(수납장 안 15개 가구 전용)는 남은 가구에서.
        foreach (var s in randomSyringes) if (s != null) jobs.Add(new Job { item = s, kind = SpawnAllow.Syringe });
        foreach (var b in randomBatteries) if (b != null) jobs.Add(new Job { item = b, kind = SpawnAllow.FlashlightBattery });
        plan = new ItemSpawnPoint[jobs.Count];

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int zoneCap = Mathf.Max(1, maxPerZone) + (attempt * 3) / maxAttempts;   // 2 우선, 시도가 쌓이면 3, 4 (AA 가 수납장 안에만 들어가야 해서 후보가 적다)
            int openLimit = maxOpenSurface + (attempt >= maxAttempts / 2 ? 3 : 0);
            usedPoints.Clear(); usedParts.Clear(); zoneCount.Clear(); usedRoots.Clear(); openUsed = 0;
            foreach (var r in fixedRoots) usedRoots.Add(r);
            bool ok = true;
            for (int i = 0; i < jobs.Count && ok; i++)
            {
                var valid = new List<ItemSpawnPoint>();
                foreach (var p in eligible)
                {
                    if (!p.Allows(jobs[i].kind) || usedPoints.Contains(p)) continue;
                    if (p.openSurface && (openUsed >= openLimit || jobs[i].kind == SpawnAllow.FlashlightBattery)) continue;   // AA 는 수납장 밖(open surface) 금지 - 완화 규칙도 적용 안 함
                    var par = PartOf(p); if (par != null && usedParts.Contains(par)) continue;
                    var r = RootOf(p); if (r != null && usedRoots.Contains(r)) continue;
                    if (ZoneCount(p) >= zoneCap) continue;
                    valid.Add(p);
                }
                if (valid.Count == 0) { ok = false; break; }
                var pick = valid[rng.Next(valid.Count)];
                plan[i] = pick; usedPoints.Add(pick);
                var pp = PartOf(pick); if (pp != null) usedParts.Add(pp);
                var rr = RootOf(pick); if (rr != null) usedRoots.Add(rr);
                string z = ZoneOf(pick); zoneCount[z] = ZoneCount(pick) + 1;
                if (pick.openSurface) openUsed++;
            }
            if (ok) { planOk = true; lastOpenCap = openLimit; lastAttempts = attempt + 1; return; }
        }
        UseSafePlan();
    }

    void UseSafePlan()
    {
        // 사전에 검증된 안전 배치: 저장된 (아이템, 지점) 쌍을 그대로 사용
        if (safeItems != null && safePoints != null && safeItems.Count == safePoints.Count && safeItems.Count > 0)
        {
            jobs = new List<Job>(); var pl = new List<ItemSpawnPoint>();
            for (int i = 0; i < safeItems.Count; i++)
            {
                if (safeItems[i] == null || safePoints[i] == null) continue;
                jobs.Add(new Job { item = safeItems[i], kind = SpawnAllow.None }); pl.Add(safePoints[i]);
            }
            plan = pl.ToArray(); planOk = jobs.Count > 0; lastAttempts = -1;
            if (planOk) { Debug.LogWarning("[RandomItemSpawner] 무작위 배치 실패 - 저장된 안전 배치를 사용합니다."); return; }
        }
        Debug.LogError("[RandomItemSpawner] 배치 계획과 안전 배치 모두 사용할 수 없습니다 (아이템은 옮기지 않음).");
    }

#if UNITY_EDITOR
    /// <summary>현재 후보/제약으로 무작위 계획을 하나 만들어 안전 배치로 저장한다 (에디터에서 컨텍스트 메뉴로 실행).</summary>
    [ContextMenu("Bake Safe Plan")]
    public void BakeSafePlan()
    {
        Collect(out var eligible);
        var keepSafe = safeItems; safeItems = null;   // 베이크 중에는 폴백을 쓰지 않는다
        BuildPlan(eligible);
        if (!planOk || lastAttempts < 0) { safeItems = keepSafe; Debug.LogError("[RandomItemSpawner] 안전 배치를 만들지 못했습니다."); return; }
        safeItems = new List<Component>(); safePoints = new List<ItemSpawnPoint>();
        for (int i = 0; i < jobs.Count; i++) { safeItems.Add(jobs[i].item); safePoints.Add(plan[i]); }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[RandomItemSpawner] 안전 배치 저장: " + safeItems.Count + "개");
    }
#endif

    /// <summary>지점이 속한 개폐부(서랍/문). 가구/루트에 매달린 지점은 null (그런 지점끼리는 '같은 개폐부'가 아니다).</summary>
    static Transform PartOf(ItemSpawnPoint p)
    {
        var par = p.transform.parent;
        return par != null && par.GetComponent<OpenablePart>() != null ? par : null;
    }

    int ZoneCount(ItemSpawnPoint p) => zoneCount.TryGetValue(ZoneOf(p), out int v) ? v : 0;

    void Apply()
    {
        for (int i = 0; i < jobs.Count; i++)
        {
            Place(jobs[i].item, plan[i]);
            report += jobs[i].item.name + "->" + ZoneOf(plan[i]) + " ";
        }
        if (logResult) Debug.Log("[RandomItemSpawner] plan applied: " + jobs.Count + " items, openCap=" + lastOpenCap + ", zones=" + zoneCount.Count);
    }

    public IReadOnlyList<Job> PlanJobs => jobs;
    public ItemSpawnPoint[] Plan => plan;
    public string Zone(ItemSpawnPoint p) => ZoneOf(p);
    public bool IsFixedRoot(Component r) => fixedRoots.Contains(r);

    // 구역 = 후보 지점끼리 clusterRadius 이내로 이어진 무리 (같은 방의 가구는 같은 구역).
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

    /// <summary>지점이 속한 수납 가구 루트. 부모 체인에 없으면(루트에 매달린 지점) 지점을 감싸는 가구/약장의 렌더러 범위로 찾는다. 열린 면이면 null.</summary>
    public Component RootOf(ItemSpawnPoint p)
    {
        if (rootCache.TryGetValue(p, out var c)) return c;
        c = p.GetComponentInParent<OpenableFurniture>(true);
        if (c == null) c = p.GetComponentInParent<DoorPair>(true);
        if (c == null)
        {
            Vector3 pos = p.transform.position + Vector3.up * 0.1f;
            float best = float.MaxValue;
            foreach (var r in roots)
            {
                if (r == null) continue;
                var rs = r.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds; foreach (var q in rs) b.Encapsulate(q.bounds);
                b.Expand(0.1f);
                if (!b.Contains(pos)) continue;
                float d = (b.center - pos).sqrMagnitude;
                if (d < best) { best = d; c = r; }
            }
        }
        rootCache[p] = c;
        return c;
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
