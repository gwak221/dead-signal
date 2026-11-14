using UnityEngine;

/// <summary>
/// 다리 파손 시 1인칭 카메라 절뚝거림 (비대칭 보행 사이클).
///
/// 걷는 동안 보행 사이클이 계속 돈다. 1사이클 = [다친 다리 걸음] + [멀쩡한 다리 걸음].
///   다친 걸음 : 눈높이가 확실하게 푹 내려감 → 아주 잠깐 낮게 지나감 → 다음 걸음으로 넘어가며 올라옴
///   멀쩡한 걸음: 다친 걸음의 20~35% 정도의 약한 Y 변화
/// 기계적인 반복을 피하기 위해 사이클마다
///   - 사이클 길이 (stepDurationMin~Max + cycleLengthRandomness)
///   - 다친 걸음 깊이 (injuredStepStrengthRandomness)
///   - 왼발/오른발 시간 비율 (footTimingJitter)
///   - 가끔 약하게 넘기는 걸음 (occasionalWeakStepChance) → 다음 걸음은 조금 더 크게 (compensationBoost)
/// 를 새로 뽑는다. 절뚝거림 자체가 사라지는 구간은 없다.
///
/// 주된 표현은 Local Position Y. Roll/Pitch/좌우 쏠림은 아주 작은 보조 효과.
///
/// FirstPersonController는 카메라 localPosition/localRotation을 모드 전환(V) 때만 설정하고,
/// 마우스 상하 회전은 CameraPivot에 적용하므로 이 스크립트는 Main Camera의 로컬 오프셋만 다룬다.
/// 3인칭일 때, 조작이 잠겼을 때(비밀번호 UI 등), 멈춰 있을 때는 적용하지 않고 원위치로 복귀한다.
/// </summary>
[DefaultExecutionOrder(100)] // FirstPersonController.Update 이후 LateUpdate에서 적용
public class PlayerLimpCamera : MonoBehaviour
{
    [Header("References (auto-found if empty)")]
    public FirstPersonController controller;
    public Camera playerCamera;

    [Header("Gait Cycle (보행 리듬)")]
    [Tooltip("보행 1사이클(다친 걸음 + 멀쩡한 걸음) 최소 길이(초). 다리 파손 걷기 속도 기준.")]
    [Range(0.3f, 2f)] public float stepDurationMin = 0.92f;
    [Tooltip("보행 1사이클 최대 길이(초). 사이클마다 Min~Max 사이에서 새로 뽑는다.")]
    [Range(0.3f, 2f)] public float stepDurationMax = 1.38f;
    [Tooltip("사이클 길이 추가 랜덤 편차(±비율). 0.15 = ±15%.")]
    [Range(0f, 0.4f)] public float cycleLengthRandomness = 0.15f;
    [Tooltip("사이클 중 다친 걸음이 차지하는 비율. 0.5 = 대칭, 작을수록 다친 다리에 체중을 짧게 싣는다.")]
    [Range(0.3f, 0.6f)] public float injuredStepRatio = 0.45f;
    [Tooltip("왼발/오른발 시간 비율의 사이클별 편차 (메트로놈 방지).")]
    [Range(0f, 0.1f)] public float footTimingJitter = 0.04f;

    [Header("Injured Step (다친 다리 걸음)")]
    [Tooltip("다친 다리를 디딜 때 눈높이가 내려가는 거리(m). 핵심 값.")]
    [Range(0f, 0.2f)] public float injuredStepDrop = 0.11f;
    [Tooltip("LimpVerticalAmount: 세로 Limp 전체 배율 (1 = 기본 하강량).")]
    [Range(0f, 2f)] public float limpVerticalAmount = 1f;
    [Tooltip("LimpFrequency: 절뚝 리듬 속도 배율 (1 = 기본, 클수록 빠름).")]
    [Range(0.3f, 2f)] public float limpFrequency = 1f;
    [Tooltip("다친 걸음 중 '푹 내려가는' 구간 비율. 작을수록 더 갑자기 꺼진다.")]
    [Range(0.1f, 0.5f)] public float injuredDropPortion = 0.28f;
    [Tooltip("다친 걸음 중 가장 낮은 상태로 지나가는 구간 비율.")]
    [Range(0f, 0.4f)] public float injuredLowPortion = 0.14f;
    [Tooltip("다친 걸음 깊이의 사이클별 랜덤 편차(±비율). 0.15 = ±15%.")]
    [Range(0f, 0.5f)] public float injuredStepStrengthRandomness = 0.15f;

