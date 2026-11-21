using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 비콘 전용 파이프 연결 QTE (기존 원형 SkillCheck 를 대체; SkillCheckManager 는 아이템 상자 잠금용으로 계속 쓴다).
/// - Beacon 이 Open(onSuccess, onCancel) 을 명시적으로 호출한다. 퍼즐 자체는 입력을 감지하지 않는다.
/// - 열려 있는 동안 SkillCheckManager 와 같은 방식으로 이동/Sprint/시선/손전등(FirstPersonController)과
///   상호작용/인벤토리(PlayerInteraction)를 잠그고, 닫힐 때 시작 전 상태로 복구한다. 마우스 커서는 풀어 두었다가 되돌린다.
/// - 해결 → 짧게 '연결됨' 상태를 보여준 뒤 닫고 onSuccess (배터리 소비/활성화/진행도는 Beacon 의 기존 Success 흐름).
/// - ESC → onCancel (아무 변화 없음, 다시 E 로 새 퍼즐). 몬스터에게 잡히면 CancelActive 로 이벤트 없이 닫힌다.
/// 프리팹은 Resources/BeaconPipePuzzle (Canvas + PuzzleManager + BoardFitter + 타일 템플릿).
/// </summary>
public class BeaconPipeQte : MonoBehaviour
{
    const string PrefabPath = "BeaconPipePuzzle";

    static BeaconPipeQte instance;
    public static BeaconPipeQte Instance
    {
        get
        {
            if (instance == null) instance = FindFirstObjectByType<BeaconPipeQte>();
            if (instance == null) instance = new GameObject("BeaconPipeQte").AddComponent<BeaconPipeQte>();
            return instance;
        }
    }

    /// <summary>퍼즐 UI 가 열려 있는지 (메뉴/컷씬 등이 읽음). 인스턴스를 만들지 않는다.</summary>
    /// <summary>(닫힌 바로 그 프레임에도 true: 같은 ESC 입력이 일시정지 메뉴를 함께 여는 것을 막는다.)</summary>
    public static bool IsOpen => instance != null && (instance.isOpen || instance.closedFrame == Time.frameCount);

    [Tooltip("해결 후 '전력 연결됨' 화면을 보여주는 시간(초, 실시간)")]
    public float solvedHold = 0.9f;

    GameObject canvasGo;
    PuzzleManager puzzle;
    CanvasGroup group;
    TextMeshProUGUI hint;

    bool isOpen;
    int closedFrame = -1;
    bool finishing;
    int startFrame;
    Action cbSuccess, cbCancel;

    FirstPersonController controller;
    PlayerInteraction interaction;
    bool prevControls, prevInteraction;
    CursorLockMode prevLock;
    bool prevCursorVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>진행 중인 퍼즐을 성공/취소 이벤트 없이 닫는다 (몬스터에게 잡힌 경우 등).</summary>
    public static void CancelActive()
    {
        if (instance != null && instance.isOpen) instance.Close(false, false);
    }

    /// <summary>퍼즐 시작. 이미 열려 있거나 UI 를 만들 수 없으면 false.</summary>
    public bool Open(Action onSuccess, Action onCancel)
    {
        if (isOpen) return false;
        if (!EnsureUi()) return false;

        cbSuccess = onSuccess;
        cbCancel = onCancel;
        isOpen = true;
        finishing = false;
        startFrame = Time.frameCount;

        controller = FindFirstObjectByType<FirstPersonController>();
        interaction = FindFirstObjectByType<PlayerInteraction>();
        prevControls = controller != null && controller.ControlsEnabled;
        prevInteraction = interaction != null && interaction.InteractionEnabled;
        if (controller != null) controller.SetControlsEnabled(false);
        if (interaction != null) interaction.SetInteractionEnabled(false);

        prevLock = Cursor.lockState;
        prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        canvasGo.SetActive(true);
        group.blocksRaycasts = true;
        puzzle.BuildNewPuzzle();
        return true;
    }

    bool EnsureUi()
    {
        if (canvasGo != null) return true;
        var prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[BeaconPipeQte] Resources/" + PrefabPath + " 프리팹이 없습니다.");
            return false;
        }
        canvasGo = Instantiate(prefab, transform);
        canvasGo.name = "BeaconPipePuzzleCanvas";
        puzzle = canvasGo.GetComponentInChildren<PuzzleManager>(true);
        group = canvasGo.GetComponent<CanvasGroup>();
        if (group == null) group = canvasGo.AddComponent<CanvasGroup>();
        if (puzzle == null)
        {
            Debug.LogWarning("[BeaconPipeQte] 프리팹에 PuzzleManager 가 없습니다.");
            Destroy(canvasGo);
            return false;
        }
        puzzle.Solved += OnSolved;
        BuildHint();
        canvasGo.SetActive(false);
        return true;
    }

    void BuildHint()
    {
        var go = new GameObject("PipeHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasGo.transform, false);
        hint = go.GetComponent<TextMeshProUGUI>();
        hint.text = "CLICK A PIPE\nTO ROTATE IT\n\nCONNECT THE POWER\nTO THE END\n\n<size=70%>ESC : CANCEL</size>";
        hint.fontSize = 38;
        hint.fontStyle = FontStyles.Bold;
        hint.alignment = TextAlignmentOptions.Left;
        hint.color = new Color(1f, 1f, 1f, 0.95f);
        hint.raycastTarget = false;
        var rt = hint.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(60f, 0f);
        rt.sizeDelta = new Vector2(440f, 420f);
    }

    void OnSolved()
    {
        if (!isOpen || finishing) return;
        finishing = true;
        group.blocksRaycasts = false;   // 해결 후 더 못 돌리게
        StartCoroutine(FinishAfterHold());
    }

    IEnumerator FinishAfterHold()
    {
        float t = 0f;
        while (t < solvedHold) { t += Time.unscaledDeltaTime; yield return null; }
        Close(true, true);
    }

    void Update()
    {
        if (!isOpen || finishing) return;
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.frameCount > startFrame)
            Close(false, true);
    }

    void Close(bool success, bool raise)
    {
        if (!isOpen) return;
        isOpen = false;
        closedFrame = Time.frameCount;
        finishing = false;
        StopAllCoroutines();
        if (canvasGo != null) canvasGo.SetActive(false);

        // 조작 복구 (시작 전 상태 기준 - 다른 시스템이 잠근 것은 건드리지 않음)
        if (controller != null && prevControls) controller.SetControlsEnabled(true);
        if (interaction != null && prevInteraction) interaction.SetInteractionEnabled(true);
        Cursor.lockState = prevLock;
        Cursor.visible = prevCursorVisible;

        var s = cbSuccess; var c = cbCancel;
        cbSuccess = cbCancel = null;
        if (!raise) { c?.Invoke(); return; }   // 잡힘 등으로 강제 종료: 호출자 상태만 해제
        if (success) s?.Invoke(); else c?.Invoke();
    }
}
