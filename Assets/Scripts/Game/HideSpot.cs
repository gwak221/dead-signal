using System.Collections;
using System.Collections.Generic;
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
    BoxCollider interiorBlocker;

    static readonly List<HideSpot> allSpots = new List<HideSpot>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => allSpots.Clear();

    void OnEnable() { if (!allSpots.Contains(this)) allSpots.Add(this); }

    void Start() { BuildInteriorBlocker(); }

    /// <summary>포획이 확정되면 호출: 진행 중인 은신 진입/퇴장 연출(코루틴, 문 상태)을 모두 정리한다. 부활 후 이전 캐비닛이 플레이어를 옮기지 못하게 한다.</summary>
    public static void CancelAllPending()
    {
        foreach (var s in allSpots) if (s != null) s.CancelPending();
    }

    void CancelPending()
    {
        StopAllCoroutines();
        Unsubscribe();
        SetDoors(false);
        busy = false;
    }

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
        if (PlayerCaughtSequence.IsRunning) return;   // 포획 연출 중에는 숨을 수 없다

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

        // 문이 열리는 사이 포획이 시작됐다면 숨지 않는다 (부활 후 캐비닛으로 끌려가는 것을 막는다)
        if (PlayerCaughtSequence.IsRunning || player == null)
        {
            SetDoors(false);
            busy = false;
            yield break;
        }

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

    // ───────────────────────── 안전한 퇴장 위치 / 내부 진입 차단 ─────────────────────────

    /// <summary>몸(hidePoint)에서 앞 방향으로, 문이 열릴 때 문짝이 휘두르는 끝까지의 거리.</summary>
    float DoorSwingAlong(Vector3 front)
    {
        float plane = 0f, swing = 0f;
        if (HasDoors)
            foreach (var d in doors)
            {
                if (d == null) continue;
                var c = d.GetComponent<Collider>();
                if (c == null) continue;
                plane = Mathf.Max(plane, Vector3.Dot(c.bounds.center - hidePoint.position, front));
                swing = Mathf.Max(swing, Mathf.Max(c.bounds.size.x, c.bounds.size.z));
            }
        return plane + swing + 0.05f;
    }

    /// <summary>
    /// 퇴장 위치: 기본 exitPoint 를 쓰되 (1) 열린 문짝이 휘두르는 범위 밖으로, (2) 플레이어 캡슐이 다른 물체와 겹치지 않는 곳으로 옮긴다.
    /// 주변 가구/벽은 건드리지 않고 플레이어 쪽을 비켜 놓는다. 적당한 자리가 없으면 문짝 범위 밖의 기본 방향 자리.
    /// </summary>
    public Vector3 ResolveExitPosition(CharacterController cc)
    {
        if (exitPoint == null) return transform.position;
        Vector3 basePos = exitPoint.position;
        if (cc == null || hidePoint == null) return basePos;

        Vector3 front = FrontDirection;
        Vector3 right = Vector3.Cross(Vector3.up, front);
        float r = cc.radius + cc.skinWidth + 0.02f;
        float along0 = Vector3.Dot(basePos - hidePoint.position, front);
        float swingAlong = DoorSwingAlong(front);
        float along = Mathf.Max(along0, swingAlong + r);
        Vector3 doorPoint = hidePoint.position + front * Mathf.Max(0f, swingAlong - 0.6f);

        float[] extra = { 0f, 0.25f, 0.5f, 0.8f, 1.2f };
        float[] lateral = { 0f, 0.35f, -0.35f, 0.7f, -0.7f };
        foreach (float da in extra)
            foreach (float lat in lateral)
            {
                Vector3 p = hidePoint.position + front * (along + da) + right * lat;
                p.y = basePos.y;
                if (IsExitFree(p, doorPoint, cc, r)) return p;
            }
        Vector3 fallback = hidePoint.position + front * along;
        fallback.y = basePos.y;
        return fallback;
    }

    bool IsExitFree(Vector3 p, Vector3 doorPoint, CharacterController cc, float r)
    {
        // 바닥이 있어야 한다
        if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 1.0f, ~0, QueryTriggerInteraction.Ignore)) return false;

        Vector3 a = p + Vector3.up * (r + 0.05f);
        Vector3 b = p + Vector3.up * Mathf.Max(r + 0.06f, cc.height - r);
        foreach (var col in Physics.OverlapCapsule(a, b, r, ~0, QueryTriggerInteraction.Ignore))
        {
            if (col.transform.IsChildOf(cc.transform)) continue;
            return false;
        }

        // 캐비닛 앞에서 그 자리까지 벽/가구로 막혀 있지 않아야 한다 (가구 뒤로 보내지 않는다)
        Vector3 from = doorPoint + Vector3.up * 0.9f, to = p + Vector3.up * 0.9f;
        foreach (var h in Physics.RaycastAll(from, to - from, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(cc.transform) || h.collider.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    /// <summary>문이 열려 있어도 걸어서 안으로 들어올 수 없게 문짝 바로 안쪽에 얇은 막(Collider)을 둔다. 은신 진입/퇴장은 순간이동이라 영향이 없다.</summary>
    void BuildInteriorBlocker()
    {
        if (interiorBlocker != null || !HasDoors || hidePoint == null) return;
        Bounds b = default;
        bool any = false;
        foreach (var d in doors)
        {
            if (d == null) continue;
            var c = d.GetComponent<Collider>();
            if (c == null) continue;
            if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
        }
        if (!any) return;

        Vector3 front = FrontDirection;
        Vector3 right = Vector3.Cross(Vector3.up, front);
        const float thickness = 0.12f;
        var go = new GameObject("HideSpot_InteriorBlocker");
        go.transform.SetParent(transform, true);
        go.transform.rotation = Quaternion.LookRotation(front, Vector3.up);
        go.transform.position = b.center - front * (thickness * 0.5f + 0.05f);   // 문짝 평면 바로 안쪽
        float width = Mathf.Abs(right.x) * b.size.x + Mathf.Abs(right.z) * b.size.z;
        interiorBlocker = go.AddComponent<BoxCollider>();
        interiorBlocker.size = new Vector3(Mathf.Max(0.5f, width), b.size.y, thickness);
    }

    void Unsubscribe()
    {
        if (subscribed != null) subscribed.OnHidingChanged -= OnHidingChanged;
        subscribed = null;
    }

    void OnDisable()
    {
        allSpots.Remove(this);
        Unsubscribe();
        busy = false;
    }
}
