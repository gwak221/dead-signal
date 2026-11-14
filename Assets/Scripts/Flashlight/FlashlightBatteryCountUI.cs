using TMPro;
using UnityEngine;

/// <summary>
/// "AA x0" — 보유 AA 배터리 개수. "STUN n/3" 라벨(24,96) 바로 위(24,126)에 작게 표시.
/// 손전등을 획득했거나 AA 를 1개 이상 가지고 있을 때만 보인다. FlashlightChargesUI 가 자동으로 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class FlashlightBatteryCountUI : MonoBehaviour
{
    public Vector2 anchoredPosition = new Vector2(24f, 126f);
    public float fontSize = 18f;
    public Color color = new Color(0.85f, 0.85f, 0.8f, 0.9f);

    FlashlightBatteryStock stock;
    PlayerInventory inventory;
    TextMeshProUGUI label;
    int shown = -1;

    void Awake()
    {
        stock = FlashlightBatteryStock.GetOrAdd(gameObject);
        inventory = GetComponent<PlayerInventory>();
    }

    void LateUpdate()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        bool show = stock.Count > 0 || (inventory != null && inventory.HasItem(InventoryItemType.Flashlight));
        if (!show)
        {
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
            return;
        }
        if (label == null && !Build()) return;
        if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
        if (shown != stock.Count)
        {
            shown = stock.Count;
            label.text = "AA x" + shown;
        }
    }

    bool Build()
    {
        RectTransform parent = FindHudRoot();
        if (parent == null) return false;
        var go = new GameObject("FlashlightAACount", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        label = go.AddComponent<TextMeshProUGUI>();
        TMP_Text existing = parent.GetComponentInChildren<TMP_Text>(true);
        if (existing != null && existing.font != null) label.font = existing.font;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(200f, 26f);
        shown = -1;
        return true;
    }

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
