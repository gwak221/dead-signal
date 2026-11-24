using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DeadSignal.Audio
{
    /// <summary>
    /// AudioManager 인스펙터 목록의 항목 하나. 게임 코드는 <see cref="name"/>으로만 이 소리를 부른다.
    /// </summary>
    [Serializable]
    public class SoundEntry
    {
        [Tooltip("호출 키. AudioManager.Play(\"이 이름\") 으로 부른다.")]
        public string name = "";

        [Tooltip("여러 개면 재생마다 무작위로 고른다.")]
        public AudioClip[] clips = new AudioClip[0];

        [Tooltip("클립이 2개 이상일 때 직전 클립이 연속으로 나오지 않게 한다.")]
        public bool avoidRepeat = true;

        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생마다 volume ± 이 값")]
        [Range(0f, 0.5f)] public float volumeJitter = 0.05f;

        [Range(0.1f, 3f)] public float pitch = 1f;
        [Tooltip("재생마다 pitch ± 이 값")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.05f;

        [Tooltip("MainMixer 그룹. 비우면 AudioManager의 Default Group")]
        public AudioMixerGroup group;

        [Tooltip("켜면 3D(거리 감쇠), 끄면 2D")]
        public bool spatial = true;
        [Min(0f)] public float minDistance = 1f;
        [Min(0f)] public float maxDistance = 20f;
        public AudioRolloffMode rolloff = AudioRolloffMode.Linear;

        [Tooltip("반복 재생. 받은 SoundHandle로 직접 멈춰야 한다.")]
        public bool loop;

        [Tooltip("일시정지(AudioListener.pause) 중에도 재생 — 메뉴/UI용")]
        public bool ignorePause;

        [TextArea(1, 3)]
        [Tooltip("메모. 게임에 영향 없음")]
        public string note = "";

        [NonSerialized] int lastClipIndex = -1;
        [NonSerialized] static readonly List<int> scratch = new List<int>(16);

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return true;
                return false;
            }
        }

        /// <summary>null 클립을 건너뛰고 무작위 클립을 고른다. avoidRepeat이면 직전 클립을 피한다.</summary>
        public AudioClip PickClip()
        {
            if (clips == null) return null;
            scratch.Clear();
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) scratch.Add(i);
            if (scratch.Count == 0) return null;

            int pick;
            if (scratch.Count == 1) pick = scratch[0];
            else
            {
                if (avoidRepeat) scratch.Remove(lastClipIndex);
                pick = scratch[UnityEngine.Random.Range(0, scratch.Count)];
            }
            lastClipIndex = pick;
            return clips[pick];
        }

        public float RollVolume() => Mathf.Clamp01(volume + UnityEngine.Random.Range(-volumeJitter, volumeJitter));
        public float RollPitch() => Mathf.Max(0.01f, pitch + UnityEngine.Random.Range(-pitchJitter, pitchJitter));
    }
}
