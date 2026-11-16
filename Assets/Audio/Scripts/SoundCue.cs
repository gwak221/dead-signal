using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사운드 하나 = 에셋 하나. 클립 묶음과 재생 설정을 담는다.
/// 코드와 프리팹은 이 에셋만 참조하므로, 클립/볼륨을 바꿀 때 씬이나 프리팹을 건드리지 않는다.
/// 클립이 비어 있으면 아무 소리도 내지 않는다(에러 없음).
/// 만들기: Project 창 우클릭 > Create > Dead Signal > Sound Cue
/// </summary>
[CreateAssetMenu(menuName = "Dead Signal/Sound Cue", fileName = "NewSoundCue")]
public class SoundCue : ScriptableObject
{
    [Tooltip("재생할 클립. 여러 개면 매번 랜덤으로 고른다.")]
    public AudioClip[] clips;

    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("재생마다 이 범위에서 피치를 랜덤으로 고른다. (1,1) = 고정")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("같은 클립이 연속으로 나오지 않게 한다 (클립 2개 이상일 때)")]
    public bool avoidRepeat = true;

    [Header("Space")]
    [Tooltip("켜면 3D(거리에 따라 작아짐), 끄면 2D(항상 같은 크기)")]
    public bool spatial3D = true;
    [Tooltip("이 거리(m)까지는 최대 볼륨")]
    public float minDistance = 1f;
    [Tooltip("이 거리(m)부터는 들리지 않음 (Linear 기준)")]
    public float maxDistance = 20f;
    public AudioRolloffMode rolloff = AudioRolloffMode.Linear;

    [Header("Routing")]
    [Tooltip("AudioMixer 그룹 (BGM / Ambience / Player / Enemy / World / UI)")]
    public AudioMixerGroup mixerGroup;
    [Tooltip("같은 이미터에서 이 시간(초) 안에 다시 요청되면 무시 (중복 재생 방지)")]
    public float minInterval = 0.05f;
    [Tooltip("메뉴/일시정지(AudioListener.pause) 중에도 재생 (메뉴 UI, 메뉴 음악용)")]
    public bool ignoreListenerPause = false;

    [TextArea(2, 4)]
    [Tooltip("어떤 소리인지, 검색 키워드 등 메모 (게임에는 영향 없음)")]
    public string note;

    [System.NonSerialized] int lastIndex = -1;

    public bool HasClips
    {
        get
        {
            if (clips == null) return false;
            foreach (var c in clips) if (c != null) return true;
            return false;
        }
    }

    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0) return null;
        int valid = 0;
        foreach (var c in clips) if (c != null) valid++;
        if (valid == 0) return null;

        for (int tries = 0; tries < 8; tries++)
        {
            int i = Random.Range(0, clips.Length);
            if (clips[i] == null) continue;
            if (avoidRepeat && valid > 1 && i == lastIndex) continue;
            lastIndex = i;
            return clips[i];
        }
        foreach (var c in clips) if (c != null) return c;
        return null;
    }

    public float RandomPitch() => Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));

    /// <summary>공간/라우팅 설정을 AudioSource에 적용한다 (볼륨/피치/클립은 제외).</summary>
    public void ApplyTo(AudioSource src)
    {
        if (src == null) return;
        src.spatialBlend = spatial3D ? 1f : 0f;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.rolloffMode = rolloff;
        src.dopplerLevel = 0f;
        src.ignoreListenerPause = ignoreListenerPause;
        if (mixerGroup != null) src.outputAudioMixerGroup = mixerGroup;
    }
}
