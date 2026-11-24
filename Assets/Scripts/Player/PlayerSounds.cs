using DeadSignal.Audio;
using UnityEngine;

/// <summary>
/// 플레이어 소리 호출 담당 (게임 코드). 플레이어 루트(FirstPersonController가 있는 오브젝트)에 붙인다.
/// 게임 이벤트를 듣고 AudioManager를 이름으로 부를 뿐, 게임 로직은 바꾸지 않는다.
///
///   발소리   : 움직이는 동안 분당 걸음 수(걷기 90 / 뛰기 150) 박자로. 달리기(IsSprinting)면 run.
///              잡힘 연출 / 숨은 상태 / 조작 불가 / 공중에서는 안 남.
///   심장박동 : HeartRateController.OnBeat 마다. heartbeatAudibleFromBpm 미만은 안 들림, BPM 높을수록 크게.
///   상호작용 : PlayerInteraction.OnInteracted. 같은 프레임에 아이템을 주웠으면 생략.
///   인벤토리 : 슬롯 선택/해제, 아이템 줍기 (종류별 소리가 없으면 item_pickup). 일시정지 중 지급은 무음.
///   숨기     : PlayerHide.OnHidingChanged
///   잡힘     : PlayerCaughtSequence.onBiteImpact
/// 숨소리는 PlayerBreathing이 따로 맡는다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerSounds : MonoBehaviour
{
    [Header("발소리")]
    public string walkSound = "plr_step_walk";
    public string runSound = "plr_step_run";
    [Tooltip("걷기 분당 걸음 수")] public float walkStepsPerMinute = 90f;
    [Tooltip("뛰기 분당 걸음 수")] public float runStepsPerMinute = 150f;
    [Tooltip("이 속도(m/s) 이하면 멈춘 것으로 본다")] public float minSpeed = 0.3f;

    [Header("심장박동")]
    public string heartbeatSound = "plr_heartbeat";
    [Tooltip("이 BPM 미만에서는 심장박동이 들리지 않는다")] public float heartbeatAudibleFromBpm = 95f;
    [Range(0f, 1f)] public float heartbeatMinVolume = 0.15f;
    [Range(0f, 1f)] public float heartbeatMaxVolume = 1f;

    [Header("상호작용 / 인벤토리")]
    public string interactSound = "plr_interact";
    public string slotSelectSound = "plr_slot_select";
    public string slotDeselectSound = "plr_slot_deselect";
    public string pickupSound = "item_pickup";
    public string pickupBatterySound = "item_pickup_battery";
    public string pickupKeySound = "item_pickup_key";

    [Header("숨기 / 잡힘")]
    public string hideEnterSound = "plr_hide_enter";
    public string hideExitSound = "plr_hide_exit";
    public string caughtBiteSound = "plr_caught_bite";

    FirstPersonController controller;
    CharacterController cc;
    PlayerInteraction interaction;
    PlayerInventory inventory;
    PlayerHide hide;
    PlayerCaughtSequence caught;
    HeartRateController heart;

    Vector3 lastPos;
    float stepPhase; // 0~1, 1이 되면 한 걸음
    int pickupFrame = -1;
    int interactFrame = -1;
    float nextLateHook;

    void Awake()
    {
        controller = GetComponentInChildren<FirstPersonController>();
        cc = GetComponentInChildren<CharacterController>();
        interaction = GetComponentInChildren<PlayerInteraction>();
        inventory = GetComponentInChildren<PlayerInventory>();
    }

    void OnEnable()
    {
        if (interaction != null) interaction.OnInteracted += HandleInteracted;
        if (inventory != null)
        {
            inventory.OnSlotToggled += HandleSlotToggled;
            inventory.OnItemAdded += HandleItemAdded;
        }
        lastPos = FootPosition;
        stepPhase = 0.5f;
    }

    void OnDisable()
    {
        if (interaction != null) interaction.OnInteracted -= HandleInteracted;
        if (inventory != null)
        {
            inventory.OnSlotToggled -= HandleSlotToggled;
            inventory.OnItemAdded -= HandleItemAdded;
        }
        if (hide != null) hide.OnHidingChanged -= HandleHiding;
        if (caught != null && caught.onBiteImpact != null) caught.onBiteImpact.RemoveListener(HandleBite);
        if (heart != null) heart.OnBeat -= HandleBeat;
        hide = null;
        caught = null;
        heart = null;
    }

    // PlayerHide / PlayerCaughtSequence / HeartRateController는 늦게 생길 수 있어 주기적으로 연결을 시도한다
    void TryHookLate()
    {
        if (hide != null && caught != null && heart != null) return;
        if (Time.unscaledTime < nextLateHook) return;
        nextLateHook = Time.unscaledTime + 0.5f;

        if (hide == null)
        {
            hide = PlayerHide.Instance != null ? PlayerHide.Instance : GetComponentInChildren<PlayerHide>();
            if (hide != null) hide.OnHidingChanged += HandleHiding;
        }
        if (caught == null)
        {
            caught = FindFirstObjectByType<PlayerCaughtSequence>();
            if (caught != null)
            {
                if (caught.onBiteImpact == null) caught.onBiteImpact = new UnityEngine.Events.UnityEvent();
                caught.onBiteImpact.AddListener(HandleBite);
            }
        }
        if (heart == null && HeartRateController.Instance != null)
        {
            heart = HeartRateController.Instance;
            heart.OnBeat += HandleBeat;
        }
    }

    Vector3 FootPosition => cc != null ? cc.transform.position : transform.position;
    Transform FootTransform => cc != null ? cc.transform : transform;

    void Update()
    {
        TryHookLate();
        UpdateFootsteps();
    }

    void LateUpdate()
    {
        // 상호작용과 줍기 이벤트의 순서와 상관없이, 같은 프레임에 주웠으면 상호작용 소리를 생략한다
        if (interactFrame == Time.frameCount && pickupFrame != Time.frameCount)
            AudioManager.Play(interactSound);
        interactFrame = -1;
    }

    void UpdateFootsteps()
    {
        Vector3 p = FootPosition;
        Vector3 d = p - lastPos; d.y = 0f;
        lastPos = p;
        float dist = d.magnitude;
        if (dist > 3f) { stepPhase = 0.5f; return; } // 순간이동(리스폰 등)은 걸음으로 치지 않는다

        float speed = Time.deltaTime > 0f ? dist / Time.deltaTime : 0f;
        bool muted = PlayerCaughtSequence.IsRunning
                     || (hide != null && hide.IsHiding)
                     || (controller != null && !controller.ControlsEnabled)
                     || (cc != null && !cc.isGrounded);
        if (muted || speed < minSpeed)
        {
            stepPhase = Mathf.Max(stepPhase, 0.5f); // 다시 걸으면 반 걸음 뒤에 첫 걸음
            return;
        }

        // 시간 기준 박자: 이동 속도와 상관없이 분당 걸음 수로 낸다. 걷기↔뛰기 전환 중에도 박자가 이어진다.
        bool running = controller != null && controller.IsSprinting;
        float spm = running ? runStepsPerMinute : walkStepsPerMinute;
        stepPhase += Time.deltaTime * spm / 60f;
        if (stepPhase >= 1f)
        {
            stepPhase = Mathf.Repeat(stepPhase, 1f);
            AudioManager.Play(running ? runSound : walkSound, FootTransform);
        }
    }

    void HandleBeat()
    {
        if (heart == null || PlayerCaughtSequence.IsRunning) return;
        float t = Mathf.InverseLerp(heartbeatAudibleFromBpm, heart.MaxBpm, heart.CurrentBpm);
        if (heart.CurrentBpm < heartbeatAudibleFromBpm) return;
        AudioManager.Play(heartbeatSound).SetVolume(Mathf.Lerp(heartbeatMinVolume, heartbeatMaxVolume, t));
    }

    void HandleInteracted() => interactFrame = Time.frameCount;

    void HandleSlotToggled(bool selected) => AudioManager.Play(selected ? slotSelectSound : slotDeselectSound);

    void HandleItemAdded(InventoryItemType type)
    {
        pickupFrame = Time.frameCount;
        // 타이틀/일시정지 중 지급(게임 시작 시 손전등 등)은 소리 없음 — 안 그러면 일시정지가 풀릴 때 뒤늦게 난다
        if (AudioListener.pause) return;
        string specific = type == InventoryItemType.Battery ? pickupBatterySound
                        : type == InventoryItemType.Key ? pickupKeySound
                        : null;
        // 종류별 소리가 아직 등록되지 않았으면 공용 줍기 소리
        AudioManager.Play(!string.IsNullOrEmpty(specific) && AudioManager.Has(specific) ? specific : pickupSound);
    }

    void HandleHiding(bool hidden) => AudioManager.Play(hidden ? hideEnterSound : hideExitSound);

    void HandleBite() => AudioManager.Play(caughtBiteSound);
}
