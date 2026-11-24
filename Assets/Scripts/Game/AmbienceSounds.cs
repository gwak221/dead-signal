using System.Collections.Generic;
using DeadSignal.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 환경음 호출 담당 (게임 코드). 게임 시작 때 스스로 생기므로 씬에 배치할 필요가 없다.
///
///   드론     : 게임 중(타이틀 아님) 항상 amb_drone 루프 (2D). 추격 음악이 켜져 있으면 chaseDuck 비율로 줄인다.
///   랜덤     : 플레이어(리스너) 주변 randomRadius 안의 랜덤 위치에서 randomInterval 간격으로 amb_random (3D).
///   형광등   : 이름이 "Fluo"로 시작하는 Light 위치에서 amb_fluorescent 루프 (3D).
///   비상등   : 이름이 "Emergency"로 시작하는 Light 위치에서 amb_emergency 루프 (3D).
///              조명은 spacing 간격으로 고른 뒤, 들리는 거리 안에 있는 것 중 가까운 lightMaxPlaying개만 재생한다.
///              (형광등이 190개라 전부 틀면 AudioManager 재생 슬롯이 루프로 가득 찬다)
///   숨기     : 플레이어가 숨어 있으면 리스너에 로우패스를 걸어 모든 소리를 먹먹하게 (기존 동작).
/// </summary>
[DisallowMultipleComponent]
public class AmbienceSounds : MonoBehaviour
{
    [Header("드론")]
    public string droneSound = "amb_drone";
    [Tooltip("추격 음악이 켜져 있을 때 드론 볼륨 비율")] [Range(0f, 1f)] public float chaseDuck = 0.35f;
    [Tooltip("드론 볼륨이 바뀌는 속도 (초당)")] public float duckSpeed = 0.8f;
    public float droneFadeIn = 2f;

    [Header("랜덤 환경음")]
    public string randomSound = "amb_random";
    [Tooltip("다음 소리까지 간격(초) 최소~최대")] public Vector2 randomInterval = new Vector2(12f, 30f);
    [Tooltip("플레이어 주변 이 반경(m) 안 랜덤 위치에서 난다")] public float randomRadius = 18f;

    [Header("조명 소리")]
    public string fluorescentSound = "amb_fluorescent";
    public string fluorescentPrefix = "Fluo";
    public float fluorescentSpacing = 9f;
    [Tooltip("형광등 소리가 들리는 거리(m) — AudioManager 항목의 Max Distance와 맞출 것")] public float fluorescentRange = 5f;
    public string emergencySound = "amb_emergency";
    public string emergencyPrefix = "Emergency";
    public float emergencySpacing = 6f;
    [Tooltip("비상등 소리가 들리는 거리(m) — AudioManager 항목의 Max Distance와 맞출 것")] public float emergencyRange = 6f;
    [Tooltip("조명 소리를 동시에 낼 최대 개수 (가까운 순)")] public int lightMaxPlaying = 6;
    [Tooltip("들리는 거리에 이만큼(m) 더한 안쪽이면 재생")] public float lightRangeMargin = 2f;
    public float lightFadeIn = 2f;
    public float lightFadeOut = 0.5f;

    [Header("숨기 (먹먹하게)")]
    public bool muffleWhenHiding = true;
    public float muffledCutoff = 1400f;
    [Tooltip("로그 스케일 보간 속도")] public float muffleSpeed = 4f;

    static AmbienceSounds instance;

    SoundHandle drone = SoundHandle.None;
    float droneFade, droneDuck = 1f;

    float nextRandom;

    class LightHum
    {
        public Transform light;
        public string sound;
        public float range;
        public SoundHandle handle = SoundHandle.None;
        public float fade;
        public float sqrDist;
    }
    readonly List<LightHum> lights = new List<LightHum>();
    readonly List<LightHum> sorted = new List<LightHum>();
    float nextLightCheck;

    AudioLowPassFilter listenerFilter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[AmbienceSounds]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AmbienceSounds>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        CollectLights();
        ScheduleRandom();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        drone.Stop();
        drone = SoundHandle.None;
        StopLights(0f);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void HandleSceneLoaded(Scene s, LoadSceneMode m)
    {
        StopLights(0f);
        CollectLights();
        ScheduleRandom();
        listenerFilter = null;
    }

    void Update()
    {
        bool inGame = !GameMenuManager.InTitle;
        Transform listener = ListenerTransform();
        UpdateDrone(inGame);
        if (inGame) UpdateRandom(listener);
        UpdateLights(inGame, listener);
        UpdateMuffle(listener);
    }

    // ───────────── 드론 ─────────────

    void UpdateDrone(bool inGame)
    {
        if (!inGame)
        {
            if (drone.IsPlaying) drone.Stop(1f);
            drone = SoundHandle.None;
            return;
        }
        if (!drone.IsPlaying)
        {
            drone = AudioManager.Play(droneSound);
            droneFade = droneFadeIn > 0f ? 0f : 1f;
        }
        droneFade = Mathf.MoveTowards(droneFade, 1f, Time.deltaTime / Mathf.Max(0.01f, droneFadeIn));
        droneDuck = Mathf.MoveTowards(droneDuck, MusicSounds.IsChaseMusicOn ? chaseDuck : 1f, Time.deltaTime * duckSpeed);
        drone.SetVolume(droneFade * droneDuck);
    }

