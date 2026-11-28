using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 포스터/튜토리얼 그림을 E 로 들여다보는 상호작용. PlayerInteraction 의 IInteractable(E) 경로로 동작한다.
/// 보기 방식(mode):
///  - Zoom            : 카메라가 포스터 정면으로 부드럽게 다가간다(그 자리 조명 그대로).
///  - Overlay         : 화면을 어둡게 하고 포스터 이미지를 화면 중앙에 2D 로 띄운다(조명과 무관하게 항상 선명).
///  - ZoomThenOverlay : 짧게 줌인한 뒤 화면 UI 로 넘어간다. 닫으면 UI 가 사라지고 원래 시점으로 돌아온다.
/// 닫기: E 또는 마우스 오른쪽 버튼 (ESC 는 일시정지 메뉴가 쓰므로 사용하지 않는다).
/// 보는 동안 FirstPersonController / PlayerInteraction 의 공개 스위치만 꺼서 이동·시선·다른 상호작용을 막는다(PlayerHide 와 같은 방식).
/// UI 는 처음 쓸 때 PosterOverlayUI 가 자동 생성하므로 씬에 따로 둘 것이 없다(프리팹만으로 동작).
/// 방향 규칙: 루트의 +Z(파란 화살표)가 벽 안쪽, 앞면은 -Z 를 본다.
/// </summary>
[DisallowMultipleComponent]
public class PosterInspect : MonoBehaviour, IInteractable
{
    public enum ViewMode { Zoom, Overlay, ZoomThenOverlay }

    [Tooltip("보기 방식")]
    public ViewMode mode = ViewMode.ZoomThenOverlay;

    [Tooltip("하단 안내 문구")]
    public string prompt = "[E] LOOK";

    [Header("화면 UI (Overlay)")]
    [Tooltip("화면에 띄울 이미지. 비우면 target 머티리얼의 텍스처(포스터 그림)를 쓴다.")]
    public Texture overlayImage;
    [Tooltip("(선택) 이미지 아래 설명 한 줄")]
    [TextArea(1, 3)] public string caption = "";
    [Tooltip("UI 가 나타나고 사라지는 시간(초)")]
    [Range(0.05f, 1f)] public float fadeSeconds = 0.2f;

    [Header("줌 (Zoom)")]
    [Tooltip("화면에 맞출 대상 렌더러. 비우면 자식 렌더러 전체 범위를 쓴다.")]
    public Renderer target;
    [Tooltip("(선택) 카메라를 둘 위치. 이 Transform 의 +Z 가 포스터를 향해야 한다. 비우면 자동 계산.")]
    public Transform viewPoint;
    [Tooltip("화면 대비 여백 배율 (1 = 꽉 참, 1.2 = 20% 여백)")]
    [Range(1f, 2f)] public float framePadding = 1.15f;
    [Tooltip("포스터에서 최소 거리(m). 카메라 near clip 보다 커야 한다.")]
    public float minDistance = 0.35f;
    [Tooltip("줌 이동 시간(초). ZoomThenOverlay 에서는 이 값의 2/3 을 쓴다.")]
    [Range(0.05f, 1.5f)] public float zoomSeconds = 0.45f;
    [Tooltip("줌 중 카메라 FOV. 0 이면 바꾸지 않는다.")]
    [Range(0f, 90f)] public float zoomFov = 0f;

    // 현재 보고 있는 포스터 (전역에 하나)
    public static PosterInspect Active { get; private set; }
    public static bool IsInspecting => Active != null;

    enum Phase { Idle, ZoomIn, Hold, OverlayIn, Overlay, OverlayOut, ZoomOut }
    Phase phase = Phase.Idle;

    FirstPersonController controller;
    PlayerInteraction interaction;
    Camera cam;
    Transform camT;

    Vector3 savedLocalPos;
    Quaternion savedLocalRot;
    float savedFov;
    bool savedControls, savedInteraction;   // 열기 전 조작 상태 (닫을 때 그대로 되돌림)

    Vector3 fromPos, toPos;
    Quaternion fromRot, toRot;
    float fromFov, toFov;
    float t;
    int enterFrame;

    bool UsesZoom => mode != ViewMode.Overlay;
    bool UsesOverlay => mode != ViewMode.Zoom;
    float ZoomTime => mode == ViewMode.ZoomThenOverlay ? zoomSeconds * 0.66f : zoomSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active = null;

    // ───────── IInteractable ─────────
    // 컴포넌트 체크를 끄면 안내 문구도, E 반응도 없다 (PlayerInteraction 은 enabled 를 확인하지 않으므로 여기서 막는다)
    public string GetInteractionText() => (!isActiveAndEnabled || IsInspecting) ? string.Empty : prompt;

