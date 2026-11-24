using DeadSignal.Audio;
using UnityEngine;

/// <summary>
/// 적의 소리 호출 담당 (게임 코드). 적 루트(Animator·MonsterChase가 있는 오브젝트)에 붙인다.
/// 오디오 모듈은 이 스크립트를 모른다 — 이 스크립트가 AudioManager를 이름으로 부를 뿐이다.
///
///   행동 소리 (PlayExclusive — 한 마리당 동시에 하나만)
///     spot  : 추격 시작 시. 직전 spot 후 spotCooldown 안이면 생략
///     hear  : PlayHear() — 소리 감지 기능에서 호출
///     idle  : 평상시 idleInterval 랜덤 간격
///     chase : 추격 중 chaseInterval 랜덤 간격
///     idle/chase는 다른 행동 소리(spot/hear)가 나는 중이면 끊지 않고 미룬다.
///   발소리 (Play — 행동 소리와 동시에 남)
///     애니메이션 이벤트 OnFootstep(string 소리이름) 으로만 재생.
///
/// 기절 중 / 플레이어 잡힘 연출 중에는 행동 소리를 내지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class MonsterVoice : MonoBehaviour
{
    [Header("소리 이름 (AudioManager)")]
    public string spotSound = "en1_spot";
    public string hearSound = "en1_hear";
    public string idleSound = "en1_idle";
    public string chaseSound = "en1_chase";

    [Header("타이밍")]
    [Tooltip("spot 소리를 다시 내기까지 최소 시간(초)")]
    public float spotCooldown = 6f;
    [Tooltip("평상시 울음 간격(초) 최소~최대")]
    public Vector2 idleInterval = new Vector2(4f, 10f);
    [Tooltip("추격 중 울음 간격(초) 최소~최대")]
    public Vector2 chaseInterval = new Vector2(1.5f, 4f);

    [Header("발소리")]
    [Tooltip("Walk↔Run 크로스페이드 중 이 가중치 미만인 클립의 발소리 이벤트는 무시")]
    [Range(0f, 1f)] public float footstepMinWeight = 0.5f;

    MonsterChase chase;
    MonsterStun stun;
    float lastSpot = float.NegativeInfinity;
    float nextVocal;
    bool wasChasing;

    bool Silenced => (stun != null && stun.IsStunned) || PlayerCaughtSequence.IsRunning;

    void Awake()
    {
        chase = GetComponent<MonsterChase>();
        stun = GetComponent<MonsterStun>();
    }

    void OnEnable()
    {
        if (chase != null) chase.OnChaseStarted += HandleChaseStarted;
        if (stun != null) stun.Stunned += HandleStunned;
        Schedule(false);
    }

    void OnDisable()
    {
        if (chase != null) chase.OnChaseStarted -= HandleChaseStarted;
        if (stun != null) stun.Stunned -= HandleStunned;
        AudioManager.StopExclusive(transform, 0.1f);
    }

    void Update()
    {
        bool chasing = chase != null && chase.IsChasing;
        if (chasing != wasChasing) { wasChasing = chasing; Schedule(chasing); }

        if (Time.time < nextVocal) return;
        if (Silenced || AudioManager.IsExclusivePlaying(transform))
        {
            nextVocal = Time.time + 0.5f; // 다른 행동 소리가 끝난 뒤 다시 시도
            return;
        }
        AudioManager.PlayExclusive(chasing ? chaseSound : idleSound, transform);
        Schedule(chasing);
    }

    /// <summary>소리 감지 기능에서 호출한다.</summary>
    public void PlayHear()
    {
        if (Silenced) return;
        AudioManager.PlayExclusive(hearSound, transform);
        Schedule(chase != null && chase.IsChasing);
    }

    void HandleChaseStarted(MonsterChase _)
    {
        if (Silenced || Time.time - lastSpot < spotCooldown) return;
        lastSpot = Time.time;
        AudioManager.PlayExclusive(spotSound, transform);
        Schedule(true);
    }

    void HandleStunned(MonsterStun _) => AudioManager.StopExclusive(transform, 0.1f);

    void Schedule(bool chasing)
    {
        Vector2 r = chasing ? chaseInterval : idleInterval;
        nextVocal = Time.time + Random.Range(Mathf.Min(r.x, r.y), Mathf.Max(r.x, r.y));
    }

    /// <summary>
    /// 애니메이션 이벤트. Walk/Run 클립의 발이 닿는 프레임에 넣고 String 파라미터에 소리 이름을 적는다.
    /// (예: Monster_Walk → "en1_step_walk", Monster_Run → "en1_step_run")
    /// </summary>
    public void OnFootstep(AnimationEvent e)
    {
        if (e.animatorClipInfo.weight < footstepMinWeight) return;
        if (stun != null && stun.IsStunned) return;
        if (!string.IsNullOrEmpty(e.stringParameter)) AudioManager.Play(e.stringParameter, transform);
    }
}
