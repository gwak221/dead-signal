using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public enum InteractionPriority
{
    ClosestToScreenCenter,
    ClosestToPlayer
}

public class PlayerInteraction : MonoBehaviour
{
    public Transform keyHoldPoint;
    public Camera playerCamera;
    public float interactionDistance = 3f;
    public TMP_Text interactionText;

    [Header("Interaction Assist (화면 중앙 주변 판정 여유)")]
    [Tooltip("SphereCast 반경(m). 0이면 기존처럼 한 줄 Raycast만 사용.")]
    [Range(0f, 0.5f)] public float interactionRadius = 0.18f;
    [Tooltip("여러 대상이 동시에 잡힐 때 우선순위")]
    public InteractionPriority interactionPriority = InteractionPriority.ClosestToScreenCenter;
    [Tooltip("판정/시야 검사 레이어 (기본값 = 기존 Raycast와 동일: Ignore Raycast 제외 전부)")]
    public LayerMask interactionMask = Physics.DefaultRaycastLayers;
    public bool drawInteractionDebug;

    readonly RaycastHit[] assistHits = new RaycastHit[32];
    readonly RaycastHit[] pickupHits = new RaycastHit[64];
    readonly RaycastHit[] pickupRayHits = new RaycastHit[64];
    readonly RaycastHit[] blockHits = new RaycastHit[64];
    readonly Vector3[] samplePts = new Vector3[8];

    [Header("Medkit")]
    [Tooltip("Medkit 사용(E) 후 치료가 완료되기까지의 시간(초)")]
    public float medkitUseSeconds = 1.5f;
    bool usingMedkit;
    float medkitUseEnd;
    public bool IsUsingMedkit => usingMedkit;

    // 주사기: E 로 주입 애니메이션 시작 → 끝나면 효과 적용 + 소비 (SyringePickup)
    bool usingSyringe;
    float syringeUseEnd;
    SyringePickup activeSyringe;
    public bool IsUsingSyringe => usingSyringe;
    string pendingPickupText; // 이번 프레임 F 줍기 대상의 프롬프트 (없으면 null)
    Component aimOwner;       // 이번 프레임 조준 중인 대상 (하이라이트용)
    bool aimIsPickup;
    Component lastAssistTarget;

    PlayerInventory inventory;
    HoldInteractable heldDevice;
    bool interactionEnabled = true;
    float messageUntil;
    string temporaryMessage;

    public bool InteractionEnabled => interactionEnabled;
    public PlayerInventory Inventory => inventory;

    /// <summary>E로 환경 상호작용을 했을 때 /줍기(F)를 했을 때 (사운드용).</summary>
    public event System.Action OnInteracted;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = gameObject.AddComponent<PlayerInventory>();

        if (keyHoldPoint != null)
            inventory.SetHoldPoint(keyHoldPoint);

