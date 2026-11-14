using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아직 활성화되지 않은 Beacon 을 '중요한 상호작용 목표'로 보이게 하는 강조 (Beacon.cs 로직은 건드리지 않는다).
///  - 모델의 ecp_beacon(램프) / ecp_screen(화면) 머티리얼 인스턴스에 차가운 청록 Emission 을 천천히 Pulse.
///  - 램프 위치에 옅은 청백색 헤일로(빌보드 스프라이트, 벽에 가려짐). Point Light 없음.
///  - Beacon.IsActivated 가 되면 원래 Emission 으로 복원하고 모두 꺼진다.
/// 배터리(따뜻한 황색)와 구분되도록 청록/청백 계열을 사용한다.
/// </summary>
[RequireComponent(typeof(Beacon))]
public class BeaconHighlight : MonoBehaviour
{
    [ColorUsage(false, true)] public Color lampColor = new Color(0.20f, 0.90f, 1.0f);
    public float lampIntensity = 9f;
    public float lampPulse = 5f;
    [Tooltip("화면(ecp_screen) 청록 Emission 기본/펄스 강도.")]
    public float screenIntensity = 2.4f;
    public float screenPulse = 1.6f;
    [Header("배터리 슬롯 주변 (따뜻한 황색 = 배터리와 같은 색 → 여기에 넣는다)")]
    [ColorUsage(false, true)] public Color slotColor = new Color(1.0f, 0.62f, 0.12f);
    public float slotIntensity = 1.6f;
    public float slotPulse = 1.2f;
    [Range(0f, 1f)] public float slotHaloAlpha = 0.45f;
    public float slotHaloSize = 1.1f;
    [Header("프레임 일부 (청록, 은은하게)")]
    public float frameIntensity = 0.35f;
    [Header("활성화 후 (작동 중 표시: 램프를 고정 초록으로)")]
    [ColorUsage(false, true)] public Color activatedLampColor = new Color(0.15f, 1.0f, 0.30f);
    public float activatedLampIntensity = 4f;
    [Range(0f, 1f)] public float haloAlpha = 0.40f;
    public float haloSize = 2.0f;
    [Tooltip("비콘 전체를 감싸는 세로 빛기둥 헤일로 알파/크기.")]
    [Range(0f, 1f)] public float pillarAlpha = 0.12f;
    public Vector2 pillarSize = new Vector2(1.5f, 3.6f);
    public float pulseSpeed = 1.3f;

    Beacon beacon;
    Renderer rend;
    Material[] mats;
    readonly List<int> lampIdx = new List<int>();
    readonly List<int> screenIdx = new List<int>();
    readonly List<int> slotIdx = new List<int>();     // 배터리 슬롯 주변(hazard 프레임 / recess)
    readonly List<int> frameIdx = new List<int>();    // 프레임 일부(steel_light)
    readonly Dictionary<int, Color> original = new Dictionary<int, Color>();
    Transform halo, pillar, slotHalo;
    SpriteRenderer haloRenderer, pillarRenderer, slotHaloRenderer;
    bool wasOn;
    float phase;
    static Sprite haloSprite;

