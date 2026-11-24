using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 일시정지 도감. 아이콘을 누르면 설명이 뜬다.
/// </summary>
public static class RecoveredItemLog
{
    struct Entry
    {
        public string title;
        public string code;
        public string body;
        public string quote;
        public string notes;
        public int icon;
    }

    static readonly Entry[] Items =
    {
        new Entry
        {
            title = "비콘 배터리",
            code = "Emergency Override Cell",
            body = "비상 봉쇄해제 비콘을 작동시킬 수 있는 유일한 동력원이다. 비콘에 장착하고 배선을 연결하면 탈출 비콘을 활성화할 수 있다.",
            quote = "무겁다. 그래도 내려놓을 수는 없다. 이곳을 나갈 방법은 이것뿐이니까….",
            notes = "[EOC-01] 시설 전력이 끊겼을 때를 대비해 비축된 비상용 셀. 승인 없이 탈거하면 경보가 울린다.",
            icon = 0
        },
        new Entry
        {
            title = "구급상자",
            code = "S-H-K_190881",
            body = "피해를 입은 신체 부위를 회복할 수 있는 도구다.",
            quote = "겉보기엔 평범한 구급상자다. 그런데 안에 든 건 내가 알던 것과 조금 다르다. 그래도 이곳에서 구할 수 있는 회복 도구는 이것뿐이다….",
            notes = "[Subject-Healing_Kit-190881]\n인체실험으로 변형된 신체에만 사용할 수 있다.\n일반 인체에 사용하면 회복 효과가 나타나지 않으며, 조직 이상 반응이 보고된 바 있다.",
            icon = 1
        },
        new Entry
        {
            title = "주사기",
            code = "S-A-S_2401",
            body = "신비로운 빛깔의 용액이 담긴 주사기다. 투여하면 일시적으로 활성 상태가 되어 심박수와 상관없이 달릴 수 있다.",
            quote = "바늘이 들어가는 순간 심장이 터질 듯 뛰었다. 그런데 숨이 차지 않는다. 이게… 정상인가?",
            notes = "[Subject Activation Serum_2401]\n(주의) 인간에게 사용하지 말 것.\n(주의) 사체에게 사용하지 말 것.\n(주의) 적정량 이상 투여 절대 금지.",
            icon = 2
        },
        new Entry
        {
            title = "손전등",
            code = "S.D.F  ·  Subject Deterrent Flashlight",
            body = "격리 실험체가 탈출했을 때 제압하기 위해 만들어진 손전등이다. 무슨 이유에서인지 일부가 파손되어 최대 성능을 발휘하지 못한다.",
            quote = "렌즈에 금이 가 있다. 불빛이 깜빡일 때마다 어둠 속의 무언가가 잠깐 멈칫한다.",
            notes = "전용 배터리(S.D.F Power Cell)로 충전할 것.\n사람에게 사용하지 말 것.\n격리 대응 요원은 반드시 1인 1지참을 원칙으로 한다.",
            icon = 3
        },
        new Entry
        {
            title = "손전등 배터리",
            code = "S.D.F Power Cell",
            body = "S.D.F를 충전하기 위해 만들어진 전용 배터리 셀이다. 일반 배터리와는 차원이 다른 전력량을 품고 있다.",
            quote = "손에 쥐자 미세하게 진동한다. 평범한 배터리가 아니다.",
            notes = "(주의) 이 배터리는 오직 S.D.F 재충전용으로 개발되었다.\nS.D.F 이외의 기기에 사용하면 과전력과 과충전으로 인한 문제가 발생할 수 있다.",
            icon = 4
        },
    };

    static readonly Color Hot = new Color(0.94f, 0.91f, 0.84f, 1f);
    static readonly Color Cold = new Color(0.72f, 0.70f, 0.64f, 1f);
    static readonly Color Plate = new Color(0.055f, 0.05f, 0.045f, 1f);
    static readonly Color Card = new Color(0.09f, 0.085f, 0.078f, 0.98f);

    static GameObject popup;
    static Text popupTitle;
    static Text popupCode;
    static Text popupBody;
    static Text popupQuote;
    static Text popupNotes;
    static LogIconButton[] icons;
    static int openIndex = -1;
    static Font osFont;
    static Sprite white;

    public static bool IsPopupOpen => popup != null && popup.activeSelf;