        // 어두운/밝은 배경 위에서도 프롬프트가 읽히도록 얇은 검은 외곽선
        if (interactionText != null)
        {
            interactionText.outlineWidth = 0.25f;
            interactionText.outlineColor = new Color32(0, 0, 0, 255);
        }
    }

    void Update()
    {
        aimOwner = null;
        aimIsPickup = false;

        UpdateMedkitUse();
        UpdateSyringeUse();

        if (!interactionEnabled)
        {
            HideInteractionText();
            AimOutline.SetTarget(null, false);
            return;
        }

        if (Time.time < messageUntil)
        {
            ShowMessage(temporaryMessage);
            AimOutline.SetTarget(null, false);
            return;
        }

        pendingPickupText = null;
        CheckInteraction();
        UpdateAimOutline();

        // F 줍기 대상이 따로 있으면(예: 열린 벽장 안 배터리) E 프롬프트 아래에 F 프롬프트를 함께 표시.
        if (!string.IsNullOrEmpty(pendingPickupText) && interactionText != null)
        {
            string cur = interactionText.gameObject.activeSelf ? interactionText.text : "";
            if (!cur.Contains(pendingPickupText))
                ShowMessage(string.IsNullOrEmpty(cur) ? pendingPickupText : cur + "\n" + pendingPickupText);
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (!enabled)
        {
            CancelMedkitUse();
            CancelSyringeUse();
            ResetHold();
            HideInteractionText();
        }
    }

    public void Notify(string message, float seconds = 0.9f)
    {
        temporaryMessage = message;
        messageUntil = Time.time + seconds;
        ShowMessage(message);
    }

    // 예전 SafeDoor/씬과의 호환용. 현재 인벤토리 시스템에서는 별도 키 플래그를 사용하지 않는다.
    public void RegisterHeldKey(GameObject _) { }
    public void HideHeldKeyVisual()
    {
        if (inventory != null && inventory.IsSelected(InventoryItemType.Key))
            inventory.SelectBareHands();
    }

    void ResetHold()
    {
        if (heldDevice != null)
            heldDevice.ResetHold();
        heldDevice = null;
    }

    void OnDisable()
    {
        usingMedkit = false;
        CancelSyringeUse();
        ResetHold();
    }

    void ShowMessage(string message)
    {
        if (interactionText == null)
            return;

        interactionText.text = message;
        interactionText.gameObject.SetActive(true);
    }

    void CheckInteraction()
    {
        if (playerCamera == null)
        {
            ResetHold();
            HideInteractionText();
            return;
        }

        if (usingMedkit)
        {
            // 사용 중에는 다른 E/F 상호작용을 받지 않고 진행 문구만 표시 (중복 사용 방지)
            ResetHold();
            float left = Mathf.Max(0f, medkitUseEnd - Time.time);
            ShowMessage("USING MEDKIT... " + left.ToString("0.0") + "s");
            return;
        }

        if (usingSyringe)
        {
            ResetHold();
            ShowMessage("INJECTING...");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // F 줍기: E 상호작용 판정과 완전히 분리된 별도 판정.
        Component pickup = FindPickup(ray, out pendingPickupText);
        if (pickup != null) { aimOwner = pickup; aimIsPickup = true; }   // Pickup 이 조준되면 겹친 가구보다 우선 표시
        var fkb = Keyboard.current;
        if (pickup != null && fkb != null && fkb.fKey.wasPressedThisFrame)
            TryPickup(pickup);

        if (FindInteractionHit(ray, out RaycastHit hit))
        {
            var kb = Keyboard.current;
            var battery = hit.collider.GetComponentInParent<BatteryPickup>();
            var device = hit.collider.GetComponentInParent<HoldInteractable>();

            if (battery != null)
            {
                // 프롬프트는 F 줍기 판정(FindPickup)이 실제로 집을 수 있다고 확인했을 때만 Update 에서 붙인다 (가려진 배터리에 가짜 프롬프트 금지).
                ResetHold();
                HideInteractionText();
                return;
            }

            if (heldDevice != device)
                ResetHold();

            if (device != null)
            {
                if (aimOwner == null) aimOwner = device;
                if (device.IsActivated)
                {
                    ShowMessage("COMPLETE: " + device.DisplayName);
                    return;
                }

                if (device.NeedsBattery)
                {
                    if (inventory != null && inventory.IsSelected(InventoryItemType.Battery))
                    {
                        ShowMessage("[E] INSERT BATTERY");

                        if (kb != null && kb.eKey.wasPressedThisFrame && device.TryInsertBattery())
                            inventory.TryConsumeSelected(InventoryItemType.Battery);
                    }
                    else
                    {
                        ShowMessage("EQUIP BATTERY  [1/2/3]");
                    }
                    return;
                }

                if (kb != null && kb.eKey.isPressed)
                {
                    heldDevice = device;
                    device.AddHoldTime(Time.deltaTime);
                }
                else
                {
                    ResetHold();
                }

                ShowMessage("[HOLD E] " + device.DisplayName + " " + device.HoldProgressPercent.ToString("0") + "%");
                return;
            }

            ResetHold();
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable is Component decor && DecorativeOpenable.IsDecorative(decor)) interactable = null;   // 장식용 Openable: 상호작용/프롬프트/Outline 없음 (Collider 는 그대로 시야/통행을 막는다)

            if (aimOwner == null && !(interactable is IPickupable) && interactable is Component ic) aimOwner = ic;

            if (interactable is IPickupable && (TryMedkitUse(kb) || TrySyringeUse(kb)))
                return;

            if (interactable is IPickupable)
            {
                // 줍기 계열은 F 줍기 판정이 통과했을 때만 프롬프트를 표시한다 (Update 가 pendingPickupText 를 붙임).
                HideInteractionText();
                return;
            }

            if (interactable != null)
            {
                string message = interactable.GetInteractionText();

                if (interactionText != null)
                {
                    interactionText.text = message;
                    interactionText.gameObject.SetActive(!string.IsNullOrEmpty(message));
                }

                // 줍기 계열(IPickupable)은 F 전용(TryPickup), 그 외 환경 상호작용은 E 전용.
                if (!(interactable is IPickupable) && kb != null && kb.eKey.wasPressedThisFrame)
                {
                    interactable.Interact(this);
                    OnInteracted?.Invoke();
                }

                return;
            }
        }

        ResetHold();
        if (TryMedkitUse(Keyboard.current)) return;
        if (TrySyringeUse(Keyboard.current)) return;
        HideInteractionText();
    }

    // ───────────────────────── 조준 하이라이트 (AimOutline) ─────────────────────────

    void UpdateAimOutline()
    {
        if (aimOwner == null) { AimOutline.SetTarget(null, false); return; }
        ResolveAimRoot(aimOwner, aimIsPickup, out Transform root, out Transform[] extras);
        AimOutline.SetTarget(root, aimIsPickup, extras);
    }

    /// <summary>
    /// 외곽선을 그릴 덩어리.
    /// - 줍기 아이템: 자기 자신.
    /// - 여닫이 파츠: 독립적으로 작동하는 파츠(= linked 로 묶인 그룹)가 둘 이상인 가구(filing/tea_niche/anesthesia 등)는
    ///   조준한 파츠(+ 같이 열리는 linked 파츠)만. 한 번에 전체가 열리는 가구(cardboard_box, 양문 1세트 등)는 가구 전체.
    /// - 그 외(Door, Beacon, med_cabinet, tall_cabinet 의 HideSpot 등): 자기 오브젝트 전체.
    /// </summary>
    static void ResolveAimRoot(Component owner, bool isPickup, out Transform root, out Transform[] extras)
    {
        root = owner.transform;
        extras = null;
        if (isPickup || !(owner is OpenablePart part)) return;
        var furniture = part.GetComponentInParent<OpenableFurniture>();
        if (furniture == null) return;
        if (!part.interactable || CountIndependentGroups(furniture) < 2) { root = furniture.transform; return; }
        root = part.transform;
        if (part.linked != null && part.linked.Length > 0)
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var l in part.linked) if (l != null && l != part) list.Add(l.transform);
            if (list.Count > 0) extras = list.ToArray();
        }
    }

    static int CountIndependentGroups(OpenableFurniture f)
    {
        var parts = f.Parts;
        int groups = 0;
        var seen = new System.Collections.Generic.HashSet<OpenablePart>();
        foreach (var p in parts)
        {
            if (p == null || !p.interactable || seen.Contains(p)) continue;
            groups++;
            seen.Add(p);
            if (p.linked != null) foreach (var l in p.linked) if (l != null) seen.Add(l);
        }
        return groups;
    }

    /// <summary>
    /// Medkit 을 손에 들고 있고 E 로 반응할 환경 대상이 없을 때: [E] USE MEDKIT. (문/상자 등 E 대상이 있으면 그쪽이 우선)
    /// 건강하면 "HEALTH FULL" 만 표시하고 소비하지 않는다.
    /// </summary>
    bool TryMedkitUse(Keyboard kb)
    {
        if (inventory == null || !inventory.IsSelected(InventoryItemType.Medkit)) return false;
        var body = GetComponent<PlayerBodyStatus>();
        bool injured = body != null && body.DamagedCount > 0;
        ShowMessage(injured ? "[E] USE MEDKIT" : "HEALTH FULL");
        if (injured && kb != null && kb.eKey.wasPressedThisFrame && CanUseMedkitNow())
            BeginMedkitUse();
        return true;
    }

    /// <summary>지금 Medkit 을 사용할 수 있는 상태인가 (잠금/컷씬/잡힘/게임오버가 아닐 때, 이미 사용 중이 아닐 때).</summary>
    bool CanUseMedkitNow() => !usingMedkit && StateAllowsMedkit();

    bool StateAllowsMedkit()
    {
        if (!interactionEnabled) return false;
        if (GameProgress.IsEnded || PlayerCaughtSequence.IsRunning) return false;
        if (ExitCutsceneController.IsPlaying || Monster2ReleaseSequence.IsPlaying) return false;
        var fpc = GetComponent<FirstPersonController>();
        return fpc == null || fpc.ControlsEnabled;
    }

    void BeginMedkitUse()
    {
        usingMedkit = true;
        medkitUseEnd = Time.time + Mathf.Max(0.1f, medkitUseSeconds);
        ShowMessage("USING MEDKIT... " + medkitUseSeconds.ToString("0.0") + "s");
        OnInteracted?.Invoke();
    }

    /// <summary>사용 중이던 Medkit 을 소비 없이 취소 (잡힘/컷씬/잠금/슬롯 변경 등).</summary>
    public void CancelMedkitUse()
    {
        if (!usingMedkit) return;
        usingMedkit = false;
        HideInteractionText();
    }

    void UpdateMedkitUse()
    {
        if (!usingMedkit) return;
        // 사용 도중 강제 상태가 되었거나 Medkit 을 내려놓았으면 안전하게 취소 (소비 없음)
        if (!StateAllowsMedkit() || inventory == null || !inventory.IsSelected(InventoryItemType.Medkit))
        {
            CancelMedkitUse();
            return;
        }
        if (Time.time < medkitUseEnd) return;
        usingMedkit = false;
        MedkitPickup.TryUseSelected(this);   // 치료 + 소비 (그 사이 건강해졌다면 소비 없이 HEALTH FULL)
    }

    // ───────────────────────── 주사기 사용 (E) ─────────────────────────

    /// <summary>주사기를 손에 들고 있고 E 로 반응할 환경 대상이 없을 때: [E] USE SYRINGE.</summary>
    bool TrySyringeUse(Keyboard kb)
    {
        if (inventory == null || !inventory.IsSelected(InventoryItemType.Syringe)) return false;
        var hr = HeartRateController.Instance;
        string prompt = hr != null && hr.IsSprintUnlocked
            ? "[E] USE SYRINGE (ACTIVE " + hr.SprintUnlockRemaining.ToString("0") + "s)"
            : "[E] USE SYRINGE";
        ShowMessage(prompt);
        if (kb != null && kb.eKey.wasPressedThisFrame && !usingSyringe && !usingMedkit && StateAllowsMedkit())
            BeginSyringeUse();
        return true;
    }

    void BeginSyringeUse()
    {
        activeSyringe = SyringePickup.Selected(inventory);
        if (activeSyringe == null) return;
        usingSyringe = true;
        syringeUseEnd = Time.time + Mathf.Max(0.05f, activeSyringe.InjectSeconds);
        activeSyringe.BeginInject();
        ShowMessage("INJECTING...");
        OnInteracted?.Invoke();
    }

    /// <summary>주입 도중 취소 (잡힘/컷씬/잠금/슬롯 변경 등). 소비 없음, 플런저 원위치.</summary>
    public void CancelSyringeUse()
    {
        if (!usingSyringe) return;
        usingSyringe = false;
        if (activeSyringe != null) activeSyringe.CancelInject();
        activeSyringe = null;
        HideInteractionText();
    }

    void UpdateSyringeUse()
    {
        if (!usingSyringe) return;
        if (!StateAllowsMedkit() || activeSyringe == null || SyringePickup.Selected(inventory) != activeSyringe)
        {
            CancelSyringeUse();
            return;
        }
        if (Time.time < syringeUseEnd) return;
        usingSyringe = false;
        activeSyringe = null;
        SyringePickup.FinishUse(this);
    }

    // ───────────────────────── F 줍기 판정 (E 판정과 분리) ─────────────────────────

    /// <summary>줍기 대상 소유자: Beacon 배터리(BatteryPickup) 또는 IPickupable. 없으면 null.</summary>
    static Component GetPickupOwner(Collider col)
    {
        if (col == null) return null;
        var battery = col.GetComponentInParent<BatteryPickup>();
        if (battery != null) return battery;
        foreach (var mb in col.GetComponentsInParent<MonoBehaviour>())
            if (mb is IPickupable && mb is IInteractable) return mb;
        return null;
    }

    /// <summary>
    /// F 전용: 화면 중앙(Ray + SphereCast 여유) 근처의 줍기 대상만 찾는다. E 용 큰 상호작용 Collider 에 영향받지 않는다.
    /// 막힘 검사에서 제외: 플레이어 자신, 대상 자신, 다른 줍기 아이템, 대상을 감싸고 있는 가구 몸통 Collider(벽장 상호작용 박스 등).
    /// 닫힌 OpenablePart(문짝/서랍)와 벽·바닥 등 다른 구조물은 그대로 시야를 막는다.
    /// </summary>
    Component FindPickup(Ray ray, out string text)
    {
        text = null;
        int n = interactionRadius > 0f
            ? Physics.SphereCastNonAlloc(ray, interactionRadius, pickupHits, interactionDistance, interactionMask)
            : 0;
        int rn = Physics.RaycastNonAlloc(ray, pickupRayHits, interactionDistance, interactionMask);

        Component best = null; string bestText = null; float bestScore = float.MaxValue;
        for (int pass = 0; pass < 2; pass++)
        {
            int count = pass == 0 ? n : rn;
            var buf = pass == 0 ? pickupHits : pickupRayHits;
            for (int i = 0; i < count; i++)
            {
                Collider col = buf[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                Component owner = GetPickupOwner(col);
                if (owner == null || !owner.gameObject.activeInHierarchy) continue;
                if (LootQteGate.IsSealed(owner)) continue;   // 닫힌/잠긴 상자 안 아이템은 F 로 못 줍는다

                string t = owner is BatteryPickup ? "[F] PICK UP BATTERY" : ((IInteractable)owner).GetInteractionText();
                if (string.IsNullOrEmpty(t)) continue; // 예: 닫힌 약장 안 AA

                // 시야 검사: 아이템 Collider 표면의 여러 점(카메라 쪽 표면/위/옆면 등) 중 하나라도 카메라에서 막힘 없이 보여야 한다.
                // 닫힌 문/서랍, 가구 몸체·칸막이·선반, 벽, 침대 등 실제 Collider 는 하나도 건너뛰지 않고 시야를 막는다.
                if (!HasVisibleSurface(ray.origin, col, owner, out Vector3 target)) continue;
                Vector3 toTarget = target - ray.origin;
                float dist = toTarget.magnitude;
                if (dist > interactionDistance + interactionRadius) continue;

                float angle = dist > 0.001f ? Vector3.Angle(ray.direction, toTarget) : 0f;
                float score = angle + dist * 0.01f;
                if (score < bestScore) { bestScore = score; best = owner; bestText = t; }
            }
        }
        text = bestText;
        return best;
    }

    /// <summary>
    /// col(줍기 아이템) 표면 위 샘플 점들 중 카메라에서 막힘 없이 보이는 점이 있으면 true (visible = 그 점, 카메라와 가장 가까운 것).
    /// 샘플: 카메라→중심 광선이 닿는 표면, 카메라에서 가장 가까운 표면, 위/좌우/앞뒤 면.
    /// 침대 위처럼 아이템이 다른 Collider 와 살짝 겹쳐 있어도 위쪽 면이 보이면 집을 수 있고, 벽/가구 반대편이면 모든 샘플이 막혀 집을 수 없다.
    /// </summary>
    bool HasVisibleSurface(Vector3 origin, Collider col, Component owner, out Vector3 visible)
    {
        visible = col.bounds.center;
        int n = 0;
        Bounds b = col.bounds;
        Vector3 toCenter = b.center - origin;
        bool precise = !(col is MeshCollider mc && !mc.convex);   // 비볼록 MeshCollider 는 ClosestPoint 불가

        if (toCenter.sqrMagnitude > 0.0001f && col.Raycast(new Ray(origin, toCenter.normalized), out RaycastHit entry, toCenter.magnitude + 0.5f))
            samplePts[n++] = entry.point - toCenter.normalized * 0.02f;
        if (precise)
        {
            Vector3 cp = col.ClosestPoint(origin);
            Vector3 dir = origin - cp;
            samplePts[n++] = dir.sqrMagnitude > 0.0001f ? cp + dir.normalized * 0.02f : cp;
            Vector3 e = b.extents;
            Vector3[] axes = { Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            for (int i = 0; i < axes.Length && n < samplePts.Length; i++)
            {
                Vector3 q = b.center + Vector3.Scale(axes[i], e) * 1.5f;
                Vector3 p = col.ClosestPoint(q);
                samplePts[n++] = p + axes[i] * 0.02f;
            }
        }
        else samplePts[n++] = b.center;

        // 문틈/칸막이 가장자리의 아주 좁은 틈으로 샘플 한 점만 보이는 경우는 '보인다'로 치지 않는다 (샘플 2개 이상 보여야 함).
        int need = Mathf.Min(2, n);
        float bestDist = float.MaxValue; int clear = 0;
        for (int i = 0; i < n; i++)
        {
            Vector3 d = samplePts[i] - origin;
            float dist = d.magnitude;
            if (dist < 0.001f) { visible = samplePts[i]; return true; }
            if (!HasClearPickupLine(origin, samplePts[i], dist, owner)) continue;
            clear++;
            if (dist < bestDist) { bestDist = dist; visible = samplePts[i]; }
        }
        return clear >= need;
    }

    /// <summary>origin→target 사이에 시야를 막는 Collider 가 없으면 true. 플레이어/대상 자신/다른 줍기 아이템/Trigger 만 무시한다.</summary>
    bool HasClearPickupLine(Vector3 origin, Vector3 target, float dist, Component owner)
    {
        if (dist < 0.001f) return true;
        Vector3 dir = (target - origin) / dist;
        int n = Physics.RaycastNonAlloc(origin, dir, blockHits, dist - 0.003f, interactionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = blockHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (c.transform.IsChildOf(owner.transform)) continue;       // 대상 자신
            if (GetPickupOwner(c) != null) continue;                    // 다른 줍기 아이템은 시야를 막지 않음
            if (IsOpenCabinetOpening(c, blockHits[i].point)) continue;   // 열린 약장의 출입구(앞면)로 들어가는 광선
            return false;                                                // 벽/바닥/가구/닫힌 문 등 실제 구조물
        }
        return true;
    }

    /// <summary>
    /// 약장(DoorPair, isPassableDoor=false)은 몸통이 속이 찬 단순 박스 Collider 하나(DoorPair 와 같은 오브젝트)라서 안쪽 아이템이 항상 가려진다.
    /// 문이 하나라도 열려 있고, 광선이 그 박스의 '문이 달린 앞면'으로 들어올 때만 그 박스를 통과시킨다.
    /// 옆면/뒷면/위아래 면으로 들어오는 광선(= 벽 관통)과 닫힌 문(문짝 Collider)은 그대로 막는다.
    /// </summary>
    static bool IsOpenCabinetOpening(Collider c, Vector3 hitPoint)
    {
        var dp = c.GetComponent<DoorPair>();
        if (dp == null || dp.isPassableDoor || !dp.IsOpen) return false;
        Bounds b = c.bounds;
        Vector3 front = dp.GetDoorCenter() - b.center; front.y = 0f;
        if (front.sqrMagnitude < 0.0001f) return false;
        front.Normalize();
        float extentAlongFront = Mathf.Abs(front.x) * b.extents.x + Mathf.Abs(front.z) * b.extents.z;
        float along = Vector3.Dot(new Vector3(hitPoint.x - b.center.x, 0f, hitPoint.z - b.center.z), front);
        return along >= extentAlongFront - 0.04f;
    }

    void TryPickup(Component owner)
    {
        if (owner is BatteryPickup battery)
        {
            if (!battery.Collect(this)) Notify("INVENTORY FULL");
            else OnInteracted?.Invoke();
            return;
        }
        ((IInteractable)owner).Interact(this);
        OnInteracted?.Invoke();
    }

    // ───────────────────────── 판정 여유 (SphereCast) ─────────────────────────

    /// <summary>
    /// 기존 Physics.Raycast 한 줄을 대체.
    /// 1) 크로스헤어가 상호작용 대상에 정확히 맞으면 기존과 동일하게 그 대상.
    /// 2) 아니면 SphereCast로 화면 중앙 주변 대상을 찾고, 카메라→대상 Raycast로
    ///    벽/장애물에 가려지지 않은 대상만 후보로 삼아 우선순위대로 선택.
    /// 3) 후보가 없으면 기존 Raycast 결과(벽 등)를 그대로 반환 → 이후 로직은 기존과 같음.
    /// </summary>
    bool FindInteractionHit(Ray ray, out RaycastHit result)
    {
        bool direct = Physics.Raycast(ray, out RaycastHit directHit, interactionDistance, interactionMask);

        if (direct && GetInteractOwner(directHit.collider) != null)
        {
            lastAssistTarget = GetInteractOwner(directHit.collider);
            result = directHit;
            return true;
        }

        if (interactionRadius > 0f)
        {
            int count = Physics.SphereCastNonAlloc(ray, interactionRadius, assistHits, interactionDistance, interactionMask);

            float bestScore = float.MaxValue;
            int bestIndex = -1;
            Component bestOwner = null;

            for (int i = 0; i < count; i++)
            {
                Collider col = assistHits[i].collider;
                if (col == null || col.transform.IsChildOf(transform))
                    continue;

                Component owner = GetInteractOwner(col);
                if (owner == null)
                    continue;

                // 시작 지점에서 이미 겹친 경우 point가 0이므로 Collider 중심 사용
                Vector3 target = assistHits[i].distance > 0f ? assistHits[i].point : col.bounds.center;
                Vector3 toTarget = target - ray.origin;
                float dist = toTarget.magnitude;
                if (dist > interactionDistance + interactionRadius)
                    continue;

                // 벽/장애물에 가려진 대상 제외
                if (!HasClearLine(ray.origin, target, owner))
                    continue;

                float angle = dist > 0.001f ? Vector3.Angle(ray.direction, toTarget) : 0f;
                float score = interactionPriority == InteractionPriority.ClosestToPlayer
                    ? dist + angle * 0.001f
                    : angle + dist * 0.01f;

                // 직전 대상 유지 (Hold E 장치가 프레임마다 바뀌어 초기화되는 것 방지)
                if (owner == lastAssistTarget)
                    score -= interactionPriority == InteractionPriority.ClosestToPlayer ? 0.1f : 1.5f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                    bestOwner = owner;
                }
            }

            if (bestIndex >= 0)
            {
                lastAssistTarget = bestOwner;
                result = assistHits[bestIndex];
                if (drawInteractionDebug)
                    Debug.DrawLine(ray.origin, result.distance > 0f ? result.point : result.collider.bounds.center, Color.green);
                return true;
            }
        }

        lastAssistTarget = null;
        result = directHit;
        return direct;
    }

    bool HasClearLine(Vector3 origin, Vector3 target, Component owner)
    {
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist < 0.001f)
            return true;

        dir /= dist;
        // 대상 표면 바로 앞까지 검사. 처음 맞은 것이 같은 대상이면 보이는 것.
        if (Physics.Raycast(origin, dir, out RaycastHit block, dist + 0.05f, interactionMask))
        {
            if (block.collider.transform.IsChildOf(transform))
                return true; // 자기 자신(드물게 시작점 겹침)은 무시
            bool visible = GetInteractOwner(block.collider) == owner;
            if (drawInteractionDebug && !visible)
                Debug.DrawLine(origin, block.point, Color.red);
            return visible;
        }
        return true;
    }

    /// <summary>기존 CheckInteraction과 같은 우선순위(배터리 → 장치 → IInteractable)로 대상 컴포넌트 반환.</summary>
    static Component GetInteractOwner(Collider col)
    {
        if (col == null) return null;

        var battery = col.GetComponentInParent<BatteryPickup>();
        if (battery != null) return battery;

        var device = col.GetComponentInParent<HoldInteractable>();
        if (device != null) return device;

        var interactable = col.GetComponentInParent<IInteractable>() as Component;
        return DecorativeOpenable.IsDecorative(interactable) ? null : interactable;
    }

    void HideInteractionText()
    {
        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }
}
