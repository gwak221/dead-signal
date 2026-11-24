using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum InventoryItemType
{
    None,
    Battery,
    Key,
    Flashlight, // 기존 값 순서 유지를 위해 맨 뒤에 추가
    Medkit,
    Syringe     // 맨 뒤에 추가 (기존 직렬화 값 유지)
}

public class PlayerInventory : MonoBehaviour
{
    const int SlotCount = 4;
    /// <summary>6시(아래) 칸. 다른 칸의 70% 크기. 손전등 AA 개수만 보여주고, 아이템은 들어가지 않는다.</summary>
    const int BatterySlot = 3;
    const float AaGemScale = 0.7f;

    const float GemSize = 64f;
    const float GemGap = 4f;
    const float ClusterMargin = 16f;
    const float ClusterPad = 8f;

    /// <summary>
    /// 엘든링형 슬롯 뭉치가 끝나는 높이 (1280x720 DesignRoot, 아래 기준).
    /// 손전등 잔량 라벨이 이 위에 앉도록 FlashlightChargesUI 가 읽는다.
    /// </summary>
    public static float HudClusterTop
    {
        get
        {
            MeasureCluster(out _, out float parentH, out _, out _, out _, out _);
            return ClusterMargin + parentH;
        }
    }

    class SlotData
    {
        public InventoryItemType type = InventoryItemType.None;
        public GameObject worldObject;
    }

    static SlotData[] NewSlots()
    {
        var created = new SlotData[SlotCount];
        for (int i = 0; i < created.Length; i++)
            created[i] = new SlotData();
        return created;
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

    readonly SlotData[] slots = NewSlots();
    readonly Image[] slotImages = new Image[SlotCount];
    readonly TMP_Text[] slotTexts = new TMP_Text[SlotCount];
    readonly Image[] slotIcons = new Image[SlotCount];
    readonly TMP_Text[] slotIndexTexts = new TMP_Text[SlotCount];
    readonly bool[] lockedSlots = new bool[SlotCount];
    readonly GameObject[] lockOverlays = new GameObject[SlotCount];
    bool hudLaidOut;
    FlashlightBatteryStock aaStock;

    static readonly Color FrameIdle = new Color(0.055f, 0.05f, 0.045f, 1f);
    static readonly Color FrameSelected = new Color(0.96f, 0.80f, 0.42f, 1f);
    static readonly Color FrameStocked = new Color(0.86f, 0.82f, 0.72f, 1f);
    static readonly Color PlateSelected = new Color(0.11f, 0.10f, 0.09f, 1f);
    static readonly Color PlateStocked = new Color(0.08f, 0.074f, 0.066f, 1f);
    static readonly Color TextIdle = new Color(0.86f, 0.82f, 0.72f, 1f);
    static readonly Color IndexColor = new Color(0.80f, 0.76f, 0.64f, 0.92f);

    PlayerInteraction interaction;
    int selectedSlot = -1;

    // 사운드용 이벤트 (플레이어가 직접 한 동작만 알린다)
    /// <summary>1/2/3/0 키로 슬롯을 선택(true)하거나 해제(false)했을 때.</summary>
    public event System.Action<bool> OnSlotToggled;
    /// <summary>아이템을 인벤토리에 넣었을 때.</summary>
    public event System.Action<InventoryItemType> OnItemAdded;

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
        Transform slot = inventorySlotsRoot.GetChild(index);
        Transform gem = slot.Find("Gem");
        return gem != null ? gem : slot;
    }

