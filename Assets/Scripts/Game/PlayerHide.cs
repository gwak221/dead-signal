using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 플레이어 숨기 상태. Player 오브젝트에 붙는다 (HideSpot이 첫 사용 시 자동 추가).
/// 숨는 동안 기존 FirstPersonController / PlayerInteraction의 공개 스위치만 사용해 차단한다:
///   - FirstPersonController.SetControlsEnabled(false) : 이동/달리기/마우스 시선 + 손전등(FlashlightController가 ControlsEnabled 필요)
///   - PlayerInteraction.SetInteractionEnabled(false)  : E 상호작용 전부 + 인벤토리 단축키(아이템 Drop 포함)
/// 인벤토리 내용, 신체 파손, 하트 상태는 건드리지 않는다.
/// 나중에 MonsterChase 등이 PlayerHide.IsPlayerHiding 또는 인스턴스의 IsHiding을 읽으면 된다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHide : MonoBehaviour
{
    static PlayerHide instance;

    /// <summary>외부(몬스터 AI 등)용 정적 읽기 전용 상태.</summary>
    public static bool IsPlayerHiding => instance != null && instance.IsHiding;
    public static PlayerHide Instance => instance;

    /// <summary>현재 숨어 있는지. (읽기 전용)</summary>
    public bool IsHiding { get; private set; }
    public HideSpot CurrentSpot { get; private set; }

    /// <summary>숨기/나오기 시 호출 (true = 숨음).</summary>
    public event Action<bool> OnHidingChanged;

    [Header("Look Limits (숨은 동안 시선 제한)")]
    [Range(5f, 80f)] public float yawLimit = 25f;
    [Range(0f, 40f)] public float pitchUpLimit = 8f;
    [Range(0f, 40f)] public float pitchDownLimit = 8f;

    [Header("Camera")]
    [Tooltip("숨은 동안 카메라 near clip (쇠창살 바로 뒤에서 앞판이 잘리지 않게).")]
    public float hiddenNearClip = 0.03f;

    FirstPersonController controller;
    PlayerInteraction interaction;
    CharacterController characterController;

    float baseYaw, yaw, pitch;
    float savedNearClip = 0.3f;
    int enterFrame;

    GameObject overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    void Awake()
    {
        instance = this;
        controller = GetComponent<FirstPersonController>();
        interaction = GetComponent<PlayerInteraction>();
        characterController = GetComponent<CharacterController>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static PlayerHide GetOrAdd(GameObject player)
    {
        var h = player.GetComponent<PlayerHide>();
        return h != null ? h : player.AddComponent<PlayerHide>();
    }

    public bool Enter(HideSpot spot)
    {
        if (IsHiding || spot == null || spot.hidePoint == null) return false;

        CurrentSpot = spot;
        if (characterController != null) characterController.enabled = false;

        Vector3 front = spot.FrontDirection;
        Quaternion look = Quaternion.LookRotation(front, Vector3.up);
        transform.SetPositionAndRotation(spot.hidePoint.position, look);

        // 카메라(눈)가 정확히 HideViewPoint에 오도록 몸 위치를 역산한다 (캐비넷마다 같은 로컬 구도).
        if (spot.viewPoint != null && controller != null && controller.playerCamera != null)
        {
            if (controller.cameraPivot != null) controller.cameraPivot.localRotation = Quaternion.identity;
            Vector3 eyeOffset = controller.playerCamera.transform.position - transform.position;
            transform.position = spot.viewPoint.position - eyeOffset;
        }
        if (controller != null && controller.playerCamera != null)
        {
            savedNearClip = controller.playerCamera.nearClipPlane;
            controller.playerCamera.nearClipPlane = Mathf.Min(savedNearClip, hiddenNearClip);
        }

        if (controller != null) controller.SetControlsEnabled(false);
        if (interaction != null) interaction.SetInteractionEnabled(false);

        baseYaw = transform.eulerAngles.y;
        yaw = 0f;
        pitch = 0f;
        enterFrame = Time.frameCount;
        IsHiding = true;

        ShowOverlay(true);
        OnHidingChanged?.Invoke(true);
        return true;
    }

    public void Exit()
    {
        if (!IsHiding) return;

        var spot = CurrentSpot;
        IsHiding = false;
        CurrentSpot = null;

        if (spot != null && spot.exitPoint != null)
        {
            // 캐비넷 앞(바깥쪽)을 보며 나온다.
            transform.SetPositionAndRotation(spot.exitPoint.position, Quaternion.LookRotation(spot.FrontDirection, Vector3.up));
        }

        if (controller != null && controller.cameraPivot != null)
            controller.cameraPivot.localRotation = Quaternion.identity;
        if (controller != null && controller.playerCamera != null)
            controller.playerCamera.nearClipPlane = savedNearClip;

        if (characterController != null) characterController.enabled = true;
        if (controller != null) controller.SetControlsEnabled(true);
        if (interaction != null) interaction.SetInteractionEnabled(true);

        ShowOverlay(false);
        OnHidingChanged?.Invoke(false);
    }

    void Update()
    {
        if (!IsHiding) return;

        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame && Time.frameCount > enterFrame)
        {
            Exit();
            return;
        }

        if (Mouse.current != null && controller != null)
            ApplyLookDelta(Mouse.current.delta.ReadValue() * controller.mouseSensitivity);
    }

    /// <summary>마우스 이동량(이미 감도 적용)을 제한 범위 안에서만 시선에 반영.</summary>
    public void ApplyLookDelta(Vector2 d)
    {
        if (!IsHiding) return;
        yaw = Mathf.Clamp(yaw + d.x, -yawLimit, yawLimit);
        pitch = Mathf.Clamp(pitch - d.y, -pitchUpLimit, pitchDownLimit);
        transform.rotation = Quaternion.Euler(0f, baseYaw + yaw, 0f);
        if (controller != null && controller.cameraPivot != null)
            controller.cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ───────────── 숨은 동안 힌트 (시야는 실제 캐비넷 모델의 쇠창살이 만든다) ─────────────

    void ShowOverlay(bool show)
    {
        if (overlay == null)
        {
            if (!show) return;
            overlay = BuildOverlay();
        }
        overlay.SetActive(show);
    }

    static GameObject BuildOverlay()
    {
        var canvasGo = new GameObject("HideOverlayCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var go = new GameObject("ExitHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasGo.transform, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = "E : Exit";
        t.fontSize = 30;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(1f, 1f, 1f, 0.75f);
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 90f);
        rt.sizeDelta = new Vector2(600f, 50f);

        return canvasGo;
    }

    static void MakePanel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.01f, 0.01f, 0.01f, 0.97f);
        img.raycastTarget = false;
    }
}
