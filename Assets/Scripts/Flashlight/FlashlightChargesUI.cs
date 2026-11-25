using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손전등 잔량. 글자 % 대신 배터리 아이콘 4칸 (한 칸 = 25%).
///  - 0% 빈 칸, 1~25% 1칸, 26~50% 2칸, 51~75% 3칸, 76~100% 4칸
///  - 0% 이면 테두리가 흐린 붉은색. 우클릭 점등 시도는 토스트
///  - 재장전 중에는 테두리가 깜빡인다
///  - 손전등을 가진 뒤에만 보인다. 사람 모양 아래, 비콘 칸 밑
/// </summary>
[DisallowMultipleComponent]
public class FlashlightChargesUI : MonoBehaviour
{
    public Vector2 anchoredPosition = new Vector2(28f, 244f);
    public Color normalColor = new Color(0.36f, 0.36f, 0.38f, 0.96f);
    public Color emptyColor = new Color(0.22f, 0.22f, 0.24f, 0.92f);
    public Color emptyCell = new Color(0.07f, 0.07f, 0.08f, 0.95f);
    public Color frameColor = new Color(0.05f, 0.045f, 0.04f, 1f);
    public Color frameEmpty = new Color(0.05f, 0.045f, 0.04f, 1f);

    FlashlightController fc;
    PlayerInventory inventory;
    RectTransform root;
    Image shell;
    Image nub;
    readonly Image[] cells = new Image[4];
    int shownBars = -2;
    bool shownEmpty;
    public const string ReloadHintText = "[R] RELOAD BATTERY";
    public const string EmptyHintText = "BATTERY EMPTY - PRESS R TO RELOAD";

    const float BodyW = 96f;
    const float BodyH = 54f;
    const float Border = 5f;
    const float NubW = 8f;
    const float NubH = 16f;

