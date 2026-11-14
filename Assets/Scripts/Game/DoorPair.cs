using UnityEngine;

/// <summary>
/// DoorLeaf_L / DoorLeaf_R 한 세트(양문). PlayerInteraction의 IInteractable(E) 경로로 동작한다.
/// 각 HingedDoor의 closedLocalRotation / openLocalRotation(팀원 FBX door_open_angle)만 사용하고,
/// openAmount를 0↔1로 약 openSeconds 동안 EaseInOut으로 보간한다. 회전각 계산/하드코딩 없음.
/// </summary>
public class DoorPair : MonoBehaviour, IInteractable
{
    public HingedDoor leafL;
    public HingedDoor leafR;
    public float openSeconds = 0.8f;
    [Header("Prompt")]
    public string openText = "[E] OPEN";
    public string closeText = "[E] CLOSE";

    [Header("Monster (몬스터 AI 가 열 수 있는 일반 통행 문인지)")]
    [Tooltip("true = 병원 일반 문 (MonsterDoorOpener 가 직접 열 수 있음). med_cabinet 등 수납장은 false.")]
    public bool isPassableDoor = true;

    float t;        // 0 닫힘 ~ 1 열림 (선형 진행도)
    float target;

    // 몬스터 AI 가 문 상태를 읽기 위한 공개 API (플레이어 상호작용 경로는 그대로)
    public bool IsOpen => target > 0.5f;
    public bool IsFullyOpen => t >= 0.95f;
    public bool IsClosed => target < 0.5f && t <= 0.05f;
    public float OpenProgress => t;

    void Awake()
    {
        // 안전장치: 이름에 cabinet 이 들어가는 수납장(med_cabinet)은 항상 통과용 문이 아니다.
        if (gameObject.name.ToLowerInvariant().Contains("cabinet")) isPassableDoor = false;
        Apply(0f);
    }

    public void Open()
    {
        if (leafL == null && leafR == null) return;
        target = 1f;
    }

    public void Close()
    {
        if (leafL == null && leafR == null) return;
        target = 0f;
    }

    /// <summary>두 문짝(경첩)의 중간 지점.</summary>
    public Vector3 GetDoorCenter()
    {
        if (leafL != null && leafR != null) return (leafL.transform.position + leafR.transform.position) * 0.5f;
        if (leafL != null) return leafL.transform.position;
        if (leafR != null) return leafR.transform.position;
        return transform.position;
    }

    public string GetInteractionText() => target > 0.5f ? closeText : openText;

    public void Interact(PlayerInteraction player)
    {
        if (leafL == null && leafR == null) return;
        target = target > 0.5f ? 0f : 1f;   // 이동 중이면 현재 위치에서 반대로
    }

    void Update()
    {
        if (Mathf.Approximately(t, target)) return;
        t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, openSeconds));
        Apply(Mathf.SmoothStep(0f, 1f, t));
    }

    void Apply(float amount)
    {
        if (leafL != null) { leafL.openAmount = amount; leafL.Apply(); }
        if (leafR != null) { leafR.openAmount = amount; leafR.Apply(); }
    }
}
