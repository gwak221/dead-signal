using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DeadSignal.Audio
{
    /// <summary>
    /// 코어 오디오 매니저. 모든 소리는 Assets/Audio/Resources/AudioManager.prefab 의 Sounds 목록에 등록하고,
    /// 게임 코드는 이름으로만 부른다.
    ///
    ///   AudioManager.Play("ui_click");                       // 2D
    ///   AudioManager.Play("door_open", door.position);        // 고정 위치 3D
    ///   AudioManager.Play("en1_step_walk", transform);        // 대상을 따라가는 3D
    ///   AudioManager.PlayExclusive("en1_spot", transform);    // 주체별 단일 재생 (이전 소리를 끊고 재생)
    ///
    /// 게임 시작 시 Resources에서 자동 생성되므로 씬에 배치하지 않는다.
    /// 없는 이름·빈 클립·매니저 부재는 경고만 남기고 무시한다 (예외 없음).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class AudioManager : MonoBehaviour
    {
        public const string ResourcePath = "AudioManager";

        [SerializeField] AudioMixer mixer;
        [SerializeField] AudioMixerGroup defaultGroup;
        [SerializeField, Range(8, 64)] int voiceCount = 32;
        [SerializeField] List<SoundEntry> sounds = new List<SoundEntry>();

        static AudioManager instance;
        static bool loadFailed;
        static bool quitting;
        static readonly HashSet<string> warned = new HashSet<string>();
        static readonly List<VoiceInfo> activeInfos = new List<VoiceInfo>();
        static int activeInfosFrame = -1;

        readonly Dictionary<string, SoundEntry> lookup = new Dictionary<string, SoundEntry>(StringComparer.Ordinal);
        AudioVoice[] voices = Array.Empty<AudioVoice>();
        AudioMixerGroupResolver groupResolver;

        /// <summary>소리가 시작될 때. 확장 모듈 연결점.</summary>
        public static event Action<VoiceInfo> VoiceStarted;
        /// <summary>소리가 끝나거나 멈췄을 때. 확장 모듈 연결점.</summary>
        public static event Action<VoiceInfo> VoiceStopped;

        // ───────────────────────── 생성 / 수명 ─────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            loadFailed = false;
            quitting = false;
            warned.Clear();
            activeInfos.Clear();
            activeInfosFrame = -1;
            VoiceStarted = null;
            VoiceStopped = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() => EnsureInstance();

        static bool EnsureInstance()
        {
            if (instance != null) return true;
            if (loadFailed || quitting) return false;
            if (!Application.isPlaying) return false; // 에디터 편집 중 호출(에디터 스크립트 등)로 씬에 매니저가 생기지 않게

            var prefab = Resources.Load<AudioManager>(ResourcePath);
            if (prefab == null)
            {
                loadFailed = true;
                Debug.LogError($"[Audio] Resources/{ResourcePath}.prefab 을 찾지 못했습니다. 모든 소리 호출이 무시됩니다.");
                return false;
            }
            var go = Instantiate(prefab);
            go.name = "AudioManager";
            return instance != null;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject); // 먼저 생긴 매니저가 유지된다
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureReady();
        }

        // 플레이 중 스크립트가 다시 컴파일되면(도메인 리로드) 정적 변수와 직렬화되지 않는 필드가 비워진다.
        // 이때 Awake는 다시 불리지 않고 OnEnable만 불리므로, 여기서 자신을 다시 등록하고 준비한다.
        void OnEnable()
        {
            if (instance == null) instance = this;
            if (instance == this) EnsureReady();
        }

        void EnsureReady()
        {
            if (groupResolver == null) groupResolver = e => e.group != null ? e.group : defaultGroup;
            if (lookup.Count == 0) BuildLookup();
            if (voices == null || voices.Length == 0) CreateVoices();
        }

        void OnApplicationQuit() => quitting = true;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void BuildLookup()
        {
            lookup.Clear();
            foreach (var e in sounds)
            {
                if (e == null) continue;
                string key = e.name != null ? e.name.Trim() : "";
                if (key.Length == 0)
                {
                    Debug.LogWarning("[Audio] 이름이 빈 항목이 있습니다. 무시합니다.", this);
                    continue;
                }
                if (lookup.ContainsKey(key))
                {
                    Debug.LogWarning($"[Audio] 중복 이름 \"{key}\" — 첫 번째 항목을 사용합니다.", this);
                    continue;
                }
                lookup.Add(key, e);
            }
        }

        void CreateVoices()
        {
            // 도메인 리로드 후에는 기존 Voice_xx 오브젝트가 남아 있으므로 재사용한다
            var existing = GetComponentsInChildren<AudioSource>(true);
            voices = new AudioVoice[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                AudioSource src;
                if (i < existing.Length) src = existing[i];
                else
                {
                    var go = new GameObject($"Voice_{i:00}");
                    go.transform.SetParent(transform, false);
                    src = go.AddComponent<AudioSource>();
                }
                src.playOnAwake = false;
                src.Stop();
                voices[i] = new AudioVoice(i, src);
            }
        }

        // ───────────────────────── 재생 ─────────────────────────

        /// <summary>2D 재생 (UI, BGM). 항목이 3D로 설정돼 있어도 2D로 낸다.</summary>
        public static SoundHandle Play(string name)
            => PlayInternal(name, Vector3.zero, null, null, force2D: true);

        /// <summary>고정 위치 3D 재생.</summary>
        public static SoundHandle Play(string name, Vector3 position)
            => PlayInternal(name, position, null, null, force2D: false);

        /// <summary>대상을 따라가는 3D 재생.</summary>
        public static SoundHandle Play(string name, Transform target)
        {
            if (target == null) { Warn("nulltarget:" + name, $"[Audio] Play(\"{name}\", target) — target이 null입니다."); return SoundHandle.None; }
            return PlayInternal(name, target.position, target, null, force2D: false);
        }

        /// <summary>
        /// 주체별 단일 재생. target의 이전 독점 소리를 끊고 새 소리를 재생한다.
        /// 한 적의 행동 소리는 항상 같은 Transform(적 루트)으로 호출할 것.
        /// </summary>
        public static SoundHandle PlayExclusive(string name, Transform target, float fadeOutPrevious = 0.05f)
        {
            if (target == null) { Warn("nulltarget:" + name, $"[Audio] PlayExclusive(\"{name}\", target) — target이 null입니다."); return SoundHandle.None; }
            if (!instance && !EnsureInstance()) return SoundHandle.None;
            if (!instance.TryResolve(name, out var entry, out var clip)) return SoundHandle.None;
            instance.StopOwned(target, fadeOutPrevious);
            return instance.StartVoice(entry, clip, target.position, target, target, false);
        }

        static SoundHandle PlayInternal(string name, Vector3 position, Transform follow, Transform owner, bool force2D)
        {
            if (!instance && !EnsureInstance()) return SoundHandle.None;
            if (!instance.TryResolve(name, out var entry, out var clip)) return SoundHandle.None;
            return instance.StartVoice(entry, clip, position, follow, owner, force2D);
        }

        bool TryResolve(string name, out SoundEntry entry, out AudioClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(name) || !lookup.TryGetValue(name, out entry))
            {
                entry = null;
                Warn("missing:" + name, $"[Audio] 등록되지 않은 소리: \"{name}\" — AudioManager 프리팹의 Sounds 목록을 확인하세요.");
                return false;
            }
            clip = entry.PickClip();
            return clip != null; // 클립 미할당은 조용히 무시 (인스펙터 경고로 확인)
        }

        SoundHandle StartVoice(SoundEntry entry, AudioClip clip, Vector3 position, Transform follow, Transform owner, bool force2D)
        {
            var v = Acquire(entry);
            if (v == null) return SoundHandle.None;
            v.Begin(entry, clip, position, follow, owner, force2D, groupResolver);
            VoiceStarted?.Invoke(v.Info);
            return v.Handle;
        }

        AudioVoice Acquire(SoundEntry forEntry)
        {
            AudioVoice fading = null, oldestFree = null, oldestAny = null;
            foreach (var v in voices)
            {
                if (!v.active) return v;
                if (v.IsLoop) continue;
                if (v.stopping) { if (fading == null || v.startTime < fading.startTime) fading = v; continue; }
                if (v.Owner == null) { if (oldestFree == null || v.startTime < oldestFree.startTime) oldestFree = v; }
                if (oldestAny == null || v.startTime < oldestAny.startTime) oldestAny = v;
            }
            var steal = fading ?? oldestFree ?? oldestAny;
            if (steal == null)
            {
                Warn("full", $"[Audio] 재생 슬롯({voices.Length})이 모두 루프로 차서 \"{forEntry.name}\"을(를) 재생하지 못했습니다.");
                return null;
            }
            Finish(steal);
            return steal;
        }

        // ───────────────────────── 정지 ─────────────────────────

        public static void Stop(SoundHandle handle, float fadeOut = 0f)
        {
            var v = Resolve(handle);
            if (v == null || v.stopping) return;
            v.BeginStop(fadeOut);
        }

        public static void StopExclusive(Transform target, float fadeOut = 0f)
        {
            if (instance == null || target == null) return;
            instance.StopOwned(target, fadeOut);
        }

        public static void StopAll(float fadeOut = 0f)
        {
            if (instance == null) return;
            foreach (var v in instance.voices) if (v.active && !v.stopping) v.BeginStop(fadeOut);
        }

        void StopOwned(Transform target, float fadeOut)
        {
            foreach (var v in voices) if (v.active && v.IsOwnedBy(target)) v.BeginStop(fadeOut);
        }

        // ───────────────────────── 조회 ─────────────────────────

        public static bool Has(string name)
            => !string.IsNullOrEmpty(name) && (instance || EnsureInstance()) && instance.lookup.ContainsKey(name);

        public static bool IsExclusivePlaying(Transform target)
        {
            if (instance == null || target == null) return false;
            foreach (var v in instance.voices) if (v.active && !v.stopping && v.IsOwnedBy(target)) return true;
            return false;
        }

        /// <summary>지금 재생 중인 소리 목록 (멈추는 중 포함). 확장 모듈 연결점.</summary>
        public static IReadOnlyList<VoiceInfo> ActiveVoices
        {
            get
            {
                if (activeInfosFrame == Time.frameCount) return activeInfos;
                activeInfosFrame = Time.frameCount;
                activeInfos.Clear();
                if (instance != null)
                    foreach (var v in instance.voices) if (v.active) activeInfos.Add(v.Info);
                return activeInfos;
            }
        }

        internal static bool IsHandlePlaying(SoundHandle h)
        {
            var v = Resolve(h);
            return v != null && !v.stopping;
        }

        internal static void SetHandleVolume(SoundHandle h, float volume01) => Resolve(h)?.SetVolumeScale(volume01);

        static AudioVoice Resolve(SoundHandle h)
        {
            if (instance == null || !h.IsValid || h.index >= instance.voices.Length) return null;
            var v = instance.voices[h.index];
            return v.active && v.generation == h.generation ? v : null;
        }

        // ───────────────────────── 갱신 ─────────────────────────

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var v in voices)
                if (v.active && !v.Tick(dt)) Finish(v);
        }

        void Finish(AudioVoice v)
        {
            var info = v.Info;
            v.Release();
            VoiceStopped?.Invoke(info);
        }

        static void Warn(string key, string message)
        {
            if (warned.Add(key)) Debug.LogWarning(message);
        }
    }
}
