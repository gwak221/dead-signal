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
    [SerializeField] float maxDetectionMultiplier = 1.6f;
    [Tooltip("x: 0(안정)~1(최대 BPM), y: 0~1 보간. 기본 선형.")]
    [SerializeField] AnimationCurve detectionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

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
        float speed = currentBpm * 0.12f;
        float s = 1f + Mathf.Sin(Time.time * speed) * 0.18f * (currentBpm / 100f);
        heartIconTrans.localScale = new Vector3(s, s, 1f);
    }
}