    [Header("Healthy Step (멀쩡한 다리 걸음)")]
    [Tooltip("멀쩡한 걸음의 하강 = 다친 걸음 기준 깊이 × 이 비율. 0.2~0.35 권장.")]
    [Range(0f, 0.6f)] public float healthyStepRatio = 0.28f;

    [Header("Irregularity (가끔 약하게 / 다음엔 크게)")]
    [Tooltip("가끔 다친 걸음을 거의 정상처럼 넘길 확률.")]
    [Range(0f, 0.5f)] public float occasionalWeakStepChance = 0.15f;
    [Tooltip("약하게 넘기는 걸음의 깊이 배율.")]
    [Range(0f, 1f)] public float weakStepScale = 0.35f;
    [Tooltip("약한 걸음 다음 다친 걸음을 추가로 키우는 비율.")]
    [Range(0f, 0.6f)] public float compensationBoost = 0.25f;

    [Header("Secondary (아주 작은 보조 효과)")]
    [Tooltip("다친 걸음에서 다친 쪽으로 기우는 Roll(도). 0~0.5 권장.")]
    [Range(0f, 1f)] public float limpTiltAngle = 0.35f;
    [Tooltip("다친 걸음에서 고개 숙임 Pitch(도). 0~0.3 권장.")]
    [Range(0f, 1f)] public float limpPitchAngle = 0.2f;
    [Tooltip("다친 걸음에서 체중이 한쪽으로 쏠리는 좌우 이동 = 세로 하강량 × 이 비율. 0.15~0.25 권장 (아주 약하게).")]
    [Range(0f, 0.4f)] public float limpHorizontalAmount = 0.17f;
    [Tooltip("true면 오른쪽 다리가 다친 것으로 처리.")]
    public bool injuredRightLeg = true;

    [Header("Comfort")]
    [Tooltip("전체 강도 배율. 멀미 옵션으로 사용.")]
    [Range(0f, 1.5f)] public float intensity = 1f;
    [Tooltip("걷기 시작/멈춤/패널티 해제 시 효과가 들어오고 빠지는 속도. 멈추면 이 속도로 원위치 복귀.")]
    [Range(1f, 20f)] public float blendSpeed = 6f;
    [Tooltip("최종 오프셋 스무딩 (높을수록 즉각적).")]
    [Range(5f, 120f)] public float smoothing = 22f;
    [Tooltip("이 속도(m/s) 이상 움직일 때 '걷는 중'으로 판단.")]
    public float minMoveSpeed = 0.2f;
    [Tooltip("이 시간(초) 이상 멈췄다가 다시 걸으면 다친 걸음부터 새로 시작.")]
    public float resetAfterIdle = 0.5f;

    CharacterController characterController;
    bool limpActive;
    float weight;          // 0~1, 효과 블렌드 (멈춤/해제 시 0으로)
    float phase;           // 0~1, 현재 사이클 진행도
    float idleTime;

    // 사이클별 랜덤 파라미터
    float cycleLength = 0.75f;
    float cycleInjuredRatio = 0.45f;
    float cycleInjuredDepth = 1f;   // injuredStepDrop 대비 배율
    float cycleHealthyDepth = 0.28f;
    float cycleTilt = 1f;
    bool lastWasWeak;
    bool forceStrongNext;

    Vector3 currentPos;
    Vector3 currentEuler;
    bool wasThirdPerson;
    bool applied;

    public bool LimpActive => limpActive;

    void Awake()
    {
        if (controller == null) controller = GetComponent<FirstPersonController>();
        if (playerCamera == null && controller != null) playerCamera = controller.playerCamera;
        characterController = GetComponent<CharacterController>();
        RollNewCycle();
    }

    public void SetLimpActive(bool active)
    {
        limpActive = active;
        if (active && weight <= 0.001f)
        {
            phase = 0f; // 다친 걸음부터 시작
            RollNewCycle();
        }
    }

