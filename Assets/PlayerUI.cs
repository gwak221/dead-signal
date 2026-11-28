using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    [Header("HP UI Elements")]
    public Slider hpBar;            
    public TextMeshProUGUI hpText;  

    [Header("Heart Rate UI Elements")]
    public TextMeshProUGUI heartRateText; // 심박수 텍스트 (예: 80 BPM)
    public RectTransform heartIconTrans;  // 심장 아이콘의 위치/크기 조절용

    [Header("Player Stats")]
    private float maxHp = 100f;     
    private float currentHp = 100f; 

    [Header("Heart Rate Stats")]
    private float currentBpm = 80f;   // 기본 심박수
    private float minBpm = 60f;
    private float maxBpm = 180f;

    void Start()
    {
        UpdateHPUI();
        UpdateHeartRateUI();
    }

    void Update()
    {
        // 2. 심박수에 맞춰 심장 아이콘이 쿵쾅거리는 애니메이션 효과
        PulseHeartIcon();
    }

    // 체력 설정 함수
    public void SetHealth(float health)
    {
        currentHp = Mathf.Clamp(health, 0, maxHp);
        UpdateHPUI();
    }

    // 심박수 설정 함수
    public void SetHeartRate(float bpm)
    {
        currentBpm = Mathf.Clamp(bpm, minBpm, maxBpm);
        UpdateHeartRateUI();
    }

    void UpdateHPUI()
    {
        if (hpBar != null) hpBar.value = currentHp / maxHp;
        if (hpText != null) hpText.text = $"{currentHp} / {maxHp}";
    }

    void UpdateHeartRateUI()
    {
        if (heartRateText != null)
        {
            heartRateText.text = $"{Mathf.Round(currentBpm)} BPM";
        }
    }

    // 심장이 두근거리는 연출
    void PulseHeartIcon()
    {
        if (heartIconTrans != null)
        {
            // 심박수(BPM)가 높을수록 쿵쾅거리는 속도가 빨라짐
            float pulseSpeed = currentBpm * 0.1f; 
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.15f * (currentBpm / 100f);
            heartIconTrans.localScale = new Vector3(scale, scale, 1f);
        }
    }
}