    void Awake()
    {
        beacon = GetComponent<Beacon>();
        rend = GetComponent<Renderer>();
        phase = Random.value * 6.28f;
        if (rend == null) return;

        mats = rend.materials; // 인스턴스화 (공유 FBX 머티리얼 보호)
        for (int i = 0; i < mats.Length; i++)
        {
            if (!mats[i].HasProperty("_EmissionColor")) continue;
            string n = mats[i].name;
            bool lamp = n.StartsWith("ecp_beacon");
            bool screen = n.StartsWith("ecp_screen");
            bool slot = n.StartsWith("ecp_hazard") || n.StartsWith("ecp_recess");
            bool frame = n.StartsWith("ecp_steel_light");
            if (!lamp && !screen && !slot && !frame) continue;
            (lamp ? lampIdx : screen ? screenIdx : slot ? slotIdx : frameIdx).Add(i);
            original[i] = mats[i].GetColor("_EmissionColor");
            // 텍스처가 있는 부분(hazard 줄무늬)은 BaseMap 을 EmissionMap 으로 써서 무늬를 유지한 채 발광
            if (slot && mats[i].HasProperty("_BaseMap") && mats[i].GetTexture("_BaseMap") != null && mats[i].GetTexture("_EmissionMap") == null)
                mats[i].SetTexture("_EmissionMap", mats[i].GetTexture("_BaseMap"));
            mats[i].EnableKeyword("_EMISSION");
        }

        var go = new GameObject("BeaconHalo");
        halo = go.transform;
        halo.SetParent(transform, false);
        Bounds b = rend.bounds;
        halo.position = new Vector3(b.center.x, b.max.y - 0.35f, b.center.z);
        haloRenderer = go.AddComponent<SpriteRenderer>();
        haloRenderer.sprite = HaloSprite();
        haloRenderer.sortingOrder = -1;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
        go.SetActive(false);

        // 배터리 슬롯 주변 헤일로 (소켓 위치)
        var sock = transform.Find("control_panel_BatterySocket");
        var sg = new GameObject("BeaconSlotHalo");
        slotHalo = sg.transform;
        slotHalo.SetParent(transform, false);
        slotHalo.position = sock != null ? sock.position : (beacon.batterySlot != null ? beacon.batterySlot.position : b.center);
        slotHaloRenderer = sg.AddComponent<SpriteRenderer>();
        slotHaloRenderer.sprite = HaloSprite();
        slotHaloRenderer.sortingOrder = 3;
        slotHaloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        slotHaloRenderer.receiveShadows = false;
        sg.SetActive(false);

        var pg = new GameObject("BeaconPillar");
        pillar = pg.transform;
        pillar.SetParent(transform, false);
        pillar.position = b.center;
        pillarRenderer = pg.AddComponent<SpriteRenderer>();
        pillarRenderer.sprite = HaloSprite();
        pillarRenderer.sortingOrder = -2;
        pillarRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pillarRenderer.receiveShadows = false;
        pg.SetActive(false);
    }

    void Update()
    {
        if (rend == null || beacon == null) return;
        bool on = !beacon.IsActivated;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed + phase);

        if (on)
        {
            foreach (int i in lampIdx) mats[i].SetColor("_EmissionColor", lampColor * (lampIntensity + lampPulse * pulse));
            foreach (int i in screenIdx) mats[i].SetColor("_EmissionColor", lampColor * (screenIntensity + screenPulse * pulse));
            float sp = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * 1.4f + phase + 1.2f);
            foreach (int i in slotIdx) mats[i].SetColor("_EmissionColor", slotColor * (slotIntensity + slotPulse * sp));
            foreach (int i in frameIdx) mats[i].SetColor("_EmissionColor", lampColor * (frameIntensity * (0.6f + 0.4f * pulse)));
        }
        else if (wasOn)
        {
            // 강조 효과는 끄고(원래 Emission 으로 복원), 램프만 고정 초록으로 '작동 중' 표시
            foreach (var kv in original) mats[kv.Key].SetColor("_EmissionColor", kv.Value);
            foreach (int i in lampIdx) mats[i].SetColor("_EmissionColor", activatedLampColor * activatedLampIntensity);
        }

        if (halo != null)
        {
            if (halo.gameObject.activeSelf != on) halo.gameObject.SetActive(on);
            if (on)
            {
                var cam = Camera.main;
                if (cam != null) halo.rotation = cam.transform.rotation;
                halo.localScale = Vector3.one * haloSize * (0.9f + 0.2f * pulse);
                haloRenderer.color = new Color(0.45f, 0.95f, 1f, haloAlpha * (0.6f + 0.4f * pulse));
            }
        }
        if (slotHalo != null)
        {
            if (slotHalo.gameObject.activeSelf != on) slotHalo.gameObject.SetActive(on);
            if (on)
            {
                var cam = Camera.main;
                if (cam != null) slotHalo.rotation = cam.transform.rotation;
                float sp = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * 1.4f + phase + 1.2f);
                slotHalo.localScale = Vector3.one * slotHaloSize * (0.85f + 0.3f * sp);
                slotHaloRenderer.color = new Color(1f, 0.7f, 0.25f, slotHaloAlpha * (0.5f + 0.5f * sp));
            }
        }
        if (pillar != null)
        {
            if (pillar.gameObject.activeSelf != on) pillar.gameObject.SetActive(on);
            if (on)
            {
                var cam = Camera.main;
                if (cam != null) pillar.rotation = cam.transform.rotation;
                pillar.localScale = new Vector3(pillarSize.x, pillarSize.y, 1f);
                pillarRenderer.color = new Color(0.35f, 0.9f, 1f, pillarAlpha * (0.6f + 0.4f * pulse));
            }
        }
        wasOn = on;
    }

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
        haloSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        return haloSprite;
    }
}
