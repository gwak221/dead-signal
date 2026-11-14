using UnityEngine;

/// <summary>
/// 약장(med_cabinet) 안에 놓이는 손전등용 AA 배터리. E 키로 줍는다. (Beacon 용 BatteryPickup 과 별개 클래스)
/// 약장 문(DoorPair)이 닫혀 있으면 프롬프트도 줍기도 불가 (벽/몸통 너머로 줍는 것 방지).
/// 기존 PlayerInteraction 의 IInteractable 경로를 그대로 사용 → PlayerInteraction 수정 없음.
/// </summary>
public class FlashlightBatteryPickup : MonoBehaviour, IInteractable
{
    [Tooltip("이 배터리가 들어 있는 약장의 문 세트. 닫혀 있으면 주울 수 없다. 비워두면 항상 주울 수 있음.")]
    public DoorPair cabinetDoor;
    public string pickupText = "[E] PICK UP BATTERY";

    bool collected;

    bool CabinetOpen => cabinetDoor == null || cabinetDoor.GetInteractionText() == cabinetDoor.closeText;

    public string GetInteractionText() => (!collected && CabinetOpen) ? pickupText : "";

    public void Interact(PlayerInteraction player)
    {
        if (collected || player == null || !CabinetOpen) return;
        collected = true;
        FlashlightBatteryStock.GetOrAdd(player.gameObject).Add(1);
        Destroy(gameObject);
    }
}
