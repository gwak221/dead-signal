using UnityEngine;

/// <summary>
/// 숨을 수 있는 캐비넷(tall_cabinet). PlayerInteraction의 IInteractable(E) 경로로 동작한다.
/// hidePoint = 플레이어 몸 위치, viewPoint(HideViewPoint) = 카메라/눈 위치(앞판 쇠창살 바로 뒤), exitPoint = 캐비넷 앞 (나올 위치).
/// 앞 방향 = 모델의 로컬 +Y (transform.up). 숨기 상태는 PlayerHide.IsHiding 에 있다.
/// </summary>
public class HideSpot : MonoBehaviour, IInteractable
{
    public Transform hidePoint;
    public Transform viewPoint;
    public Transform exitPoint;

    /// <summary>캐비넷이 바깥을 향한 수평 방향.</summary>
    public Vector3 FrontDirection
    {
        get
        {
            Vector3 f = transform.up;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : transform.forward;
        }
    }

    public string GetInteractionText() => "E : Hide";

    public void Interact(PlayerInteraction player)
    {
        if (player == null) return;
        PlayerHide.GetOrAdd(player.gameObject).Enter(this);
    }
}