    public static void Build(RectTransform entries, Transform pausePanel)
    {
        EnsureFont();
        var preload = "";
        for (int i = 0; i < Items.Length; i++)
            preload += Items[i].title + Items[i].code + Items[i].body + Items[i].quote + Items[i].notes;
        Request(preload + "\"");

        icons = new LogIconButton[Items.Length];
        for (int i = 0; i < Items.Length; i++)
            icons[i] = MakeRow(entries, i);

        var root = new GameObject("ItemPopup", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(pausePanel, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var dim = root.GetComponent<Image>();
        dim.sprite = WhiteSprite();
        dim.color = new Color(0f, 0f, 0f, 0.45f);
        dim.raycastTarget = true;
        var close = root.GetComponent<Button>();
        close.targetGraphic = dim;
        close.transition = Selectable.Transition.None;
        close.navigation = new Navigation { mode = Navigation.Mode.None };
        close.onClick.AddListener(() => ClosePopup());
        popup = root;

        var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
        cardGo.transform.SetParent(root.transform, false);
        var card = (RectTransform)cardGo.transform;
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(740f, 500f);
        card.anchoredPosition = new Vector2(-150f, -8f);
        var cardImg = cardGo.GetComponent<Image>();
        cardImg.sprite = HudRoughSprite.Get(740, 500);
        cardImg.type = Image.Type.Simple;
        cardImg.color = Card;
        cardImg.raycastTarget = true;

        popupTitle = MakeText(card, "Title", 30, Hot, new Vector2(32f, -28f), new Vector2(676f, 40f), TextAnchor.UpperLeft);
        popupCode = MakeText(card, "Code", 18, new Color(0.62f, 0.60f, 0.55f, 1f), new Vector2(32f, -70f), new Vector2(676f, 28f), TextAnchor.UpperLeft);
        popupBody = MakeText(card, "Body", 22, Hot, new Vector2(32f, -112f), new Vector2(676f, 96f), TextAnchor.UpperLeft);
        popupQuote = MakeText(card, "Quote", 20, new Color(0.78f, 0.75f, 0.68f, 1f), new Vector2(32f, -214f), new Vector2(676f, 88f), TextAnchor.UpperLeft);
        popupQuote.fontStyle = FontStyle.Italic;
        popupNotes = MakeText(card, "Notes", 18, new Color(0.58f, 0.56f, 0.50f, 1f), new Vector2(32f, -310f), new Vector2(676f, 150f), TextAnchor.UpperLeft);

        root.SetActive(false);
    }

    public static bool ClosePopup()
    {
        if (!IsPopupOpen) return false;
        popup.SetActive(false);
        SetSelected(-1);
        return true;
    }

    static void Open(int index)
    {
        if (popup == null || index < 0 || index >= Items.Length) return;
        var e = Items[index];
        popupTitle.text = e.title;
        popupCode.text = e.code;
        popupBody.text = e.body;
        popupQuote.text = "\"" + e.quote + "\"";
        popupNotes.text = e.notes;
        Request(e.title + e.code + e.body + e.quote + e.notes);
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
        SetSelected(index);
    }

    static void SetSelected(int index)
    {
        openIndex = index;
        if (icons == null) return;
        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null) icons[i].SetSelected(i == index);
    }

    static LogIconButton MakeRow(RectTransform entries, int index)
    {
        var go = new GameObject("Item_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(entries, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(600f, 74f);
        rt.anchoredPosition = new Vector2(0f, -index * 76f);

        var hit = go.GetComponent<Image>();
        hit.sprite = WhiteSprite();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.None;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        int captured = index;
        btn.onClick.AddListener(() => Open(captured));

        var plateGo = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        plateGo.transform.SetParent(go.transform, false);
        var plate = (RectTransform)plateGo.transform;
        plate.anchorMin = plate.anchorMax = new Vector2(0f, 0.5f);
        plate.pivot = new Vector2(0f, 0.5f);
        plate.sizeDelta = new Vector2(58f, 58f);
        plate.anchoredPosition = new Vector2(0f, 0f);
        var plateImg = plateGo.GetComponent<Image>();
        plateImg.sprite = HudRoughSprite.Get(116, 116);
        plateImg.color = Plate;
        plateImg.raycastTarget = false;

        var glyphGo = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
        glyphGo.transform.SetParent(plateGo.transform, false);
        var gr = (RectTransform)glyphGo.transform;
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = new Vector2(8f, 8f);
        gr.offsetMax = new Vector2(-8f, -8f);
        var glyph = glyphGo.GetComponent<Image>();
        glyph.sprite = Glyph(Items[index].icon);
        glyph.color = Cold;
        glyph.preserveAspect = true;
        glyph.raycastTarget = false;

        var title = MakeText(rt, "Name", 22, Cold, new Vector2(74f, -8f), new Vector2(510f, 30f), TextAnchor.UpperLeft);
        title.text = Items[index].title;
        var code = MakeText(rt, "Code", 15, new Color(0.48f, 0.46f, 0.42f, 1f), new Vector2(74f, -36f), new Vector2(510f, 24f), TextAnchor.UpperLeft);
        code.text = Items[index].code;

        var hover = go.AddComponent<LogIconButton>();
        hover.glyph = glyph;
        hover.title = title;
        hover.code = code;
        return hover;
    }

    static Text MakeText(Transform parent, string name, int size, Color color, Vector2 pos, Vector2 sizeDelta, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = osFont != null ? osFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = false;
        t.raycastTarget = false;
        t.text = "";
        t.lineSpacing = 1.05f;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return t;
    }

    static void EnsureFont()
    {
        if (osFont != null) return;
        osFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Gulim", "굴림" }, 64);
    }

    static void Request(string value)
    {
        if (osFont == null || string.IsNullOrEmpty(value)) return;
        int[] sizes = { 15, 18, 20, 22, 30 };
        for (int i = 0; i < sizes.Length; i++)
        {
            osFont.RequestCharactersInTexture(value, sizes[i], FontStyle.Normal);
            osFont.RequestCharactersInTexture(value, sizes[i], FontStyle.Italic);
        }
    }

    static readonly System.Collections.Generic.Dictionary<int, Sprite> glyphCache = new System.Collections.Generic.Dictionary<int, Sprite>();

    /// <summary>기록 아이콘 (0 비콘 배터리, 1 구급상자, 2 주사기, 3 손전등, 4 손전등 배터리). 인벤토리 슬롯이 같은 아이콘을 쓴다. 캐시됨.</summary>
    public static Sprite ItemGlyph(int kind)
    {
        if (glyphCache.TryGetValue(kind, out var s) && s != null) return s;
        return glyphCache[kind] = Glyph(kind);
    }

    static Sprite Glyph(int kind)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        var px = new Color32[n * n];
        var ink = new Color32(255, 255, 255, 255);
        var clear = new Color32(0, 0, 0, 0);

        void Box(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if ((uint)x < n && (uint)y < n) px[y * n + x] = c;
        }

        void Disc(int cx, int cy, int r, Color32 c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if ((uint)x >= n || (uint)y >= n) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) px[y * n + x] = c;
            }
        }

