using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// 탈출 벽: 표시등 4개가 1 → 4 순서대로 빨강(잠김) → 초록(활성)으로 바뀌고, 4개 모두 켜지면 미닫이 양문이 열림.
// escape_wall.fbx 를 씬에 놓고 루트(EscapeWall)에 이 컴포넌트를 붙이면 표시등·문을 이름으로 자동 연결함.
//   - 배터리를 패널에 꽂았을 때 등: ActivateNext() 호출 → 다음 번호 표시등이 초록으로
//   - 테스트: 컴포넌트 ⋮ 메뉴의 "테스트: 다음 표시등 켜기" / "테스트: 순서대로 전부 켜기"
public class EscapeDoorController : MonoBehaviour
{
    [Header("표시등 (1 → 4 순서)")]
    public Renderer[] lampLenses = new Renderer[4];   // 현재 모델의 표시등 4개 (개수는 배열 길이 기준 — 아래 코드는 4를 가정하지 않는다)
    public Light[] lampLights = new Light[4];
    public Color lockedColor = new Color(1f, 0.06f, 0.04f);   // 빨강 = 잠김
    public Color activeColor = new Color(0.1f, 1f, 0.25f);    // 초록 = 활성
    public float emissionIntensity = 4f;
    public float lightIntensity = 1.2f;
    public float lightRange = 3f;

