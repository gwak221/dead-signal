using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SoundCue를 한 번 재생하는 공용 컴포넌트.
/// 다른 스크립트에서: GetComponent&lt;SoundEmitter&gt;().Play(cue);
/// 인스펙터 UnityEvent에서: Play() (defaultCue 재생)
/// 오브젝트 없이 재생: SoundEmitter.PlayAt(cue, position) / SoundEmitter.Play2D(cue)
/// </summary>
[DisallowMultipleComponent]
public class SoundEmitter : MonoBehaviour
{
    [Tooltip("Play()로 재생할 기본 사운드")]
    public SoundCue defaultCue;
    [Range(0f, 2f)] public float volumeScale = 1f;

    AudioSource source;
    readonly Dictionary<SoundCue, float> lastPlayed = new Dictionary<SoundCue, float>();

    public AudioSource Source
    {
        get
        {
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
            }
            return source;
        }
    }

    public void Play() => Play(defaultCue);

    public void Play(SoundCue cue) => Play(cue, 1f);

    public void Play(SoundCue cue, float volumeMultiplier)
    {
        if (cue == null || !isActiveAndEnabled) return;
        // 메뉴(timeScale 0)에서도 동작하도록 unscaled 시간 사용
        if (lastPlayed.TryGetValue(cue, out float t) && Time.unscaledTime - t < cue.minInterval) return;
        var clip = cue.PickClip();
        if (clip == null) return;
        lastPlayed[cue] = Time.unscaledTime;

        var src = Source;
        cue.ApplyTo(src);
        src.pitch = cue.RandomPitch();
        src.PlayOneShot(clip, cue.volume * volumeScale * volumeMultiplier);
    }

    // ───────────── 오브젝트 없이 재생 ─────────────

    /// <summary>지정 위치에서 한 번 재생 (임시 오브젝트, 끝나면 삭제).</summary>
    public static AudioSource PlayAt(SoundCue cue, Vector3 position, float volumeMultiplier = 1f)
    {
        if (cue == null) return null;
        var clip = cue.PickClip();
        if (clip == null) return null;

        var go = new GameObject("OneShot_" + cue.name);
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        cue.ApplyTo(src);
        src.pitch = cue.RandomPitch();
        src.clip = clip;
        src.volume = cue.volume * volumeMultiplier;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.05f, Mathf.Abs(src.pitch)) + 0.1f);
        return src;
    }

    /// <summary>2D로 한 번 재생 (UI, 스팅어 등). cue가 3D여도 2D로 낸다.</summary>
    public static AudioSource Play2D(SoundCue cue, float volumeMultiplier = 1f)
    {
        var listener = Object.FindFirstObjectByType<AudioListener>();
        var src = PlayAt(cue, listener != null ? listener.transform.position : Vector3.zero, volumeMultiplier);
        if (src != null) src.spatialBlend = 0f;
        return src;
    }
}
