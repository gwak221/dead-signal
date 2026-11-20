using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 화면 중앙 아래쪽에 잠깐 뜨는 짧은 안내 문구 (예: "TOO EXHAUSTED", "BATTERY EMPTY - PRESS R TO RELOAD").
/// 키(key)당 라벨을 하나만 만들어 재사용하므로 연타해도 UI 가 중첩 생성되지 않는다. 다시 호출하면 표시 시간만 갱신.
/// 기존 HUD(Canvas/SafeArea/DesignRoot)를 부모로 사용. 시간 정지(메뉴) 중에도 unscaledTime 으로 만료.
/// </summary>
public class HudToast : MonoBehaviour
{
    static HudToast instance;
    class Entry { public TextMeshProUGUI label; public float until; }
    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static void Show(string key, string text, float seconds, float yOffset = 150f)
    {
        if (instance == null)
        {
            var go = new GameObject("HudToast");
            instance = go.AddComponent<HudToast>();
        }
        instance.ShowInternal(key, text, seconds, yOffset);
    }

    void ShowInternal(string key, string text, float seconds, float yOffset)
    {
        if (!entries.TryGetValue(key, out var e) || e.label == null)
        {
            e = new Entry { label = Build(key, yOffset) };
            if (e.label == null) return;
            entries[key] = e;
        }
        e.label.text = text;
        e.label.gameObject.SetActive(true);
        e.until = Time.unscaledTime + seconds;
    }

    void Update()
    {
        foreach (var e in entries.Values)
            if (e.label != null && e.label.gameObject.activeSelf && Time.unscaledTime >= e.until)
                e.label.gameObject.SetActive(false);
    }

    static TextMeshProUGUI Build(string key, float yOffset)
    {
        RectTransform parent = FindHudRoot();
        if (parent == null) return null;
        var go = new GameObject("Toast_" + key, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        TMP_Text existing = parent.GetComponentInChildren<TMP_Text>(true);
        if (existing != null && existing.font != null) label.font = existing.font;
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.35f, 0.3f, 0.95f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color32(0, 0, 0, 255);
        var rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(700f, 36f);
        return label;
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
