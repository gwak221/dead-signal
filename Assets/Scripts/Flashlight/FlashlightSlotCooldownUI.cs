using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손전등이 들어있는 인벤토리 슬롯 위에 재사용 대기시간 표시.
///  - 어두운 반투명 Overlay (Fill 방식, 시간이 지날수록 줄어듦)
///  - 중앙에 남은 시간 숫자 (3.0 → 2.1 → 1.0)
///  - 쿨타임이 끝나면 숨김
///  - 다른 슬롯을 선택 중이어도 손전등 슬롯에 계속 표시
/// 기존 인벤토리 UI는 수정하지 않고, 손전등 슬롯 오브젝트 아래에 자식 UI만 하나 추가한다.
/// 손전등이 다른 슬롯으로 옮겨지면(팔 파손 등) Overlay도 따라 이동한다.
/// </summary>
[DisallowMultipleComponent]
public class FlashlightSlotCooldownUI : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public PlayerInventory inventory;

    [Header("Overlay")]
    public Color overlayColor = new Color(0f, 0f, 0f, 0.65f);
    public Image.FillMethod fillMethod = Image.FillMethod.Radial360;
    public bool fillClockwise = true;

    [Header("Text")]
    public bool showNumber = true;
    public float fontSize = 22f;
    public Color textColor = Color.white;

    GameObject root;
    Image fillImage;
    TextMeshProUGUI label;
    Transform currentSlot;
    static Sprite whiteSprite;

    void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
    }

    void LateUpdate()
    {
        if (inventory == null) { Hide(); return; }

        int slot = inventory.FindSlotOf(InventoryItemType.Flashlight);
        GameObject obj = slot >= 0 ? inventory.GetSlotWorldObject(slot) : null;
        FlashlightItem item = obj != null ? obj.GetComponent<FlashlightItem>() : null;

        if (item == null || !item.IsOnCooldown)
        {
            Hide();
            return;
        }

        Transform slotUI = inventory.GetSlotUI(slot);
        if (slotUI == null) { Hide(); return; }

        EnsureUI(slotUI);

        if (!root.activeSelf)
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling(); // 슬롯 번호/아이템 텍스트 위
        }

        fillImage.fillAmount = item.CooldownNormalized;

        if (label != null)
        {
            label.gameObject.SetActive(showNumber);
            label.text = Mathf.Max(0.1f, Mathf.Ceil(item.CooldownRemaining * 10f) / 10f).ToString("0.0");
        }
    }

    void Hide()
    {
        if (root != null && root.activeSelf)
            root.SetActive(false);
    }

    void EnsureUI(Transform slotUI)
    {
        if (root == null)
            Build(slotUI);

        if (currentSlot != slotUI)
        {
            currentSlot = slotUI;
            root.transform.SetParent(slotUI, false);
            Stretch((RectTransform)root.transform);
            root.transform.SetAsLastSibling();
        }
    }

    void Build(Transform slotUI)
    {
        root = new GameObject("FlashlightCooldownOverlay", typeof(RectTransform));
        root.layer = slotUI.gameObject.layer;
        root.transform.SetParent(slotUI, false);
        Stretch((RectTransform)root.transform);
        currentSlot = slotUI;

        // Fill은 스프라이트가 있어야 동작하므로 흰색 스프라이트를 런타임 생성.
        fillImage = root.AddComponent<Image>();
        fillImage.sprite = GetWhiteSprite();
        fillImage.color = overlayColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = fillMethod;
        fillImage.fillOrigin = fillMethod == Image.FillMethod.Vertical ? (int)Image.OriginVertical.Top
                             : fillMethod == Image.FillMethod.Horizontal ? (int)Image.OriginHorizontal.Left
                             : (int)Image.Origin360.Top;
        fillImage.fillClockwise = fillClockwise;
        fillImage.raycastTarget = false;

        var textGo = new GameObject("CooldownText", typeof(RectTransform));
        textGo.layer = root.layer;
        textGo.transform.SetParent(root.transform, false);
        Stretch((RectTransform)textGo.transform);

        label = textGo.AddComponent<TextMeshProUGUI>();
        TMP_Text existing = slotUI.GetComponentInChildren<TMP_Text>(true);
        if (existing != null && existing != label && existing.font != null)
            label.font = existing.font; // 기존 슬롯 폰트와 통일
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "";

        root.SetActive(false);
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

    static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }
}
