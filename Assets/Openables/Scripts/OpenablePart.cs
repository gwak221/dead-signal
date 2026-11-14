using System;
using UnityEngine;

public enum OpenableKind
{
    Hinge,  // 경첩 문·뚜껑 (피벗 = 경첩)
    Slide   // 서랍
}

/// <summary>
/// 여닫는 파츠 하나(문짝·서랍·뚜껑 날개). PlayerInteraction의 IInteractable(E) 경로로 동작한다.
/// 닫힘/열림 로컬 자세 두 개 사이를 SmoothStep으로 보간한다(DoorPair와 같은 방식).
/// 값은 Openables 모델 임포트 시 Blender의 "&lt;파츠&gt;__open" 빈 오브젝트에서 자동으로 채워진다.
///
/// interactable = false 이면 E 입력과 안내 문구를 부모의 다른 IInteractable(예: HideSpot)에 넘긴다.
/// linked 파츠는 같이 열리고 닫힌다(양문, 상자 날개).
/// </summary>
[DisallowMultipleComponent]
public class OpenablePart : MonoBehaviour, IInteractable
{
    public OpenableKind kind = OpenableKind.Hinge;

    [Header("Poses (local)")]
    public Vector3 closedLocalPosition;
    public Quaternion closedLocalRotation = Quaternion.identity;
    public Vector3 openLocalPosition;
    public Quaternion openLocalRotation = Quaternion.identity;

    [Header("Motion")]
    [Min(0.05f)] public float openSeconds = 0.45f;
    [Tooltip("열기 시작 전 대기(초). 상자 날개처럼 순서가 있는 파츠용")]
    [Min(0f)] public float openDelay;
    [Tooltip("닫기 시작 전 대기(초)")]
    [Min(0f)] public float closeDelay;
    public bool startOpen;

    [Header("Interaction")]
    [Tooltip("false면 E 입력/문구를 부모의 다른 IInteractable(HideSpot 등)에 넘긴다")]
    public bool interactable = true;
    [Tooltip("같이 여닫히는 파츠 (양문, 날개 묶음)")]
    public OpenablePart[] linked = Array.Empty<OpenablePart>();
    public string openText = "E : Open";
    public string closeText = "E : Close";

    [Header("Sound (비우면 상위 OpenableSound 기본 클립)")]
    public AudioClip openClip;
    public AudioClip closeClip;

    /// <summary>(파츠, 열림 여부) — 목표 상태가 바뀔 때 호출.</summary>
    public event Action<OpenablePart, bool> StateChanged;

    float t;          // 0 닫힘 ~ 1 열림 (선형 진행도)
    float target;
    float startAt;    // 지연 후 움직이기 시작하는 시각
    bool soundPending;
    OpenableSound sound;
    IInteractable forwardTarget;
    bool forwardResolved;

    public bool IsOpen => target > 0.5f;
    public bool IsMoving => !Mathf.Approximately(t, target);
    /// <summary>지연 포함, 끝까지 움직이는 데 걸리는 최대 시간.</summary>
    public float FullMotionSeconds => openSeconds + Mathf.Max(openDelay, closeDelay);

    void Awake()
    {
        sound = GetComponentInParent<OpenableSound>();
        t = target = startOpen ? 1f : 0f;
        Apply(t);
    }

    // ───────── IInteractable ─────────

    public string GetInteractionText()
    {
        if (!interactable)
            return ForwardTarget != null ? ForwardTarget.GetInteractionText() : string.Empty;
        return IsOpen ? closeText : openText;
    }

    public void Interact(PlayerInteraction player)
    {
        if (!interactable)
        {
            ForwardTarget?.Interact(player);
            return;
        }
        Toggle();
    }

    IInteractable ForwardTarget
    {
        get
        {
            if (!forwardResolved)
            {
                forwardResolved = true;
                foreach (var mb in GetComponentsInParent<MonoBehaviour>(true))
                    if (mb is IInteractable i && !(mb is OpenablePart)) { forwardTarget = i; break; }
            }
            return forwardTarget;
        }
    }

    // ───────── API ─────────

    /// <summary>자신과 linked 파츠를 함께 반대 상태로.</summary>
    public void Toggle() => SetOpenWithLinked(!IsOpen);

    public void SetOpenWithLinked(bool open)
    {
        SetOpen(open);
        if (linked == null) return;
        foreach (var p in linked)
            if (p != null && p != this) p.SetOpen(open);
    }

    public void SetOpen(bool open, bool instant = false)
    {
        float newTarget = open ? 1f : 0f;
        if (instant)
        {
            target = t = newTarget;
            soundPending = false;
            Apply(t);
            StateChanged?.Invoke(this, open);
            return;
        }
        if (Mathf.Approximately(newTarget, target)) return;
        target = newTarget;
        // 이미 움직이는 중이면 지연 없이 그 자리에서 반대로
        bool midway = t > 0.001f && t < 0.999f;
        startAt = Time.time + (midway ? 0f : (open ? openDelay : closeDelay));
        soundPending = true;
        StateChanged?.Invoke(this, open);
    }

    void Update()
    {
        if (!IsMoving || Time.time < startAt) return;

        if (soundPending)
        {
            soundPending = false;
            if (sound != null) sound.Play(IsOpen ? openClip : closeClip, IsOpen);
        }

        t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, openSeconds));
        Apply(Mathf.SmoothStep(0f, 1f, t));
    }

    public void Apply(float amount)
    {
        transform.localPosition = Vector3.LerpUnclamped(closedLocalPosition, openLocalPosition, amount);
        transform.localRotation = Quaternion.SlerpUnclamped(closedLocalRotation, openLocalRotation, amount);
    }

    // ───────── 에디터 확인용 ─────────

    [ContextMenu("Preview/Open Pose")]
    void PreviewOpen() => Apply(1f);

    [ContextMenu("Preview/Closed Pose")]
    void PreviewClosed() => Apply(0f);

    [ContextMenu("Capture Current As Closed Pose")]
    void CaptureClosed()
    {
        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
    }

    [ContextMenu("Capture Current As Open Pose")]
    void CaptureOpen()
    {
        openLocalPosition = transform.localPosition;
        openLocalRotation = transform.localRotation;
    }
}
