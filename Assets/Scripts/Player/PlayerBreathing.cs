using DeadSignal.Audio;
using UnityEngine;

/// <summary>
/// 숨소리 (게임 코드). 숨 한 번 = 원샷 한 번을 심박에 맞춰 반복한다. (docs/spec/audio-list.md "숨소리 규칙")
///
///   구간: tenseEnterBpm 이상 → 거친 숨, calmReturnBpm 이하 → 일반 숨 (경계에서 왔다 갔다 방지)
///         구간이 바뀌어도 지금 나는 숨은 끊지 않고 다음 숨부터 바뀐다.
///   빠르기: 구간마다 간격 범위(최대~최소)를 따로 둔다. 구간의 BPM 범위 안에서 심박이 높을수록 최소 쪽으로.
///           일반 숨과 거친 숨의 간격이 겹치지 않아, 구간이 바뀌면 빠르기도 확 달라진다.
///           최대 심박(160, 달리기 불가)에서는 거친 숨이 가장 짧은 간격으로 몰아쉰다.
///   간격에 ±jitter 무작위 폭. 직전 숨이 아직 나는 중이면 끝난 뒤 minGap 만큼 쉬고 낸다(겹침 방지).
///   잡힘 연출 중에는 소리 없음.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBreathing : MonoBehaviour
{
    public string calmSound = "plr_breath_idle";
    public string tenseSound = "plr_breath_tense";

    [Header("구간 (BPM)")]
    [Tooltip("이 BPM 이상이 되면 거친 숨")] public float tenseEnterBpm = 120f;
    [Tooltip("거친 숨에서 이 BPM 이하로 내려가면 일반 숨")] public float calmReturnBpm = 110f;

    [Header("일반 숨 간격 (초) — 최저 BPM에서 최대, tenseEnterBpm에서 최소")]
    public float calmMaxInterval = 4.0f;
    public float calmMinInterval = 2.6f;

    [Header("거친 숨 간격 (초) — calmReturnBpm에서 최대, 최고 BPM에서 최소")]
    public float tenseMaxInterval = 1.6f;
    [Tooltip("거친 숨 클립(약 0.8초)보다 짧으면 숨끼리 겹친다")] public float tenseMinInterval = 0.9f;

    [Tooltip("간격 무작위 폭 (0.1 = ±10%)")] [Range(0f, 0.5f)] public float jitter = 0.1f;
    [Tooltip("직전 숨이 끝난 뒤 다음 숨까지 최소로 쉬는 시간(초)")] public float minGap = 0.1f;
    [Tooltip("심박 정보가 없을 때 쓰는 BPM")] public float fallbackBpm = 70f;
    [Tooltip("심박 정보가 없을 때 쓰는 최저/최고 BPM")] public float fallbackMinBpm = 60f, fallbackMaxBpm = 160f;

    bool tense;
    float nextBreath;
    SoundHandle lastBreath = SoundHandle.None;

    public bool IsTense => tense;

    void OnEnable() => nextBreath = Time.time + Random.Range(0.5f, 1.5f);

    void Update()
    {
        var heart = HeartRateController.Instance;
        float bpm = heart != null ? heart.CurrentBpm : fallbackBpm;
        float minBpm = heart != null ? heart.MinBpm : fallbackMinBpm;
        float maxBpm = heart != null ? heart.MaxBpm : fallbackMaxBpm;

        if (!tense && bpm >= tenseEnterBpm) tense = true;
        else if (tense && bpm <= calmReturnBpm) tense = false;

        if (Time.time < nextBreath) return;
        // 직전 숨이 아직 나는 중이면 기다린다 (지터·피치 변화로 클립이 간격보다 길어질 수 있음)
        if (lastBreath.IsPlaying) { nextBreath = Time.time + minGap; return; }

        float interval = tense
            ? Mathf.Lerp(tenseMaxInterval, tenseMinInterval, Mathf.InverseLerp(calmReturnBpm, maxBpm, bpm))
            : Mathf.Lerp(calmMaxInterval, calmMinInterval, Mathf.InverseLerp(minBpm, tenseEnterBpm, bpm));
        nextBreath = Time.time + interval * Random.Range(1f - jitter, 1f + jitter);

        if (PlayerCaughtSequence.IsRunning) return;
        lastBreath = AudioManager.Play(tense ? tenseSound : calmSound);
    }
}
