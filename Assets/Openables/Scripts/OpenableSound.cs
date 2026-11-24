using DeadSignal.Audio;
using UnityEngine;

/// <summary>
/// 가구 하나(프리팹 루트)에 붙는 공용 사운드. 하위 OpenablePart들이 열기/닫기 시작할 때 재생한다.
/// 양문·날개처럼 같은 순간 여러 파츠가 움직이면 한 번만 낸다.
/// 기본: AudioManager에 등록된 이름(openSound / closeSound)으로 재생.
/// 파츠에 개별 클립이 지정돼 있으면 그 클립을 이 오브젝트의 AudioSource로 직접 재생한다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OpenableSound : MonoBehaviour
{
    [Header("AudioManager 소리 이름")]
    public string openSound = "box_open";
    public string closeSound = "box_close";

    [Header("파츠 개별 클립 재생 설정")]
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("재생마다 피치를 이 범위에서 흔든다 (파츠 개별 클립용)")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Tooltip("이 시간(초) 안에 다시 요청되면 무시 (동시 재생 방지)")]
    public float minInterval = 0.08f;

    AudioSource source;
    float lastPlay = -10f;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
    }

    public void Play(AudioClip clip, bool opening)
    {
        if (Time.time - lastPlay < minInterval) return;
        lastPlay = Time.time;

        if (clip != null && source != null)
        {
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(clip, volume);
            return;
        }
        AudioManager.Play(opening ? openSound : closeSound, transform.position);
    }
}
