using UnityEngine;
using System.Collections;

public class SafeDoor : MonoBehaviour, IInteractable
{
    public Transform doorPivot;
    public float openAngle = 90f;
    public float openSpeed = 2f;

    bool isOpen;
    bool isMoving;
    Quaternion closedRotation;
    Quaternion openRotation;

    void Start()
    {
        if (doorPivot == null)
            return;

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public string GetInteractionText()
    {
        if (isMoving)
            return "";

        return isOpen ? "[E] CLOSE" : "[E] OPEN";
    }

    public void Interact(PlayerInteraction player)
    {
        if (isMoving || doorPivot == null || player == null)
            return;

        // 키가 인벤토리에 있는 것만으로는 안 되고, 실제로 손에 들어야 한다.
        if (player.Inventory == null || !player.Inventory.IsSelected(InventoryItemType.Key))
        {
            player.Notify("Equip Key (1-3)");
            return;
        }

        StartCoroutine(ToggleDoor());
    }

    IEnumerator ToggleDoor()
    {
        isMoving = true;

        Quaternion startRotation = doorPivot.localRotation;
        Quaternion targetRotation = isOpen ? closedRotation : openRotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            doorPivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            yield return null;
        }

        doorPivot.localRotation = targetRotation;
        isOpen = !isOpen;
        isMoving = false;
    }
}
