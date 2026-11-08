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
    public string openText = "E : Open Door";
    public string closeText = "E : Close Door";

    float t;        // 0 닫힘 ~ 1 열림 (선형 진행도)
    float target;

    void Awake() => Apply(0f);

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
