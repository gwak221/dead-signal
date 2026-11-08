using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 몬스터 스턴용 손전등 (Dead by Daylight 스타일 '눈뽕').
///  - 손전등은 인벤토리 아이템(InventoryItemType.Flashlight, FlashlightItem). 게임 시작 시 1개 지급.
///  - 손전등 슬롯을 선택해 '장착한 상태'에서만 우클릭 사용 가능. 맨손/다른 아이템이면 무반응.
///  - 스턴 성공 후 재사용 대기시간은 FlashlightItem 오브젝트에 저장 → 버렸다 주워도 유지.
///  - 배터리 무한.
///  - 정확한 에임 대신: 몬스터가 화면 중앙 영역(screenArea) 안에 있고, 사거리 안이고,
///    Raycast로 벽에 가려지지 않았으면 '비추는 중'으로 판정.
///  - requiredExposure초 동안 계속 비추면 스턴 + 플래시 연출.
///  - 플레이어 조작이 잠긴 상태(비밀번호 UI 등)에서는 사용 불가.
/// Player에 이 컴포넌트가 없으면 씬 로드 시 자동 추가된다. (MonsterChase에는 MonsterStun 자동 추가)
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(110)] // PlayerLimpCamera(100) 이후 LateUpdate에서 빔 위치를 렌즈에 맞춤
public class FlashlightController : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public FirstPersonController controller;
    public Camera playerCamera;
    public FlashlightFeedback feedback;
    public PlayerInventory inventory;

    [Header("Starting Item")]
    [Tooltip("게임 시작 시 손전등 1개를 인벤토리에 지급")]
    public bool giveFlashlightOnStart = true;
    [Tooltip("비우면 Primitive 임시 모델(FlashlightItem.Create) 사용. 실제 모델 프리팹이 나오면 여기에 연결.")]
    public GameObject flashlightPrefab;

    [Header("Stun Rules")]
    [Tooltip("사거리(m)")]
    public float range = 10f;
    [Tooltip("판정에 사용할 화면 중앙 영역 비율. 0.8 = 가장자리 10%씩 제외.")]
    [Range(0.3f, 1f)] public float screenArea = 0.8f;
    [Tooltip("눈 파손 상태일 때 사용할 화면 판정 영역. 0.68 = 가장자리 16%씩 제외.")]
    [Range(0.3f, 1f)] public float eyeDamagedScreenArea = 0.68f;
    [Tooltip("비우면 같은 오브젝트에서 자동 탐색")]
    public PlayerBodyStatus bodyStatus;
    [Tooltip("스턴에 필요한 연속 노출 시간(초)")]
    public float requiredExposure = 0.6f;
    [Tooltip("몬스터 스턴 시간(초)")]
    public float stunDuration = 2.5f;
    [Tooltip("스턴 성공 후 손전등 재사용 대기시간(초). 손전등 아이템에 저장되고 슬롯에 표시됨.")]
    public float restunCooldown = 3f;
    [Tooltip("판정 영역을 벗어나면 누적 시간을 즉시 0으로.")]
    public bool resetExposureInstantly = false;
    [Tooltip("즉시 초기화가 아닐 때 초당 감소량(초). 4 = 0.6초 누적이 0.15초 만에 사라짐.")]
    public float exposureDecayRate = 4f;

    [Header("Line of Sight")]
    [Tooltip("시야 차단 검사에 쓰는 레이어. 기본 Everything.")]
    public LayerMask obstacleMask = ~0;

    [Header("Beam Visual (실제 Spot Light 조명)")]
    public bool createBeamLight = true;
    public Light beamLight;
    public float beamIntensity = 40f;
    public float beamRange = 22f;
    [Range(10f, 90f)] public float beamSpotAngle = 55f;

    [Header("Debug")]
    public bool drawDebugRays;

    float exposure;
    MonsterStun currentTarget;
    bool isOn;
    FlashlightItem equipped;
    readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public bool IsOn => isOn;
    public float Exposure01 => requiredExposure > 0f ? Mathf.Clamp01(exposure / requiredExposure) : 0f;
    public MonsterStun CurrentTarget => currentTarget;
    public FlashlightItem EquippedFlashlight => equipped;

    /// <summary>현재 적용 중인 화면 판정 영역 (눈 파손 시 축소).</summary>
    public float EffectiveScreenArea
    {
        get
        {
            if (bodyStatus == null) bodyStatus = GetComponent<PlayerBodyStatus>(); // 자동 부착 순서 대비
            return bodyStatus != null && bodyStatus.EyePenalty ? Mathf.Min(screenArea, eyeDamagedScreenArea) : screenArea;
        }
    }

    /// <summary>스턴 성공 시 (사운드 연결용).</summary>
    public event Action<MonsterStun> OnMonsterStunned;

    // ───────────────────────── 자동 부착 ─────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureComponents();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => EnsureComponents();

    static void EnsureComponents()
    {
        foreach (var fpc in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if (fpc.GetComponent<FlashlightController>() == null)
                fpc.gameObject.AddComponent<FlashlightController>();

        foreach (var m in FindObjectsByType<MonsterChase>(FindObjectsSortMode.None))
            if (m.GetComponent<MonsterStun>() == null)
                m.gameObject.AddComponent<MonsterStun>();
    }

    // ───────────────────────── Unity ─────────────────────────

    void Awake()
    {
        if (controller == null) controller = GetComponent<FirstPersonController>();
        if (playerCamera == null && controller != null) playerCamera = controller.playerCamera;
        if (playerCamera == null) playerCamera = Camera.main;
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (bodyStatus == null) bodyStatus = GetComponent<PlayerBodyStatus>();
        if (GetComponent<FlashlightSlotCooldownUI>() == null)
            gameObject.AddComponent<FlashlightSlotCooldownUI>();
    }

    IEnumerator Start()
    {
        if (feedback == null) feedback = GetComponent<FlashlightFeedback>();
        if (feedback == null) feedback = gameObject.AddComponent<FlashlightFeedback>();
        feedback.Init(playerCamera);

        if (beamLight == null && createBeamLight)
            beamLight = CreateBeamLight();
        if (beamLight != null)
            beamLight.enabled = false;

        // PlayerInventory.Start(UI 탐색)가 끝난 다음 프레임에 지급.
        yield return null;
        if (giveFlashlightOnStart)
            GiveStartingFlashlight();
    }

    void GiveStartingFlashlight()
    {
        if (inventory == null || inventory.HasItem(InventoryItemType.Flashlight))
            return;

        Vector3 spawnPos = transform.position + transform.forward * 0.6f + Vector3.up * 0.5f;
        GameObject go;
        if (flashlightPrefab != null)
        {
            go = Instantiate(flashlightPrefab, spawnPos, Quaternion.identity);
            if (go.GetComponent<FlashlightItem>() == null) go.AddComponent<FlashlightItem>();
        }
        else
        {
            go = FlashlightItem.Create(spawnPos).gameObject;
        }

        // 기존 획득 경로 재사용. 실패(가득 참)하면 발 앞에 놓아 E로 주울 수 있게 둔다.
        if (!inventory.TryAddWorldItem(InventoryItemType.Flashlight, go))
            Debug.LogWarning("[FlashlightController] 인벤토리가 가득 차 손전등을 바닥에 놓았습니다.", go);
    }

    FlashlightItem FindEquippedFlashlight()
    {
        if (inventory == null || !inventory.IsSelected(InventoryItemType.Flashlight))
            return null;
        GameObject go = inventory.SelectedWorldObject;
        return go != null ? go.GetComponent<FlashlightItem>() : null;
    }

    void Update()
    {
        // 장착 중인 손전등 확인 (맨손/다른 아이템이면 null → 우클릭 무반응)
        FlashlightItem nowEquipped = FindEquippedFlashlight();
        if (nowEquipped != equipped)
        {
            SetOn(false); // 이전 손전등 렌즈/빔 끄기
            if (equipped != null) equipped.SetLit(false);
            equipped = nowEquipped;
            exposure = 0f;
            currentTarget = null;
        }

        bool canUse = equipped != null && (controller == null || controller.ControlsEnabled);
        bool held = canUse && Mouse.current != null && Mouse.current.rightButton.isPressed;

        SetOn(held);

        if (!held)
        {
            DecayExposure(false);
            feedback?.SetExposure(0f, 0);
            return;
        }

        if (equipped.IsOnCooldown)
        {
            // 손전등 재사용 대기 중: 빛은 켜지지만 누적 안 됨 (게이지 회색)
            exposure = 0f;
            currentTarget = null;
            feedback?.SetExposure(0f, 2);
            return;
        }

        MonsterStun best = FindBestTarget();

        // 다른 몬스터로 바뀌면 누적 초기화
        if (best != null && best != currentTarget)
        {
            exposure = 0f;
            currentTarget = best;
        }

        if (best == null)
        {
            // 판정 영역 이탈 / 가려짐 → 즉시 초기화 또는 빠르게 감소
            DecayExposure(false);
            if (exposure <= 0f) currentTarget = null;
            feedback?.SetExposure(Exposure01, exposure > 0f ? 1 : 0);
            return;
        }

        if (!best.CanBeStunned)
        {
            // 스턴 중/쿨타임: 누적 안 됨 (무한 스턴 방지)
            exposure = 0f;
            feedback?.SetExposure(0f, 2);
            return;
        }

        exposure += Time.deltaTime;
        feedback?.SetExposure(Exposure01, 1);

        if (exposure >= requiredExposure)
        {
            if (best.TryStun(stunDuration))
            {
                equipped.StartCooldown(restunCooldown);
                feedback?.PlayStunFlash(best.FacePosition);
                OnMonsterStunned?.Invoke(best);
            }
            exposure = 0f;
        }
    }

    void OnDisable()
    {
        SetOn(false);
        if (equipped != null) equipped.SetLit(false);
        exposure = 0f;
        currentTarget = null;
        feedback?.SetExposure(0f, 0);
    }

    // ───────────────────────── 판정 ─────────────────────────

    MonsterStun FindBestTarget()
    {
        if (playerCamera == null) return null;

        Vector3 origin = GetBeamOrigin();
        MonsterStun best = null;
        float bestScore = float.MaxValue;

        var list = MonsterStun.All;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || !m.isActiveAndEnabled) continue;

            // 얼굴 또는 몸 중심 중 하나라도 조건을 만족하면 '비추는 중'
            if (IsPointLit(m, m.FacePosition, origin, out float s1) && s1 < bestScore) { best = m; bestScore = s1; }
            else if (IsPointLit(m, m.CenterPosition, origin, out float s2) && s2 < bestScore) { best = m; bestScore = s2; }
        }
        return best;
    }

    /// <summary>score = 화면 중앙으로부터의 거리 (작을수록 우선)</summary>
    bool IsPointLit(MonsterStun monster, Vector3 point, Vector3 origin, out float score)
    {
        score = float.MaxValue;

        // 1) 사거리
        Vector3 toPoint = point - origin;
        float dist = toPoint.magnitude;
        if (dist > range || dist < 0.01f) return false;

        // 2) 화면 중앙 영역 (WorldToViewportPoint)
        Vector3 vp = playerCamera.WorldToViewportPoint(point);
        if (vp.z <= 0f) return false;
        float margin = (1f - EffectiveScreenArea) * 0.5f;
        if (vp.x < margin || vp.x > 1f - margin || vp.y < margin || vp.y > 1f - margin) return false;

        // 3) 시야 확보 (벽/장애물이 있으면 절대 판정 X)
        if (!HasLineOfSight(monster, origin, toPoint / dist, dist)) return false;

        score = new Vector2(vp.x - 0.5f, vp.y - 0.5f).sqrMagnitude;
        return true;
    }

    bool HasLineOfSight(MonsterStun monster, Vector3 origin, Vector3 dir, float dist)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, dist, obstacleMask, QueryTriggerInteraction.Ignore);

        // 가까운 순으로 검사
        Array.Sort(hitBuffer, 0, count, HitDistanceComparer.Instance);

        bool visible = true;
        for (int i = 0; i < count; i++)
        {
            Transform t = hitBuffer[i].collider.transform;
            if (t.IsChildOf(transform)) continue;            // 플레이어 자신
            visible = t.IsChildOf(monster.transform);        // 몬스터면 보임, 그 외 = 가려짐
            break;
        }

        if (drawDebugRays)
            Debug.DrawRay(origin, dir * dist, visible ? Color.green : Color.red);

        return visible;
    }

    sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    /// <summary>
    /// 손전등 원점 = 플레이어 눈 위치.
    /// 1인칭: 카메라 위치 / 3인칭: CameraPivot(머리) 위치 → 3인칭 카메라 뒤쪽 벽 때문에 막히지 않도록.
    /// </summary>
    Vector3 GetBeamOrigin()
    {
        if (controller != null && controller.IsThirdPerson && controller.cameraPivot != null)
            return controller.cameraPivot.position;
        return playerCamera.transform.position;
    }

    void DecayExposure(bool instant)
    {
        if (instant || resetExposureInstantly) exposure = 0f;
        else exposure = Mathf.Max(0f, exposure - exposureDecayRate * Time.deltaTime);
    }

    // ───────────────────────── 빔 표시 ─────────────────────────

    void SetOn(bool on)
    {
        if (isOn == on) return;
        isOn = on;
        if (beamLight != null) beamLight.enabled = on;
        if (equipped != null) equipped.SetLit(on);
    }

    [Header("Close-Range Safety (벽/캐비넷에 바짝 붙었을 때)")]
    [Tooltip("보이는 손전등 모델이 표면 앞에서 멈추도록 카메라 쪽으로 당길 수 있는 최소 비율 (1 = 평소 위치).")]
    [Range(0.15f, 1f)] public float modelMinScale = 0.3f;
    [Tooltip("모델/빛이 표면에서 떨어져 있어야 하는 최소 거리.")]
    public float surfaceMargin = 0.06f;
    [Tooltip("카메라 전방 이 거리 안에 표면이 있으면 빛 원점의 전방 이동을 제한.")]
    public float lightProbeDistance = 1.6f;
    [Tooltip("표면이 가까울 때 빛 원점의 전방 이동 한계 = (표면거리 - margin) × 이 비율. 낮을수록 원점이 카메라에 가까워져 핫스팟이 줄어든다.")]
    [Range(0.1f, 0.6f)] public float lightForwardRatio = 0.3f;

    float modelScale = 1f;

    void LateUpdate()
    {
        if (equipped != null)
            UpdateHeldModelClearance();
        else
            modelScale = 1f;

        // 빛 = 모델(렌즈)과 분리된 안전한 원점 + 카메라가 보는 방향 (카메라 흔들림 반영 후).
        if (!isOn || beamLight == null || equipped == null || equipped.beamOrigin == null || playerCamera == null)
            return;

        Transform cam = playerCamera.transform;
        Vector3 camPos = cam.position;
        Vector3 off = equipped.beamOrigin.position - camPos;   // 평소 위치 = 렌즈(손) 위치 (기존과 동일)

        // 1) 카메라 → 렌즈 사이가 막혀 있으면(벽/캐비넷 안으로 들어간 경우) 막히기 직전까지만.
        float len = off.magnitude;
        if (len > 0.001f && Physics.Raycast(camPos, off / len, out RaycastHit h1, len + surfaceMargin, obstacleMask, QueryTriggerInteraction.Ignore)
            && !h1.collider.transform.IsChildOf(transform))
            off = off / len * Mathf.Max(0f, h1.distance - surfaceMargin);

        // 2) 정면 가까이에 표면이 있으면 전방 성분을 표면까지 거리의 일부로 제한 (빛이 표면 뒤로 넘어가지 않고 핫스팟도 줄도록).
        float fwd = Vector3.Dot(off, cam.forward);
        if (fwd > 0f && Physics.Raycast(camPos, cam.forward, out RaycastHit h2, lightProbeDistance, obstacleMask, QueryTriggerInteraction.Ignore)
            && !h2.collider.transform.IsChildOf(transform))
        {
            float limit = Mathf.Max(0f, (h2.distance - surfaceMargin) * lightForwardRatio);
            if (fwd > limit) off -= cam.forward * (fwd - limit);
        }

        beamLight.transform.SetPositionAndRotation(camPos + off, Quaternion.LookRotation(cam.forward, cam.up));
    }

    /// <summary>
    /// 보이는 손전등 모델만 앞 표면에 파고들지 않게 카메라 쪽으로 당긴다 (화면상 같은 방향 → 같은 자리에서 가까워질 뿐).
    /// 평소 손 위치(holdLocalPosition)와 애니메이션은 그대로이고, 표면이 가까울 때만 보정.
    /// </summary>
    void UpdateHeldModelClearance()
    {
        if (playerCamera == null || equipped.beamOrigin == null) return;
        Transform item = equipped.transform;
        Transform parent = item.parent;
        if (parent == null) return;

        Transform cam = playerCamera.transform;
        Vector3 basePos = parent.TransformPoint(equipped.holdLocalPosition);        // 평소 위치
        Vector3 lensOffset = equipped.beamOrigin.position - item.position;          // 모델 중심 → 렌즈
        Vector3 tipV = basePos + lensOffset - cam.position;                         // 카메라 → 렌즈(평소)
        float tipLen = tipV.magnitude;

        float want = 1f;
        if (tipLen > 0.01f
            && Physics.Raycast(cam.position, tipV / tipLen, out RaycastHit hit, tipLen + surfaceMargin, obstacleMask, QueryTriggerInteraction.Ignore)
            && !hit.collider.transform.IsChildOf(transform))
            want = Mathf.Clamp((hit.distance - surfaceMargin) / tipLen, modelMinScale, 1f);

        modelScale = Mathf.MoveTowards(modelScale, want, Time.deltaTime * 4f);
        item.position = cam.position + (basePos - cam.position) * modelScale;
    }

    Light CreateBeamLight()
    {
        // CameraPivot에 붙여서 1인칭/3인칭 모두 '플레이어 머리에서 시선 방향'으로 나가게 한다.
        Transform parent = controller != null && controller.cameraPivot != null
            ? controller.cameraPivot
            : playerCamera != null ? playerCamera.transform : transform;

        var go = new GameObject("FlashlightBeam");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0.15f, -0.12f, 0.1f);
        go.transform.localRotation = Quaternion.identity;

        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = new Color(1f, 0.96f, 0.85f);
        l.intensity = beamIntensity;
        l.range = beamRange;
        l.spotAngle = beamSpotAngle;
        l.innerSpotAngle = beamSpotAngle * 0.5f;
        l.shadows = LightShadows.Soft;
        return l;
    }
}
