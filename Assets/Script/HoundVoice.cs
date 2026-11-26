using DeadSignal.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 적2(MonsterHoundAI, 돌진형)의 소리 호출 담당. MonsterHoundAI 의 상태(CurrentState)를 보고 소리를 낸다.
/// 씬/프리팹에 붙일 필요 없음 — HoundAudioAutoAttach 가 게임 시작·씬 로드 때 MonsterHoundAI 에 자동으로 붙인다.
///
///   행동 소리 (PlayExclusive — 한 마리당 동시에 하나만)
///     idle : Patrol 중 idleInterval 랜덤 간격
///     spot : ChargePrep 진입 (플레이어 발견 → 돌진 준비)
///     dash : Charge 진입 (돌진 시작) — spot 을 끊고 재생
///   발소리 (Play — 행동 소리와 동시에 남)
///     돌진은 NavMeshAgent 를 멈추고 직접 이동하므로 애니메이션 이벤트 대신 "이동 거리"로 재생한다.
///     Patrol → stepWalk / Charge, AcceleratedChase → stepDash
///
/// 기절 중 / 플레이어 잡힘 연출 중에는 행동 소리를 내지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class HoundVoice : MonoBehaviour
{
    [Header("소리 이름 (AudioManager)")]
    public string idleSound = "en2_idle";
    public string spotSound = "en2_spot";
    public string dashSound = "en2_dash";
    public string stepWalkSound = "en2_step_walk";
    public string stepDashSound = "en2_step_dash";

    [Header("타이밍")]
    [Tooltip("순찰 중 울음 간격(초) 최소~최대")]
    public Vector2 idleInterval = new Vector2(5f, 12f);
    [Tooltip("dash 가 spot 을 끊을 때 페이드 시간(초)")]
    public float dashCutFade = 0.15f;

    [Header("발소리 보폭 (m 마다 한 번)")]
    public float walkStride = 0.9f;
    public float chaseStride = 1.5f;
    public float chargeStride = 2.2f;

    MonsterHoundAI ai;
    MonsterStun stun;
    MonsterHoundAI.HoundState lastState;
    Vector3 lastPos;
    float stepAccum;
    float nextIdle;

    bool Stunned => stun != null && stun.IsStunned;
    bool Silenced => Stunned || PlayerCaughtSequence.IsRunning;

    void Awake()
    {
        ai = GetComponent<MonsterHoundAI>();
        stun = GetComponent<MonsterStun>();
    }

    void OnEnable()
    {
        if (stun == null) stun = GetComponent<MonsterStun>();   // MonsterHoundAI.Awake 가 늦게 붙였을 수 있음
        if (stun != null) stun.Stunned += HandleStunned;
        lastPos = transform.position;
        stepAccum = 0f;
        if (ai != null) lastState = ai.CurrentState;
        ScheduleIdle();
    }

    void OnDisable()
    {
        if (stun != null) stun.Stunned -= HandleStunned;
        AudioManager.StopExclusive(transform, 0.1f);
    }

    void Update()
    {
        if (ai == null) return;

        var state = ai.CurrentState;
        if (state != lastState)
        {
            OnStateChanged(lastState, state);
            lastState = state;
        }

        UpdateFootsteps(state);

        if (state == MonsterHoundAI.HoundState.Patrol && Time.time >= nextIdle)
        {
            if (Silenced || AudioManager.IsExclusivePlaying(transform)) nextIdle = Time.time + 0.5f;   // 다른 행동 소리가 끝난 뒤 다시 시도
            else { AudioManager.PlayExclusive(idleSound, transform); ScheduleIdle(); }
        }
    }

    void OnStateChanged(MonsterHoundAI.HoundState from, MonsterHoundAI.HoundState to)
    {
        stepAccum = 0f;
        if (Silenced) return;

        switch (to)
        {
            case MonsterHoundAI.HoundState.ChargePrep:
                AudioManager.PlayExclusive(spotSound, transform);
                break;
            case MonsterHoundAI.HoundState.Charge:
                AudioManager.PlayExclusive(dashSound, transform, dashCutFade);
                break;
            case MonsterHoundAI.HoundState.Patrol:
                ScheduleIdle();
                break;
        }
    }

    void UpdateFootsteps(MonsterHoundAI.HoundState state)
    {
        Vector3 pos = transform.position;
        Vector3 d = pos - lastPos;
        lastPos = pos;
        d.y = 0f;
        float moved = d.magnitude;
        if (moved > 5f) { stepAccum = 0f; return; }   // Relocate 등 순간이동은 무시

        float stride;
        string sound;
        switch (state)
        {
            case MonsterHoundAI.HoundState.Patrol: stride = walkStride; sound = stepWalkSound; break;
            case MonsterHoundAI.HoundState.AcceleratedChase: stride = chaseStride; sound = stepDashSound; break;
            case MonsterHoundAI.HoundState.Charge: stride = chargeStride; sound = stepDashSound; break;
            default: stepAccum = 0f; return;   // ChargePrep / Attack / Recovery: 제자리
        }
        if (Stunned) { stepAccum = 0f; return; }

        stepAccum += moved;
        if (stepAccum >= stride)
        {
            stepAccum %= stride;
            AudioManager.Play(sound, transform);
        }
    }

    void HandleStunned(MonsterStun _) => AudioManager.StopExclusive(transform, 0.1f);

    void ScheduleIdle() =>
        nextIdle = Time.time + Random.Range(Mathf.Min(idleInterval.x, idleInterval.y), Mathf.Max(idleInterval.x, idleInterval.y));
}

/// <summary>
/// HoundVoice 를 게임 시작·씬 로드 때 MonsterHoundAI 에 자동으로 붙인다 (PlayerAudioAutoAttach 와 같은 방식).
/// 씬/프리팹 파일을 고치지 않아서 씬 병합 충돌이 없다. 이미 붙어 있으면 그대로 둔다.
/// </summary>
static class HoundAudioAutoAttach
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachAll();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachAll();

    static void AttachAll()
    {
        foreach (var h in Object.FindObjectsByType<MonsterHoundAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (h.GetComponent<HoundVoice>() == null) h.gameObject.AddComponent<HoundVoice>();
    }
}
