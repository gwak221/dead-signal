using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 심박수(BPM). 기존 HUD(HeartRateUI 의 HeartRate_Text / HeartIcon)를 그대로 사용한다.
/// - Sprint 중: restingBpm → maxBpm 으로 서서히 상승 (sprintIncreaseRate BPM/초)
/// - Sprint 종료: 수 초에 걸쳐 안정 상태로 감소 (recoveryRate BPM/초)
/// - DetectionMultiplier: MonsterChase 의 탐지 '거리'에 곱해진다 (시야/LOS 판정은 그대로).
/// 추격 중이라고 BPM을 강제로 올리지 않는다.
/// </summary>
public class HeartRateController : MonoBehaviour
{
    public static HeartRateController Instance { get; private set; }

    [Header("UI (비우면 자식에서 이름으로 자동 탐색)")]
    public TMP_Text heartRateText;
    public RectTransform heartIconTrans;
    public Image heartIconImage;

    [Header("BPM")]
    [SerializeField] float restingBpm = 70f;
    [Tooltip("안정 상태에서 숫자가 미세하게 흔들리는 폭(±).")]
    [SerializeField] float idleFluctuation = 4f;
    [SerializeField] float idleFluctuationSpeed = 1.2f;
    [SerializeField] float minBpm = 60f;
    [SerializeField] float maxBpm = 160f;

    [Header("변화 속도 (BPM/초)")]
    [Tooltip("Sprint 중 상승 속도. 70→160 약 15초.")]
    [SerializeField] float sprintIncreaseRate = 6f;
    [Tooltip("Sprint 종료 후 감소 속도. 160→70 약 22초.")]
    [SerializeField] float recoveryRate = 4f;

    [Header("Monster Detection")]
    [Tooltip("maxBpm 일 때 탐지거리 배율 (restingBpm 일 때 1.0).")]
    [SerializeField] float maxDetectionMultiplier = 2.4f; // 이전 1.6. BPM 70→1.0x, 115→~1.7x, 160→2.4x (선형)
    [Tooltip("x: 0(안정)~1(최대 BPM), y: 0~1 보간. 기본 선형.")]
    [SerializeField] AnimationCurve detectionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Heartbeat UI (시각 박동만 완화. BPM 숫자/탐지 계산은 실제 값 그대로)")]
    [Tooltip("안정 BPM 일 때 아이콘이 한 번 뛰는 간격(초).")]
    [SerializeField] float uiBeatIntervalResting = 1.0f;
    [Tooltip("최대 BPM 일 때 아이콘이 한 번 뛰는 간격(초).")]
    [SerializeField] float uiBeatIntervalMax = 0.5f;
    [Tooltip("박동 순간 아이콘이 커지는 비율 (0.10 = 1.0 → 1.10).")]
    [SerializeField] float uiBeatScale = 0.10f;
    float beatPhase;

    [SerializeField] float currentBpm = 70f;
    FirstPersonController player;

    public float CurrentBpm => currentBpm;
    public float MinBpm => minBpm;
    public float MaxBpm => maxBpm;
    public float RestingBpm => restingBpm;

    /// <summary>1.0(안정) ~ maxDetectionMultiplier(최대 BPM).</summary>
    public float DetectionMultiplier
    {
        get
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(restingBpm, maxBpm, currentBpm));
            return Mathf.Lerp(1f, maxDetectionMultiplier, detectionCurve.Evaluate(t));
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        Instance = this;
        if (heartRateText == null)
        {
            var t = transform.Find("HeartRate_Text");
            if (t != null) heartRateText = t.GetComponent<TMP_Text>();
            if (heartRateText == null) heartRateText = GetComponentInChildren<TMP_Text>(true);
        }
        if (heartIconTrans == null)
        {
            var i = transform.Find("HeartIcon");
            if (i != null) heartIconTrans = i.GetComponent<RectTransform>();
        }
        if (heartIconTrans != null && heartIconImage == null) heartIconImage = heartIconTrans.GetComponent<Image>();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        currentBpm = restingBpm;
        player = FindFirstObjectByType<FirstPersonController>();
        UpdateUI();
    }

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<FirstPersonController>();
        bool sprinting = player != null && player.IsSprinting;

        if (sprinting)
        {
            currentBpm += sprintIncreaseRate * Time.deltaTime;
        }
        else if (currentBpm > restingBpm + idleFluctuation)
        {
            currentBpm = Mathf.MoveTowards(currentBpm, restingBpm, recoveryRate * Time.deltaTime);
        }
        else
        {
            // 안정 상태: restingBpm ± idleFluctuation 에서 천천히 흔들림
            float wave = (Mathf.Sin(Time.time * idleFluctuationSpeed) + Mathf.Sin(Time.time * idleFluctuationSpeed * 1.7f) * 0.5f) / 1.5f;
            float target = restingBpm + wave * idleFluctuation;
            currentBpm = Mathf.MoveTowards(currentBpm, target, 4f * Time.deltaTime);
        }

        currentBpm = Mathf.Clamp(currentBpm, minBpm, maxBpm);
        UpdateUI();
        Pulse();
    }

    /// <summary>부활 직후 등: 회복 도중 상태로 설정.</summary>
    public void SetBpm(float bpm) => currentBpm = Mathf.Clamp(bpm, minBpm, maxBpm);

    /// <summary>외부(QTE 실패 등)에서 심박수를 즉시 올린다. maxBpm 을 넘지 않는다. 회복은 기존 recoveryRate 로 자연스럽게.</summary>
    public void AddHeartRate(float amount) => currentBpm = Mathf.Clamp(currentBpm + amount, minBpm, maxBpm);

    void UpdateUI()
    {
        if (heartRateText == null) return;
        heartRateText.text = Mathf.RoundToInt(currentBpm) + " BPM";
        float ratio = Mathf.InverseLerp(restingBpm + idleFluctuation, maxBpm, currentBpm);
        heartRateText.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.25f), ratio);
    }

    void Pulse()
    {
        if (heartIconTrans == null) return;
        // 실제 BPM 을 1:1 로 따라가지 않고, 안정~최대 BPM 을 [Resting, Max] 간격으로 매핑 (쿵 ... 쿵 ... 쿵)
        float t = Mathf.Clamp01(Mathf.InverseLerp(restingBpm, maxBpm, currentBpm));
        float interval = Mathf.Lerp(uiBeatIntervalResting, uiBeatIntervalMax, t);
        beatPhase += Time.deltaTime / Mathf.Max(0.1f, interval);
        if (beatPhase >= 1f) beatPhase -= Mathf.Floor(beatPhase);

        // 한 번 부드럽게 부풀었다가 가라앉는 단일 박동 (전체 주기의 앞 22%)
        float k = beatPhase < 0.22f ? Mathf.Sin(beatPhase / 0.22f * Mathf.PI) : 0f;
        float s = 1f + uiBeatScale * k;
        heartIconTrans.localScale = new Vector3(s, s, 1f);
    }
}