    /// <summary>다음 다친 걸음을 강하게 (예: 피격 순간 호출).</summary>
    public void TriggerLimp()
    {
        forceStrongNext = true;
    }

    void LateUpdate()
    {
        if (controller == null || playerCamera == null)
            return;

        // 3인칭: FirstPersonController가 위치를 설정했으므로 건드리지 않고 상태만 초기화.
        if (controller.IsThirdPerson)
        {
            wasThirdPerson = true;
            weight = 0f;
            currentPos = Vector3.zero;
            currentEuler = Vector3.zero;
            applied = false;
            return;
        }

        if (wasThirdPerson)
        {
            // 1인칭으로 막 돌아옴 → FirstPersonController가 원위치를 이미 설정함.
            wasThirdPerson = false;
            applied = false;
        }

        float dt = Time.deltaTime;

        float planarSpeed = 0f;
        bool grounded = true;
        if (characterController != null)
        {
            Vector3 v = characterController.velocity;
            v.y = 0f;
            planarSpeed = v.magnitude;
            grounded = characterController.isGrounded;
        }

        bool walking = limpActive && controller.ControlsEnabled && grounded && planarSpeed > minMoveSpeed;

        weight = Mathf.MoveTowards(weight, walking ? 1f : 0f, blendSpeed * dt);

        // ── 보행 사이클 진행 (걷는 동안 계속) ──
        if (walking)
        {
            if (idleTime > resetAfterIdle)
            {
                phase = 0f;
                RollNewCycle();
            }
            idleTime = 0f;

            // 실제 이동 속도에 맞춰 리듬 변화 (느리게 걸으면 느린 절뚝거림)
            float refSpeed = Mathf.Max(0.1f, controller.moveSpeed * Mathf.Max(0.1f, controller.SpeedMultiplier));
            float speedFactor = Mathf.Clamp(planarSpeed / refSpeed, 0.5f, 1.3f);

            phase += dt * speedFactor * limpFrequency / Mathf.Max(0.1f, cycleLength);
            while (phase >= 1f)
            {
                phase -= 1f;
                RollNewCycle();
            }
        }
        else
        {
            idleTime += dt;
        }

        Vector3 targetPos = Vector3.zero;
        Vector3 targetEuler = Vector3.zero;

        if (weight > 0.0001f)
        {
            EvaluateGait(phase, out float injured, out float healthy);

            float amp = intensity * weight;
            float side = injuredRightLeg ? 1f : -1f;

            // 핵심: 눈높이 (다친 걸음 크게 + 멀쩡한 걸음 약하게)
            float vDrop = injuredStepDrop * limpVerticalAmount;
            targetPos.y = -(injured * cycleInjuredDepth + healthy * cycleHealthyDepth) * vDrop * amp;
            // 보조: 다친 쪽으로 살짝 쏠렸다가 멀쩡한 걸음에서 약간 되돌아옴
            targetPos.x = (injured * cycleInjuredDepth - healthy * 0.4f) * vDrop * limpHorizontalAmount * side * amp;
            targetEuler.z = -injured * cycleInjuredDepth * cycleTilt * limpTiltAngle * side * amp; // Roll
            targetEuler.x = injured * cycleInjuredDepth * limpPitchAngle * amp;                     // Pitch
        }

        float k = 1f - Mathf.Exp(-smoothing * dt);
        currentPos = Vector3.Lerp(currentPos, targetPos, k);
        currentEuler = Vector3.Lerp(currentEuler, targetEuler, k);

        bool atRest = weight <= 0.0001f && currentPos.sqrMagnitude < 1e-8f && currentEuler.sqrMagnitude < 1e-6f;

        if (atRest)
        {
            if (applied)
            {
                // 완전히 복귀 → 정확한 원래 값으로 맞추고 이후로는 건드리지 않음.
                playerCamera.transform.localPosition = controller.firstPersonPosition;
                playerCamera.transform.localRotation = Quaternion.identity;
                applied = false;
            }
            currentPos = Vector3.zero;
            currentEuler = Vector3.zero;
            return;
        }

        playerCamera.transform.localPosition = controller.firstPersonPosition + currentPos;
        playerCamera.transform.localRotation = Quaternion.Euler(currentEuler);
        applied = true;
    }

