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
    }

    // 체력 설정 함수
    public void SetHealth(float health)
    {
        currentHp = Mathf.Clamp(health, 0, maxHp);
        UpdateHPUI();
    }

    // 예전 하트/BPM 글자 대신 HeartRateController(심전도)로 넘긴다.
    public void SetHeartRate(float bpm)
    {
        currentBpm = Mathf.Clamp(bpm, minBpm, maxBpm);
        if (HeartRateController.Instance != null)
            HeartRateController.Instance.SetBpm(currentBpm);
    }

    void UpdateHPUI()
    {
        if (hpBar != null) hpBar.value = currentHp / maxHp;
        if (hpText != null) hpText.text = $"{currentHp} / {maxHp}";
    }

}