        if (kind == 0)
        {
            Box(18, 6, 46, 36, ink);
            Box(26, 36, 38, 44, ink);
            Box(29, 44, 35, 52, ink);
            Disc(32, 56, 5, ink);
            Box(24, 14, 40, 28, clear);
            Disc(32, 21, 3, ink);
        }
        else if (kind == 1)
        {
            Box(12, 16, 52, 46, ink);
            Box(22, 46, 42, 56, ink);
            Box(29, 22, 35, 40, clear);
            Box(18, 28, 46, 34, clear);
        }
        else if (kind == 2)
        {
            Box(8, 28, 16, 36, ink);
            Box(16, 30, 24, 34, ink);
            Box(22, 22, 26, 42, ink);
            Box(24, 24, 48, 40, ink);
            Box(28, 28, 42, 36, clear);
            Box(48, 30, 61, 34, ink);
        }
        else if (kind == 3)
        {
            Box(6, 26, 34, 42, ink);
            Box(34, 18, 52, 50, ink);
            Box(50, 22, 58, 46, ink);
            Box(14, 26, 16, 42, clear);
            Box(22, 26, 24, 42, clear);
        }
        else
        {
            Box(8, 20, 48, 44, ink);
            Box(48, 26, 58, 38, ink);
            Box(14, 26, 20, 38, clear);
            Box(24, 26, 30, 38, clear);
            Box(34, 26, 40, 38, clear);
        }

        tex.SetPixels32(px);
        tex.Apply(false);
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    static Sprite WhiteSprite()
    {
        if (white != null) return white;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply(false);
        white = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        white.hideFlags = HideFlags.HideAndDontSave;
        return white;
    }
}

public class LogIconButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image glyph;
    public Text title;
    public Text code;
    public bool over;
    public bool selected;

    static readonly Color Hot = new Color(0.94f, 0.91f, 0.84f, 1f);
    static readonly Color Cold = new Color(0.72f, 0.70f, 0.64f, 1f);
    static readonly Color CodeHot = new Color(0.78f, 0.75f, 0.68f, 1f);
    static readonly Color CodeCold = new Color(0.48f, 0.46f, 0.42f, 1f);

    float hot;

    public void SetSelected(bool value) => selected = value;

    public void OnPointerEnter(PointerEventData e) => over = true;
    public void OnPointerExit(PointerEventData e) => over = false;

    void Update()
    {
        float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        hot = Mathf.Lerp(hot, over || selected ? 1f : 0f, k);
        if (glyph != null) glyph.color = Color.Lerp(Cold, Hot, hot);
        if (title != null) title.color = Color.Lerp(Cold, Hot, hot);
        if (code != null) code.color = Color.Lerp(CodeCold, CodeHot, hot);
    }
}
