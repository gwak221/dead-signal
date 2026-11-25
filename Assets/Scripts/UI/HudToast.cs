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
    class Entry { public TextMeshProUGUI label; public float until; public int rank; public float baseY; }
    readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    /// <summary>rank: 동시에 여러 개가 떠 있을 때 위→아래 순서 (작을수록 위). 하나만 떠 있으면 yOffset 위치 그대로.</summary>
    public static void Show(string key, string text, float seconds, float yOffset = 150f, int rank = 0)
    {
        if (instance == null)
        {
            var go = new GameObject("HudToast");
            instance = go.AddComponent<HudToast>();
        }
        instance.ShowInternal(key, text, seconds, yOffset, rank);
    }

    void ShowInternal(string key, string text, float seconds, float yOffset, int rank)
    {
        if (!entries.TryGetValue(key, out var e) || e.label == null)
        {
            e = new Entry { label = Build(key, yOffset) };
            if (e.label == null) return;
            entries[key] = e;
        }
        e.rank = rank;
        e.baseY = yOffset;
        e.label.text = text;
        e.label.gameObject.SetActive(true);
        e.until = Time.unscaledTime + seconds;
    }

    readonly List<Entry> active = new List<Entry>();
    const float StackSpacing = 44f;   // 라벨 높이(36) + 여백

    void Update()
    {
        active.Clear();
        foreach (var e in entries.Values)
        {
            if (e.label == null || !e.label.gameObject.activeSelf) continue;
            if (Time.unscaledTime >= e.until) { e.label.gameObject.SetActive(false); continue; }
            active.Add(e);
        }

        // 여러 개가 동시에 떠 있으면 rank 순으로 세로 나열 (아래쪽 = rank 큰 것). 서로 숨기거나 덮지 않는다.
        // 가장 아래 메시지는 자기 기본 위치, 위쪽은 아래 메시지 위로 최소 간격만큼 올린다 (하나뿐이면 기존 위치 그대로).
        active.Sort((a, b) => b.rank.CompareTo(a.rank));
        float below = float.NegativeInfinity;
        foreach (var e in active)
        {
            float y = Mathf.Max(e.baseY, below + StackSpacing);
            e.label.rectTransform.anchoredPosition = new Vector2(0f, y);
            below = y;
        }
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
