using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모든 비콘 활성화(GameProgress.EscapeUnlocked = 활성 >= 필요 개수, 개수 하드코딩 없음) 시
/// 탈출구 컷씬을 한 번만 재생: 조작 잠금 → ExitCutsceneCamera → 닫힌 EscapeWall/EXIT → EscapeWall 개방(기존 EscapeDoorController 재사용)
/// → 열린 출구 → 플레이어 카메라 복귀 → 조작 재개.
///  - 입력 잠금: 기존 FirstPersonController.SetControlsEnabled(false)(이동/시선/손전등/R) + PlayerInteraction.SetInteractionEnabled(false)(E/슬롯/Drop).
///    ESC 는 GameMenuManager.CanPause 가 IsPlaying 을 보고 막는다. Time.timeScale 은 건드리지 않는다.
///  - 몬스터: 컷씬 동안 MonsterChase 비활성(추적/공격/PlayerCaughtSequence 발생 불가) + NavMeshAgent 정지, 종료 시 복구.
///  - Clear 는 건드리지 않음 (EscapeZone 통과 시에만 기존대로).
/// 씬에 ExitCutscene 오브젝트로 배치(ExitCutsceneCamera 참조). 카메라/문 참조가 비어 있으면 자동 탐색.
/// </summary>
public class ExitCutsceneController : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    public Camera cutsceneCamera;
    public EscapeDoorController escapeWall;

    [Header("Timing (초)")]
    public float fadeTime = 0.2f;
    [Tooltip("페이드 인 후 닫힌 문을 보여주는 시간.")]
    public float closedHold = 0.5f;
    [Tooltip("문이 완전히 열린 뒤 보여주는 시간.")]
    public float openHold = 1.0f;
    [Tooltip("EscapeWall 개방 시간. EscapeDoorController.openDuration 에 적용(컷씬에 맞춰 약 3초).")]
    public float doorOpenDuration = 3.0f;

    [Header("Cinematic Camera (ExitCutsceneCamera 의 배치 위치가 시작점)")]
    [Tooltip("문이 열리는 동안 EscapeWall 쪽으로 천천히 접근하는 거리(m).")]
    public float dollyDistance = 1.2f;
    [Tooltip("종료 시선: 0 = 문 중앙, 1 = EXIT 표지. 약한 회전용.")]
    [Range(0f, 1f)] public float endAimTowardExit = 0.45f;
    [Tooltip("시작 시선이 문 중앙에서 옆으로 비켜 있는 거리(m). 접근하며 중앙으로 정렬되는 약한 패닝.")]
    public float startAimSideOffset = 1.2f;

    [Header("HUD")]
    [Tooltip("컷씬 중 숨길 Gameplay HUD 캔버스(Screen Space). 비어 있으면 씬의 루트 캔버스를 자동 수집(Fade 캔버스 제외).")]
    public bool hideHudDuringCutscene = true;

    bool played;
    Image fade;
    Canvas fadeCanvas;
    readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new System.Collections.Generic.List<Canvas>();

    void Awake()
    {
        if (escapeWall == null) escapeWall = FindFirstObjectByType<EscapeDoorController>();
        if (escapeWall != null) escapeWall.deferOpen = true;   // 컷씬이 개방 시점을 제어
        if (cutsceneCamera != null) cutsceneCamera.enabled = false;
    }

    void OnEnable() => GameProgress.Changed += OnProgress;

    void OnDisable()
    {
        GameProgress.Changed -= OnProgress;
        ShowHud();
        IsPlaying = false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // [테스트 전용 — Editor / Development Build 에서만 컴파일. Release Build 에는 코드 자체가 없다]
    // F9: 아직 활성화되지 않은 모든 Beacon 을 완료 처리(진행도/상태등/EscapeWall 표시등 갱신) → 마지막 Beacon 에서 정상 경로와 같이 컷씬 시작.
    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.f9Key.wasPressedThisFrame) return;
        if (played || IsPlaying || GameProgress.IsEnded || GameMenuManager.MenuOpen) return;
        if (SkillCheckManager.Instance != null && SkillCheckManager.Instance.IsActive) return;
        int n = 0;
        foreach (var b in FindObjectsByType<Beacon>(FindObjectsSortMode.None))
            if (b.DebugComplete()) n++;
        Debug.Log("[ExitCutscene] F9 debug: completed " + n + " beacon(s) -> " + GameProgress.ActiveBeacons + "/" + GameProgress.RequiredBeacons);
    }