    // ───────────── 랜덤 ─────────────

    void ScheduleRandom()
        => nextRandom = Time.time + Random.Range(Mathf.Min(randomInterval.x, randomInterval.y), Mathf.Max(randomInterval.x, randomInterval.y));

    void UpdateRandom(Transform listener)
    {
        if (Time.time < nextRandom || listener == null) return;
        ScheduleRandom();
        Vector2 c = Random.insideUnitCircle * randomRadius;
        AudioManager.Play(randomSound, listener.position + new Vector3(c.x, 0f, c.y));
    }

    // ───────────── 조명 ─────────────

    void CollectLights()
    {
        lights.Clear();
        var fluo = new List<Vector3>();
        var emer = new List<Vector3>();
        float fluoRange = fluorescentRange, emerRange = emergencyRange;
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
        {
            if (l.name.StartsWith(fluorescentPrefix)) TryAdd(l.transform, fluorescentSound, fluorescentSpacing, fluoRange, fluo);
            else if (l.name.StartsWith(emergencyPrefix)) TryAdd(l.transform, emergencySound, emergencySpacing, emerRange, emer);
        }
    }

    void TryAdd(Transform t, string sound, float spacing, float range, List<Vector3> picked)
    {
        Vector3 p = t.position;
        foreach (var q in picked) if ((q - p).sqrMagnitude < spacing * spacing) return;
        picked.Add(p);
        lights.Add(new LightHum { light = t, sound = sound, range = range });
    }


    void StopLights(float fade)
    {
        foreach (var h in lights) { h.handle.Stop(fade); h.handle = SoundHandle.None; }
    }

    void UpdateLights(bool inGame, Transform listener)
    {
        // 거리 정렬은 0.25초마다, 페이드는 매 프레임
        if (Time.unscaledTime >= nextLightCheck)
        {
            nextLightCheck = Time.unscaledTime + 0.25f;
            sorted.Clear();
            for (int i = lights.Count - 1; i >= 0; i--)
            {
                var h = lights[i];
                if (h.light == null) { h.handle.Stop(lightFadeOut); lights.RemoveAt(i); continue; }
                if (!inGame || listener == null || !h.light.gameObject.activeInHierarchy) { h.sqrDist = float.MaxValue; continue; }
                h.sqrDist = (h.light.position - listener.position).sqrMagnitude;
                float r = h.range + lightRangeMargin;
                if (h.sqrDist <= r * r) sorted.Add(h);
            }
            sorted.Sort((a, b) => a.sqrDist.CompareTo(b.sqrDist));
            if (sorted.Count > lightMaxPlaying) sorted.RemoveRange(lightMaxPlaying, sorted.Count - lightMaxPlaying);

            foreach (var h in lights)
            {
                bool want = sorted.Contains(h);
                if (want && !h.handle.IsPlaying)
                {
                    h.handle = AudioManager.Play(h.sound, h.light);
                    h.fade = lightFadeIn > 0f ? 0f : 1f;
                    h.handle.SetVolume(h.fade);
                }
                else if (!want && h.handle.IsPlaying)
                {
                    h.handle.Stop(lightFadeOut);
                    h.handle = SoundHandle.None;
                }
            }
        }

        foreach (var h in sorted)
        {
            if (h.fade >= 1f || !h.handle.IsValid) continue;
            h.fade = Mathf.MoveTowards(h.fade, 1f, Time.deltaTime / Mathf.Max(0.01f, lightFadeIn));
            h.handle.SetVolume(h.fade);
        }
    }

    // ───────────── 숨기 ─────────────

    void UpdateMuffle(Transform listener)
    {
        if (!muffleWhenHiding || listener == null) return;
        if (listenerFilter == null || listenerFilter.transform != listener)
        {
            listenerFilter = listener.GetComponent<AudioLowPassFilter>();
            if (listenerFilter == null)
            {
                listenerFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
                listenerFilter.cutoffFrequency = 22000f;
            }
        }
        float target = PlayerHide.IsPlayerHiding ? muffledCutoff : 22000f;
        float cur = Mathf.Log(listenerFilter.cutoffFrequency), tgt = Mathf.Log(target); // 로그 스케일로 보간해야 자연스럽다
        listenerFilter.cutoffFrequency = Mathf.Exp(Mathf.MoveTowards(cur, tgt, muffleSpeed * Time.deltaTime));
    }

    // ───────────── 공통 ─────────────

    Transform cachedListener;
    float nextListenerCheck;

    Transform ListenerTransform()
    {
        if (cachedListener != null && cachedListener.gameObject.activeInHierarchy && Time.unscaledTime < nextListenerCheck) return cachedListener;
        nextListenerCheck = Time.unscaledTime + 1f; // 컷신 카메라 등으로 리스너가 바뀔 수 있다
        cachedListener = null;
        foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (l.isActiveAndEnabled) { cachedListener = l.transform; break; }
        return cachedListener;
    }
}
