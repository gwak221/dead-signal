using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// DoorLeaf_L / DoorLeaf_R 한 세트(양문). PlayerInteraction의 IInteractable(E) 경로로 동작한다.
/// 각 HingedDoor의 closedLocalRotation / openLocalRotation(팀원 FBX door_open_angle)만 사용하고,
/// openAmount를 0↔1로 약 openSeconds 동안 EaseInOut으로 보간한다. 회전각 계산/하드코딩 없음.
/// </summary>
public class DoorPair : MonoBehaviour, IInteractable
{
    public HingedDoor leafL;
    public HingedDoor leafR;
    public float openSeconds = 0.8f;
    [Header("Prompt")]
    public string openText = "[E] OPEN";
    public string closeText = "[E] CLOSE";

    [Header("Monster (몬스터 AI 가 열 수 있는 일반 통행 문인지)")]
    [Tooltip("true = 병원 일반 문 (MonsterDoorOpener 가 직접 열 수 있음). med_cabinet 등 수납장은 false.")]
    public bool isPassableDoor = true;

    [Header("Lock (잠긴 동안 플레이어/몬스터 모두 열 수 없음)")]
    [Tooltip("시작부터 잠김. 튜토리얼 비콘(Beacon.unlockDoor)에 연결하면 비콘이 시작 시 잠그고 활성화 시 풀어 준다.")]
    public bool locked;
    public string lockedText = "LOCKED";
    [Tooltip("잠긴 동안 NavMeshObstacle 로 NavMesh 를 막아 몬스터가 이 문으로 경로를 잡지 않게 한다 (문 프레임은 Bake 에서 제외돼 있으므로).")]
    public bool blockNavMeshWhileLocked = true;

    NavMeshObstacle lockObstacle;

    public bool IsLocked => locked;

    float t;       // 0 닫힘 ~ 1 열림 (선형 진행도)
    float target;

    // 몬스터 AI 가 문 상태를 읽기 위한 공개 API (플레이어 상호작용 경로는 그대로)
    public bool IsOpen => target > 0.5f;
    public bool IsFullyOpen => t >= 0.95f;
    public bool IsClosed => target < 0.5f && t <= 0.05f;
    public float OpenProgress => t;

    void Awake()
    {
        // 안전장치: 이름에 cabinet 이 들어가는 수납장(med_cabinet)은 항상 통과용 문이 아니다.
        if (gameObject.name.ToLowerInvariant().Contains("cabinet")) isPassableDoor = false;
        Apply(0f);
        if (locked) Lock();
    }

