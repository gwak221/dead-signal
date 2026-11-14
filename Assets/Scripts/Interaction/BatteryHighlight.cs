using UnityEngine;

/// <summary>
/// 월드에 놓인 배터리를 '줍는 아이템'으로 보이게 하는 약한 강조 (맵 조명은 건드리지 않는다).
///  - 배터리 모델의 노랑/호박/녹색 부분만 은은하게 숨쉬는 Emission (머티리얼 인스턴스 사용, 원본 FBX 머티리얼 불변)
///  - 아주 옅은 따뜻한 헤일로(빌보드 스프라이트, 벽에 가려짐)
/// 줍기/손에 든 상태/비콘 삽입 후에는 꺼진다. BatteryPickup.Awake 에서 자동 추가.
/// </summary>
[DisallowMultipleComponent]
public class BatteryHighlight : MonoBehaviour
{
    static readonly string[] GlowMaterials = { "yellow", "amber", "greenlight" };
    static readonly Color EmissionColor = new Color(1.0f, 0.72f, 0.25f);
    static Sprite haloSprite;

    [Range(0f, 1f)] public float emissionBase = 0.55f;
    [Range(0f, 1f)] public float emissionPulse = 0.20f;
    [Range(0f, 0.5f)] public float haloAlpha = 0.32f;
    public float haloSize = 1.1f;

    Renderer rend;
    Material[] mats;
    readonly System.Collections.Generic.List<int> glowIdx = new System.Collections.Generic.List<int>();
    Transform halo;
    SpriteRenderer haloRenderer;
    bool lastOn;
    float phase;

    void Awake()
    {
        phase = Random.value * 6.28f;
        rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        mats = rend.materials; // 인스턴스화 (공유 FBX 머티리얼 보호)
        for (int i = 0; i < mats.Length; i++)
        {
            string n = mats[i].name.ToLowerInvariant();
            foreach (var g in GlowMaterials)
                if (n.StartsWith(g) && mats[i].HasProperty("_EmissionColor")) { glowIdx.Add(i); mats[i].EnableKeyword("_EMISSION"); break; }
        }

        var go = new GameObject("PickupHalo");
        halo = go.transform;
        halo.SetParent(transform, false);
        halo.position = rend.bounds.center;
        haloRenderer = go.AddComponent<SpriteRenderer>();
        haloRenderer.sprite = HaloSprite();
        haloRenderer.sortingOrder = -1;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
        go.SetActive(false);
    }

    void Update()
    {
        if (rend == null) return;
        bool on = IsInWorld();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.6f + phase);

        if (on || lastOn)
        {
            float e = on ? emissionBase + emissionPulse * pulse : 0f;
            foreach (int i in glowIdx) mats[i].SetColor("_EmissionColor", EmissionColor * e);
        }
        if (halo != null)
        {
            if (halo.gameObject.activeSelf != on) halo.gameObject.SetActive(on);
            if (on)
            {
                var cam = Camera.main;
                if (cam != null) halo.rotation = cam.transform.rotation;
                halo.localScale = Vector3.one * haloSize;
                haloRenderer.color = new Color(1f, 0.8f, 0.4f, haloAlpha * (0.7f + 0.3f * pulse));
            }
        }
        lastOn = on;
    }

    // 월드에 놓인 상태 = 줍기 컴포넌트가 있고 플레이어(카메라) 아래에 붙어 있지 않음
    bool IsInWorld() => GetComponent<BatteryPickup>() != null && GetComponentInParent<Camera>() == null;

    void OnDestroy()
    {
        if (mats != null) foreach (var m in mats) if (m != null) Destroy(m);
    }

    static Sprite HaloSprite()
    {
        if (haloSprite != null) return haloSprite;
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        haloSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s); // 1 unit
        return haloSprite;
    }
}