    void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
    }

    void Start()
    {
        aaStock = FlashlightBatteryStock.GetOrAdd(gameObject);
        aaStock.OnCountChanged += OnAaCountChanged;
        ResolveHoldPoint();
        ResolveUI();
        RefreshHeldVisuals();
        RefreshUI();
    }

    void OnDestroy()
    {
        if (aaStock != null)
            aaStock.OnCountChanged -= OnAaCountChanged;
    }

    void OnAaCountChanged(int _) => RefreshUI();

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
        {
            bool had = selectedSlot >= 0;
            SelectBareHands();
            if (had) OnSlotToggled?.Invoke(false);
        }

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
        OnItemAdded?.Invoke(type);
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

    void ToggleSlot(int index)
    {
        if (index < 0 || index >= SlotCount || index == BatterySlot)
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
            OnSlotToggled?.Invoke(false);
            return;
        }

        // 빈 칸은 선택하지 않고 맨손으로 둔다.
        if (slots[index].type == InventoryItemType.None)
        {
            bool had = selectedSlot >= 0;
            SelectBareHands();
            if (had) OnSlotToggled?.Invoke(false);
            return;
        }

        selectedSlot = index;
        RefreshHeldVisuals();
        RefreshUI();
        HeldItemOutline.Flash(slots[index].worldObject);   // 꺼낸 아이템에 외곽선 → 2초에 걸쳐 사라짐
        OnSlotToggled?.Invoke(true);
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
        else if (type == InventoryItemType.Medkit)
        {
            // 의료상자: 손에 든 모습이 화면을 가리지 않도록 약간 아래/앞에, 크기를 줄여서.
            item.transform.localPosition = new Vector3(0.02f, -0.14f, 0.16f);
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = savedScale * 0.5f;
        }
        else if (type == InventoryItemType.Syringe)
        {
            // 주사기: 화면 오른쪽 아래에 눕혀서, 손에 든 동안만 키워서 든다 (자세/배율은 SyringePickup 에서 조정)
            SyringePickup sp = item.GetComponent<SyringePickup>();
            if (sp != null) sp.CaptureScale();
            item.transform.localPosition = sp != null ? sp.holdLocalPosition : new Vector3(0.07f, -0.09f, 0.2f);
            item.transform.localRotation = Quaternion.Euler(sp != null ? sp.holdLocalEuler : new Vector3(75f, 0f, 0f));
            item.transform.localScale = sp != null ? sp.worldScale * sp.holdScale : savedScale;
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

        MedkitPickup medkit = item.GetComponent<MedkitPickup>();
        if (medkit != null) medkit.enabled = false;

        SyringePickup syringe = item.GetComponent<SyringePickup>();
        if (syringe != null) syringe.enabled = false;
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
        else if (type == InventoryItemType.Medkit)
        {
            MedkitPickup medkit = item.GetComponent<MedkitPickup>();
            if (medkit == null) medkit = item.AddComponent<MedkitPickup>();
            medkit.enabled = true;
            item.transform.localScale = medkit.worldScale;
        }
        else if (type == InventoryItemType.Syringe)
        {
            SyringePickup syringe = item.GetComponent<SyringePickup>();
            if (syringe == null) syringe = item.AddComponent<SyringePickup>();
            syringe.enabled = true;
            item.transform.localScale = syringe.worldScale;
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
        {
            if (i == BatterySlot)
                continue;
            if (IsSlotOpen(i))
                return i;
        }

        return -1;
    }

    bool IsSlotOpen(int index)
    {
        return index >= 0 && index < SlotCount
            && !lockedSlots[index]
            && slots[index].type == InventoryItemType.None;
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

        if (!hudLaidOut)
        {
            LayoutEldenRing();
            hudLaidOut = true;
        }

        for (int i = 0; i < SlotCount && i < inventorySlotsRoot.childCount; i++)
        {
            Transform slot = inventorySlotsRoot.GetChild(i);
            Transform gem = slot.Find("Gem");
            slotImages[i] = gem != null ? gem.GetComponent<Image>() : slot.GetComponent<Image>();

            Transform item = slot.Find("ItemLabel");
            slotTexts[i] = item != null ? item.GetComponent<TMP_Text>() : null;

            Transform index = slot.Find("Index");
            slotIndexTexts[i] = index != null ? index.GetComponent<TMP_Text>() : null;

            slotIcons[i] = i == BatterySlot ? null : EnsureIcon((RectTransform)slot);
        }
    }

    /// <summary>슬롯 가운데 아이템 아이콘 (마름모 안쪽 정사각형 크기, 회전 없음). 아이콘은 RecoveredItemLog 의 기록 아이콘과 같은 것.</summary>
    static Image EnsureIcon(RectTransform slot)
    {
        Transform existing = slot.Find("ItemIcon");
        Image image;
        if (existing == null)
        {
            var go = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            go.layer = slot.gameObject.layer;
            go.transform.SetParent(slot, false);
            Transform gem = slot.Find("Gem");
            go.transform.SetSiblingIndex(gem != null ? gem.GetSiblingIndex() + 1 : 0);   // 틀 위, 번호/라벨 아래
            image = go.GetComponent<Image>();
        }
        else
        {
            image = existing.GetComponent<Image>();
            if (image == null) image = existing.gameObject.AddComponent<Image>();
        }

        var rt = image.rectTransform;
        float size = GemSize * 0.66f;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    /// <summary>슬롯 아이콘 (기록 아이콘 재사용). 아이콘이 없는 종류(Key)는 null → 글자 라벨로 표시.</summary>
    static Sprite IconFor(InventoryItemType type) => type switch
    {
        InventoryItemType.Battery => RecoveredItemLog.ItemGlyph(0),     // 비콘 배터리
        InventoryItemType.Medkit => RecoveredItemLog.ItemGlyph(1),      // 구급상자
        InventoryItemType.Syringe => RecoveredItemLog.ItemGlyph(2),     // 주사기
        InventoryItemType.Flashlight => RecoveredItemLog.ItemGlyph(3),  // 손전등
        _ => null
    };

    /// <summary>
    /// 칸을 45도 돌려 마름모로 붙인다.
    /// 1 = 9시, 2 = 12시, 3 = 3시, 4 = 6시(70% 크기, 손전등 AA 개수).
    /// </summary>
    void LayoutEldenRing()
    {
        HorizontalLayoutGroup row = inventorySlotsRoot.GetComponent<HorizontalLayoutGroup>();
        if (row != null)
        {
            row.enabled = false;
            Destroy(row);
        }

        if (inventorySlotsRoot.childCount == 0)
            return;

        while (inventorySlotsRoot.childCount < SlotCount)
        {
            GameObject clone = Instantiate(inventorySlotsRoot.GetChild(0).gameObject, inventorySlotsRoot);
            clone.name = "Slot_AA";
        }

        MeasureCluster(out float parentW, out float parentH, out float radius, out float batteryY, out float originX, out float originY);

        var root = (RectTransform)inventorySlotsRoot;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(ClusterMargin, ClusterMargin);
        root.sizeDelta = new Vector2(parentW, parentH);
        root.localScale = Vector3.one;

        Image rootImage = root.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = false;
        }

        string[] names = { "Slot_1", "Slot_2", "Slot_3", "Slot_AA" };
        Vector2 origin = new Vector2(originX, originY);
        Vector2[] positions =
        {
            origin + new Vector2(-radius, 0f),
            origin + new Vector2(0f, radius),
            origin + new Vector2(radius, 0f),
            origin + new Vector2(0f, batteryY),
        };

        Sprite frame = RoughFrame();
        for (int i = 0; i < SlotCount; i++)
        {
            var slot = (RectTransform)inventorySlotsRoot.GetChild(i);
            slot.gameObject.name = names[i];
            slot.anchorMin = slot.anchorMax = Vector2.zero;
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.localScale = Vector3.one;
            slot.localRotation = Quaternion.identity;

            bool aa = i == BatterySlot;
            float gem = aa ? GemSize * AaGemScale : GemSize;
            float box = gem * Mathf.Sqrt(2f);
            slot.sizeDelta = new Vector2(box, box);
            slot.anchoredPosition = positions[i];

            Image slotImage = slot.GetComponent<Image>();
            if (slotImage != null)
            {
                slotImage.enabled = false;
                slotImage.raycastTarget = false;
            }

            Image gemImage = EnsureGem(slot, gem, frame);
            gemImage.color = FrameIdle;

            TMP_Text label = null;
            foreach (TMP_Text text in slot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.gameObject.name == "Index")
                    continue;
                label = text;
                break;
            }

            if (label != null)
            {
                label.gameObject.name = "ItemLabel";
                label.enableAutoSizing = true;
                label.fontSizeMin = aa ? 12f : 11f;
                label.fontSizeMax = aa ? 22f : 18f;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                label.color = TextIdle;
                var textRt = label.rectTransform;
                textRt.anchorMin = textRt.anchorMax = new Vector2(0.5f, 0.5f);
                textRt.pivot = new Vector2(0.5f, 0.5f);
                textRt.sizeDelta = new Vector2(gem * 0.72f, gem * 0.42f);
                textRt.anchoredPosition = Vector2.zero;
                textRt.localRotation = Quaternion.identity;
                textRt.localScale = Vector3.one;
            }

            EnsureIndex(slot, i, label);
        }
    }

    static void MeasureCluster(out float parentW, out float parentH, out float radius, out float batteryY, out float originX, out float originY)
    {
        float fullReach = GemSize / Mathf.Sqrt(2f);
        float batteryReach = (GemSize * AaGemScale) / Mathf.Sqrt(2f);
        radius = (GemSize + GemGap) / Mathf.Sqrt(2f);
        batteryY = radius - fullReach - GemGap - batteryReach;

        float side = radius + fullReach;
        float below = -batteryY + batteryReach;
        parentW = side * 2f + ClusterPad * 2f;
        parentH = side + below + ClusterPad * 2f;
        originX = ClusterPad + side;
        originY = ClusterPad + below;
    }

    static Image EnsureGem(RectTransform slot, float gemSize, Sprite frame)
    {
        Transform existing = slot.Find("Gem");
        RectTransform gemRt;
        Image image;
        if (existing == null)
        {
            var go = new GameObject("Gem", typeof(RectTransform));
            go.layer = slot.gameObject.layer;
            go.transform.SetParent(slot, false);
            go.transform.SetAsFirstSibling();
            gemRt = (RectTransform)go.transform;
            image = go.AddComponent<Image>();
        }
        else
        {
            gemRt = (RectTransform)existing;
            image = existing.GetComponent<Image>();
            if (image == null)
                image = existing.gameObject.AddComponent<Image>();
        }

        gemRt.anchorMin = gemRt.anchorMax = new Vector2(0.5f, 0.5f);
        gemRt.pivot = new Vector2(0.5f, 0.5f);
        gemRt.sizeDelta = new Vector2(gemSize, gemSize);
        gemRt.anchoredPosition = Vector2.zero;
        gemRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        gemRt.localScale = Vector3.one;
        image.sprite = frame;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    static void EnsureIndex(RectTransform slot, int index, TMP_Text fontSource)
    {
        Transform existing = slot.Find("Index");
        TextMeshProUGUI label;
        if (existing == null)
        {
            var go = new GameObject("Index", typeof(RectTransform));
            go.layer = slot.gameObject.layer;
            go.transform.SetParent(slot, false);
            label = go.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            label = existing.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }

        if (fontSource != null && fontSource.font != null)
            label.font = fontSource.font;

        bool aa = index == BatterySlot;
        label.text = (index + 1).ToString();
        label.fontSize = aa ? 11f : 13f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = IndexColor;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = aa ? new Vector2(0.32f, 0.68f) : new Vector2(0.26f, 0.74f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = aa ? new Vector2(16f, 12f) : new Vector2(22f, 16f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
    }

    static Sprite RoughFrame() => HudRoughSprite.Get(128, 128);

    void RefreshUI()
    {
        // UI가 늦게 생성된 경우 한 번 더 찾는다.
        if (inventorySlotsRoot == null)
            ResolveUI();

        for (int i = 0; i < SlotCount; i++)
        {
            bool aaSlot = i == BatterySlot;
            bool selected = i == selectedSlot;
            int aaCount = aaStock != null ? aaStock.Count : 0;

            Sprite icon = aaSlot ? null : IconFor(slots[i].type);
            if (slotIcons[i] != null)
            {
                slotIcons[i].sprite = icon;
                slotIcons[i].enabled = icon != null;
                slotIcons[i].color = lockedSlots[i] ? new Color(0.45f, 0.42f, 0.38f, 0.55f)
                                   : selected ? FrameSelected : TextIdle;
            }

            if (slotTexts[i] != null)
            {
                if (aaSlot)
                    slotTexts[i].text = "x" + aaCount;
                else if (icon != null && slotIcons[i] != null)
                    slotTexts[i].text = "";   // 아이콘이 있으면 글자 라벨은 숨긴다
                else
                {
                    slotTexts[i].text = slots[i].type switch
                    {
                        InventoryItemType.Battery => "BAT",
                        InventoryItemType.Key => "KEY",
                        InventoryItemType.Flashlight => "LIGHT",
                        InventoryItemType.Medkit => "MED",
                        InventoryItemType.Syringe => "SYR",
                        _ => ""
                    };
                }

                if (lockedSlots[i])
                    slotTexts[i].color = new Color(0.45f, 0.42f, 0.38f, 0.55f);
                else if (aaSlot && aaCount > 0)
                    slotTexts[i].color = FrameStocked;
                else
                    slotTexts[i].color = selected ? FrameSelected : TextIdle;
            }

            if (slotIndexTexts[i] != null)
                slotIndexTexts[i].color = lockedSlots[i] ? new Color(0.4f, 0.38f, 0.34f, 0.55f) : IndexColor;

            if (slotImages[i] != null)
            {
                if (lockedSlots[i])
                    slotImages[i].color = lockedSlotTint;
                else if (selected)
                    slotImages[i].color = PlateSelected;
                else if (aaSlot && aaCount > 0)
                    slotImages[i].color = PlateStocked;
                else
                    slotImages[i].color = FrameIdle;
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
    /// 2) 빈 슬롯이 없으면 플레이어 앞 바닥에 떨어뜨린다 (다시 F로 주울 수 있음).
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
            int target = FindFirstEmptySlot(); // 잠긴 슬롯과 6시 AA 칸은 제외
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

            Transform host = inventorySlotsRoot.GetChild(i);
            Transform gem = host.Find("Gem");
            if (gem != null)
                host = gem;
            lockOverlays[i] = CreateLockOverlay(host);
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

        // Gem 이 이미 45도 돌아가 있으므로, 막대는 로컬 0/90 이어야 화면에서 X가 된다.
        bool diamond = Mathf.Abs(Mathf.DeltaAngle(slot.localEulerAngles.z, 45f)) < 1f;
        CreateXBar(rt, diamond ? 0f : 45f);
        CreateXBar(rt, diamond ? 90f : -45f);
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