    /// <summary>문이 움직이기 시작할 때 (문, 여는 중인지, 플레이어가 했는지). 사운드용.</summary>
    public static event System.Action<DoorPair, bool, bool> AnyMoved;
    /// <summary>잠긴 문을 플레이어가 열려고 했을 때. 사운드용.</summary>
    public static event System.Action<DoorPair> AnyLocked;
    /// <summary>잠금이 풀렸을 때.</summary>
    public static event System.Action<DoorPair> AnyUnlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AnyMoved = null; AnyLocked = null; AnyUnlocked = null; }

    /// <summary>문을 닫힌 상태로 잠근다 (몬스터 경로도 막음).</summary>
    public void Lock()
    {
        locked = true;
        target = 0f;
        if (blockNavMeshWhileLocked) SetObstacle(true);
    }

    public void Unlock()
    {
        if (!locked) return;
        locked = false;
        SetObstacle(false);
        AnyUnlocked?.Invoke(this);
    }

    void SetObstacle(bool on)
    {
        if (!on) { if (lockObstacle != null) lockObstacle.enabled = false; return; }
        if (lockObstacle == null)
        {
            // 두 문짝의 월드 AABB 크기로 문 앞을 막는 박스 (월드 축 정렬)
            Bounds b = new Bounds(GetDoorCenter(), Vector3.zero);
            bool any = false;
            foreach (var leaf in new[] { leafL, leafR })
                if (leaf != null)
                    foreach (var r in leaf.GetComponentsInChildren<Renderer>())
                    {
                        if (!any) { b = r.bounds; any = true; }
                        else b.Encapsulate(r.bounds);
                    }
            var go = new GameObject("LockedNavBlock");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            lockObstacle = go.AddComponent<NavMeshObstacle>();
            lockObstacle.shape = NavMeshObstacleShape.Box;
            lockObstacle.size = new Vector3(Mathf.Max(b.size.x, 0.4f), Mathf.Max(b.size.y, 2f), Mathf.Max(b.size.z, 0.4f));
            lockObstacle.carving = true;
            lockObstacle.carveOnlyStationary = false;
        }
        lockObstacle.enabled = true;
    }

    // Open/Close = 몬스터 AI 경로, Interact = 플레이어 경로
    public void Open()
    {
        if (locked) return;
        if (leafL == null && leafR == null) return;
        if (target < 0.5f) AnyMoved?.Invoke(this, true, false);
        target = 1f;
    }

    float burstUntil;   // Time.time 이 이 값 이전이면 짧은 개방 시간(BurstOpen)으로 보간

    /// <summary>E-07 Hound 돌진용: 잠기지 않은 문을 짧은 시간(seconds)에 강제로 연다.</summary>
    public void BurstOpen(float seconds)
    {
        if (locked) return;
        if (leafL == null && leafR == null) return;
        if (target < 0.5f) AnyMoved?.Invoke(this, true, false);
        target = 1f;
        burstUntil = Time.time + Mathf.Max(0.05f, seconds) + 0.5f;
        burstSeconds = Mathf.Max(0.05f, seconds);
    }
    float burstSeconds;

    public void Close()
    {
        if (leafL == null && leafR == null) return;
        if (target > 0.5f) AnyMoved?.Invoke(this, false, false);
        target = 0f;
    }

    /// <summary>두 문짝(경첩)의 중간 지점.</summary>
    public Vector3 GetDoorCenter()
    {
        if (leafL != null && leafR != null) return (leafL.transform.position + leafR.transform.position) * 0.5f;
        if (leafL != null) return leafL.transform.position;
        if (leafR != null) return leafR.transform.position;
        return transform.position;
    }

    public string GetInteractionText()
    {
        if (DecorativeOpenable.IsDecorative(this)) return string.Empty;   // 장식용: 프롬프트 없음
        if (locked) return lockedText;
        if (target <= 0.5f)
        {
            var gate = GetComponentInParent<LootQteGate>();
            if (gate != null && gate.locked) return gate.lockedText;   // 아이템이 든 약장: 잠금 QTE 필요
        }
        return target > 0.5f ? closeText : openText;
    }

    public void Interact(PlayerInteraction player)
    {
        if (leafL == null && leafR == null) return;
        if (DecorativeOpenable.IsDecorative(this)) return;   // 장식용: 열고 닫기 불가
        if (locked)
        {
            if (player != null) player.Notify(lockedText);
            AnyLocked?.Invoke(this);
            return;
        }
        if (target <= 0.5f)
        {
            var gate = GetComponentInParent<LootQteGate>();
            if (gate != null && gate.Intercept(() => { target = 1f; AnyMoved?.Invoke(this, true, true); })) return;
        }
        target = target > 0.5f ? 0f : 1f;   // 이동 중이면 현재 위치에서 반대로
        AnyMoved?.Invoke(this, target > 0.5f, true);
    }

    void Update()
    {
        if (Mathf.Approximately(t, target)) return;
        float dur = Time.time < burstUntil ? burstSeconds : openSeconds;
        t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, dur));
        Apply(Mathf.SmoothStep(0f, 1f, t));
    }

    void Apply(float amount)
    {
        if (leafL != null) { leafL.openAmount = amount; leafL.Apply(); }
        if (leafR != null) { leafR.openAmount = amount; leafR.Apply(); }
    }
}
