using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum InventoryItemType
{
    None,
    Battery,
    Key,
    Flashlight // 기존 값 순서 유지를 위해 맨 뒤에 추가
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

    [Header("Locked slot (arm penalty)")]
    [Tooltip("비워두면 기본 X UI를 코드로 생성. 나중에 피 묻은 X 프리팹을 넣으면 그걸 사용.")]
    [SerializeField] GameObject lockedSlotOverlayPrefab;
    [SerializeField] Color lockedSlotTint = new Color(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] Color lockedXColor = new Color(0.85f, 0.12f, 0.12f, 0.95f);
    [SerializeField] float lockedXThickness = 6f;

    readonly SlotData[] slots = { new SlotData(), new SlotData(), new SlotData() };
    readonly Image[] slotImages = new Image[SlotCount];
    readonly TMP_Text[] slotTexts = new TMP_Text[SlotCount];
    readonly Color[] normalSlotColors = new Color[SlotCount];
    readonly bool[] lockedSlots = new bool[SlotCount];
    readonly GameObject[] lockOverlays = new GameObject[SlotCount];

    PlayerInteraction interaction;
    int selectedSlot = -1;

    public int SelectedSlotIndex => selectedSlot;
    public InventoryItemType SelectedItemType => selectedSlot >= 0 ? slots[selectedSlot].type : InventoryItemType.None;
    public bool HasSelectedItem => SelectedItemType != InventoryItemType.None;
    public int BatteryCount => Count(InventoryItemType.Battery);
    public bool HasKey => Count(InventoryItemType.Key) > 0;
    public int TotalSlotCount => SlotCount;
    public GameObject SelectedWorldObject => selectedSlot >= 0 ? slots[selectedSlot].worldObject : null;

    public InventoryItemType GetSlotType(int index)
    {
        return index >= 0 && index < SlotCount ? slots[index].type : InventoryItemType.None;
    }

    public GameObject GetSlotWorldObject(int index)
    {
        return index >= 0 && index < SlotCount ? slots[index].worldObject : null;
    }

    /// <summary>해당 타입이 들어있는 첫 슬롯 번호 (없으면 -1)</summary>
    public int FindSlotOf(InventoryItemType type)
    {
        for (int i = 0; i < SlotCount; i++)
            if (slots[i].type == type)
                return i;
        return -1;
    }

    /// <summary>슬롯 UI(Transform). 쿨타임 표시 등 슬롯 위에 UI를 얹을 때 사용.</summary>
    public Transform GetSlotUI(int index)
    {
        if (inventorySlotsRoot == null)
            ResolveUI();
        if (inventorySlotsRoot == null || index < 0 || index >= SlotCount || index >= inventorySlotsRoot.childCount)
            return null;
        return inventorySlotsRoot.GetChild(index);
    }

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

    /// <summary>
    /// 해당 타입 아이템을 슬롯에서 꺼내 반환한다 (파괴하지 않음). 선택 중인 슬롯을 우선.
    /// 비콘 삽입처럼 월드 오브젝트를 재사용할 때 사용.
    /// </summary>
    public bool TryTakeItem(InventoryItemType type, out GameObject worldObject)
    {
        worldObject = null;
        int idx = selectedSlot >= 0 && slots[selectedSlot].type == type ? selectedSlot : FindSlotOf(type);
        if (idx < 0)
            return false;

        worldObject = slots[idx].worldObject;
        slots[idx].type = InventoryItemType.None;
        slots[idx].worldObject = null;
        if (selectedSlot == idx)
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

        // 팔 파손으로 잠긴 슬롯은 선택 불가. 현재 선택 상태는 그대로 유지.
        if (lockedSlots[index])
        {
            if (interaction != null)
                interaction.Notify("Slot " + (index + 1) + " Locked");
            return;
        }

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

        FlashlightItem flashlightItem = type == InventoryItemType.Flashlight ? item.GetComponent<FlashlightItem>() : null;

        if (flashlightItem != null)
        {
            // 손전등: 1인칭 화면 오른쪽 아래, 정면을 향하도록.
            item.transform.localPosition = flashlightItem.holdLocalPosition;
            item.transform.localRotation = Quaternion.Euler(flashlightItem.holdLocalEuler);
        }
        else if (type == InventoryItemType.Key)
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
        else if (type == InventoryItemType.Flashlight)
        {
            // FlashlightItem은 쿨타임을 보관하므로 비활성화하지 않는다 (Collider만 위에서 다시 켜짐).
            FlashlightItem flashlight = item.GetComponent<FlashlightItem>();
            if (flashlight == null) flashlight = item.AddComponent<FlashlightItem>();
            flashlight.enabled = true;
        }
    }

    int FindFirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (!lockedSlots[i] && slots[i].type == InventoryItemType.None)
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
                    InventoryItemType.Flashlight => "LIGHT",
                    _ => "-"
                };

                slotTexts[i].text = (i + 1) + "\n" + itemText;
            }

            if (slotImages[i] != null)
            {
                if (lockedSlots[i])
                    slotImages[i].color = lockedSlotTint;
                else
                    slotImages[i].color = i == selectedSlot
                        ? new Color(0.72f, 0.68f, 0.22f, 1f)
                        : normalSlotColors[i];
            }

            RefreshLockOverlay(i);
        }
    }

    // ───────────────────────── 슬롯 잠금 (팔 파손) ─────────────────────────

    public bool IsSlotLocked(int index)
    {
        return index >= 0 && index < SlotCount && lockedSlots[index];
    }

    /// <summary>
    /// 슬롯 잠금/해제. 잠글 때 그 슬롯에 아이템이 있으면
    /// 1) 비어있는 다른 슬롯으로 옮기고 (장착 중이었다면 장착 유지),
    /// 2) 빈 슬롯이 없으면 플레이어 앞 바닥에 떨어뜨린다 (다시 E로 주울 수 있음).
    /// 아이템이 사라지는 경우는 없다.
    /// </summary>
    public void SetSlotLocked(int index, bool locked)
    {
        if (index < 0 || index >= SlotCount)
            return;

        if (lockedSlots[index] == locked)
            return;

        lockedSlots[index] = locked;

        if (locked && slots[index].type != InventoryItemType.None)
        {
            int target = FindFirstEmptySlot(); // 잠긴 슬롯은 자동 제외
            if (target >= 0)
            {
                slots[target].type = slots[index].type;
                slots[target].worldObject = slots[index].worldObject;
                slots[index].type = InventoryItemType.None;
                slots[index].worldObject = null;

                if (selectedSlot == index)
                    selectedSlot = target;

                if (interaction != null)
                    interaction.Notify("Item moved to Slot " + (target + 1), 1.4f);
            }
            else
            {
                EjectSlotItemToWorld(index);

                if (interaction != null)
                    interaction.Notify("Arm Injured : Item Dropped", 1.4f);
            }
        }
        else if (locked && selectedSlot == index)
        {
            // 빈 슬롯이 선택되어 있던 예외 상황 방어.
            selectedSlot = -1;
        }

        RefreshHeldVisuals();
        RefreshUI();
    }

    void EjectSlotItemToWorld(int index)
    {
        GameObject item = slots[index].worldObject;
        InventoryItemType type = slots[index].type;

        slots[index].type = InventoryItemType.None;
        slots[index].worldObject = null;

        if (selectedSlot == index)
            selectedSlot = -1;

        if (item == null)
            return;

        Vector3 forward = transform.forward;
        Vector3 eye = transform.position + Vector3.up * 1.2f;

        if (interaction != null && interaction.playerCamera != null)
            eye = interaction.playerCamera.transform.position;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        // 벽 속에 생성되지 않도록 앞쪽을 한 번 검사.
        Vector3 origin = eye + forward * 0.6f - Vector3.up * 0.4f;
        if (Physics.Raycast(eye, forward, out RaycastHit hit, 0.9f, ~0, QueryTriggerInteraction.Ignore))
            origin = hit.point - forward * 0.25f - Vector3.up * 0.4f;

        item.SetActive(true);
        item.transform.SetParent(null, true);
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
        rb.AddForce(forward * 0.8f + Vector3.up * 0.4f, ForceMode.VelocityChange);
    }

    void RefreshLockOverlay(int i)
    {
        bool locked = lockedSlots[i];

        if (lockOverlays[i] == null)
        {
            if (!locked || inventorySlotsRoot == null || i >= inventorySlotsRoot.childCount)
                return;

            lockOverlays[i] = CreateLockOverlay(inventorySlotsRoot.GetChild(i));
        }

        lockOverlays[i].SetActive(locked);
        if (locked)
            lockOverlays[i].transform.SetAsLastSibling(); // 숫자/아이템 텍스트 위에 표시
    }

    GameObject CreateLockOverlay(Transform slot)
    {
        if (lockedSlotOverlayPrefab != null)
        {
            GameObject custom = Instantiate(lockedSlotOverlayPrefab, slot, false);
            custom.name = "LockedSlotOverlay";
            if (custom.transform is RectTransform crt)
                Stretch(crt);
            return custom;
        }

        // 기본 X UI: 반투명 어두운 배경 + 대각선 막대 2개.
        var root = new GameObject("LockedSlotOverlay", typeof(RectTransform));
        root.layer = slot.gameObject.layer;
        var rt = (RectTransform)root.transform;
        rt.SetParent(slot, false);
        Stretch(rt);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = false;

        CreateXBar(rt, 45f);
        CreateXBar(rt, -45f);
        return root;
    }

    void CreateXBar(RectTransform parent, float angle)
    {
        var bar = new GameObject("XBar", typeof(RectTransform));
        bar.layer = parent.gameObject.layer;
        var rt = (RectTransform)bar.transform;
        rt.SetParent(parent, false);

        // 가로로 슬롯 폭만큼 늘린 막대를 회전 → 해상도와 무관하게 슬롯 크기에 맞는 X.
        rt.anchorMin = new Vector2(0.1f, 0.5f);
        rt.anchorMax = new Vector2(0.9f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, lockedXThickness);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        rt.localScale = new Vector3(1.15f, 1f, 1f);

        var img = bar.AddComponent<Image>();
        img.color = lockedXColor;
        img.raycastTarget = false;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }
}
