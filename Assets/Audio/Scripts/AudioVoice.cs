using UnityEngine;

namespace DeadSignal.Audio
{
    /// <summary>AudioSource 하나를 감싸는 재생 슬롯. AudioManager만 사용한다.</summary>
    internal sealed class AudioVoice
    {
        public readonly int index;
        public readonly AudioSource source;

        public SoundEntry entry;
        public int generation;
        public bool active;
        public bool stopping;
        public float startTime;

        Transform follow;
        bool hasFollow;
        Transform owner;
        bool hasOwner;

        float baseVolume;
        float volumeScale = 1f;
        float fade = 1f;
        float fadeDuration;

        public AudioVoice(int index, AudioSource source)
        {
            this.index = index;
            this.source = source;
        }

        public Transform Owner => hasOwner ? owner : null;
        public bool IsOwnedBy(Transform t) => hasOwner && ReferenceEquals(owner, t);
        public bool IsLoop => entry != null && entry.loop;

        public void Begin(SoundEntry e, AudioClip clip, Vector3 position, Transform followTarget, Transform ownerTarget,
                          bool force2D, AudioMixerGroupResolver groupFor)
        {
            entry = e;
            generation++;
            active = true;
            stopping = false;
            startTime = Time.unscaledTime;
            follow = followTarget;
            hasFollow = followTarget != null;
            owner = ownerTarget;
            hasOwner = ownerTarget != null;
            baseVolume = e.RollVolume();
            volumeScale = 1f;
            fade = 1f;

            var s = source;
            s.clip = clip;
            s.pitch = e.RollPitch();
            s.outputAudioMixerGroup = groupFor(e);
            s.spatialBlend = force2D || !e.spatial ? 0f : 1f;
            s.minDistance = e.minDistance;
            s.maxDistance = Mathf.Max(e.minDistance + 0.01f, e.maxDistance);
            s.rolloffMode = e.rolloff;
            s.loop = e.loop;
            s.ignoreListenerPause = e.ignorePause;
            s.dopplerLevel = 0f;
            s.transform.position = hasFollow ? followTarget.position : position;
            ApplyVolume();
            s.Play();
        }

        public void BeginStop(float fadeOut)
        {
            if (!active) return;
            stopping = true;
            hasOwner = false; // 페이드 중인 소리는 더 이상 주체의 독점 슬롯이 아니다
            owner = null;
            fadeDuration = Mathf.Max(0f, fadeOut);
            if (fadeDuration <= 0f) fade = 0f;
        }

        public void SetVolumeScale(float v)
        {
            volumeScale = v;
            ApplyVolume();
        }

        /// <summary>한 프레임 진행. false를 반환하면 재생이 끝난 것이다.</summary>
        public bool Tick(float dt)
        {
            if (hasFollow)
            {
                if (follow == null)
                {
                    hasFollow = false;
                    if (entry.loop && !stopping) BeginStop(0.05f); // 대상이 사라지면 루프는 정지, 원샷은 끝까지
                }
                else source.transform.position = follow.position;
            }
            if (hasOwner && owner == null) { hasOwner = false; owner = null; }

            if (stopping)
            {
                fade = fadeDuration <= 0f ? 0f : fade - dt / fadeDuration;
                if (fade <= 0f) return false;
            }
            ApplyVolume();

            bool paused = AudioListener.pause && !entry.ignorePause;
            if (!paused && !source.isPlaying) return false;
            return true;
        }

        public void Release()
        {
            active = false;
            stopping = false;
            entry = null;
            follow = null;
            hasFollow = false;
            owner = null;
            hasOwner = false;
            source.Stop();
            source.clip = null;
        }

        void ApplyVolume() => source.volume = baseVolume * volumeScale * Mathf.Clamp01(fade);

        public SoundHandle Handle => new SoundHandle(index, generation);

        public VoiceInfo Info => new VoiceInfo(Handle, entry != null ? entry.name : "", source, source.transform.position, Owner);
    }

    internal delegate UnityEngine.Audio.AudioMixerGroup AudioMixerGroupResolver(SoundEntry entry);
}