#endif

    void OnProgress()
    {
        if (played || !GameProgress.EscapeUnlocked) return;
        played = true;                       // 두 번 재생 금지
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return null;                   // Beacon 이 마지막 표시등(ActivateNext)을 켠 뒤부터 시작

        // 잡힘 연출 중이면 끝날 때까지 대기 (조작 상태 충돌 방지)
        while (PlayerCaughtSequence.IsRunning) yield return null;

        if (GameProgress.IsEnded || cutsceneCamera == null || escapeWall == null)
        {
            // 안전장치: 컷씬 불가 시에도 문은 연다 (진행 불능 방지)
            if (escapeWall != null) { escapeWall.openDelay = 0.5f; escapeWall.OpenNow(); }
            yield break;
        }

        IsPlaying = true;
        var fpc = FindFirstObjectByType<FirstPersonController>();
        var pi = FindFirstObjectByType<PlayerInteraction>();
        bool hadControls = fpc != null && fpc.ControlsEnabled;
        bool hadInteraction = pi != null && pi.InteractionEnabled;
        Camera playerCam = fpc != null && fpc.playerCamera != null ? fpc.playerCamera : Camera.main;

        // 0.0s: 조작 잠금 + 몬스터 정지
        if (fpc != null) fpc.SetControlsEnabled(false);
        if (pi != null) pi.SetInteractionEnabled(false);
        var monsters = FindObjectsByType<MonsterChase>(FindObjectsSortMode.None);
        var wasEnabled = new bool[monsters.Length];
        for (int i = 0; i < monsters.Length; i++)
        {
            wasEnabled[i] = monsters[i].enabled;
            monsters[i].enabled = false;
            var ag = monsters[i].GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (ag != null && ag.enabled && ag.isOnNavMesh) { ag.isStopped = true; ag.velocity = Vector3.zero; }
        }

        EnsureFade();
        yield return Fade(0f, 1f);                       // 암전
        if (hideHudDuringCutscene) HideHud();            // 표시 여부만 끈다 (삭제/재생성 없음)
        if (playerCam != null) playerCam.enabled = false;
        cutsceneCamera.enabled = true;
        SetupCameraMove();
        yield return Fade(1f, 0f);                       // 컷씬 카메라 화면
        StartCoroutine(MoveCamera(closedHold + doorOpenDuration));   // 닫힌 문 → 개방 동안 천천히 접근

        yield return new WaitForSeconds(closedHold);     // 닫힌 문 + EXIT

        escapeWall.openDelay = 0f;
        escapeWall.openDuration = doorOpenDuration;
        escapeWall.OpenNow();                            // 기존 EscapeWall 개방 로직 재사용
        float guard = doorOpenDuration + 3f;
        while (!escapeWall.IsOpen && guard > 0f) { guard -= Time.deltaTime; yield return null; }

        yield return new WaitForSeconds(openHold);       // 열린 출구

        yield return Fade(0f, 1f);
        cutsceneCamera.enabled = false;
        if (playerCam != null) playerCam.enabled = true;
        ShowHud();

        // 조작 재개 + 몬스터 복구 (Clear 는 EscapeZone 이 담당 — 여기서는 처리하지 않음)
        if (fpc != null && hadControls) fpc.SetControlsEnabled(true);
        if (pi != null && hadInteraction) pi.SetInteractionEnabled(true);
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] == null) continue;
            monsters[i].enabled = wasEnabled[i];
            var ag = monsters[i].GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (ag != null && ag.enabled && ag.isOnNavMesh) ag.isStopped = false;
        }
        IsPlaying = false;
        yield return Fade(1f, 0f);
    }

    // ───────────────────────── Cinematic camera ─────────────────────────

    Vector3 camP0, camP1;
    Quaternion camR0, camR1;

    void SetupCameraMove()
    {
        Transform ct = cutsceneCamera.transform;
        camP0 = ct.position;

        // 문 중앙(닫힌 상태의 두 문짝 중간) / EXIT 표지
        Vector3 doorCenter = escapeWall.transform.position + Vector3.up * 1.5f;
        if (escapeWall.doorLeft != null && escapeWall.doorRight != null)
        {
            doorCenter = (escapeWall.doorLeft.position + escapeWall.doorRight.position) * 0.5f;
            doorCenter.y = Mathf.Max(doorCenter.y, 1.4f);
        }
        Vector3 exitPos = doorCenter + Vector3.up * 1.1f;
        var sign = FindFirstObjectByType<ExitSign>();
        if (sign != null) exitPos = sign.transform.position;

        // 접근: 수평으로 문 쪽 dollyDistance, 높이는 그대로 (크게 날지 않는다)
        Vector3 flat = doorCenter - camP0; flat.y = 0f;
        camP1 = camP0 + flat.normalized * dollyDistance;

        Vector3 aimEnd = Vector3.Lerp(doorCenter, exitPos, endAimTowardExit);
        // 시작 시선은 문 중앙에서 옆(왼쪽)으로 살짝 비켜 주변 공간을 보여주고, 접근하면서 문/EXIT 쪽으로 천천히 정렬 (약한 패닝).
        Vector3 right = Vector3.Cross(Vector3.up, flat.normalized);
        Vector3 aimStart = doorCenter - right * startAimSideOffset;
        camR0 = Quaternion.LookRotation(aimStart - camP0, Vector3.up);
        camR1 = Quaternion.LookRotation(aimEnd - camP1, Vector3.up);

        ct.SetPositionAndRotation(camP0, camR0);
    }

    // 개방이 끝나는 시점에 속도 0 으로 도착 (SmoothStep) → 열린 출구를 보여주는 구간에서는 거의 정지.
    IEnumerator MoveCamera(float duration)
    {
        float t = 0f;
        Transform ct = cutsceneCamera.transform;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duration)));
            ct.SetPositionAndRotation(Vector3.Lerp(camP0, camP1, k), Quaternion.Slerp(camR0, camR1, k));
            yield return null;
        }
        ct.SetPositionAndRotation(camP1, camR1);
    }

    // ───────────────────────── HUD ─────────────────────────

    void HideHud()
    {
        hiddenCanvases.Clear();
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c == fadeCanvas || !c.enabled) continue;
            if (c.renderMode == RenderMode.WorldSpace) continue;
            c.enabled = false;
            hiddenCanvases.Add(c);
        }
    }

    void ShowHud()
    {
        foreach (var c in hiddenCanvases)
            if (c != null) c.enabled = true;
        hiddenCanvases.Clear();
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            SetFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, fadeTime))));
            yield return null;
        }
        SetFade(to);
    }

    void SetFade(float a)
    {
        if (fade == null) return;
        fade.color = new Color(0f, 0f, 0f, a);
        fade.gameObject.SetActive(a > 0.001f);
    }

    void EnsureFade()
    {
        if (fade != null) return;
        var go = new GameObject("ExitCutsceneFade", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var c = go.GetComponent<Canvas>();
        fadeCanvas = c;
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 5000;
        var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
        img.transform.SetParent(go.transform, false);
        fade = img.GetComponent<Image>();
        fade.raycastTarget = false;
        var rt = fade.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        SetFade(0f);
    }
}
