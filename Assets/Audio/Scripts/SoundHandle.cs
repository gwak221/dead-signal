using System;
using UnityEngine;

namespace DeadSignal.Audio
{
    /// <summary>
    /// 재생 중인 소리 하나를 가리킨다. 루프를 멈추거나 볼륨을 바꿀 때 쓴다.
    /// voice가 다른 재생에 재사용되면 자동으로 무효가 되므로, 남의 소리를 끄는 일이 없다.
    /// </summary>
    public readonly struct SoundHandle : IEquatable<SoundHandle>
    {
        public static readonly SoundHandle None = new SoundHandle(-1, 0);

        internal readonly int index;
        internal readonly int generation;

        internal SoundHandle(int index, int generation)
        {
            this.index = index;
            this.generation = generation;
        }

        /// <summary>None이 아니면 true. (재생 중인지는 <see cref="IsPlaying"/>)</summary>
        public bool IsValid => index >= 0;

        /// <summary>이 재생이 아직 진행 중이면 true. 멈추는 중(페이드 아웃)이면 false.</summary>
        public bool IsPlaying => AudioManager.IsHandlePlaying(this);

        public void Stop(float fadeOut = 0f) => AudioManager.Stop(this, fadeOut);

        /// <summary>항목 볼륨에 곱해지는 배율 (0~1).</summary>
        public void SetVolume(float volume01) => AudioManager.SetHandleVolume(this, Mathf.Clamp01(volume01));

        public bool Equals(SoundHandle other) => index == other.index && generation == other.generation;
        public override bool Equals(object obj) => obj is SoundHandle h && Equals(h);
        public override int GetHashCode() => (index * 397) ^ generation;
        public static bool operator ==(SoundHandle a, SoundHandle b) => a.Equals(b);
        public static bool operator !=(SoundHandle a, SoundHandle b) => !a.Equals(b);
        public override string ToString() => IsValid ? $"SoundHandle({index}:{generation})" : "SoundHandle(None)";
    }

    /// <summary>확장 모듈(room 필터 등)에 노출하는 재생 중 소리 정보. 읽기 전용.</summary>
    public readonly struct VoiceInfo
    {
        public SoundHandle Handle { get; }
        public string Name { get; }
        /// <summary>확장 모듈이 필터 컴포넌트를 붙이거나 값을 조정할 수 있다. 코어는 재사용 시 이를 제거하지 않는다.</summary>
        public AudioSource Source { get; }
        public Vector3 Position { get; }
        /// <summary>PlayExclusive로 재생한 경우의 주체. 아니면 null.</summary>
        public Transform Owner { get; }

        internal VoiceInfo(SoundHandle handle, string name, AudioSource source, Vector3 position, Transform owner)
        {
            Handle = handle;
            Name = name;
            Source = source;
            Position = position;
            Owner = owner;
        }
    }
}
