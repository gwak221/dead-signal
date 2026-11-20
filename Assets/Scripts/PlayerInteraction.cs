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
    readonly RaycastHit[] pickupHits = new RaycastHit[32];
    readonly RaycastHit[] pickupRayHits = new RaycastHit[32];
    readonly RaycastHit[] blockHits = new RaycastHit[32];
    string pendingPickupText; // 이번 프레임 F 줍기 대상의 프롬프트 (없으면 null)
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
        if (!interactionEnabled)
        {
            HideInteractionText();
            return;
        }

        if (Time.time < messageUntil)
        {
            ShowMessage(temporaryMessage);
            return;
        }

        pendingPickupText = null;
        CheckInteraction();

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

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // F 줍기: E 상호작용 판정과 완전히 분리된 별도 판정.
        Component pickup = FindPickup(ray, out pendingPickupText);
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
                ResetHold();
                ShowMessage("[F] PICK UP BATTERY"); // 실제 줍기는 TryPickup(F 전용 판정)에서 처리
                return;
            }

            if (heldDevice != device)
                ResetHold();

            if (device != null)
            {
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
        HideInteractionText();
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

                string t = owner is BatteryPickup ? "[F] PICK UP BATTERY" : ((IInteractable)owner).GetInteractionText();
                if (string.IsNullOrEmpty(t)) continue; // 예: 닫힌 약장 안 AA

                Vector3 target = col.bounds.center;
                Vector3 toTarget = target - ray.origin;
                float dist = toTarget.magnitude;
                if (dist > interactionDistance + interactionRadius) continue;
                if (!HasClearPickupLine(ray.origin, target, dist, owner)) continue;

                float angle = dist > 0.001f ? Vector3.Angle(ray.direction, toTarget) : 0f;
                float score = angle + dist * 0.01f;
                if (score < bestScore) { bestScore = score; best = owner; bestText = t; }
            }
        }
        text = bestText;
        return best;
    }

    bool HasClearPickupLine(Vector3 origin, Vector3 target, float dist, Component owner)
    {
        if (dist < 0.001f) return true;
        Vector3 dir = (target - origin) / dist;
        int n = Physics.RaycastNonAlloc(origin, dir, blockHits, dist, interactionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = blockHits[i].collider;
            if (c == null || c.transform.IsChildOf(transform)) continue;
            if (c.transform.IsChildOf(owner.transform)) continue;       // 대상 자신
            if (GetPickupOwner(c) != null) continue;                    // 다른 줍기 아이템은 시야를 막지 않음
            if (c.bounds.Contains(target))
            {
                // 대상을 감싸는 Collider: 가구 몸통(큰 상호작용 박스)은 무시, 닫힌 문짝/서랍은 막음.
                var part = c.GetComponentInParent<OpenablePart>();
                if (part != null && !part.IsOpen) return false;
                if (c.GetComponentInParent<IInteractable>() != null) continue;
            }
            return false; // 벽/바닥/닫힌 문 등 실제 막힌 구조물
        }
        return true;
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

        var interactable = col.GetComponentInParent<IInteractable>();
        return interactable as Component;
    }

    void HideInteractionText()
    {
        if (interactionText != null)
            interactionText.gameObject.SetActive(false);
    }
}
