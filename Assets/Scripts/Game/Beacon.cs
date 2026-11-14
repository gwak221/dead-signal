using UnityEngine;

/// <summary>
/// control_panel_00X 비콘. 배터리를 가지고 E → QTE → 성공 시에만 배터리를 삽입구에 고정하고 활성화.
/// 기존 PlayerInteraction의 IInteractable 경로를 그대로 사용한다.
/// QTE 실패 시: 배터리/비콘/진행도/EscapeWall 모두 변화 없음 (E를 다시 눌러야 새 QTE).
/// </summary>
public class Beacon : MonoBehaviour, IInteractable
{
    [Header("Visual")]
    public Transform batterySlot;
    public Light statusLight;

    [Header("Escape wall (비어 있으면 씬에서 자동 탐색)")]
    public EscapeDoorController escapeWall;

    [Header("QTE (비어 있으면 씬에서 자동 탐색)")]
    public SkillCheckManager skillCheck;

    PlayerInventory inv;
    bool qteRunning;

    public bool IsActivated { get; private set; }

    void OnEnable() => GameProgress.RegisterBeacon();

    void Start() => ApplyVisual();

    public string GetInteractionText()
    {
        if (IsActivated) return "BEACON ONLINE";
        if (inv == null) inv = FindFirstObjectByType<PlayerInventory>();
        return inv != null && inv.HasItem(InventoryItemType.Battery)
            ? "[E] INSERT BATTERY"
            : "BATTERY REQUIRED";
    }

    public void Interact(PlayerInteraction player)
    {
        if (IsActivated || qteRunning || player == null || player.Inventory == null) return;

        // 배터리는 아직 소비하지 않는다 (보유 여부만 확인).
        if (!player.Inventory.HasItem(InventoryItemType.Battery))
        {
            player.Notify("BATTERY REQUIRED");
            return;
        }

        if (skillCheck == null) skillCheck = SkillCheckManager.Instance != null ? SkillCheckManager.Instance : FindFirstObjectByType<SkillCheckManager>();
        if (skillCheck == null)
        {
            Debug.LogWarning("[Beacon] SkillCheckManager가 씬에 없습니다.");
            return;
        }

        qteRunning = skillCheck.TriggerSkillCheck(() => OnQteSuccess(player), OnQteFail);
    }

    void OnQteFail()
    {
        qteRunning = false; // 진행 없음. 다시 E를 눌러야 새 QTE 시작.
    }

    void OnQteSuccess(PlayerInteraction player)
    {
        qteRunning = false;
        if (IsActivated || player == null || player.Inventory == null) return;
        ActivateWithBattery(player);
    }

    /// <summary>QTE 성공 후의 실제 활성화 처리 (기존 로직 그대로).</summary>
    void ActivateWithBattery(PlayerInteraction player)
    {
        if (!player.Inventory.TryTakeItem(InventoryItemType.Battery, out GameObject battery))
        {
            player.Notify("BATTERY REQUIRED");
            return;
        }

        Insert(battery);
        IsActivated = true;
        ApplyVisual();
        GameProgress.ReportBeaconActivated();

        // 팀의 EscapeDoorController: 표시등 1개 켜기 (IsActivated로 한 번만 도달하므로 중복 증가 없음)
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// [테스트 전용 — Editor / Development Build 에서만 컴파일] F9 디버그 완료.
    /// 배터리/QTE 없이, ActivateWithBattery 와 같은 진행 상태 갱신(IsActivated, 상태등, GameProgress, EscapeWall 표시등)만 수행한다.
    /// </summary>
    public bool DebugComplete()
    {
        if (IsActivated) return false;
        IsActivated = true;
        qteRunning = false;
        ApplyVisual();
        GameProgress.ReportBeaconActivated();
        if (escapeWall == null) escapeWall = FindFirstObjectByType<EscapeDoorController>();
        if (escapeWall != null) escapeWall.ActivateNext();
        return true;
    }
#endif

    void ApplyVisual()
    {
        // 팀 원본 디스플레이 모델은 건드리지 않는다. 활성 표시는 상태등만 사용.
        if (statusLight != null) statusLight.enabled = IsActivated;
    }
}
