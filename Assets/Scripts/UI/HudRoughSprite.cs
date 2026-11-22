using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가장자리만 거친 판. 안쪽과 테두리는 같은 색이고, 색은 얹는 Image 틴트로 정한다.
/// </summary>
public static class HudRoughSprite
{
    static readonly Dictionary<long, Sprite> cache = new Dictionary<long, Sprite>();

    public static Sprite Get(int width, int height)
    {
        width = Mathf.Max(4, width);
        height = Mathf.Max(4, height);
        long key = ((long)width << 32) | (uint)height;
        if (cache.TryGetValue(key, out var existing) && existing != null)
            return existing;

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float scale = Mathf.Min(width, height) / 128f;
        var px = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float edge = Mathf.Min(x + 0.5f, y + 0.5f, width - x - 0.5f, height - y - 0.5f);
                float grit = Hash01(x, y);
                float outer = (1.4f + (grit - 0.5f) * 2.4f) * scale;

                if (edge < outer)
                {
                    px[y * width + x] = Color.clear;
                }
                else
                {
                    float pit = Hash01(x * 3 + 1, y * 3 - 4);
                    float v = pit > 0.96f ? 0.86f : 1f;
                    px[y * width + x] = new Color(v, v, v, 1f);
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply(false);
        var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        cache[key] = sprite;
        return sprite;
    }

    static float Hash01(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h & 65535u) / 65535f;
    }
}
