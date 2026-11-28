using System.Collections;
using UnityEngine;

/// <summary>
/// 숨을 수 있는 캐비넷(tall_cabinet). PlayerInteraction의 IInteractable(E) 경로로 동작한다.
/// hidePoint = 플레이어 몸 위치, viewPoint(HideViewPoint) = 카메라/눈 위치(앞판 쇠창살 바로 뒤), exitPoint = 캐비넷 앞 (나올 위치).
/// 앞 방향 = 모델의 로컬 +Y (원본 FBX 배치) 또는 로컬 +Z (Openables 프리팹). 숨기 상태는 PlayerHide.IsHiding 에 있다.
///
/// doors(선택): Openables 프리팹의 문짝. 비어 있으면 예전처럼 바로 숨는다.
/// 있으면 숨을 때 문 열림 → 숨기 → 문 닫힘, 나올 때 문 열림 → 잠시 뒤 닫힘 연출을 한다.
/// </summary>
public class HideSpot : MonoBehaviour, IInteractable
{
    public Transform hidePoint;
    public Transform viewPoint;
    public Transform exitPoint;

    [Header("Doors (선택)")]
    public OpenablePart[] doors;
    [Tooltip("나온 뒤 문이 열린 채로 있는 시간(초)")]
    public float exitDoorHoldSeconds = 0.6f;

    bool busy;
    PlayerHide subscribed;

    /// <summary>캐비넷이 바깥을 향한 수평 방향.
    /// 원본 FBX 배치(루트가 X 90° 누움)는 로컬 +Y 가 앞, Openables 프리팹(루트가 서 있음)은 로컬 +Z 가 앞.</summary>
    public Vector3 FrontDirection
    {
        get
        {
            Vector3 f = transform.up;
            f.y = 0f;
            if (f.sqrMagnitude > 0.25f) return f.normalized;   // 루트가 누운 원본 배치
            f = transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }
    }

    bool HasDoors => doors != null && doors.Length > 0;

    /// <summary>하단 Interaction Prompt 문구. ChaseCabinetHighlight 가 이 문구를 보고 월드 안내 [E] HIDE 와 겹치지 않게 숨는다.</summary>
    public const string PromptHide = "[E] HIDE";

    public string GetInteractionText() => busy ? string.Empty : PromptHide;

    public void Interact(PlayerInteraction player)
    {
        if (player == null || busy) return;

        if (!HasDoors)
        {
            PlayerHide.GetOrAdd(player.gameObject).Enter(this);
            return;
        }
        StartCoroutine(EnterRoutine(player));
    }

    IEnumerator EnterRoutine(PlayerInteraction player)
    {
        busy = true;
        SetDoors(true);
        yield return new WaitForSeconds(DoorSeconds());

        var hide = PlayerHide.GetOrAdd(player.gameObject);
        if (hide.Enter(this))
        {
            Unsubscribe();
            subscribed = hide;
            hide.OnHidingChanged += OnHidingChanged;
        }
        SetDoors(false);
        busy = false;
    }

    void OnHidingChanged(bool hiding)
    {
        if (this == null) return; // 캐비넷이 파괴된 뒤 들어온 이벤트
        if (hiding) return;
        Unsubscribe();
        if (isActiveAndEnabled) StartCoroutine(ExitRoutine());
    }

    IEnumerator ExitRoutine()
    {
        busy = true;
        SetDoors(true);
        yield return new WaitForSeconds(DoorSeconds() + exitDoorHoldSeconds);
        SetDoors(false);
        yield return new WaitForSeconds(DoorSeconds());
        busy = false;
    }

    void SetDoors(bool open)
    {
        if (!HasDoors) return;
        foreach (var d in doors)
            if (d != null) d.SetOpen(open);
    }

    float DoorSeconds()
    {
        float s = 0f;
        if (HasDoors)
            foreach (var d in doors)
                if (d != null) s = Mathf.Max(s, d.FullMotionSeconds);
        return s;
    }

    void Unsubscribe()
    {
        if (subscribed != null) subscribed.OnHidingChanged -= OnHidingChanged;
        subscribed = null;
    }

    void OnDisable()
    {
        Unsubscribe();
        busy = false;
    }
}
