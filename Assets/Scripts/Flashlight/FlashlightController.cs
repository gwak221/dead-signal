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
    public float restunCooldown = 8f; // 알파 피드백으로 3 → 8
    [Tooltip("판정 영역을 벗어나면 누적 시간을 즉시 0으로.")]
    public bool resetExposureInstantly = false;
    [Tooltip("즉시 초기화가 아닐 때 초당 감소량(초). 4 = 0.6초 누적이 0.15초 만에 사라짐.")]
    public float exposureDecayRate = 4f;

    [Header("Stun Charges (손전등 조명은 무제한, 몬스터 스턴만 횟수 제한)")]
    [Tooltip("한 판에서 손전등으로 몬스터를 스턴시킬 수 있는 최대 횟수. 스턴이 실제로 성공했을 때만 1 소모.")]
    public int maxStunCharges = 3;

    [Header("Reload (R키: 0/3 방전 + AA 배터리 보유 시에만)")]
    [Tooltip("손전등이 아래로 내려가는 시간(초).")] public float reloadLowerTime = 0.35f;
    [Tooltip("아래에서 배터리를 갈아 끼우는 정지 시간(초).")] public float reloadHoldTime = 0.45f;
    [Tooltip("원래 위치로 돌아오는 시간(초).")] public float reloadRaiseTime = 0.4f;
    [Tooltip("Reload 중 손전등 모델이 내려가는 양 (Held 기준 로컬, m).")] public Vector3 reloadLowerOffset = new Vector3(-0.02f, -0.17f, 0.03f);
    [Tooltip("Reload 중 손전등 모델의 추가 기울기(도).")] public Vector3 reloadTiltEuler = new Vector3(24f, 0f, -12f);
    public bool showReloadBatteryModel = true;

    [Header("Line of Sight")]
    [Tooltip("시야 차단 검사에 쓰는 레이어. 기본 Everything.")]
    public LayerMask obstacleMask = ~0;

    [Header("Beam Visual (실제 Spot Light 조명)")]
    public bool createBeamLight = true;
    public Light beamLight;
    public float beamIntensity = 29f; // 가까운 벽 화이트아웃 완화: 40 → 29 (약 72%)
    public float beamRange = 22f;
    [Range(10f, 90f)] public float beamSpotAngle = 55f;

    [Header("Near-Surface Auto Dim (SphereCast → 거리 SmoothDamp → 연속 곡선)")]
    [Tooltip("전방 감지 SphereCast 반지름(m). 점이 아니라 면적으로 감지해 문틀/모서리에서 값이 튀지 않게 한다.")]
    public float nearDimSphereRadius = 0.3f;
    [Tooltip("감지 최대 거리(m). 아무것도 없으면(No Hit) 이 값을 거리로 취급 → 항상 거리값이 존재.")]
    public float maxDetectionDistance = 8f;
    [Tooltip("x = 필터링된 전방 거리(m), y = intensity 배율(×beamIntensity). 기본(29 기준): 8m 29 / 6m 25 / 5m 21 / 4m 17 / 3m 12 / 2m 7 / 1m 2.5 / 0.5m 1.")]
    public AnimationCurve nearDimCurve = DefaultNearDimCurve();
    [Tooltip("가까워져 어두워질 때 거리 SmoothDamp 시간(초).")]
    public float dimSmoothTime = 0.3f;
    [Tooltip("멀어져 다시 밝아질 때 거리 SmoothDamp 시간(초). 더 느리게.")]
    public float brightenSmoothTime = 0.6f;
    [Tooltip("빛 원점(Near-wall correction)의 카메라 기준 위치 보간 속도(1/초).")]
    public float beamOriginSmoothing = 14f;

    static AnimationCurve DefaultNearDimCurve()
    {
        float[] d = { 0.5f, 1f, 2f, 3f, 4f, 5f, 6f, 8f };
        float[] i = { 1f, 2.5f, 7f, 12f, 17f, 21f, 25f, 29f };
        var keys = new Keyframe[d.Length];
        for (int k = 0; k < d.Length; k++)
        {
            float slope = 0f;
            if (k > 0 && k < d.Length - 1)
            {
                float s0 = (i[k] - i[k - 1]) / (d[k] - d[k - 1]);
                float s1 = (i[k + 1] - i[k]) / (d[k + 1] - d[k]);
                slope = 0.5f * (s0 + s1) / 29f;
            }
            keys[k] = new Keyframe(d[k], i[k] / 29f, slope, slope);
        }
        return new AnimationCurve(keys);
    }

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

    /// <summary>스턴 횟수를 모두 써서 손전등이 방전된 상태인지 (Light 점등 불가).</summary>
    public bool IsBatteryEmpty => stunCharges <= 0;
    /// <summary>근거리 자동 감광 배율 (1 = 감광 없음). 디버그/테스트용 읽기 전용.</summary>
    public float NearDimFactor => nearDim;
    /// <summary>SmoothDamp 로 필터링된 전방 거리 (디버그/테스트용).</summary>
    public float FilteredDistance => filteredDistance;

    int stunCharges;

    bool reloading;
    float reloadElapsed;
    GameObject reloadBatteryModel;
    FlashlightBatteryStock batteryStock;
    /// <summary>배터리 교체 연출 중 (손전등 사용/스턴 불가).</summary>
    public bool IsReloading => reloading;
    public float ReloadTotalTime => reloadLowerTime + reloadHoldTime + reloadRaiseTime;
    /// <summary>남은 스턴 횟수 (0 이면 빛은 켜지지만 더 이상 스턴되지 않음).</summary>
    public int StunCharges => stunCharges;
    public int MaxStunCharges => maxStunCharges;
    /// <summary>(남은 횟수, 최대 횟수) 변경 시.</summary>
    public event Action<int, int> OnStunChargesChanged;

    /// <summary>스턴 횟수를 최대로 되돌린다. 씬이 다시 로드되면(Retry / Quit to Title) 새 컴포넌트가 만들어져 자동으로 초기화된다.</summary>
    public void ResetStunCharges()
    {
        stunCharges = Mathf.Max(0, maxStunCharges);
        OnStunChargesChanged?.Invoke(stunCharges, maxStunCharges);
    }

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
        stunCharges = Mathf.Max(0, maxStunCharges);
        if (GetComponent<FlashlightSlotCooldownUI>() == null)
            gameObject.AddComponent<FlashlightSlotCooldownUI>();
        if (GetComponent<FlashlightChargesUI>() == null)
            gameObject.AddComponent<FlashlightChargesUI>();
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

        UpdateReload();

        bool canUse = equipped != null && (controller == null || controller.ControlsEnabled) && !reloading;
        // 스턴 횟수를 다 쓰면(0/3) 방전: 우클릭해도 점등되지 않는다.
        bool held = canUse && stunCharges > 0 && Mouse.current != null && Mouse.current.rightButton.isPressed;

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
                stunCharges = Mathf.Max(0, stunCharges - 1); // 실제로 스턴이 성공했을 때만 소모
                OnStunChargesChanged?.Invoke(stunCharges, maxStunCharges);
                if (stunCharges <= 0) SetOn(false); // 마지막 스턴으로 방전되는 순간 즉시 Light OFF
                equipped.StartCooldown(restunCooldown);
                feedback?.PlayStunFlash(best.FacePosition);
                OnMonsterStunned?.Invoke(best);
            }
            exposure = 0f;
        }
    }

    // ───────────────────────── Reload (배터리 교체) ─────────────────────────

    bool CanStartReload()
    {
        if (reloading) return false;
        if (stunCharges > 0) return false;                                   // 1/3, 2/3 에서는 교체 금지 (AA 낭비 방지)
        if (controller != null && !controller.ControlsEnabled) return false; // Game Over / Caught / QTE / 숨기 / 메뉴 / 암호 UI 등
        if (GameProgress.IsEnded || GameMenuManager.MenuOpen) return false;
        // 손전등이 현재 선택되어 실제로 손에 들려 있을 때만 (다른 슬롯 / 맨손이면 R 무반응).
        if (equipped == null || inventory == null || !inventory.IsSelected(InventoryItemType.Flashlight)) return false;
        if (batteryStock == null) batteryStock = GetComponent<FlashlightBatteryStock>();
        return batteryStock != null && batteryStock.Count >= 1;
    }

    void UpdateReload()
    {
        if (!reloading)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && CanStartReload())
            {
                // 안전한 쪽: 시작 순간 1개 소비(예약). reloading 플래그로 재진입이 막혀 R 연타해도 정확히 1개.
                if (batteryStock.TryConsume(1))
                {
                    reloading = true;
                    reloadElapsed = 0f;
                    SetOn(false);
                    exposure = 0f;
                    currentTarget = null;
                    feedback?.SetExposure(0f, 0);
                }
            }
            return;
        }

        reloadElapsed += Time.deltaTime;
        if (reloadElapsed >= ReloadTotalTime)
        {
            reloading = false;
            DestroyReloadModel();
            ResetStunCharges();   // BATTERY EMPTY → STUN 3/3
            if (equipped != null) equipped.transform.localRotation = Quaternion.Euler(equipped.holdLocalEuler);
        }
    }

    // 0 = 평소 Held 위치, 1 = 화면 아래로 완전히 내려감
    float ReloadAmount()
    {
        float t = reloadElapsed;
        if (t < reloadLowerTime) return Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, reloadLowerTime));
        t -= reloadLowerTime;
        if (t < reloadHoldTime) return 1f;
        t -= reloadHoldTime;
        return 1f - Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, reloadRaiseTime));
    }

    // LateUpdate 에서 호출: Held 모델에 내림/기울기 추가 + 중간 구간에 AA 모델 잠깐 표시.
    void ApplyReloadPose()
    {
        if (!reloading || equipped == null || playerCamera == null) { DestroyReloadModel(); return; }
        float a = ReloadAmount();
        Transform item = equipped.transform;
        Transform parent = item.parent;
        if (parent == null) return;

        float sway = a >= 1f ? Mathf.Sin((reloadElapsed - reloadLowerTime) * 14f) * 0.004f : 0f; // 교체하는 느낌의 미세한 흔들림
        item.position += parent.TransformVector(reloadLowerOffset * a + new Vector3(0f, sway, 0f));
        item.localRotation = Quaternion.Euler(equipped.holdLocalEuler) * Quaternion.Euler(reloadTiltEuler * a);

        // AA 모델: 아래에 내려간 구간(정지 시작 ~ 끝 직전)에만, 옆에서 손전등 쪽으로 밀려 들어오고 사라진다.
        float st = reloadLowerTime, en = reloadLowerTime + reloadHoldTime;
        if (showReloadBatteryModel && reloadElapsed >= st + 0.03f && reloadElapsed <= en - 0.03f)
        {
            if (reloadBatteryModel == null) reloadBatteryModel = CreateReloadModel();
            if (reloadBatteryModel != null)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(st + 0.03f, en - 0.03f, reloadElapsed));
                Transform cam = playerCamera.transform;
                Vector3 target = item.position + cam.up * 0.13f - cam.right * 0.03f;
                Vector3 start = target + cam.right * 0.16f + cam.up * 0.05f;
                reloadBatteryModel.transform.SetPositionAndRotation(Vector3.Lerp(start, target, k), cam.rotation * Quaternion.Euler(10f, 30f, 75f));
            }
        }
        else DestroyReloadModel();
    }

    GameObject CreateReloadModel()
    {
        var prefab = Resources.Load<GameObject>("AABatteryViewModel");
        if (prefab == null) return null;
        var go = Instantiate(prefab);
        go.name = "ReloadBatteryModel";
        return go;
    }

    void DestroyReloadModel()
    {
        if (reloadBatteryModel != null) Destroy(reloadBatteryModel);
        reloadBatteryModel = null;
    }

    void OnDisable()
    {
        DestroyReloadModel();
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
    float nearDim = 1f;
    float filteredDistance = -1f, filteredVel;
    Vector3 smoothedLocalOff; bool offInit;

    void LateUpdate()
    {
        if (equipped != null)
            UpdateHeldModelClearance();
        else
            modelScale = 1f;

        ApplyReloadPose();
        UpdateBeamPower();

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
            // 경계(lightProbeDistance)에서 갑자기 제한이 걸리지 않도록: 표면이 probe 거리 끝에 있으면 영향 0 → 60% 거리 안쪽에서 100% (smoothstep).
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lightProbeDistance, lightProbeDistance * 0.6f, h2.distance));
            limit = Mathf.Lerp(fwd, limit, w);
            if (fwd > limit) off -= cam.forward * (fwd - limit);
        }

        // 위 보정이 특정 거리에서 즉시 바뀌어도 빛 원점이 튀지 않도록 카메라 로컬 공간에서 지수 보간.
        Vector3 localOff = cam.InverseTransformVector(off);
        if (!offInit) { smoothedLocalOff = localOff; offInit = true; }
        else smoothedLocalOff = Vector3.Lerp(smoothedLocalOff, localOff, 1f - Mathf.Exp(-beamOriginSmoothing * Time.deltaTime));
        off = cam.TransformVector(smoothedLocalOff);

        beamLight.transform.SetPositionAndRotation(camPos + off, Quaternion.LookRotation(cam.forward, cam.up));
    }

    // 전방 SphereCast → 항상 존재하는 원시 거리 (No Hit = maxDetectionDistance).
    float RawFrontDistance()
    {
        Transform cam = playerCamera.transform;
        float max = Mathf.Max(0.5f, maxDetectionDistance);
        float r = Mathf.Max(0.01f, nearDimSphereRadius);
        int n = Physics.SphereCastNonAlloc(cam.position, r, cam.forward, hitBuffer, max, obstacleMask, QueryTriggerInteraction.Ignore);
        float best = max;
        for (int k = 0; k < n; k++)
        {
            var h = hitBuffer[k];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance <= 0f) continue; // 시작부터 겹친 콜라이더
            best = Mathf.Min(best, h.distance + r); // 구 중심 이동거리 + 반지름 ≈ 표면까지 거리
        }
        return best;
    }

    /// <summary>
    /// Physics 감지(raw 거리) → SmoothDamp(filteredDistance) → AnimationCurve → 최종 intensity.
    /// Hit/NoHit 전환도 거리 값이 연속적으로 변하므로 밝기가 튀지 않는다. Range / Spot Angle 은 항상 그대로.
    /// </summary>
    void UpdateBeamPower()
    {
        if (beamLight == null) return;

        float max = Mathf.Max(0.5f, maxDetectionDistance);
        if (equipped != null && playerCamera != null)
        {
            float raw = RawFrontDistance();
            if (filteredDistance < 0f) filteredDistance = raw;
            float st = raw < filteredDistance ? dimSmoothTime : brightenSmoothTime;
            filteredDistance = Mathf.SmoothDamp(filteredDistance, raw, ref filteredVel, Mathf.Max(0.01f, st), Mathf.Infinity, Time.deltaTime);
        }
        else
        {
            filteredDistance = max; filteredVel = 0f; // 미장착: 다음 장착 시 최대 거리에서 시작
        }

        nearDim = (nearDimCurve != null && nearDimCurve.length > 0)
            ? Mathf.Clamp01(nearDimCurve.Evaluate(Mathf.Clamp(filteredDistance, 0f, max))) : 1f;

        beamLight.intensity = beamIntensity * nearDim;
        beamLight.range = beamRange;
        beamLight.spotAngle = beamSpotAngle;
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
