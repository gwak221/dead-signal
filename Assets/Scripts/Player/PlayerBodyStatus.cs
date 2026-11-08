using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum BodyPart
{
    Eye,
    Arm,
    Leg
}

/// <summary>
/// 플레이어 신체 파손 상태 관리.
/// 실제 게임 이벤트에서는 SetEyePenalty / SetArmPenalty / SetLegPenalty 를 호출하면 된다.
///   예) FindFirstObjectByType<PlayerBodyStatus>().SetLegPenalty(true);
///
/// 기존 스크립트(FirstPersonController / PlayerInventory)는 공개 함수로만 제어하고 내부 로직은 바꾸지 않는다.
/// Player 오브젝트에 이 컴포넌트가 없으면 씬 로드 시 자동으로 추가된다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBodyStatus : MonoBehaviour
{
    [Header("Initial State")]
    [SerializeField] bool startEyePenalty;
    [SerializeField] bool startArmPenalty;
    [SerializeField] bool startLegPenalty;

    [Header("Debug Keys (F6 Eye / F7 Arm / F8 Leg)")]
    [SerializeField] bool enableDebugKeys = true;

    [Header("Eye")]
    [Tooltip("눈 파손 화면 효과 (가장자리 어둠/비네팅/흐림). 비우면 자동 추가.")]
    [SerializeField] EyeDamageEffect eyeDamageEffect;
    [Tooltip("이전 방식(화면 절반 검은 패널). 기본 사용 안 함 → 항상 숨김.")]
    [SerializeField] bool useLegacyHalfScreenPanel = false;
    [Tooltip("이전 방식에서 쓰던 EyePenalty UI. 비우면 이름(EyePenalty)으로 자동 탐색.")]
    [SerializeField] GameObject eyePenaltyUI;
    [SerializeField] string eyePenaltyObjectName = "EyePenalty";

    [Header("Arm")]
    [Tooltip("팔 파손 시 잠글 인벤토리 슬롯 (0 = 1번, 1 = 2번, 2 = 3번)")]
    [Range(0, 2)] [SerializeField] int armLockedSlotIndex = 2;

    [Header("Leg")]
    [Tooltip("다리 파손 시 걷기 속도 배율 (1 = 정상)")]
    [Range(0.1f, 1f)] [SerializeField] float legWalkSpeedMultiplier = 0.55f;

    [Header("References (auto-found if empty)")]
    [SerializeField] FirstPersonController controller;
    [SerializeField] PlayerInventory inventory;
    [SerializeField] PlayerLimpCamera limpCamera;

    bool eyePenalty;
    bool armPenalty;
    bool legPenalty;
    int appliedArmSlot = -1;
    bool initialized;

    public bool EyePenalty => eyePenalty;
    public bool ArmPenalty => armPenalty;
    public bool LegPenalty => legPenalty;
    /// <summary>현재 파손된 신체 부위 수 (하트 UI / Game Over 기준).</summary>
    public int DamagedCount => (eyePenalty ? 1 : 0) + (armPenalty ? 1 : 0) + (legPenalty ? 1 : 0);

    /// <summary>파손 상태가 바뀔 때 호출 (사운드/연출 연결용).</summary>
    public event Action<BodyPart, bool> OnPenaltyChanged;

    // ───────────────────────── 자동 부착 ─────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureOnPlayer();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureOnPlayer();

    static void EnsureOnPlayer()
    {
        foreach (var fpc in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
        {
            if (fpc.GetComponent<PlayerBodyStatus>() == null)
                fpc.gameObject.AddComponent<PlayerBodyStatus>();
        }
    }

    // ───────────────────────── Unity ─────────────────────────

    void Awake()
    {
        ResolveReferences();
    }

    IEnumerator Start()
    {
        // PlayerInventory.Start(UI 탐색/기본 색 저장)가 먼저 끝난 뒤 초기 상태를 적용.
        yield return null;

        initialized = true;
        ApplyEye(startEyePenalty, true);
        ApplyArm(startArmPenalty, true);
        ApplyLeg(startLegPenalty, true);
    }

    void Update()
    {
        if (!enableDebugKeys || Keyboard.current == null)
            return;

        var kb = Keyboard.current;
        if (kb.f6Key.wasPressedThisFrame) SetEyePenalty(!eyePenalty);
        if (kb.f7Key.wasPressedThisFrame) SetArmPenalty(!armPenalty);
        if (kb.f8Key.wasPressedThisFrame) SetLegPenalty(!legPenalty);
    }

    // ───────────────────────── Public API ─────────────────────────

    public void SetEyePenalty(bool active) => ApplyEye(active, false);
    public void SetArmPenalty(bool active) => ApplyArm(active, false);
    public void SetLegPenalty(bool active) => ApplyLeg(active, false);

    public void SetPenalty(BodyPart part, bool active)
    {
        switch (part)
        {
            case BodyPart.Eye: SetEyePenalty(active); break;
            case BodyPart.Arm: SetArmPenalty(active); break;
            case BodyPart.Leg: SetLegPenalty(active); break;
        }
    }

    public bool HasPenalty(BodyPart part)
    {
        return part switch
        {
            BodyPart.Eye => eyePenalty,
            BodyPart.Arm => armPenalty,
            BodyPart.Leg => legPenalty,
            _ => false
        };
    }

    public void ClearAllPenalties()
    {
        SetEyePenalty(false);
        SetArmPenalty(false);
        SetLegPenalty(false);
    }

    // ───────────────────────── 적용 ─────────────────────────

    void ApplyEye(bool active, bool force)
    {
        if (!force && eyePenalty == active) return;
        eyePenalty = active;

        // 새 방식: 한쪽 시야 저하 효과
        if (eyeDamageEffect == null) eyeDamageEffect = GetComponent<EyeDamageEffect>();
        if (eyeDamageEffect == null) eyeDamageEffect = gameObject.AddComponent<EyeDamageEffect>();
        eyeDamageEffect.SetActive(active);

        // 이전 방식(화면 절반 검은 패널)은 기본적으로 항상 숨김.
        if (eyePenaltyUI == null)
            eyePenaltyUI = FindEyePenaltyUI();

        if (eyePenaltyUI != null)
        {
            // Canvas의 첫 번째 자식 = 가장 먼저 그려짐 → HUD/비밀번호 UI가 항상 그 위에 표시.
            eyePenaltyUI.transform.SetAsFirstSibling();
            eyePenaltyUI.SetActive(useLegacyHalfScreenPanel && active);
        }

        Raise(BodyPart.Eye, active, force);
    }

    void ApplyArm(bool active, bool force)
    {
        if (!force && armPenalty == active) return;
        armPenalty = active;

        if (inventory == null) ResolveReferences();

        if (inventory != null)
        {
            int slot = Mathf.Clamp(armLockedSlotIndex, 0, inventory.TotalSlotCount - 1);

            // 잠금 슬롯 번호가 바뀐 경우 이전 슬롯 해제.
            if (appliedArmSlot >= 0 && appliedArmSlot != slot)
                inventory.SetSlotLocked(appliedArmSlot, false);

            inventory.SetSlotLocked(slot, active);
            appliedArmSlot = active ? slot : -1;
        }

        Raise(BodyPart.Arm, active, force);
    }

    void ApplyLeg(bool active, bool force)
    {
        if (!force && legPenalty == active) return;
        legPenalty = active;

        if (controller == null || limpCamera == null) ResolveReferences();

        if (controller != null)
        {
            controller.SetSprintAllowed(!active);
            controller.SetSpeedMultiplier(active ? legWalkSpeedMultiplier : 1f);
        }

        if (limpCamera != null)
            limpCamera.SetLimpActive(active);

        Raise(BodyPart.Leg, active, force);
    }

    void Raise(BodyPart part, bool active, bool silent)
    {
        if (silent && !active) return; // 시작 시 '정상' 상태 적용은 이벤트로 알리지 않음
        if (initialized)
        {
            OnPenaltyChanged?.Invoke(part, active);
            // 이미 하나 파손된 상태에서 두 번째 파손 = Game Over
            if (active && DamagedCount >= 2)
                GameProgress.TriggerGameOver();
        }
    }

    // ───────────────────────── 참조 ─────────────────────────

    void ResolveReferences()
    {
        if (controller == null) controller = GetComponent<FirstPersonController>();
        if (inventory == null) inventory = GetComponent<PlayerInventory>();

        if (limpCamera == null) limpCamera = GetComponent<PlayerLimpCamera>();
        if (limpCamera == null && controller != null)
            limpCamera = gameObject.AddComponent<PlayerLimpCamera>();
    }

    GameObject FindEyePenaltyUI()
    {
        // 비활성 상태여도 찾을 수 있도록 Canvas 하위를 직접 탐색.
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform t = FindDeep(canvas.transform, eyePenaltyObjectName);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    void OnValidate()
    {
        // 플레이 중 Inspector에서 다리 속도 배율을 바꾸면 즉시 반영.
        if (Application.isPlaying && legPenalty && controller != null)
            controller.SetSpeedMultiplier(legWalkSpeedMultiplier);
    }
}