    // ───────────────────────── 사이클 ─────────────────────────

    /// <summary>사이클마다 길이/깊이/좌우 시간 비율을 새로 뽑는다.</summary>
    void RollNewCycle()
    {
        float min = Mathf.Min(stepDurationMin, stepDurationMax);
        float max = Mathf.Max(stepDurationMin, stepDurationMax);
        cycleLength = Random.Range(min, max) * (1f + Random.Range(-cycleLengthRandomness, cycleLengthRandomness));
        cycleLength = Mathf.Max(0.25f, cycleLength);

        cycleInjuredRatio = Mathf.Clamp(injuredStepRatio + Random.Range(-footTimingJitter, footTimingJitter), 0.25f, 0.7f);

        float depth = 1f + Random.Range(-injuredStepStrengthRandomness, injuredStepStrengthRandomness);

        bool weak = !forceStrongNext && !lastWasWeak && Random.value < occasionalWeakStepChance;
        if (weak)
            depth *= weakStepScale;          // 거의 정상처럼 넘김
        else if (lastWasWeak || forceStrongNext)
            depth *= 1f + compensationBoost; // 그 다음 걸음은 조금 더 크게

        lastWasWeak = weak;
        forceStrongNext = false;

        cycleInjuredDepth = depth;
        // 멀쩡한 걸음은 '기준' 깊이 대비 비율 (약한 걸음 사이클이어도 그대로 유지)
        cycleHealthyDepth = healthyStepRatio * (1f + Random.Range(-0.5f, 0.5f) * injuredStepStrengthRandomness);
        cycleTilt = 1f + Random.Range(-0.3f, 0.3f);
    }

    /// <summary>
    /// 사이클 진행도 t(0~1)에 대한 두 걸음의 하강량 (각 0~1).
    /// injured: 푹 내려감(dropPortion) → 낮게 지나감(lowPortion) → 다음 걸음으로 넘어가며 올라옴
    ///          (올라오는 꼬리가 멀쩡한 걸음 초반까지 이어져 '조금 올라옴 → 약한 걸음'으로 연결)
    /// healthy: 다친 걸음이 어느 정도 올라온 뒤 얕고 부드러운 한 번의 하강
    /// 두 값 모두 t=0, t=1에서 0이므로 사이클 경계에서 랜덤 파라미터가 바뀌어도 끊기지 않는다.
    /// </summary>
    void EvaluateGait(float t, out float injured, out float healthy)
    {
        float r = cycleInjuredRatio;

        float injuredSpan = Mathf.Min(0.95f, r + (1f - r) * 0.3f);
        float drop = Mathf.Clamp(injuredDropPortion, 0.05f, 0.6f) * r;
        float low = Mathf.Clamp(injuredLowPortion, 0f, 0.5f) * r;

        if (t < drop)
            injured = EaseInOutSine(t / drop);                       // 푹 (부드러운 곡선)
        else if (t < drop + low)
            injured = 1f;                                            // 낮게 지나감
        else if (t < injuredSpan)
            injured = 1f - EaseInOutSine((t - drop - low) / Mathf.Max(0.01f, injuredSpan - drop - low)); // 올라옴
        else
            injured = 0f;

        float hStart = r + (1f - r) * 0.15f;
        if (t > hStart)
        {
            float u = (t - hStart) / Mathf.Max(0.01f, 1f - hStart);
            healthy = Mathf.Sin(u * Mathf.PI);
            healthy *= healthy; // sin² → 시작/끝이 부드러움
        }
        else
        {
            healthy = 0f;
        }
    }

    static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
    static float EaseInOutSine(float x) => 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(x) * Mathf.PI);

    void OnDisable()
    {
        if (applied && playerCamera != null && controller != null && !controller.IsThirdPerson)
        {
            playerCamera.transform.localPosition = controller.firstPersonPosition;
            playerCamera.transform.localRotation = Quaternion.identity;
        }
        applied = false;
        weight = 0f;
        phase = 0f;
        currentPos = Vector3.zero;
        currentEuler = Vector3.zero;
    }
}
