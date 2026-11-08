using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 왼쪽 위 하트 2칸. PlayerBodyStatus의 신체 파손 수를 그대로 표시한다 (별도 HP 없음).
/// 파손 0 = ♥♥, 1 = ♥♡, 2 이상 = ♡♡ (Game Over). 매 프레임 상태를 읽어 F6/F7/F8에도 즉시 반영.
/// </summary>
public class BodyHeartsUI : MonoBehaviour
{
    public float heartSize = 44f;
    public float spacing = 8f;

    Image[] hearts;
    Sprite full, empty;
    PlayerBodyStatus status;
    int shown = -1;

    void Awake()
    {
        full = MakeHeart(false);
        empty = MakeHeart(true);
        hearts = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("Heart_" + (i + 1), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(heartSize, heartSize);
            rt.anchoredPosition = new Vector2(i * (heartSize + spacing), 0f);
            hearts[i] = go.GetComponent<Image>();
            hearts[i].raycastTarget = false;
        }
        Refresh(2);
    }

    void Update()
    {
        if (status == null)
        {
            status = FindFirstObjectByType<PlayerBodyStatus>();
            if (status == null) return;
        }
        Refresh(Mathf.Clamp(2 - status.DamagedCount, 0, 2));
    }

    void Refresh(int count)
    {
        if (count == shown) return;
        shown = count;
        for (int i = 0; i < 2; i++)
        {
            bool on = i < count;
            hearts[i].sprite = on ? full : empty;
            hearts[i].color = on ? new Color(0.9f, 0.1f, 0.15f) : new Color(0.75f, 0.75f, 0.75f, 0.8f);
        }
    }

    // 하트 곡선 (x²+y²-1)³ - x²y³ <= 0 으로 그린 64x64 스프라이트. outline = 테두리만.
    static Sprite MakeHeart(bool outline)
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float fx = (x + 0.5f) / N * 3f - 1.5f;
                float fy = (y + 0.5f) / N * 3f - 1.3f;
                float a = fx * fx + fy * fy - 1f;
                float f = a * a * a - fx * fx * fy * fy * fy;
                bool inside = f <= 0f;
                bool ring = inside && f > -0.35f;
                px[y * N + x] = (outline ? ring : inside) ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }
}
