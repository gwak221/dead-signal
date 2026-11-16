using UnityEngine;

/// <summary>
/// 랜덤 간격으로 짧은 소리를 내는 공용 컴포넌트 (적 Mm_random, 멀리서 나는 환경 소리).
/// 두 가지 모드: Idle(평소) / Active(추격 등). SetActive(true/false)로 전환.
/// randomOffset을 켜면 매번 주변 랜덤 위치에서 소리가 난다 (환경음용).
/// </summary>
public class RandomVocalizer : MonoBehaviour
{
    [Header("Idle (평소)")]
    public SoundCue idleCue;
    [Tooltip("다음 소리까지 간격(초) 최소~최대")]
    public Vector2 idleInterval = new Vector2(4f, 10f);

    [Header("Active (추격 등)")]
    public SoundCue activeCue;
    public Vector2 activeInterval = new Vector2(1.5f, 4f);

    [Header("Position")]
    [Tooltip("0보다 크면 이 반경(m) 안의 랜덤 위치에서 재생 (환경음용)")]
    public float randomOffset = 0f;

    [Header("Source (비우면 자기 위치에서 재생)")]
    public SoundEmitter emitter;

    bool active;
    float nextTime;

    public bool Muted { get; set; }

    void Awake()
    {
        if (emitter == null) emitter = GetComponent<SoundEmitter>();
        if (emitter == null && randomOffset <= 0f) emitter = gameObject.AddComponent<SoundEmitter>();
    }

    void OnEnable() => Schedule();

    public void SetActive(bool on)
    {
        if (active == on) return;
        active = on;
        Schedule(); // 모드가 바뀌면 새 간격으로 바로 다시 잡는다
    }

    void Schedule()
    {
        Vector2 r = active ? activeInterval : idleInterval;
        nextTime = Time.time + Random.Range(Mathf.Min(r.x, r.y), Mathf.Max(r.x, r.y));
    }

    void Update()
    {
        if (Time.time < nextTime) return;
        Schedule();
        if (Muted) return;

        var cue = active && activeCue != null ? activeCue : idleCue;
        if (cue == null) return;

        if (randomOffset > 0f)
        {
            Vector2 c = Random.insideUnitCircle * randomOffset;
            SoundEmitter.PlayAt(cue, transform.position + new Vector3(c.x, 0f, c.y));
        }
        else
        {
            emitter.Play(cue);
        }
    }
}