    void Awake()
    {
        fc = GetComponent<FlashlightController>();
        inventory = GetComponent<PlayerInventory>();
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

    void OnEmptyHint() => HudToast.Show("FlashlightEmpty", EmptyHintText, fc != null ? fc.emptyHintSeconds : 1.6f, 150f, 1);

    void LateUpdate()
    {
        if (fc == null) return;
        if (inventory == null) inventory = GetComponent<PlayerInventory>();

        bool has = inventory != null && inventory.HasItem(InventoryItemType.Flashlight);
        if (!has)
        {
            if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }

        // 0% + 여분 AA 보유 + 손전등 장착 상태에서만 재장전 안내 (AA 없거나 재장전 시작/완료 시 갱신이 멈춰 0.25초 내 사라짐).
        if (fc.CanReloadNow) HudToast.Show("ReloadHint", ReloadHintText, 0.25f, 110f, 2);

        if (root == null && !Build()) return;
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
        PlaceUnderBody();
        Refresh();
        UpdateCooldown();

        if (shell == null) return;
        Color frame = shownEmpty ? frameEmpty : frameColor;
        if (fc.IsReloading)
        {
            float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f));
            frame = Color.Lerp(frameEmpty, frameColor, pulse);
        }
        shell.color = frame;
        if (nub != null) nub.color = shownEmpty ? emptyColor : normalColor;
    }

    void Refresh()
    {
        if (root == null || fc == null) return;

        int pct = Mathf.Clamp(Mathf.RoundToInt(fc.BatteryPercent), 0, 100);
        if (pct == 0 && fc.BatteryPercent > 0f) pct = 1;
        int bars = pct <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(pct / 25f), 1, 4);
        bool empty = pct <= 0 && !fc.IsReloading;
        if (bars == shownBars && empty == shownEmpty) return;
        shownBars = bars;
        shownEmpty = empty;

        Color ink = empty ? emptyColor : normalColor;
        if (shell != null) shell.color = empty ? frameEmpty : frameColor;
        if (nub != null) nub.color = ink;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null) continue;
            cells[i].color = i < bars ? ink : emptyCell;
        }
    }

    // ───────────────── 눈뽕 재사용 대기시간 (배터리 게이지 바로 아래) ─────────────────
    RectTransform cdRoot;
    Image cdFill;
    TextMeshProUGUI cdLabel;
    bool cdWasCooling;
    float cdReadyUntil;
    static readonly Color CdBarColor = new Color(0.86f, 0.78f, 0.52f, 0.95f);
    static readonly Color CdReadyColor = new Color(0.55f, 0.85f, 0.55f, 0.95f);

    void BuildCooldown()
    {
        var go = new GameObject("FlashStunCooldown", typeof(RectTransform));
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root, false);
        cdRoot = (RectTransform)go.transform;
        // 배터리 게이지(root)의 아래 가장자리에 붙여서 게이지가 이동/스케일되면 함께 움직인다 (같은 앵커 체계 → 해상도/비율 무관).
        cdRoot.anchorMin = cdRoot.anchorMax = new Vector2(1f, 0f);
        cdRoot.pivot = new Vector2(1f, 1f);
        cdRoot.anchoredPosition = new Vector2(0f, -6f);
        cdRoot.sizeDelta = new Vector2(BodyW + NubW, 34f);

        var quad = WhiteQuad();
        var bg = MakeImage(cdRoot, "BarBg", quad, new Color(0.05f, 0.045f, 0.04f, 0.94f), new Vector2(0f, 0f), new Vector2(BodyW + NubW, 9f), Image.Type.Simple);
        cdFill = MakeImage(cdRoot, "BarFill", quad, CdBarColor, new Vector2(2f, 2f), new Vector2(BodyW + NubW - 4f, 5f), Image.Type.Simple);

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.layer = root.gameObject.layer;
        tgo.transform.SetParent(cdRoot, false);
        cdLabel = tgo.AddComponent<TextMeshProUGUI>();
        TMP_Text existing = root.parent != null ? root.parent.GetComponentInChildren<TMP_Text>(true) : null;
        if (existing != null && existing != cdLabel && existing.font != null) cdLabel.font = existing.font;
        cdLabel.fontSize = 19f;
        cdLabel.fontStyle = FontStyles.Bold;
        cdLabel.alignment = TextAlignmentOptions.Right;
        cdLabel.textWrappingMode = TextWrappingModes.NoWrap;
        cdLabel.raycastTarget = false;
        cdLabel.outlineWidth = 0.2f;
        cdLabel.outlineColor = new Color32(0, 0, 0, 255);
        var rt = cdLabel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(0f, 11f);
        rt.sizeDelta = new Vector2(BodyW + NubW, 22f);
        cdRoot.gameObject.SetActive(false);
    }

    void UpdateCooldown()
    {
        FlashlightItem item = null;
        if (inventory != null)
        {
            int slot = inventory.FindSlotOf(InventoryItemType.Flashlight);
            GameObject obj = slot >= 0 ? inventory.GetSlotWorldObject(slot) : null;
            item = obj != null ? obj.GetComponent<FlashlightItem>() : null;
        }

        bool cooling = item != null && item.IsOnCooldown;
        if (cooling)
        {
            if (cdRoot == null) BuildCooldown();
            cdWasCooling = true;
            cdRoot.gameObject.SetActive(true);
            cdFill.color = CdBarColor;
            cdFill.rectTransform.sizeDelta = new Vector2((BodyW + NubW - 4f) * (1f - item.CooldownNormalized), 5f);
            cdLabel.color = CdBarColor;
            cdLabel.text = "FLASH " + Mathf.Max(0.1f, Mathf.Ceil(item.CooldownRemaining * 10f) / 10f).ToString("0.0") + "s";
            return;
        }

        if (cdWasCooling)
        {
            cdWasCooling = false;
            cdReadyUntil = Time.unscaledTime + 1.2f;   // 끝났음을 잠깐 알려준 뒤 숨김
        }
        if (cdRoot == null) return;
        if (Time.unscaledTime < cdReadyUntil)
        {
            cdRoot.gameObject.SetActive(true);
            cdFill.color = CdReadyColor;
            cdFill.rectTransform.sizeDelta = new Vector2(BodyW + NubW - 4f, 5f);
            cdLabel.color = CdReadyColor;
            cdLabel.text = "FLASH READY";
        }
        else if (cdRoot.gameObject.activeSelf) cdRoot.gameObject.SetActive(false);
    }

    bool Build()
    {
        RectTransform parent = FindHudRoot();
        if (parent == null) return false;

        var go = new GameObject("FlashlightBattery", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        root = (RectTransform)go.transform;
        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.sizeDelta = new Vector2(BodyW + NubW, BodyH);
        PlaceUnderBody();

        Sprite round = RoundedRect();
        Sprite quad = WhiteQuad();
        Sprite rough = HudRoughSprite.Get((int)BodyW * 2, (int)BodyH * 2);

        shell = MakeImage(root, "Shell", rough, frameColor, Vector2.zero, new Vector2(BodyW, BodyH), Image.Type.Simple);
        var inner = MakeImage(root, "Inner", quad, new Color(0.05f, 0.045f, 0.04f, 0.94f),
            new Vector2(Border, Border), new Vector2(BodyW - Border * 2f, BodyH - Border * 2f), Image.Type.Simple);
        inner.transform.SetAsLastSibling();

        float nubY = (BodyH - NubH) * 0.5f;
        nub = MakeImage(root, "Nub", round, normalColor, new Vector2(BodyW - 1f, nubY), new Vector2(NubW + 1f, NubH), Image.Type.Simple);

        float innerX = Border + 4f;
        float innerY = Border + 4f;
        float innerW = BodyW - Border * 2f - 8f;
        float innerH = BodyH - Border * 2f - 8f;
        const float gap = 4f;
        float cellW = (innerW - gap * 3f) / 4f;
        for (int i = 0; i < 4; i++)
        {
            float x = innerX + i * (cellW + gap);
            cells[i] = MakeImage(root, "Cell" + i, quad, emptyCell, new Vector2(x, innerY), new Vector2(cellW, innerH), Image.Type.Simple);
        }

        shownBars = -2;
        return true;
    }

    static Image MakeImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size, Image.Type type)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Sprite roundSprite;
    static Sprite whiteSprite;

    static Sprite RoundedRect()
    {
        if (roundSprite != null) return roundSprite;
        const int n = 48;
        const int radius = 7;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool inside = InRound(x + 0.5f, y + 0.5f, 0.5f, 0.5f, n - 0.5f, n - 0.5f, radius);
                px[y * n + x] = inside ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        roundSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        roundSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundSprite;
    }

    static bool InRound(float x, float y, float x0, float y0, float x1, float y1, float r)
    {
        float cx = Mathf.Clamp(x, x0 + r, x1 - r);
        float cy = Mathf.Clamp(y, y0 + r, y1 - r);
        float dx = x - cx;
        float dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    }

    static Sprite WhiteQuad()
    {
        if (whiteSprite != null) return whiteSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }

    void PlaceUnderBody()
    {
        if (root == null) return;
        var hrGo = GameObject.Find("HeartRateUI");
        if (hrGo == null) return;
        var hr = (RectTransform)hrGo.transform;
        if (root.parent != hr.parent)
            root.SetParent(hr.parent, false);

        root.anchorMin = root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        float right = hr.anchoredPosition.x + (1f - hr.pivot.x) * hr.sizeDelta.x;
        float ringBottom = hr.anchoredPosition.y - hr.pivot.y * hr.sizeDelta.y;

        var beacon = GameObject.Find("BeaconHud");
        float top = ringBottom - 8f;
        if (beacon != null)
        {
            var br = (RectTransform)beacon.transform;
            top = br.anchoredPosition.y - br.pivot.y * br.sizeDelta.y - 6f;
        }
        root.anchoredPosition = new Vector2(right, top);
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
