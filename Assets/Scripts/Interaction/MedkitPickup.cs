using UnityEngine;

/// <summary>
/// 의료상자(Medkit). F 로 줍고(인벤토리 1/2/3 슬롯), 슬롯에서 꺼낸 뒤 E 로 사용한다.
/// 사용 효과는 새 체력 시스템 없이 기존 PlayerBodyStatus(Eye/Arm/Leg 패널티 = 하트 UI/Sprint/슬롯 잠금)만 되돌린다.
/// 이미 완전히 건강하면 소비하지 않는다.
/// </summary>
public class MedkitPickup : MonoBehaviour, IInteractable, IPickupable
{
    /// <summary>손에 들기 전 원래 월드 스케일 (손에서 내려놓을 때 복원).</summary>
    [System.NonSerialized] public Vector3 worldScale = Vector3.one;

    bool scaleCaptured;

    void Awake() => CaptureScale();

    void CaptureScale()
    {
        if (scaleCaptured) return;
        scaleCaptured = true;
        worldScale = transform.localScale;
    }

    public string GetInteractionText() => "[F] PICK UP MEDKIT";

    public void Interact(PlayerInteraction player)
    {
        if (player == null || player.Inventory == null) return;
        CaptureScale();
        if (!player.Inventory.TryAddWorldItem(InventoryItemType.Medkit, gameObject))
            player.Notify("INVENTORY FULL - SELECT AN ITEM, PRESS [G] TO DROP", 1.6f);
    }

    /// <summary>선택 중인 Medkit 을 사용. 건강하면 소비하지 않고 false.</summary>
    public static bool TryUseSelected(PlayerInteraction player)
    {
        if (player == null || player.Inventory == null || !player.Inventory.IsSelected(InventoryItemType.Medkit)) return false;
        var body = player.GetComponent<PlayerBodyStatus>();
        if (body == null) body = FindFirstObjectByType<PlayerBodyStatus>();
        if (body == null) return false;

        if (body.DamagedCount == 0)
        {
            player.Notify("HEALTH FULL");
            return false;
        }

        player.Inventory.TryConsumeSelected(InventoryItemType.Medkit);
        body.ClearAllPenalties();   // Eye / Arm / Leg 패널티 해제 → 하트 ♥♥, Sprint/슬롯 잠금 복구
        ItemUseScreenEffect.Pulse(ItemUseScreenEffect.MedkitColor);
        player.Notify("MEDKIT USED - HEALTH RESTORED", 1.4f);
        return true;
    }
}
