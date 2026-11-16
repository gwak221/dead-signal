using UnityEngine;

/// <summary>
/// 플레이어 사운드 연결 컴포넌트. Player 아래 "Audio" 자식에 붙는다.
/// 게임 스크립트의 이벤트를 듣고 SoundCue를 재생할 뿐, 게임 로직은 바꾸지 않는다.
///   발소리   : FootstepEmitter (FirstPersonController.IsSprinting으로 걷기/뛰기)
///   숨소리   : BreathingAudio (HeartRateController BPM)
///   심장박동 : HeartRateController.OnBeat (BPM이 높을수록 크게)
///   상호작용 : PlayerInteraction.OnInteracted
///   인벤토리 : PlayerInventory.OnSlotToggled / OnItemDropped / OnItemAdded
///   피격     : PlayerBodyStatus.OnPenaltyChanged
///   숨기     : PlayerHide.OnHidingChanged
///   잡힘     : PlayerCaughtSequence.onBiteImpact
/// 같은 오브젝트의 SoundEmitter / FootstepEmitter / BreathingAudio를 사용한다 (없으면 자동 추가).
/// </summary>
[DefaultExecutionOrder(-50)] // FootstepEmitter보다 먼저 달리기 상태를 넘긴다
public class PlayerAudio : MonoBehaviour
{
    [Header("Interaction / Inventory")]
    public SoundCue interactCue;
    public SoundCue slotSelectCue;
    public SoundCue slotDeselectCue;
    public SoundCue dropCue;
    public SoundCue pickupBatteryCue;
    public SoundCue pickupKeyCue;

    [Header("Body")]
    public SoundCue heartbeatCue;
    [Tooltip("이 BPM 이하에서는 심장박동이 들리지 않는다")]
    public float heartbeatAudibleFromBpm = 95f;
    [Tooltip("최대 BPM일 때 심장박동 볼륨 배율")]
    [Range(0f, 1f)] public float heartbeatMaxVolume = 1f;
    public SoundCue hurtCue;
    public SoundCue caughtBiteCue;
    public SoundCue hideEnterCue;
    public SoundCue hideExitCue;

    FirstPersonController controller;
    PlayerInteraction interaction;
    PlayerInventory inventory;
    PlayerBodyStatus body;
    PlayerHide hide;
    PlayerCaughtSequence caught;
    HeartRateController heart;

    SoundEmitter emitter;
    FootstepEmitter footsteps;
    BreathingAudio breathing;
    int lastPickupFrame = -1;

    void Awake()
    {
        emitter = GetComponent<SoundEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<SoundEmitter>();
        footsteps = GetComponent<FootstepEmitter>();
        breathing = GetComponent<BreathingAudio>();

        controller = GetComponentInParent<FirstPersonController>();
        interaction = GetComponentInParent<PlayerInteraction>();
        inventory = GetComponentInParent<PlayerInventory>();
        body = GetComponentInParent<PlayerBodyStatus>();
        hide = GetComponentInParent<PlayerHide>();
    }

    void OnEnable()
    {
        if (interaction != null) interaction.OnInteracted += HandleInteracted;
        if (inventory != null)
        {
            inventory.OnSlotToggled += HandleSlotToggled;
            inventory.OnItemDropped += HandleDropped;
            inventory.OnItemAdded += HandleAdded;
        }
        if (body != null) body.OnPenaltyChanged += HandlePenalty;
        TryHookLate();
    }

    void OnDisable()
    {
        if (interaction != null) interaction.OnInteracted -= HandleInteracted;
        if (inventory != null)
        {
            inventory.OnSlotToggled -= HandleSlotToggled;
            inventory.OnItemDropped -= HandleDropped;
            inventory.OnItemAdded -= HandleAdded;
        }
        if (body != null) body.OnPenaltyChanged -= HandlePenalty;
        if (caught != null && caughtHooked) caught.onBiteImpact.RemoveListener(HandleBite);
        if (hide != null && hideHooked) hide.OnHidingChanged -= HandleHiding;
        if (heart != null) heart.OnBeat -= HandleBeat;
        hideHooked = false;
        caughtHooked = false;
        heart = null;
    }

    bool hideHooked;
    bool caughtHooked;
    float nextLateHook;

    // PlayerHide / PlayerCaughtSequence는 런타임에 추가될 수 있고, HeartRateController는 싱글톤이라 늦게 생길 수 있다
    void TryHookLate()
    {
        if (hideHooked && caughtHooked && heart != null) return;
        if (Time.unscaledTime < nextLateHook) return;
        nextLateHook = Time.unscaledTime + 0.5f;

        if (!caughtHooked)
        {
            if (caught == null) caught = FindFirstObjectByType<PlayerCaughtSequence>();
            if (caught != null)
            {
                if (caught.onBiteImpact == null) caught.onBiteImpact = new UnityEngine.Events.UnityEvent();
                caught.onBiteImpact.AddListener(HandleBite);
                caughtHooked = true;
            }
        }
        if (!hideHooked)
        {
            if (hide == null) hide = PlayerHide.Instance != null ? PlayerHide.Instance : GetComponentInParent<PlayerHide>();
            if (hide != null) { hide.OnHidingChanged += HandleHiding; hideHooked = true; }
        }
        if (heart == null && HeartRateController.Instance != null)
        {
            heart = HeartRateController.Instance;
            heart.OnBeat += HandleBeat;
        }
    }

    void Update()
    {
        TryHookLate();

        bool caughtNow = PlayerCaughtSequence.IsRunning;
        bool hiding = hide != null && hide.IsHiding;

        if (footsteps != null)
        {
            footsteps.Muted = caughtNow || hiding || (controller != null && !controller.ControlsEnabled);
            footsteps.RunningOverride = controller != null ? controller.IsSprinting : (bool?)null;
        }
        if (breathing != null) breathing.Muted = caughtNow;
    }

    // ───────────── 핸들러 ─────────────

    void HandleInteracted()
    {
        if (Time.frameCount == lastPickupFrame) return; // 줍기 소리와 겹치지 않게
        emitter.Play(interactCue);
    }

    void HandleSlotToggled(bool selected) => emitter.Play(selected ? slotSelectCue : slotDeselectCue);

    void HandleDropped(InventoryItemType type, GameObject item)
    {
        Vector3 pos = item != null ? item.transform.position : transform.position;
        SoundEmitter.PlayAt(dropCue, pos);
    }

    void HandleAdded(InventoryItemType type)
    {
        SoundCue cue = type == InventoryItemType.Battery ? pickupBatteryCue
                     : type == InventoryItemType.Key ? pickupKeyCue
                     : null;
        if (cue == null) return;
        lastPickupFrame = Time.frameCount;
        emitter.Play(cue);
    }

    void HandlePenalty(BodyPart part, bool active)
    {
        if (active) emitter.Play(hurtCue);
    }

    void HandleHiding(bool hidden) => emitter.Play(hidden ? hideEnterCue : hideExitCue);

    void HandleBite() => emitter.Play(caughtBiteCue);

    void HandleBeat()
    {
        if (heart == null || heartbeatCue == null || PlayerCaughtSequence.IsRunning) return;
        float t = Mathf.InverseLerp(heartbeatAudibleFromBpm, heart.MaxBpm, heart.CurrentBpm);
        if (t <= 0f) return;
        emitter.Play(heartbeatCue, Mathf.Lerp(0.15f, heartbeatMaxVolume, t));
    }
}
