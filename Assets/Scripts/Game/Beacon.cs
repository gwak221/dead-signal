using UnityEngine;

/// <summary>
/// control_panel_00X 비콘. 배터리를 가지고 E → 배터리를 삽입구에 고정하고 활성화.
/// 기존 PlayerInteraction의 IInteractable 경로를 그대로 사용한다.
/// </summary>
public class Beacon : MonoBehaviour, IInteractable
{
    [Header("Visual")]
    public Transform batterySlot;
    public Light statusLight;

    [Header("Escape wall (비어 있으면 씬에서 자동 탐색)")]
    public EscapeDoorController escapeWall;

    PlayerInventory inv;

    public bool IsActivated { get; private set; }

    void OnEnable() => GameProgress.RegisterBeacon();

    void Start() => ApplyVisual();

    public string GetInteractionText()
    {
        if (IsActivated) return "Beacon Online";
        if (inv == null) inv = FindFirstObjectByType<PlayerInventory>();
        return inv != null && inv.HasItem(InventoryItemType.Battery)
            ? "E : Insert Battery"
            : "Battery Required";
    }

    public void Interact(PlayerInteraction player)
    {
        if (IsActivated || player == null || player.Inventory == null) return;

        if (!player.Inventory.TryTakeItem(InventoryItemType.Battery, out GameObject battery))
        {
            player.Notify("Battery Required");
            return;
        }

        Insert(battery);
        IsActivated = true;
        ApplyVisual();
        GameProgress.ReportBeaconActivated();

        // 팀의 EscapeDoorController: 표시등 1개 켜기 (비콘은 IsActivated로 한 번만 도달하므로 중복 증가 없음)
        if (escapeWall == null) escapeWall = FindFirstObjectByType<EscapeDoorController>();
        if (escapeWall != null) escapeWall.ActivateNext();
        player.Notify("Beacon Activated  " + GameProgress.ActiveBeacons + " / " + GameProgress.RequiredBeacons, 1.5f);
    }

    void Insert(GameObject battery)
    {
        if (battery == null) return;

        var pickup = battery.GetComponent<BatteryPickup>();
        if (pickup != null) Destroy(pickup);
        var rb = battery.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        foreach (var c in battery.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        Transform slot = batterySlot != null ? batterySlot : transform;
        battery.transform.SetParent(slot, false);
        battery.transform.localPosition = Vector3.zero;
        battery.transform.localRotation = Quaternion.identity;
        battery.SetActive(true);
    }

    void ApplyVisual()
    {
        // 팀 원본 디스플레이 모델은 건드리지 않는다. 활성 표시는 상태등만 사용.
        if (statusLight != null) statusLight.enabled = IsActivated;
    }
}
