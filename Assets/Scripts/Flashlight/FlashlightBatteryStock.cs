using System;
using UnityEngine;

/// <summary>
/// 플레이어가 보유한 "손전등 교체용 AA 배터리" 개수. (Beacon 용 큰 Battery / PlayerInventory 슬롯과 완전히 별개)
/// 플레이어 오브젝트에 붙으며, 씬 Retry/새 게임(씬 재로드) 시 새로 만들어져 0 으로 시작한다.
/// 이번 단계에서는 개수만 저장 — 손전등 StunCharges 는 건드리지 않는다 (R키 재장전은 다음 작업).
/// </summary>
[DisallowMultipleComponent]
public class FlashlightBatteryStock : MonoBehaviour
{
    public int Count { get; private set; }
    public event Action<int> OnCountChanged;

    public void Add(int n = 1)
    {
        if (n <= 0) return;
        Count += n;
        OnCountChanged?.Invoke(Count);
    }

    /// <summary>다음 작업(R키 재장전)에서 사용할 소모 함수. 없으면 false.</summary>
    public bool TryConsume(int n = 1)
    {
        if (n <= 0 || Count < n) return false;
        Count -= n;
        OnCountChanged?.Invoke(Count);
        return true;
    }

    public static FlashlightBatteryStock GetOrAdd(GameObject player)
    {
        var s = player.GetComponent<FlashlightBatteryStock>();
        return s != null ? s : player.AddComponent<FlashlightBatteryStock>();
    }
}
