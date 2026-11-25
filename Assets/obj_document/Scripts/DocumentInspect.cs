using DeadSignal.Audio;
using UnityEngine;

/// <summary>
/// 바닥·책상·상자 속 서류(일기, 쪽지, 실험 기록 등)를 E 로 읽는 공통 상호작용.
/// 같은 서류 메시에 이 컴포넌트를 붙이고 인스펙터 값만 바꿔 서로 다른 내용을 띄운다.
///  - Image : 배경을 어둡게 하고 서류 이미지를 화면 중앙에 띄운다 (조작 잠금, E 또는 ESC 로 닫기). PosterOverlayUI 사용.
///  - Text  : 게임을 멈추지 않고 화면 하단에 자막처럼 글을 띄웠다가 저절로 사라진다 (조작 그대로). DocumentSubtitleUI 사용.
/// 줌은 없다 (PosterInspect 와 달리 놓인 방향 무관). 콜라이더가 없으면 메시 크기에 맞는 BoxCollider 를 자동으로 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class DocumentInspect : MonoBehaviour, IInteractable
{
    public enum ContentMode { Image, Text }

    [Tooltip("Image: 서류 이미지 / Text: 화면 하단 글")]
    public ContentMode mode = ContentMode.Text;

    [Tooltip("하단 안내 문구")]
    public string prompt = "[E] READ";

    [Header("Image 모드")]
    [Tooltip("화면에 띄울 서류 이미지")]
    public Texture image;
    [Tooltip("(선택) 이미지 아래 설명 한 줄")]
    [TextArea(1, 3)] public string caption = "";

    [Header("Text 모드")]
    [Tooltip("화면 하단에 그대로 출력할 글 (줄바꿈 그대로 표시)")]
    [TextArea(4, 15)] public string text = "";
    [Tooltip("글이 완전히 보이는 시간(초). 0 이면 글 길이로 자동 (3~14초)")]
    [Min(0f)] public float displaySeconds = 0f;

    [Header("연출")]
    [Tooltip("열 때 재생할 사운드 (AudioManager 이름). 비우면 없음")]
    public string openSound = "item_docu";
    [Tooltip("UI 가 나타나고 사라지는 시간(초)")]
    [Range(0.05f, 1f)] public float fadeSeconds = 0.2f;

    public static DocumentInspect Active { get; private set; }
    public static bool IsInspecting => Active != null;

    enum Phase { Idle, In, Open, Out }
    Phase phase = Phase.Idle;
    float t;
    int enterFrame;

    FirstPersonController controller;
    PlayerInteraction interaction;
    bool savedControls, savedInteraction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active = null;

    void Awake() => EnsureCollider(gameObject);

    // ───────── IInteractable ─────────
    public string GetInteractionText() => (!isActiveAndEnabled || IsInspecting || PosterInspect.IsInspecting) ? string.Empty : prompt;

    public void Interact(PlayerInteraction player)
    {
        if (!isActiveAndEnabled || player == null || phase != Phase.Idle) return;
        if (IsInspecting || PosterInspect.IsInspecting) return;
        if (PlayerHide.IsPlayerHiding || GameMenuManager.MenuOpen) return;

        if (mode == ContentMode.Text)
        {
            // 원샷 자막: 조작을 막지 않고 바로 끝난다 (다시 E 를 누르면 처음부터 다시 표시)
            DocumentSubtitleUI.Show(text, displaySeconds > 0f ? displaySeconds : DocumentSubtitleUI.AutoSeconds(text));
            if (!string.IsNullOrEmpty(openSound)) AudioManager.Play(openSound);
            return;
        }

        controller = player.GetComponent<FirstPersonController>();
        interaction = player;
        savedControls = controller == null || controller.ControlsEnabled;
        savedInteraction = interaction.InteractionEnabled;
        if (controller != null) controller.SetControlsEnabled(false);
        interaction.SetInteractionEnabled(false);

        Active = this;
        enterFrame = Time.frameCount;

        PosterOverlayUI.Show(image, caption);
        PosterOverlayUI.SetAlpha(0f);
        if (!string.IsNullOrEmpty(openSound)) AudioManager.Play(openSound);

        t = 0f;
        phase = Phase.In;
    }

    // ───────── 진행 ─────────
    void LateUpdate()
    {
        if (phase == Phase.Idle) return;

        // 잡힘 연출 등 다른 연출이 시작되면 즉시 닫는다
        if (PlayerCaughtSequence.IsRunning) { Finish(); return; }

        if (Time.timeScale > 0f && Time.frameCount > enterFrame && PosterInspect.ExitPressed())
            BeginClose();

        float dt = Time.unscaledDeltaTime;
        switch (phase)
        {
            case Phase.In:
                t += dt / Mathf.Max(0.01f, fadeSeconds);
                PosterOverlayUI.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                if (t >= 1f) phase = Phase.Open;
                break;

            case Phase.Open:
                PosterOverlayUI.SetAlpha(GameMenuManager.MenuOpen ? 0f : 1f);
                break;

            case Phase.Out:
                t += dt / Mathf.Max(0.01f, fadeSeconds);
                PosterOverlayUI.SetAlpha(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                if (t >= 1f) Finish();
                break;
        }
    }

    void BeginClose()
    {
        if (phase == Phase.In) { t = 1f - Mathf.Clamp01(t); phase = Phase.Out; }   // 현재 투명도에서 자연스럽게 사라지게
        else if (phase == Phase.Open) { t = 0f; phase = Phase.Out; }
    }

    void Finish()
    {
        if (phase == Phase.Idle) return;
        PosterOverlayUI.Hide();

        if (!PlayerCaughtSequence.IsRunning)   // 잡힘 연출이 조작을 쥐고 있으면 건드리지 않는다
        {
            if (controller != null) controller.SetControlsEnabled(savedControls);
            if (interaction != null) interaction.SetInteractionEnabled(savedInteraction);
        }

        phase = Phase.Idle;
        if (Active == this) Active = null;
        controller = null;
        interaction = null;
    }

    void OnDisable() => Finish();

    /// <summary>연출 없이 즉시 닫고 조작을 되돌린다.</summary>
    public void Close() => Finish();

    /// <summary>지금 보고 있는 서류가 있으면 즉시 닫는다.</summary>
    public static void CloseActive()
    {
        if (Active != null) Active.Finish();
    }

    // ───────── 콜라이더 ─────────
    /// <summary>자식 포함 콜라이더가 하나도 없으면 렌더러 전체 범위에 맞는 BoxCollider 를 루트에 붙인다.</summary>
    public static BoxCollider EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>(true) != null) return null;

        var root = go.transform;
        bool any = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds lb;
            if (r is SkinnedMeshRenderer smr) lb = smr.localBounds;
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                lb = mf.sharedMesh.bounds;
            }
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = root.InverseTransformPoint(r.transform.TransformPoint(c));
                if (!any) { min = max = l; any = true; }
                else { min = Vector3.Min(min, l); max = Vector3.Max(max, l); }
            }
        }

        var box = go.AddComponent<BoxCollider>();
        if (any)
        {
            box.center = (min + max) * 0.5f;
            // 얇은 종이도 레이캐스트에 잘 걸리도록 모든 축에 최소 두께 보장 (월드 기준 약 1cm)
            Vector3 s = root.lossyScale;
            float scale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
            box.size = Vector3.Max(max - min, Vector3.one * (0.01f / scale));
        }
        return box;
    }
}
