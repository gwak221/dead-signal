using UnityEngine;

/// <summary>
/// 벽 차폐(occlusion). 리스너와 소리 사이에 벽이 있으면 소리를 먹먹하게(저역통과) + 작게 만든다.
/// 직접 붙이지 않는다: SoundCue.ApplyTo가 3D 소리(occlusion &gt; 0)에 자동으로 붙이고 켠다.
///   - checkInterval마다 리스너 → 소리 방향으로 Raycast 여러 개(가운데 + 옆)를 쏴서 막힌 벽 수의 평균을 구한다
///     (옆 레이 덕분에 문틈/모서리에서 갑자기 바뀌지 않고 부드럽게 바뀐다)
///   - 벽 0개 = 그대로, 1개 = oneWall 값, 2개 이상 = twoWall 값 사이를 보간
///   - 소리를 내는 오브젝트 자신(부모/자식 콜라이더)과 플레이어 몸은 벽으로 치지 않는다
///   - 닫힌 문도 콜라이더가 있으면 벽이다 → 문을 열면 소리가 트인다. 사물함 안에 숨으면 바깥이 먹먹해진다
/// 설정: AudioSystem 프리팹 > RoomAudioController > Occlusion
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class AudioOcclusion : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("벽 차폐 전체 켜기/끄기")]
        public bool enabled = true;
        [Tooltip("벽으로 칠 레이어 (트리거는 항상 무시)")]
        public LayerMask obstacleMask = ~((1 << 2) | (1 << 5)); // Ignore Raycast, UI 제외
        [Tooltip("벽 검사 주기(초)")]
        public float checkInterval = 0.1f;
        [Tooltip("값이 바뀌는 속도 (클수록 빠름)")]
        public float smoothing = 8f;
        [Tooltip("소리 하나당 레이 개수 (1 = 가운데만, 3 = 좌우 추가, 5 = 위아래 추가)")]
        [Range(1, 5)] public int rayCount = 3;
        [Tooltip("옆 레이를 소리 위치에서 얼마나 벌릴지(m)")]
        public float raySpread = 0.6f;
        [Tooltip("벽을 최대 몇 개까지 셀지")]
        [Range(1, 4)] public int maxWalls = 2;

        [Header("무엇을 벽으로 칠지")]
        [Tooltip("이 높이(m) 이상인 콜라이더만 벽으로 친다 (맵 벽 = 3m). 커튼/선반/침대/캐비닛 같은 가구는 빠진다")]
        public float minWallHeight = 2.8f;
        [Tooltip("높이와 상관없이 벽으로 칠 이름 (대소문자 무시, 포함 여부). 문짝(2.2m)을 위해")]
        public string[] alwaysBlockNames = { "DoorLeaf" };
        [Tooltip("높이와 상관없이 무시할 이름 (대소문자 무시, 포함 여부)")]
        public string[] ignoreNames = { "cutton", "curtain" };
        [Tooltip("이 거리(m) 안에서 연달아 맞은 콜라이더는 벽 하나로 친다 (겹쳐 있는 벽 조각)")]
        public float mergeDistance = 0.6f;

        [Header("벽 1개")]
        [Tooltip("저역통과 컷오프(Hz). 낮을수록 먹먹함")]
        public float oneWallCutoff = 3000f;
        [Range(0f, 1f)] public float oneWallVolume = 0.8f;

        [Header("벽 2개 이상")]
        public float twoWallCutoff = 1500f;
        [Range(0f, 1f)] public float twoWallVolume = 0.6f;

        [Header("무시 거리")]
        [Tooltip("리스너에서 이 거리(m) 안의 콜라이더는 무시 (플레이어 몸, 손에 든 물건)")]
        public float listenerPadding = 0.3f;
        [Tooltip("소리 위치에서 이 거리(m) 안의 콜라이더는 무시 (적 몸통 등)")]
        public float sourcePadding = 0.5f;

        [Tooltip("Scene 뷰에 레이 표시 (초록 = 트임, 빨강 = 막힘)")]
        public bool debugDraw = false;
    }

    static readonly Settings fallback = new Settings();
    public static Settings Current => RoomAudioController.Instance != null && RoomAudioController.Instance.occlusion != null
        ? RoomAudioController.Instance.occlusion : fallback;

    const float OpenCutoff = 22000f;
    static readonly RaycastHit[] hits = new RaycastHit[16];
    static Transform listener;
    static float nextListenerLookup;

    /// <summary>이 소리를 낸 오브젝트. 이 아래 콜라이더는 벽으로 치지 않는다.</summary>
    public Transform owner;

    AudioSource source;
    float strength = 1f;
    float target;    // 막힌 벽 수 (0~maxWalls, 레이 평균)
    float current;   // 보간된 값
    float nextCheck;
    float lastCheck = -100f;

    // 메인 스레드가 쓰고 오디오 스레드가 읽는다
    volatile float gain = 1f;
    volatile float cutoff = OpenCutoff;
    int sampleRate = 48000;

    // 오디오 스레드 전용 (저역통과 필터 상태)
    float appliedGain = 1f;
    float appliedCutoff = OpenCutoff;
    bool filterActive;
    float b0, b1, b2, a1, a2;
    readonly float[] x1 = new float[8], x2 = new float[8], y1 = new float[8], y2 = new float[8];

    /// <summary>현재 막힌 벽 수 (보간됨, 0 = 트임)</summary>
    public float Walls => current;
    /// <summary>현재 볼륨 배율 (디버그용)</summary>
    public float Gain => gain;
    /// <summary>현재 저역통과 컷오프 Hz (디버그용)</summary>
    public float Cutoff => cutoff;

    // ───────────── SoundCue / Emitter에서 호출 ─────────────

    /// <summary>Emitter가 AudioSource를 만들 때 호출. 소리 주인을 알려 둔다 (꺼진 채로 붙음).</summary>
    public static void Attach(AudioSource src, Transform owner)
    {
        if (src == null || !Application.isPlaying) return;
        var occ = src.GetComponent<AudioOcclusion>();
        if (occ == null)
        {
            occ = src.gameObject.AddComponent<AudioOcclusion>();
            occ.enabled = false;
        }
        occ.owner = owner;
    }

    /// <summary>SoundCue.ApplyTo에서 호출. strength 0이면 끈다.</summary>
    public static void Configure(AudioSource src, float strength)
    {
        if (src == null || !Application.isPlaying) return;
        var occ = src.GetComponent<AudioOcclusion>();
        if (strength <= 0f || src.spatialBlend < 0.5f)
        {
            if (occ != null) occ.enabled = false;
            return;
        }
        if (occ == null) occ = src.gameObject.AddComponent<AudioOcclusion>();
        occ.strength = Mathf.Clamp01(strength);
        if (!occ.enabled) occ.enabled = true;                                   // OnEnable에서 바로 계산
        else if (Time.time - occ.lastCheck > Current.checkInterval * 3f) occ.Snap(); // 오래 쉬었으면 새로 계산
        occ.Apply();
    }

    // ───────────── 동작 ─────────────

    void Awake()
    {
        source = GetComponent<AudioSource>();
        sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
        if (owner == null) owner = transform;
    }

    void OnEnable()
    {
        nextCheck = Time.time + Random.Range(0f, Current.checkInterval); // 여러 소리의 검사 시점을 흩는다
        Snap();
    }

    void OnDisable()
    {
        current = target = 0f;
        gain = 1f;
        cutoff = OpenCutoff;
    }

    void Update()
    {
        var s = Current;
        if (!s.enabled) target = 0f; // 전체 끄기: 부드럽게 트인 상태로 (플레이 중 켜고 끄며 비교 가능)
        else if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + s.checkInterval * Random.Range(0.8f, 1.2f);
            Evaluate(s);
        }
        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-s.smoothing * Time.deltaTime));
        Apply();
    }

    void Snap()
    {
        if (Current.enabled) Evaluate(Current); else target = 0f;
        current = target;
        Apply();
    }

    void Apply()
    {
        var s = Current;
        float w = current * strength;
        float fc, vol;
        if (w <= 0.01f)
        {
            fc = OpenCutoff;
            vol = 1f;
        }
        else if (w <= 1f)
        {
            fc = LogLerp(OpenCutoff, s.oneWallCutoff, w);
            vol = Mathf.Lerp(1f, s.oneWallVolume, w);
        }
        else
        {
            float t = Mathf.Clamp01(w - 1f);
            fc = LogLerp(s.oneWallCutoff, s.twoWallCutoff, t);
            vol = Mathf.Lerp(s.oneWallVolume, s.twoWallVolume, t);
        }
        cutoff = fc;
        gain = vol;
    }

    void Evaluate(Settings s)
    {
        lastCheck = Time.time;
        var l = Listener();
        if (l == null) { target = 0f; return; }

        Vector3 from = l.position;
        Vector3 to = transform.position;
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.05f) { target = 0f; return; }
        // 어차피 안 들리는 거리면 검사 생략 (값 유지)
        if (source != null && source.rolloffMode == AudioRolloffMode.Linear && dist > source.maxDistance + 1f) return;

        Vector3 dir = d / dist;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, dir).normalized;

        int rays = Mathf.Clamp(s.rayCount, 1, 5);
        float sum = 0f;
        for (int i = 0; i < rays; i++)
        {
            Vector3 off;
            switch (i)
            {
                case 1: off = side * s.raySpread; break;
                case 2: off = -side * s.raySpread; break;
                case 3: off = up * s.raySpread * 0.5f; break;
                case 4: off = -up * s.raySpread * 0.5f; break;
                default: off = Vector3.zero; break;
            }
            int walls = CountWalls(from, to + off, l, s);
            sum += Mathf.Min(walls, s.maxWalls);
            if (s.debugDraw) Debug.DrawLine(from, to + off, walls > 0 ? Color.red : Color.green, s.checkInterval);
        }
        target = sum / rays;
    }

    int CountWalls(Vector3 from, Vector3 to, Transform l, Settings s)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return 0;
        int n = Physics.RaycastNonAlloc(from, d / len, hits, len, s.obstacleMask, QueryTriggerInteraction.Ignore);
        int valid = 0;
        Transform own = owner != null ? owner : transform;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance < s.listenerPadding || h.distance > len - s.sourcePadding) continue;
            var ct = h.collider.transform;
            if (ct.IsChildOf(own) || own.IsChildOf(ct)) continue; // 소리 낸 오브젝트 자신 (적 몸통, 가구 문짝)
            if (l.IsChildOf(ct)) continue;                         // 플레이어 몸
            if (!IsWall(h.collider, s)) continue;                  // 가구 등
            hitDist[valid++] = h.distance;
        }
        if (valid == 0) return 0;

        // 가까운 순으로 정렬 후, 붙어 있는 조각은 벽 하나로 합친다
        System.Array.Sort(hitDist, 0, valid);
        int count = 1;
        for (int i = 1; i < valid; i++)
            if (hitDist[i] - hitDist[i - 1] > s.mergeDistance) count++;
        return count;
    }

    static readonly float[] hitDist = new float[16];
    static readonly System.Collections.Generic.Dictionary<int, bool> wallCache = new System.Collections.Generic.Dictionary<int, bool>();
    static Settings cacheOwner;

    /// <summary>벽으로 칠 콜라이더인지 (높이/이름 기준, 콜라이더마다 한 번만 계산)</summary>
    static bool IsWall(Collider c, Settings s)
    {
        if (cacheOwner != s) { wallCache.Clear(); cacheOwner = s; }
        int id = c.GetInstanceID();
        if (wallCache.TryGetValue(id, out bool w)) return w;

        string name = c.name;
        w = c.bounds.size.y >= s.minWallHeight;
        if (s.alwaysBlockNames != null)
            foreach (var k in s.alwaysBlockNames)
                if (!string.IsNullOrEmpty(k) && name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) { w = true; break; }
        if (s.ignoreNames != null)
            foreach (var k in s.ignoreNames)
                if (!string.IsNullOrEmpty(k) && name.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) { w = false; break; }
        wallCache[id] = w;
        return w;
    }

    /// <summary>인스펙터에서 벽 판정 기준을 바꿨을 때 캐시 비우기</summary>
    public static void ClearWallCache() => wallCache.Clear();

    // 오디오 스레드. 저역통과(2-pole, Q 0.707) + 볼륨.
    // AudioLowPassFilter는 쓰지 않는다: AudioSource에 붙으면 거리 커브(기본 5000Hz 고정)가 컷오프를 덮어써서 설정값이 안 먹는다.
    void OnAudioFilterRead(float[] data, int channels)
    {
        int frames = channels > 0 ? data.Length / channels : 0;
        if (frames == 0) return;
        channels = Mathf.Min(channels, 8);

        float g0 = appliedGain, g1 = gain;
        float fcTarget = cutoff;
        appliedGain = g1;

        // 트인 상태: 아무것도 안 함
        if (g0 == 1f && g1 == 1f && fcTarget >= OpenCutoff - 1f && appliedCutoff >= OpenCutoff - 1f)
        {
            filterActive = false;
            return;
        }

        // 컷오프는 버퍼마다 조금씩 따라간다 (계수가 갑자기 튀지 않게)
        if (!filterActive)
        {
            System.Array.Clear(x1, 0, 8); System.Array.Clear(x2, 0, 8);
            System.Array.Clear(y1, 0, 8); System.Array.Clear(y2, 0, 8);
            filterActive = true;
        }
        appliedCutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(appliedCutoff), Mathf.Log(fcTarget), 0.5f));
        if (Mathf.Abs(appliedCutoff - fcTarget) < 1f) appliedCutoff = fcTarget;
        bool filter = appliedCutoff < OpenCutoff - 1f;
        if (filter) ComputeLowpass(appliedCutoff);

        float step = (g1 - g0) / frames;
        float g = g0;
        int stride = data.Length / frames;
        for (int f = 0; f < frames; f++)
        {
            g += step;
            int k = f * stride;
            for (int c = 0; c < channels; c++)
            {
                float x = data[k + c];
                if (filter)
                {
                    float y = b0 * x + b1 * x1[c] + b2 * x2[c] - a1 * y1[c] - a2 * y2[c];
                    x2[c] = x1[c]; x1[c] = x;
                    y2[c] = y1[c]; y1[c] = y;
                    x = y;
                }
                data[k + c] = x * g;
            }
        }
    }

    void ComputeLowpass(float fc)
    {
        float nyq = sampleRate * 0.5f;
        fc = Mathf.Clamp(fc, 20f, nyq * 0.95f);
        float w0 = 2f * Mathf.PI * fc / sampleRate;
        float cs = Mathf.Cos(w0);
        float alpha = Mathf.Sin(w0) / (2f * 0.7071f);
        float a0 = 1f + alpha;
        b0 = (1f - cs) * 0.5f / a0;
        b1 = (1f - cs) / a0;
        b2 = b0;
        a1 = -2f * cs / a0;
        a2 = (1f - alpha) / a0;
    }

    static float LogLerp(float a, float b, float t) => Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), Mathf.Clamp01(t)));

    static Transform Listener()
    {
        if ((listener == null || !listener.gameObject.activeInHierarchy) && Time.unscaledTime >= nextListenerLookup)
        {
            nextListenerLookup = Time.unscaledTime + 0.5f;
            var a = FindFirstObjectByType<AudioListener>();
            listener = a != null ? a.transform : null;
        }
        return listener;
    }
}
