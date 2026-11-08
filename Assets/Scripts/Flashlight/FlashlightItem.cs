using UnityEngine;

/// <summary>
/// 월드/인벤토리 손전등 아이템. 키/배터리와 같은 방식으로 PlayerInventory 슬롯 하나를 차지한다.
///
/// 구조 (실제 모델이 나오면 Visual 아래만 교체):
///   FlashlightItem      ← 이 컴포넌트, BoxCollider(줍기/물리용), Rigidbody(드롭 시 자동 추가)
///   └─ Visual
///      ├─ Body
///      ├─ Head
///      ├─ Lens          (켜지면 발광)
///      └─ BeamOrigin    (FlashlightBeam이 나오는 위치/방향, +Z 정면)
///
/// 쿨타임은 이 오브젝트에 저장된다. 버렸다가 다시 주워도 같은 오브젝트이므로 쿨타임이 유지된다.
/// </summary>
[DisallowMultipleComponent]
public class FlashlightItem : MonoBehaviour, IInteractable
{
    [Header("Parts (auto-found by name if empty)")]
    public Transform visual;
    public Renderer lensRenderer;
    public Transform beamOrigin;

    [Header("Held Pose (KeyHoldPoint 기준 로컬)")]
    [Tooltip("1인칭 화면 오른쪽 아래에 보이도록 하는 위치")]
    public Vector3 holdLocalPosition = new Vector3(0f, -0.06f, -0.32f);
    public Vector3 holdLocalEuler = new Vector3(-2f, -5f, 0f);

    [Header("Lens")]
    public Color lensOffColor = new Color(0.55f, 0.55f, 0.5f);
    public Color lensOnColor = new Color(1f, 0.97f, 0.85f);
    [ColorUsage(false, true)] public Color lensOnEmission = new Color(2.2f, 2.1f, 1.8f);

    float cooldownStart;
    float cooldownEnd;
    bool lit;

    // ───────────────────────── 쿨타임 (아이템 자체에 저장) ─────────────────────────

    public bool IsOnCooldown => Time.time < cooldownEnd;
    public float CooldownRemaining => Mathf.Max(0f, cooldownEnd - Time.time);
    public float CooldownDuration => Mathf.Max(0.0001f, cooldownEnd - cooldownStart);
    /// <summary>1 = 막 시작, 0 = 끝남</summary>
    public float CooldownNormalized => IsOnCooldown ? CooldownRemaining / CooldownDuration : 0f;

    public void StartCooldown(float seconds)
    {
        if (seconds <= 0f) return;
        cooldownStart = Time.time;
        cooldownEnd = Time.time + seconds;
    }

    // ───────────────────────── IInteractable (E로 줍기) ─────────────────────────

    public string GetInteractionText() => "E : Pick Up Flashlight";

    public void Interact(PlayerInteraction player)
    {
        if (player == null || player.Inventory == null)
            return;

        if (!player.Inventory.TryAddWorldItem(InventoryItemType.Flashlight, gameObject))
            player.Notify("Inventory Full");
    }

    // ───────────────────────── 렌즈 ─────────────────────────

    void Awake()
    {
        ResolveParts();
        SetLit(false);
    }

    void ResolveParts()
    {
        if (visual == null) visual = transform.Find("Visual");
        Transform root = visual != null ? visual : transform;
        if (lensRenderer == null)
        {
            Transform lens = root.Find("Lens");
            if (lens != null) lensRenderer = lens.GetComponent<Renderer>();
        }
        if (beamOrigin == null) beamOrigin = root.Find("BeamOrigin");
        if (beamOrigin == null) beamOrigin = transform;
    }

    public void SetLit(bool on)
    {
        lit = on;
        if (lensRenderer == null) return;

        Material m = lensRenderer.material;
        m.color = on ? lensOnColor : lensOffColor;
        if (m.HasProperty("_EmissionColor"))
        {
            if (on) m.EnableKeyword("_EMISSION"); else m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", on ? lensOnEmission : Color.black);
        }
    }

    public bool IsLit => lit;

    void OnDisable()
    {
        // 슬롯 전환/드롭 시 켜진 채로 남지 않게.
        if (lit) SetLit(false);
    }

    // ───────────────────────── 임시 모델 생성 ─────────────────────────

    /// <summary>Primitive로 임시 손전등 생성.</summary>
    public static FlashlightItem Create(Vector3 position)
    {
        var root = new GameObject("FlashlightItem");
        root.transform.position = position;

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);

        // 몸통: 어두운 회색 원통 (Z축 정면)
        Part(PrimitiveType.Cylinder, "Body", visual,
            new Vector3(0f, 0f, 0f), new Vector3(0.065f, 0.12f, 0.065f), new Color(0.12f, 0.12f, 0.13f));

        // 헤드: 조금 더 넓은 앞부분
        Part(PrimitiveType.Cylinder, "Head", visual,
            new Vector3(0f, 0f, 0.15f), new Vector3(0.1f, 0.04f, 0.1f), new Color(0.07f, 0.07f, 0.08f));

        // 렌즈: 앞면의 얇은 원판 (Collider 없음)
        var lens = Part(PrimitiveType.Cylinder, "Lens", visual,
            new Vector3(0f, 0f, 0.191f), new Vector3(0.085f, 0.004f, 0.085f), new Color(0.55f, 0.55f, 0.5f));

        // 스위치 버튼 (식별용 작은 디테일, Collider 없음)
        var button = Part(PrimitiveType.Cube, "Button", visual,
            new Vector3(0f, 0.034f, 0.02f), new Vector3(0.02f, 0.012f, 0.035f), new Color(0.6f, 0.15f, 0.1f));
        button.transform.localRotation = Quaternion.identity;

        var beam = new GameObject("BeamOrigin").transform;
        beam.SetParent(visual, false);
        beam.localPosition = new Vector3(0f, 0f, 0.2f);
        beam.localRotation = Quaternion.identity;

        // 파츠 Collider는 제거하고 루트에 하나만 둔다 (E 상호작용 Raycast가 맞추기 쉽고 굴러가지 않음).
        foreach (var col in visual.GetComponentsInChildren<Collider>(true))
            Object.Destroy(col);
        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0f, 0.03f);
        box.size = new Vector3(0.12f, 0.12f, 0.36f);

        var item = root.AddComponent<FlashlightItem>();
        item.visual = visual;
        item.lensRenderer = lens.GetComponent<Renderer>();
        item.beamOrigin = beam;
        item.SetLit(false);
        return item;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // Cylinder는 Y축이 길이 방향 → X 90도 회전해서 Z(정면) 방향으로 눕힌다.
        go.transform.localRotation = type == PrimitiveType.Cylinder ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        go.transform.localScale = scale;

        var r = go.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = color;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return go;
    }
}
