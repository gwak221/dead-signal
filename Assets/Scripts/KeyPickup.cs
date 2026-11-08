using UnityEngine;

public class KeyPickup : MonoBehaviour, IInteractable
{
    public string GetInteractionText()
    {
        return "E : Pick Up Key";
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
