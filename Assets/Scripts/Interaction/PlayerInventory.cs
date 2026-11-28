using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum InventoryItemType
{
    None,
    Battery,
    Key
}

public class PlayerInventory : MonoBehaviour
{
    const int SlotCount = 3;

    class SlotData
    {
        public InventoryItemType type = InventoryItemType.None;
        public GameObject worldObject;
    }

    [Header("Held item")]
    [SerializeField] Transform holdPoint;
    [SerializeField] float throwForce = 4.5f;
    [SerializeField] float throwUpForce = 1.25f;

    [Header("UI (auto-found if empty)")]
    [SerializeField] Transform inventorySlotsRoot;

    readonly SlotData[] slots = { new SlotData(), new SlotData(), new SlotData() };
    readonly Image[] slotImages = new Image[SlotCount];
    readonly TMP_Text[] slotTexts = new TMP_Text[SlotCount];
    readonly Color[] normalSlotColors = new Color[SlotCount];

    PlayerInteraction interaction;
    int selectedSlot = -1;

    public int SelectedSlotIndex => selectedSlot;
    public InventoryItemType SelectedItemType => selectedSlot >= 0 ? slots[selectedSlot].type : InventoryItemType.None;
    public bool HasSelectedItem => SelectedItemType != InventoryItemType.None;
    public int BatteryCount => Count(InventoryItemType.Battery);
    public bool HasKey => Count(InventoryItemType.Key) > 0;

