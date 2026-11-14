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
    Component lastAssistTarget;

    PlayerInventory inventory;
    HoldInteractable heldDevice;
    bool interactionEnabled = true;
    float messageUntil;
    string temporaryMessage;

    public bool InteractionEnabled => interactionEnabled;
    public PlayerInventory Inventory => inventory;

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

        CheckInteraction();
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

        if (FindInteractionHit(ray, out RaycastHit hit))
        {
            var kb = Keyboard.current;
            var battery = hit.collider.GetComponentInParent<BatteryPickup>();
            var device = hit.collider.GetComponentInParent<HoldInteractable>();

            if (battery != null)
            {
                ResetHold();
                ShowMessage("[E] PICK UP BATTERY");

                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    if (!battery.Collect(this))
                        Notify("INVENTORY FULL");
                }
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

                if (kb != null && kb.eKey.wasPressedThisFrame)
                    interactable.Interact(this);

                return;
            }
        }

        ResetHold();
        HideInteractionText();
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