    public void Interact(PlayerInteraction player)
    {
        if (!isActiveAndEnabled || player == null || IsInspecting || phase != Phase.Idle) return;
        if (PlayerHide.IsPlayerHiding || GameMenuManager.MenuOpen) return;

        controller = player.GetComponent<FirstPersonController>();
        interaction = player;
        cam = controller != null && controller.playerCamera != null ? controller.playerCamera : player.playerCamera;
        if (cam == null) return;
        camT = cam.transform;

        savedLocalPos = camT.localPosition;
        savedLocalRot = camT.localRotation;
        savedFov = cam.fieldOfView;

        savedControls = controller == null || controller.ControlsEnabled;
        savedInteraction = interaction.InteractionEnabled;
        if (controller != null) controller.SetControlsEnabled(false);
        interaction.SetInteractionEnabled(false);

        Active = this;
        enterFrame = Time.frameCount;
        t = 0f;

        if (UsesZoom)
        {
            ComputeView(out toPos, out toRot);
            fromPos = camT.position;
            fromRot = camT.rotation;
            fromFov = savedFov;
            toFov = zoomFov > 0f ? zoomFov : savedFov;
            phase = Phase.ZoomIn;
        }
        else
        {
            BeginOverlay();
        }
    }

    // ───────── 진행 ─────────
    void LateUpdate()
    {
        if (phase == Phase.Idle) return;

        // 잡힘 컷씬 등 다른 연출이 시작되면 즉시 원래대로 (카메라/화면 다툼 방지)
        if (PlayerCaughtSequence.IsRunning || camT == null) { Finish(); return; }

        if (Time.timeScale > 0f && Time.frameCount > enterFrame && ExitPressed())
            BeginClose();

        float dt = Time.unscaledDeltaTime;
        switch (phase)
        {
            case Phase.ZoomIn:
                t += dt / Mathf.Max(0.01f, ZoomTime);
                ApplyLerp(fromPos, toPos, fromRot, toRot, fromFov, toFov, t);
                if (t >= 1f)
                {
                    if (UsesOverlay) BeginOverlay();
                    else phase = Phase.Hold;
                }
                break;

            case Phase.Hold:
                ApplyLerp(toPos, toPos, toRot, toRot, toFov, toFov, 1f);
                break;

            case Phase.OverlayIn:
                HoldZoomIfAny();
                t += dt / Mathf.Max(0.01f, fadeSeconds);
                PosterOverlayUI.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                if (t >= 1f) phase = Phase.Overlay;
                break;

            case Phase.Overlay:
                HoldZoomIfAny();
                PosterOverlayUI.SetAlpha(GameMenuManager.MenuOpen ? 0f : 1f);   // 일시정지 메뉴와 겹쳐 글씨가 안 읽히는 것 방지 (재개하면 다시 표시)
                break;

            case Phase.OverlayOut:
                HoldZoomIfAny();
                t += dt / Mathf.Max(0.01f, fadeSeconds);
                PosterOverlayUI.SetAlpha(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                if (t >= 1f)
                {
                    PosterOverlayUI.Hide();
                    if (UsesZoom) BeginZoomOut();
                    else Finish();
                }
                break;

            case Phase.ZoomOut:
                t += dt / Mathf.Max(0.01f, ZoomTime);
                // 복귀 목표는 매 프레임 원래 로컬 자리(부모 기준)로 다시 계산
                Transform parent = camT.parent;
                Vector3 homePos = parent != null ? parent.TransformPoint(savedLocalPos) : savedLocalPos;
                Quaternion homeRot = parent != null ? parent.rotation * savedLocalRot : savedLocalRot;
                ApplyLerp(fromPos, homePos, fromRot, homeRot, fromFov, savedFov, t);
                if (t >= 1f) Finish();
                break;
        }
    }

    void HoldZoomIfAny()
    {
        if (UsesZoom) ApplyLerp(toPos, toPos, toRot, toRot, toFov, toFov, 1f);
    }

    void BeginOverlay()
    {
        PosterOverlayUI.Show(ResolveImage(), caption);
        PosterOverlayUI.SetAlpha(0f);
        t = 0f;
        phase = Phase.OverlayIn;
    }

    void BeginClose()
    {
        switch (phase)
        {
            case Phase.ZoomIn:                                // UI 가 뜨기 전에 닫으면 바로 복귀
            case Phase.Hold:
                BeginZoomOut();
                break;
            case Phase.OverlayIn:
            case Phase.Overlay:
                // 현재 투명도에서 자연스럽게 사라지도록 진행도를 맞춘다
                t = phase == Phase.OverlayIn ? 1f - Mathf.Clamp01(t) : 0f;
                phase = Phase.OverlayOut;
                break;
        }
    }

    void ApplyLerp(Vector3 p0, Vector3 p1, Quaternion r0, Quaternion r1, float f0, float f1, float k)
    {
        float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        camT.SetPositionAndRotation(Vector3.Lerp(p0, p1, e), Quaternion.Slerp(r0, r1, e));
        cam.fieldOfView = Mathf.Lerp(f0, f1, e);
    }

    void BeginZoomOut()
    {
        fromPos = camT.position;
        fromRot = camT.rotation;
        fromFov = cam.fieldOfView;
        t = 0f;
        phase = Phase.ZoomOut;
    }

    static bool ExitPressed()
    {
        var kb = Keyboard.current;
        var ms = Mouse.current;
        return (kb != null && kb.eKey.wasPressedThisFrame) || (ms != null && ms.rightButton.wasPressedThisFrame);
    }

    void Finish()
    {
        if (phase == Phase.Idle) return;
        if (UsesOverlay) PosterOverlayUI.Hide();

        if (camT != null)
        {
            camT.localPosition = savedLocalPos;
            camT.localRotation = savedLocalRot;
        }
        if (cam != null) cam.fieldOfView = savedFov;
        if (!PlayerCaughtSequence.IsRunning)   // 잡힘 연출이 조작을 쥐고 있으면 건드리지 않는다
        {
            var ms = Mouse.current;
            if (controller != null && ms != null && ms.rightButton.isPressed && controller.isActiveAndEnabled)
            {
                // 우클릭으로 닫은 뒤 버튼을 계속 누르고 있으면, 조작이 돌아오는 순간 손전등이 켜지고(= 몬스터 노출/Stun 배터리 소모) 의도치 않게 동작한다.
                // 닫는 데 쓴 우클릭은 버튼을 뗄 때까지 소비된 것으로 보고, 그때 조작을 돌려준다.
                controller.StartCoroutine(RestoreAfterRmbReleased(controller, interaction, savedControls, savedInteraction));
            }
            else
            {
                if (controller != null) controller.SetControlsEnabled(savedControls);
                if (interaction != null) interaction.SetInteractionEnabled(savedInteraction);
            }
        }

        phase = Phase.Idle;
        if (Active == this) Active = null;
        controller = null;
        interaction = null;
        cam = null;
        camT = null;
    }

    static System.Collections.IEnumerator RestoreAfterRmbReleased(FirstPersonController c, PlayerInteraction pi, bool controls, bool inter)
    {
        var ms = Mouse.current;
        while (ms != null && ms.rightButton.isPressed) yield return null;
        // 기다리는 사이 다른 연출(잡힘)이나 새 포스터 조사가 조작을 쥐었다면 건드리지 않는다
        if (PlayerCaughtSequence.IsRunning || IsInspecting || c == null) yield break;
        c.SetControlsEnabled(controls);
        if (pi != null) pi.SetInteractionEnabled(inter);
    }

    void OnDisable() => Finish();

    /// <summary>E 를 누른 것과 같이 연출(UI 사라짐 → 줌아웃)과 함께 닫는다.</summary>
    public void RequestClose()
    {
        if (phase != Phase.Idle) BeginClose();
    }

    /// <summary>연출 없이 즉시 닫고 조작을 되돌린다. (컷씬 등 외부에서 강제로 끊을 때)</summary>
    public void Close() => Finish();

    /// <summary>지금 보고 있는 포스터가 있으면 즉시 닫는다.</summary>
    public static void CloseActive()
    {
        if (Active != null) Active.Finish();
    }

    // ───────── 이미지 ─────────
    Texture ResolveImage()
    {
        if (overlayImage != null) return overlayImage;
        var r = target != null ? target : GetComponentInChildren<Renderer>();
        var m = r != null ? r.sharedMaterial : null;
        if (m == null) return null;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m.GetTexture("_BaseMap");
        return m.mainTexture;
    }

    // ───────── 카메라 위치 계산 ─────────
    void ComputeView(out Vector3 pos, out Quaternion rot)
    {
        Vector3 front = -transform.forward;   // 포스터 앞면이 보는 방향(방 안쪽)

        if (viewPoint != null)
        {
            pos = viewPoint.position;
            rot = viewPoint.rotation;
            return;
        }

        GetFrontBounds(out Vector3 center, out float width, out float height);

        float fov = (zoomFov > 0f ? zoomFov : cam.fieldOfView) * Mathf.Deg2Rad;
        float tanV = Mathf.Tan(fov * 0.5f);
        float tanH = tanV * Mathf.Max(0.1f, cam.aspect);
        float d = Mathf.Max(height * 0.5f / tanV, width * 0.5f / tanH) * framePadding;
        d = Mathf.Max(d, minDistance, cam.nearClipPlane * 2f);

        pos = center + front * d;
        rot = Quaternion.LookRotation(-front, transform.up);
    }

    /// <summary>포스터 평면(로컬 XY) 기준 중심·가로·세로. 회전된 포스터도 정확하게 계산한다.</summary>
    void GetFrontBounds(out Vector3 center, out float width, out float height)
    {
        Renderer[] rs = target != null ? new[] { target } : GetComponentsInChildren<Renderer>();
        bool any = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;

        foreach (var r in rs)
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = transform.InverseTransformPoint(r.transform.TransformPoint(c));
                if (!any) { min = max = l; any = true; }
                else { min = Vector3.Min(min, l); max = Vector3.Max(max, l); }
            }
        }

        if (!any) { center = transform.position; width = height = 1f; return; }

        Vector3 lc = (min + max) * 0.5f;
        lc.z = min.z;                                   // 앞면 기준
        center = transform.TransformPoint(lc);
        Vector3 s = transform.lossyScale;
        width = (max.x - min.x) * Mathf.Abs(s.x);
        height = (max.y - min.y) * Mathf.Abs(s.y);
    }
}