    void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
    }

    void Start()
    {
        ResolveHoldPoint();
        ResolveUI();
        RefreshHeldVisuals();
        RefreshUI();
    }

    void Update()
    {
        // Password UI 등으로 플레이어 조작이 잠긴 동안 인벤토리 단축키도 잠근다.
        if (interaction != null && !interaction.InteractionEnabled)
            return;

        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) ToggleSlot(0);
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) ToggleSlot(1);
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) ToggleSlot(2);

        if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame)
            SelectBareHands();

        if (kb.fKey.wasPressedThisFrame)
            DropSelectedItem();
    }

    public void SetHoldPoint(Transform point)
    {
        if (point == null)
            return;

        holdPoint = point;
        RefreshHeldVisuals();
    }

    public bool TryAddWorldItem(InventoryItemType type, GameObject worldObject)
    {
        if (type == InventoryItemType.None || worldObject == null)
            return false;

        int empty = FindFirstEmptySlot();
        if (empty < 0)
            return false;

        ResolveHoldPoint();
        if (holdPoint == null)
            return false;

        PrepareForInventory(worldObject, type);
        slots[empty].type = type;
        slots[empty].worldObject = worldObject;

        // 습득만 하고 자동 장착하지 않는다. 1/2/3으로 직접 꺼낸다.
        worldObject.SetActive(false);
        RefreshUI();
        return true;
    }

    public bool IsSelected(InventoryItemType type)
    {
        return SelectedItemType == type;
    }

    public bool HasItem(InventoryItemType type)
    {
        return Count(type) > 0;
    }

    public bool TryConsumeSelected(InventoryItemType requiredType)
    {
        if (selectedSlot < 0 || slots[selectedSlot].type != requiredType)
            return false;

        GameObject visual = slots[selectedSlot].worldObject;
        if (visual != null)
            Destroy(visual);

        slots[selectedSlot].type = InventoryItemType.None;
        slots[selectedSlot].worldObject = null;
        selectedSlot = -1;

        RefreshHeldVisuals();
        RefreshUI();
        return true;
    }

    public void SelectBareHands()
    {
        selectedSlot = -1;
        RefreshHeldVisuals();
        RefreshUI();
    }

    public bool DropSelectedItem()
    {
        if (selectedSlot < 0)
            return false;

        SlotData slot = slots[selectedSlot];
        if (slot.type == InventoryItemType.None || slot.worldObject == null)
        {
            SelectBareHands();
            return false;
        }

        GameObject item = slot.worldObject;
        InventoryItemType type = slot.type;

        slots[selectedSlot].type = InventoryItemType.None;
        slots[selectedSlot].worldObject = null;
        selectedSlot = -1;

        item.SetActive(true);
        item.transform.SetParent(null, true);

        Vector3 forward = transform.forward;
        Vector3 origin = transform.position + Vector3.up * 1.25f + forward * 1.0f;

        if (interaction != null && interaction.playerCamera != null)
        {
            forward = interaction.playerCamera.transform.forward;
            origin = interaction.playerCamera.transform.position + forward * 1.0f - interaction.playerCamera.transform.up * 0.15f;
        }

        item.transform.position = origin;

        RestorePickupComponents(item, type);

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null)
            rb = item.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.detectCollisions = true;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(forward * throwForce + Vector3.up * throwUpForce, ForceMode.VelocityChange);

        RefreshHeldVisuals();
        RefreshUI();
        return true;
    }

    void ToggleSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
            return;

        // 같은 번호를 한 번 더 누르면 맨손.
        if (selectedSlot == index)
        {
            SelectBareHands();
            return;
        }

        // 빈 칸은 선택하지 않고 맨손으로 둔다.
        if (slots[index].type == InventoryItemType.None)
        {
            SelectBareHands();
            return;
        }

        selectedSlot = index;
        RefreshHeldVisuals();
        RefreshUI();
    }

    void ResolveHoldPoint()
    {
        if (holdPoint != null)
            return;

        if (interaction == null)
            interaction = GetComponent<PlayerInteraction>();

        if (interaction != null && interaction.keyHoldPoint != null)
            holdPoint = interaction.keyHoldPoint;
    }

    void PrepareForInventory(GameObject item, InventoryItemType type)
    {
        Vector3 savedScale = item.transform.localScale;

        item.transform.SetParent(holdPoint, false);
        item.transform.localScale = savedScale;

        if (type == InventoryItemType.Key)
        {
            // 기존 KeyHoldPoint가 키에 맞춰져 있으므로 그대로 사용.
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
        }
        else
        {
            // 배터리는 손에서 세로로 보이도록 약간 회전.
            item.transform.localPosition = new Vector3(0f, -0.08f, 0.08f);
            item.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        foreach (Collider col in item.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        KeyPickup key = item.GetComponent<KeyPickup>();
        if (key != null) key.enabled = false;

        BatteryPickup battery = item.GetComponent<BatteryPickup>();
        if (battery != null) battery.enabled = false;
    }

    void RestorePickupComponents(GameObject item, InventoryItemType type)
    {
        foreach (Collider col in item.GetComponentsInChildren<Collider>(true))
            col.enabled = true;

        if (type == InventoryItemType.Key)
        {
            KeyPickup key = item.GetComponent<KeyPickup>();
            if (key == null) key = item.AddComponent<KeyPickup>();
            key.enabled = true;
        }
        else if (type == InventoryItemType.Battery)
        {
            BatteryPickup battery = item.GetComponent<BatteryPickup>();
            if (battery == null) battery = item.AddComponent<BatteryPickup>();
            battery.enabled = true;
        }
    }

    int FindFirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (slots[i].type == InventoryItemType.None)
                return i;

        return -1;
    }

    int Count(InventoryItemType type)
    {
        int count = 0;
        for (int i = 0; i < SlotCount; i++)
            if (slots[i].type == type)
                count++;
        return count;
    }

    void RefreshHeldVisuals()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            GameObject obj = slots[i].worldObject;
            if (obj != null)
                obj.SetActive(i == selectedSlot && slots[i].type != InventoryItemType.None);
        }
    }

    void ResolveUI()
    {
        if (inventorySlotsRoot == null)
        {
            GameObject root = GameObject.Find("InventorySlots");
            if (root != null)
                inventorySlotsRoot = root.transform;
        }

        if (inventorySlotsRoot == null)
            return;

        for (int i = 0; i < SlotCount && i < inventorySlotsRoot.childCount; i++)
        {
            Transform slot = inventorySlotsRoot.GetChild(i);
            slotImages[i] = slot.GetComponent<Image>();
            if (slotImages[i] != null)
                normalSlotColors[i] = slotImages[i].color;

            slotTexts[i] = slot.GetComponentInChildren<TMP_Text>(true);
        }
    }

    void RefreshUI()
    {
        // UI가 늦게 생성된 경우 한 번 더 찾는다.
        if (inventorySlotsRoot == null)
            ResolveUI();

        for (int i = 0; i < SlotCount; i++)
        {
            if (slotTexts[i] != null)
            {
                string itemText = slots[i].type switch
                {
                    InventoryItemType.Battery => "BAT",
                    InventoryItemType.Key => "KEY",
                    _ => "-"
                };

                slotTexts[i].text = (i + 1) + "\n" + itemText;
            }

            if (slotImages[i] != null)
            {
                slotImages[i].color = i == selectedSlot
                    ? new Color(0.72f, 0.68f, 0.22f, 1f)
                    : normalSlotColors[i];
            }
        }
    }
}
