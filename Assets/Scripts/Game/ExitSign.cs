using TMPro;
using UnityEngine;

/// <summary>
/// EscapeWall 앞에 걸린 비상구 EXIT 표지. EscapeDoorController(문 개방 로직)는 건드리지 않고
/// GameProgress.EscapeUnlocked(4/4 비콘)만 읽어 밝기를 바꾼다.
///  - 잠김: 은은한 녹색 (보이되 어둡게)
///  - 해금: 더 밝아지고 천천히 숨쉬듯 Pulse
/// 자체 Emission 만 사용 (Light 없음).
/// </summary>
public class ExitSign : MonoBehaviour
{
    public Renderer plate;
    public TMP_Text label;
    public Renderer[] extras;                     // 화살표 등 추가 발광부
    [ColorUsage(false, true)] public Color plateColor = new Color(0.05f, 0.65f, 0.20f);
    public float lockedIntensity = 0.9f;
    public float unlockedIntensity = 1.6f;
    public float unlockedPulse = 0.35f;
    public float blend = 2f;

    Material plateMat;
    float level;   // 0 잠김 ~ 1 해금

    void Start()
    {
        if (plate != null) plateMat = plate.material;
        Apply();
    }

    void Update()
    {
        bool unlocked = GameProgress.EscapeUnlocked;
        level = Mathf.MoveTowards(level, unlocked ? 1f : 0f, blend * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
        float k = Mathf.Lerp(lockedIntensity, unlockedIntensity + unlockedPulse * pulse, level);
        if (plateMat != null)
        {
            plateMat.SetColor("_BaseColor", plateColor * 0.25f);
            plateMat.EnableKeyword("_EMISSION");
            plateMat.SetColor("_EmissionColor", plateColor * k);
        }
        if (label != null)
        {
            float v = Mathf.Lerp(0.55f, 1f, level);
            label.color = new Color(v, v, v, 1f);
        }
        if (extras != null)
            foreach (var r in extras)
                if (r != null)
                {
                    var m = r.material;
                    m.SetColor("_BaseColor", Color.black);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(0.9f, 1f, 0.9f) * Mathf.Lerp(0.5f, 1.8f, level));
                }
    }

    void OnDestroy()
    {
        if (plateMat != null) Destroy(plateMat);
    }
}
