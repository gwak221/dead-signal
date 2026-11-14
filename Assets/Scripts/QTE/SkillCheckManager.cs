using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 팀원 QTE(원형 스킬체크)를 New Input System으로 이식한 버전. 비콘 배터리 삽입 전용.
/// - 외부에서 TriggerSkillCheck() / TriggerSkillCheck(onSuccess, onFail) 로 시작.
/// - 판정은 Space(Keyboard.current.spaceKey)만 사용.
/// - OnSuccess / OnFail (UnityEvent) + 호출별 1회용 콜백 제공. QTE 코드는 비콘 로직을 모른다.
/// - 진행 중에는 이동/Sprint/상호작용/인벤토리/손전등을 잠그고, 종료 시 이전 상태로 복구한다.
/// </summary>
public class SkillCheckManager : MonoBehaviour
{
    static SkillCheckManager active;
    public static SkillCheckManager Instance { get; private set; }

    [Header("UI Components")]
    public GameObject skillCheckCanvas;
    public RectTransform needle;
    public Image successZoneImage;
    public RectTransform successZoneHolder;

    [Header("Settings")]
    public float rotationSpeed = 300f;
    public float successZoneSize = 45f;

    [Header("Calibration (위치 맞춤 보정)")]
    public float angleOffset = 0f;
    public float visualImageOffset = 69f; // 그림 위치 보정: 스크린샷으로 판정 구역(45°)과 흰 호가 겹치게 맞춤

    [Header("Fail Penalty")]
    [Tooltip("QTE 실패 시 HeartRateController.AddHeartRate 로 즉시 올릴 BPM (Max BPM 은 넘지 않음).")]
    public float failHeartRateBoost = 38f;

    [Header("Events")]
    public UnityEvent OnSuccess;
    public UnityEvent OnFail;

    [Header("Debug (실제 게임에서는 꺼둔다)")]
    public bool debugKeyT = false;

    float currentAngle;
    float zoneStartAngle;
    bool isActive;
    int startFrame;

    FirstPersonController controller;
    PlayerInteraction interaction;
    bool prevControls, prevInteraction;
    Action cbSuccess, cbFail;

    public bool IsActive => isActive;

    void Awake()
    {
        Instance = this;
        if (skillCheckCanvas != null) skillCheckCanvas.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (active == this) active = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; active = null; }

    /// <summary>진행 중인 QTE를 성공/실패 이벤트 없이 닫는다 (몬스터에게 잡힌 경우 등).</summary>
    public static void CancelActive()
    {
        if (active != null) active.End(false, false);
    }

    public bool TriggerSkillCheck() => TriggerSkillCheck(null, null);

    /// <summary>QTE 시작. 이미 진행 중이면 false (중복 실행 금지).</summary>
    public bool TriggerSkillCheck(Action onSuccess, Action onFail)
    {
        if (isActive) return false;

        cbSuccess = onSuccess;
        cbFail = onFail;
        isActive = true;
        active = this;
        startFrame = Time.frameCount;

        controller = FindFirstObjectByType<FirstPersonController>();
        interaction = FindFirstObjectByType<PlayerInteraction>();
        prevControls = controller != null && controller.ControlsEnabled;
        prevInteraction = interaction != null && interaction.InteractionEnabled;
        if (controller != null) controller.SetControlsEnabled(false);       // WASD/Sprint/시선/손전등
        if (interaction != null) interaction.SetInteractionEnabled(false);  // E 상호작용/인벤토리 선택·Drop

        EnsurePrompt();
        if (skillCheckCanvas != null) skillCheckCanvas.SetActive(true);

        zoneStartAngle = UnityEngine.Random.Range(30f, 300f);
        if (successZoneHolder != null)
            successZoneHolder.localRotation = Quaternion.Euler(0, 0, zoneStartAngle + angleOffset + visualImageOffset);
        currentAngle = 0f;
        if (needle != null) needle.localRotation = Quaternion.identity;
        return true;
    }

    /// <summary>QTE UI 아래에 'PRESS SPACE' 안내를 한 번만 만든다 (판정 로직과 무관).</summary>
    void EnsurePrompt()
    {
        if (skillCheckCanvas == null || skillCheckCanvas.transform.Find("PressSpacePrompt") != null) return;
        var go = new GameObject("PressSpacePrompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(skillCheckCanvas.transform, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = "PRESS SPACE";
        t.fontSize = 44;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(1f, 1f, 1f, 0.95f);
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -310f);
        rt.sizeDelta = new Vector2(800f, 70f);
    }

    void Update()
    {
        var kb = Keyboard.current;

        if (!isActive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugKeyT && kb != null && kb.tKey.wasPressedThisFrame) TriggerSkillCheck();
#endif
            return;
        }

        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle >= 360f) currentAngle -= 360f;
        if (needle != null) needle.localRotation = Quaternion.Euler(0, 0, currentAngle);

        if (kb != null && kb.spaceKey.wasPressedThisFrame && Time.frameCount > startFrame)
            Evaluate();
    }

    void Evaluate()
    {
        float zoneMin = Mod360(zoneStartAngle + angleOffset);
        float zoneMax = Mod360(zoneStartAngle + successZoneSize + angleOffset);

        bool ok = zoneMin <= zoneMax
            ? (currentAngle >= zoneMin && currentAngle <= zoneMax)
            : (currentAngle >= zoneMin || currentAngle <= zoneMax);

        Debug.Log("[SkillCheck] needle=" + currentAngle.ToString("F1") + " zone=" + zoneMin.ToString("F1") + "~" + zoneMax.ToString("F1") + " -> " + (ok ? "SUCCESS" : "FAIL"));
        End(true, ok);
    }

    static float Mod360(float a) => (a % 360f + 360f) % 360f;

    void End(bool raise, bool success)
    {
        if (!isActive) return;
        isActive = false;
        if (active == this) active = null;

        if (skillCheckCanvas != null) skillCheckCanvas.SetActive(false);

        // 조작 복구 (QTE 시작 전 상태 기준 - 다른 시스템이 잠근 것은 건드리지 않음)
        if (controller != null && prevControls) controller.SetControlsEnabled(true);
        if (interaction != null && prevInteraction) interaction.SetInteractionEnabled(true);

        var s = cbSuccess; var f = cbFail;
        cbSuccess = cbFail = null;
        if (!raise) { f?.Invoke(); return; } // 취소(잡힘 등): 이벤트 없이 호출자 상태만 해제

        if (success) { OnSuccess?.Invoke(); s?.Invoke(); }
        else
        {
            if (HeartRateController.Instance != null) HeartRateController.Instance.AddHeartRate(failHeartRateBoost); // 당황
            OnFail?.Invoke(); f?.Invoke();
        }
    }
}
