using UnityEngine;

/// <summary>
/// 적 사운드 연결 컴포넌트. 적 오브젝트 아래 "Audio" 자식에 붙는다.
///   발소리        : FootstepEmitter (추격 중이면 뛰기)
///   Mm_random     : RandomVocalizer (평소 = idle, 추격 = chase)
///   플레이어 감지 : MonsterChase.OnChaseStarted
///   기절          : MonsterStun.Stunned
///   공격          : MonsterChase.OnAttack
///   소리 감지     : PlayHear() — 소리 감지 기능이 생기면 그쪽에서 호출
/// 적 2도 같은 컴포넌트를 쓰고, 돌진 소리만 PlayCue(...)로 직접 낸다.
/// </summary>
public class MonsterAudio : MonoBehaviour
{
    public SoundCue spotCue;
    public SoundCue hearCue;
    public SoundCue stunCue;
    public SoundCue attackCue;

    [Tooltip("추격 시작 후 이 시간(초) 동안은 같은 감지 소리를 다시 내지 않는다")]
    public float spotCooldown = 6f;

    MonsterChase chase;
    MonsterStun stun;
    SoundEmitter emitter;
    FootstepEmitter footsteps;
    RandomVocalizer vocalizer;
    float lastSpot = -100f;

    void Awake()
    {
        chase = GetComponentInParent<MonsterChase>();
        stun = GetComponentInParent<MonsterStun>();
        emitter = GetComponent<SoundEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<SoundEmitter>();
        footsteps = GetComponent<FootstepEmitter>();
        vocalizer = GetComponent<RandomVocalizer>();
    }

    void OnEnable()
    {
        if (chase != null) { chase.OnChaseStarted += HandleChaseStarted; chase.OnAttack += HandleAttack; }
        if (stun != null) stun.Stunned += HandleStunned;
    }

    void OnDisable()
    {
        if (chase != null) { chase.OnChaseStarted -= HandleChaseStarted; chase.OnAttack -= HandleAttack; }
        if (stun != null) stun.Stunned -= HandleStunned;
    }

    void Update()
    {
        bool chasing = chase != null && chase.IsChasing;
        bool stunned = stun != null && stun.IsStunned;
        bool caught = PlayerCaughtSequence.IsRunning;

        if (footsteps != null)
        {
            footsteps.RunningOverride = chasing;
            footsteps.Muted = stunned;
        }
        if (vocalizer != null)
        {
            vocalizer.SetActive(chasing);
            vocalizer.Muted = stunned || caught;
        }
    }

    public void PlayHear() => emitter.Play(hearCue);

    public void PlayCue(SoundCue cue) => emitter.Play(cue);

    void HandleChaseStarted(MonsterChase _)
    {
        if (Time.time - lastSpot < spotCooldown) return;
        lastSpot = Time.time;
        emitter.Play(spotCue);
    }

    void HandleStunned(MonsterStun _) => emitter.Play(stunCue);

    void HandleAttack(MonsterChase _) => emitter.Play(attackCue);
}
