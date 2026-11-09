using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonsterSpawnPoints 부모에 붙는다. 자식 Transform = 몬스터 스폰 후보.
/// 게임 시작 시 몬스터가 서 있는 지점(가장 가까운 자식)을 Initial로 기록하고,
/// 첫 피격 후 재배치 때는 현재/최초 지점을 제외하고 PlayerSpawn에서 충분히 먼 지점을 고른다.
/// </summary>
public class MonsterSpawnPoints : MonoBehaviour
{
    public float minDistanceFromPlayer = 25f;

    Transform initial, current;

    public static MonsterSpawnPoints Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        var m = FindFirstObjectByType<MonsterChase>();
        if (m != null) initial = current = Nearest(m.transform.position);
    }

    Transform Nearest(Vector3 p)
    {
        Transform best = null; float bd = float.MaxValue;
        foreach (Transform c in transform)
        {
            float d = (c.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    /// <summary>현재/최초 지점 제외, playerPos에서 minDistance 이상이며 시야가 막힌 지점 중 무작위. 없으면 가장 먼 지점.</summary>
    public bool RelocateMonster(MonsterChase monster, Vector3 playerPos)
    {
        if (monster == null || transform.childCount == 0) return false;
        if (current == null) current = Nearest(monster.transform.position);
        if (initial == null) initial = current;

        var good = new List<Transform>(); Transform farthest = null; float fd = -1f;
        Vector3 eye = playerPos + Vector3.up * 1.6f;
        foreach (Transform c in transform)
        {
            if (c == current || c == initial) continue;
            float d = Vector3.Distance(c.position, playerPos);
            if (d > fd) { fd = d; farthest = c; }
            if (d >= minDistanceFromPlayer && Physics.Linecast(eye, c.position + Vector3.up, ~0, QueryTriggerInteraction.Ignore))
                good.Add(c);
        }
        Transform pick = good.Count > 0 ? good[Random.Range(0, good.Count)] : farthest;
        if (pick == null) return false;
        current = pick;
        monster.Relocate(pick.position, pick.rotation);
        Debug.Log("[MonsterSpawnPoints] Monster -> " + pick.name + " " + pick.position);
        return true;
    }
}
