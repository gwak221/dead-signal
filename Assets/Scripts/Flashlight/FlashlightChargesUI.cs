using TMPro;
using UnityEngine;

/// <summary>
/// 손전등 배터리 % 표시 ("FLASHLIGHT 100%").
///  - 손전등을 획득한 뒤에만 표시 (인벤토리에 손전등이 없으면 숨김)
///  - 배터리 변화 시 FlashlightController.OnBatteryChanged 로 즉시 갱신 (정수 %)
///  - Reload 중 "RELOADING..." (R키 배터리 교체)
///  - 0% 이면 흐린 붉은색 (방전 — Light 점등 불가). 0% 에서 우클릭하면 "BATTERY EMPTY - PRESS R TO RELOAD" 토스트
///  - HUD 의 DesignRoot(1280x720 기준) 왼쪽 아래, 인벤토리 슬롯 바로 위에 배치 → Heart Rate / 비콘 HUD 와 겹치지 않음
/// FlashlightController 가 자동으로 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class FlashlightChargesUI : MonoBehaviour
{
    public Vector2 anchoredPosition = new Vector2(24f, 96f);   // 인벤토리 슬롯(24,24 / 높이 64) 바로 위
    public float fontSize = 22f;
    public Color normalColor = new Color(1f, 0.92f, 0.7f, 0.95f);
    public Color emptyColor = new Color(0.75f, 0.3f, 0.25f, 0.75f);

    FlashlightController fc;
    PlayerInventory inventory;
    TextMeshProUGUI label;
    int shown = -1;
    public const string EmptyHintText = "BATTERY EMPTY - PRESS R TO RELOAD";

    void Awake()
    {
        fc = GetComponent<FlashlightController>();
        inventory = GetComponent<PlayerInventory>();
        if (GetComponent<FlashlightBatteryCountUI>() == null) gameObject.AddComponent<FlashlightBatteryCountUI>(); // "AA xN" (보유 개수만 표시)
    }

    void OnEnable()
    {
        if (fc == null) fc = GetComponent<FlashlightController>();
        if (fc != null) { fc.OnBatteryChanged += OnChanged; fc.OnEmptyHint += OnEmptyHint; }
    }

    void OnDisable()
    {
        if (fc != null) { fc.OnBatteryChanged -= OnChanged; fc.OnEmptyHint -= OnEmptyHint; }
    }

    void OnChanged(float percent) => Refresh();

    void OnEmptyHint() => HudToast.Show("FlashlightEmpty", EmptyHintText, fc != null ? fc.emptyHintSeconds : 1.6f);

    void LateUpdate()
    {
        if (fc == null) return;
        if (inventory == null) inventory = GetComponent<PlayerInventory>();

        bool has = inventory != null && inventory.HasItem(InventoryItemType.Flashlight);
        if (!has)
        {
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
            return;
        }

        if (label == null && !Build()) return;
        if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
        Refresh();
    }

    void Refresh()
    {
        if (label == null || fc == null) return;
        int pct = Mathf.Clamp(Mathf.RoundToInt(fc.BatteryPercent), 0, 100);
        if (pct == 0 && fc.BatteryPercent > 0f) pct = 1; // 0% 는 실제로 방전됐을 때만 표시
        bool rl = fc.IsReloading;
        int key = pct + (rl ? 1000 : 0);
        if (key == shown) return;
        shown = key;
        label.text = rl ? "RELOADING..." : "FLASHLIGHT " + pct + "%";
        label.color = (pct <= 0 && !rl) ? emptyColor : normalColor;
    }

    bool Build()
    {
        RectTransform parent = FindHudRoot();
        if (parent == null) return false;

        var go = new GameObject("FlashlightStunCharges", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        label = go.AddComponent<TextMeshProUGUI>();
        TMP_Text existing = parent.GetComponentInChildren<TMP_Text>(true);
        if (existing != null && existing.font != null) label.font = existing.font;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = "";

        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(320f, 30f);
        shown = -1;
        return true;
    }

    // 기존 HUD 구조: Canvas / SafeArea / DesignRoot (1280x720 비율 유지)
    static RectTransform FindHudRoot()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            var safe = c.transform.Find("SafeArea");
            if (safe == null) continue;
            var design = safe.Find("DesignRoot");
            return (RectTransform)(design != null ? design : safe);
        }
        return null;
    }
}