    [Header("문 (미닫이 양문)")]
    public Transform doorLeft;
    public Transform doorRight;
    public float slideDistance = 1.12f;     // 한쪽 문짝이 옆으로 밀리는 거리 (m)
    public float openDelay = 1.0f;          // 마지막 비콘이 활성화된 뒤 문이 움직이기까지
    public float openDuration = 3.0f;
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("순서대로 켜기 데모")]
    public bool playSequenceOnStart = false;
    public float sequenceInterval = 1.5f;

    [Header("컷씬 연동")]
    [Tooltip("true 면 마지막 표시등이 켜져도 문이 바로 열리지 않고, ExitCutsceneController 가 OpenNow() 를 호출할 때 열린다.")]
    public bool deferOpen = false;

    [Header("충돌체 자동 추가")]
    public bool addColliders = true;

    [Header("이벤트")]
    public UnityEvent<int> onLampActivated;   // 켜진 표시등 번호(1~4)
    public UnityEvent onAllLampsActive;
    public UnityEvent onDoorOpened;

    /// <summary>문이 열리는 데 필요한 활성 비콘 수 = GameProgress 가 쓰는 값(씬의 Beacon 등록 수). 표시등 개수와는 무관.</summary>
    public int RequiredCount => Mathf.Max(1, GameProgress.RequiredBeacons);
    public int LampCount => lampLenses != null ? lampLenses.Length : 0;

    public int ActiveCount { get; private set; }
    public bool IsOpen { get; private set; }

    Vector3 leftClosed, rightClosed, leftOpen, rightOpen;
    bool opening;

    void Reset() => AutoFind();

    void Awake()
    {
        if (doorLeft == null || doorRight == null || (lampLenses.Length > 0 && lampLenses[0] == null) || lampLenses.Length == 0) AutoFind();
        if (doorLeft != null && doorRight != null)
        {
            leftClosed = doorLeft.localPosition;
            rightClosed = doorRight.localPosition;
            // 두 문짝을 잇는 방향으로 밀어 엶 (FBX 축 설정과 무관)
            Vector3 dir = (rightClosed - leftClosed).normalized;
            leftOpen = leftClosed - dir * slideDistance;
            rightOpen = rightClosed + dir * slideDistance;
        }
        if (addColliders) AddColliders();
        for (int i = 0; i < LampCount; i++) SetLamp(i, false);
    }

    void Start()
    {
        if (playSequenceOnStart) StartCoroutine(PlaySequence());
    }

    // 다음 번호 표시등을 켬. 활성 수가 RequiredCount(GameProgress 기준)에 도달하면 문이 열림.
    public void ActivateNext()
    {
        if (ActiveCount >= RequiredCount) return;
        if (ActiveCount < LampCount) SetLamp(ActiveCount, true);   // 표시등보다 비콘이 많아도(예: 5개) 안전
        ActiveCount++;
        onLampActivated?.Invoke(ActiveCount);
        if (ActiveCount >= RequiredCount && !deferOpen && !opening && !IsOpen) StartCoroutine(OpenRoutine());
    }

    // 컷씬 등 외부에서 문 개방 시점을 제어할 때 호출 (기존 OpenRoutine 재사용). 이미 열리는 중/열림이면 무시.
    public void OpenNow()
    {
        if (!opening && !IsOpen) StartCoroutine(OpenRoutine());
    }

    // 순서대로 하나씩 켜고 문 열기 (데모·컷신용)
    public IEnumerator PlaySequence()
    {
        while (ActiveCount < RequiredCount)
        {
            yield return new WaitForSeconds(sequenceInterval);
            ActivateNext();
        }
    }

    // 다시 잠금 (전부 빨강, 문 닫힘)
    public void ResetLock()
    {
        StopAllCoroutines();
        opening = false;
        IsOpen = false;
        ActiveCount = 0;
        for (int i = 0; i < LampCount; i++) SetLamp(i, false);
        if (doorLeft != null) doorLeft.localPosition = leftClosed;
        if (doorRight != null) doorRight.localPosition = rightClosed;
    }

    IEnumerator OpenRoutine()
    {
        opening = true;
        onAllLampsActive?.Invoke();
        yield return new WaitForSeconds(openDelay);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, openDuration);
            float k = openCurve.Evaluate(Mathf.Clamp01(t));
            if (doorLeft != null) doorLeft.localPosition = Vector3.LerpUnclamped(leftClosed, leftOpen, k);
            if (doorRight != null) doorRight.localPosition = Vector3.LerpUnclamped(rightClosed, rightOpen, k);
            yield return null;
        }
        opening = false;
        IsOpen = true;
        onDoorOpened?.Invoke();
    }

    void SetLamp(int i, bool on)
    {
        Color c = on ? activeColor : lockedColor;
        if (i < lampLenses.Length && lampLenses[i] != null)
        {
            var m = lampLenses[i].material;            // 표시등마다 따로 바뀌도록 인스턴스 재질
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c * 0.5f);   // URP
            if (m.HasProperty("_Color")) m.SetColor("_Color", c * 0.5f);           // Built-in
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * emissionIntensity);
        }
        if (i < lampLights.Length && lampLights[i] != null)
        {
            var l = lampLights[i];
            l.type = LightType.Point;
            l.color = c;
            l.intensity = lightIntensity;
            l.range = lightRange;
            l.enabled = true;
        }
    }

    void AutoFind()
    {
        // Lamp_N_Lens / Lamp_N_Light 이름에서 가장 큰 N 을 찾아 표시등 개수를 정한다 (현재 모델은 4개).
        int count = 0;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            var m = System.Text.RegularExpressions.Regex.Match(t.name, @"^Lamp_(\d+)_(Lens|Light)$");
            if (m.Success) count = Mathf.Max(count, int.Parse(m.Groups[1].Value));
        }
        if (lampLenses == null || lampLenses.Length != count) lampLenses = new Renderer[count];
        if (lampLights == null || lampLights.Length != count) lampLights = new Light[count];
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            for (int i = 0; i < count; i++)
            {
                if (t.name == $"Lamp_{i + 1}_Lens") lampLenses[i] = t.GetComponent<Renderer>();
                if (t.name == $"Lamp_{i + 1}_Light") lampLights[i] = t.GetComponent<Light>();
            }
            if (t.name == "Door_L") doorLeft = t;
            if (t.name == "Door_R") doorRight = t;
        }
    }

    void AddColliders()
    {
        foreach (var name in new[] { "EscapeWall_Wall", "EscapeWall_Frame" })
        {
            var t = FindChild(name);
            if (t != null && t.GetComponent<Collider>() == null)
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null) t.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }
        }
        foreach (var d in new[] { doorLeft, doorRight })   // 문짝은 움직이므로 박스 충돌체
            if (d != null && d.GetComponent<Collider>() == null) d.gameObject.AddComponent<BoxCollider>();
    }

    Transform FindChild(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    [ContextMenu("테스트: 다음 표시등 켜기")]
    void TestNext() => ActivateNext();

    [ContextMenu("테스트: 순서대로 전부 켜기")]
    void TestSequence() => StartCoroutine(PlaySequence());

    [ContextMenu("테스트: 다시 잠금")]
    void TestReset() => ResetLock();
}
