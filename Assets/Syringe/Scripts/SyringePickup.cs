using UnityEngine;

/// <summary>
/// 주사기 아이템 (S-A-S_2401). F 로 줍고(인벤토리 1/2/3 슬롯), 슬롯에서 꺼낸 뒤 E 로 사용한다.
/// 사용: SyringeAnimation.Inject() 로 플런저를 끝까지 밀고, 다 들어가면
/// effectSeconds 동안 심박수와 상관없이 달릴 수 있게 한 뒤(HeartRateController.ApplySprintUnlock) 소비된다.
/// BPM 은 평소대로 오르고 몬스터 탐지 배율도 그대로다. 효과 중에는 화면 가장자리가 은은하게 빛난다(ItemUseScreenEffect).
/// 효과 중에 또 쓰면 남은 시간이 effectSeconds 로 갱신된다(중첩 없음).
/// </summary>
[DisallowMultipleComponent]
public class SyringePickup : MonoBehaviour, IInteractable, IPickupable
{
    [Header("Effect")]
    [Tooltip("심박수와 상관없이 달릴 수 있는 시간(초).")]
    [Min(0f)] public float effectSeconds = 15f;
    [Tooltip("효과 중 화면 가장자리 색.")]
    public Color edgeColor = new Color(0.25f, 0.95f, 0.85f, 1f);

    [Header("Held pose (holdPoint 기준)")]
    public Vector3 holdLocalPosition = new Vector3(0.07f, -0.09f, 0.2f);
    public Vector3 holdLocalEuler = new Vector3(75f, 0f, 0f);
    [Tooltip("손에 들었을 때만 적용되는 크기 배율 (월드에 놓인 크기는 그대로).")]
    [Min(0.1f)] public float holdScale = 1.6f;

    /// <summary>손에 들기 전 원래 로컬 스케일 (손에서 내려놓을 때 복원).</summary>
    [System.NonSerialized] public Vector3 worldScale = Vector3.one;
    bool scaleCaptured;

    SyringeAnimation anim;

    public SyringeAnimation Animation => anim != null ? anim : (anim = GetComponentInChildren<SyringeAnimation>(true));

    /// <summary>주입 애니메이션 길이(초). 애니메이션이 없으면 0.</summary>
    public float InjectSeconds => Animation != null ? Animation.duration : 0f;

    void Awake() => CaptureScale();

    public void CaptureScale()
    {
        if (scaleCaptured) return;
        scaleCaptured = true;
        worldScale = transform.localScale;
    }

    public string GetInteractionText() => "[F] PICK UP SYRINGE";

    public void Interact(PlayerInteraction player)
    {
        if (player == null || player.Inventory == null) return;
        CaptureScale();
        if (Animation != null) Animation.SetProgress(0f);   // 바닥에 버렸다가 다시 줍는 경우 대비
        if (!player.Inventory.TryAddWorldItem(InventoryItemType.Syringe, gameObject))
            player.Notify("INVENTORY FULL");
    }

    /// <summary>현재 손에 든 주사기 (없으면 null).</summary>
    public static SyringePickup Selected(PlayerInventory inv)
    {
        if (inv == null || !inv.IsSelected(InventoryItemType.Syringe)) return null;
        var go = inv.SelectedWorldObject;
        return go != null ? go.GetComponentInChildren<SyringePickup>(true) : null;
    }

    /// <summary>주입 시작 (애니메이션만). 효과와 소비는 FinishUse 에서.</summary>
    public void BeginInject()
    {
        if (Animation != null) Animation.Inject();
    }

    /// <summary>주입 취소 — 플런저를 원위치 (소비 없음).</summary>
    public void CancelInject()
    {
        if (Animation != null) { Animation.StopAllCoroutines(); Animation.SetProgress(0f); }
    }

    /// <summary>주입 완료: 달리기 제한 해제 + 가장자리 효과 + 손에 든 주사기 소비.</summary>
    public static bool FinishUse(PlayerInteraction player)
    {
        if (player == null || player.Inventory == null) return false;
        var syringe = Selected(player.Inventory);
        if (syringe == null) return false;

        float seconds = syringe.effectSeconds;
        Color color = syringe.edgeColor;
        var hr = HeartRateController.Instance != null ? HeartRateController.Instance : FindFirstObjectByType<HeartRateController>();
        if (hr != null) hr.ApplySprintUnlock(seconds);
        else Debug.LogWarning("[SyringePickup] HeartRateController 가 씬에 없어 효과 없이 소비합니다.");

        ItemUseScreenEffect.Sustain(color, seconds);
        player.Inventory.TryConsumeSelected(InventoryItemType.Syringe);
        player.Notify("SYRINGE INJECTED - SPRINT UNLOCKED (" + seconds.ToString("0") + "s)", 1.6f);
        return true;
    }
}
