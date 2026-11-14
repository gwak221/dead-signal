using UnityEngine;

/// <summary>
/// 가구 하나(프리팹 루트)에 붙는 공용 사운드. 하위 OpenablePart들이 열기/닫기 시작할 때 재생한다.
/// 파츠에 개별 클립이 없으면 여기 기본 클립을 쓴다. 양문·날개처럼 같은 순간 여러 파츠가 움직이면 한 번만 낸다.
/// 클립이 비어 있으면 아무 소리도 내지 않는다(클립은 나중에 할당).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OpenableSound : MonoBehaviour
{
    public AudioClip defaultOpenClip;
    public AudioClip defaultCloseClip;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("이 시간(초) 안에 다시 요청되면 무시 (동시 재생 방지)")]
    public float minInterval = 0.08f;
    [Tooltip("재생마다 피치를 이 범위에서 흔든다")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    AudioSource source;
    float lastPlay = -10f;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
    }

    public void Play(AudioClip clip, bool opening)
    {
        if (clip == null) clip = opening ? defaultOpenClip : defaultCloseClip;
        if (clip == null || source == null) return;
        if (Time.time - lastPlay < minInterval) return;
        lastPlay = Time.time;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume);
    }
}
