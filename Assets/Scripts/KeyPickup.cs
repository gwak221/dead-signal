using UnityEngine;

public class KeyPickup : MonoBehaviour, IInteractable, IPickupable
{
    public string GetInteractionText()
    {
        return "[F] PICK UP KEY";
    }

    public void Interact(PlayerInteraction player)
    {
        if (player == null || player.Inventory == null)
            return;

        if (!player.Inventory.TryAddWorldItem(InventoryItemType.Key, gameObject))
        {
            player.Notify("Inventory Full");
            return;
        }
    }
}
