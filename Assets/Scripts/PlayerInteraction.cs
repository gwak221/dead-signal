using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    public Transform keyHoldPoint;
    public Camera playerCamera;
    public float interactionDistance = 3f;
    public TMP_Text interactionText;

    PlayerInventory inventory;
    HoldInteractable heldDevice;
    bool interactionEnabled = true;
    float messageUntil;
    string temporaryMessage;

    public bool InteractionEnabled => interactionEnabled;
    public PlayerInventory Inventory => inventory;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = gameObject.AddComponent<PlayerInventory>();

        if (keyHoldPoint != null)
            inventory.SetHoldPoint(keyHoldPoint);
    }

    void Update()
    {
        if (!interactionEnabled)
        {
            HideInteractionText();
            return;
        }

        if (Time.time < messageUntil)
        {
            ShowMessage(temporaryMessage);
            return;
        }

        CheckInteraction();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (!enabled)
        {
            ResetHold();
            HideInteractionText();
        }
    }

    public void Notify(string message, float seconds = 0.9f)
    {
        temporaryMessage = message;
        messageUntil = Time.time + seconds;
        ShowMessage(message);
    }

    // 예전 SafeDoor/씬과의 호환용. 현재 인벤토리 시스템에서는 별도 키 플래그를 사용하지 않는다.
    public void RegisterHeldKey(GameObject _) { }
    public void HideHeldKeyVisual()
    {
        if (inventory != null && inventory.IsSelected(InventoryItemType.Key))
            inventory.SelectBareHands();
    }

    void ResetHold()
    {
        if (heldDevice != null)
            heldDevice.ResetHold();
        heldDevice = null;
    }

    void OnDisable()
    {
        ResetHold();
    }

    void ShowMessage(string message)
    {
        if (interactionText == null)
            return;

        interactionText.text = message;
        interactionText.gameObject.SetActive(true);
    }

    void CheckInteraction()
    {
        if (playerCamera == null)
        {
            ResetHold();
            HideInteractionText();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            var kb = Keyboard.current;
            var battery = hit.collider.GetComponentInParent<BatteryPickup>();
            var device = hit.collider.GetComponentInParent<HoldInteractable>();

            if (battery != null)
            {
                ResetHold();
                ShowMessage("E : Pick Up Battery");

                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    if (!battery.Collect(this))
                        Notify("Inventory Full");
                }
                return;
            }

            if (heldDevice != device)
                ResetHold();

            if (device != null)
            {
                if (device.IsActivated)
                {
                    ShowMessage("Complete : " + device.DisplayName);
                    return;
                }

                if (device.NeedsBattery)
                {
                    if (inventory != null && inventory.IsSelected(InventoryItemType.Battery))
                    {
                        ShowMessage("E : Insert Battery");

                        if (kb != null && kb.eKey.wasPressedThisFrame && device.TryInsertBattery())
                            inventory.TryConsumeSelected(InventoryItemType.Battery);
                    }
                    else
                    {
                        ShowMessage("Equip Battery (1-3)");
                    }
                    return;
                }

                if (kb != null && kb.eKey.isPressed)
                {
                    heldDevice = device;
                    device.AddHoldTime(Time.deltaTime);
                }
                else
                {
                    ResetHold();
                }

                ShowMessage("Hold E : " + device.DisplayName + " " + device.HoldProgressPercent.ToString("0") + "%");
                return;
            }

            ResetHold();
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                string message = interactable.GetInteractionText();

                if (interactionText != null)
                {
                    interactionText.text = message;
                    interactionText.gameObject.SetActive(!string.IsNullOrEmpty(message));
                }

                if (kb != null && kb.eKey.wasPressedThisFrame)
                    interactable.Interact(this);

                return;
            }
        }

        ResetHold();
        HideInteractionText();
    }

    void HideInteractionText()
    {
        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }
}
