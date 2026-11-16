using UnityEngine;

/// <summary>
/// 이동 거리에 맞춰 발소리를 한 걸음마다 한 번 재생하는 공용 컴포넌트.
/// 플레이어(CharacterController)와 적(NavMeshAgent) 모두 위치 변화만으로 동작한다.
/// 속도가 runSpeedThreshold 이상이면 뛰기 소리와 뛰기 보폭을 쓴다.
/// </summary>
public class FootstepEmitter : MonoBehaviour
{
    public SoundCue walkCue;
    public SoundCue runCue;

    [Tooltip("이 속도(m/s) 이상이면 뛰기로 판단")]
    public float runSpeedThreshold = 5.5f;
    [Tooltip("걷기 한 걸음 거리(m)")]
    public float walkStride = 0.75f;
    [Tooltip("뛰기 한 걸음 거리(m)")]
    public float runStride = 1.1f;
    [Tooltip("이 속도(m/s) 이하면 멈춘 것으로 보고 발소리를 내지 않음")]
    public float minSpeed = 0.3f;
    [Tooltip("CharacterController가 있으면 공중에서는 발소리를 내지 않음")]
    public bool requireGrounded = true;

    [Header("Source (비우면 자기 위치에서 재생)")]
    public SoundEmitter emitter;

    CharacterController cc;
    Vector3 lastPos;
    float travelled;

    /// <summary>외부에서 일시적으로 끌 때 (숨기, 컷신 등).</summary>
    public bool Muted { get; set; }
    /// <summary>값을 넣으면 속도 대신 이 값으로 걷기/뛰기를 판단한다 (예: 플레이어 IsSprinting). null이면 속도로 판단.</summary>
    public bool? RunningOverride { get; set; }
    public float CurrentSpeed { get; private set; }

    void Awake()
    {
        cc = GetComponentInParent<CharacterController>();
        if (emitter == null) emitter = GetComponent<SoundEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<SoundEmitter>();
    }

    void OnEnable()
    {
        lastPos = transform.position;
        travelled = 0f;
    }

    void Update()
    {
        Vector3 p = transform.position;
        Vector3 d = p - lastPos; d.y = 0f;
        lastPos = p;
        float dist = d.magnitude;

        // 순간이동(리스폰, Relocate)은 걸음으로 치지 않는다
        if (dist > 3f) { travelled = 0f; CurrentSpeed = 0f; return; }

        CurrentSpeed = Time.deltaTime > 0f ? dist / Time.deltaTime : 0f;
        if (Muted || CurrentSpeed < minSpeed || (requireGrounded && cc != null && !cc.isGrounded))
        {
            travelled = Mathf.Min(travelled, walkStride * 0.5f); // 다시 걸으면 곧바로 첫 걸음
            return;
        }

        bool running = RunningOverride ?? CurrentSpeed >= runSpeedThreshold;
        float stride = running ? runStride : walkStride;
        travelled += dist;
        if (travelled >= stride)
        {
            travelled -= stride;
            emitter.Play(running && runCue != null ? runCue : walkCue);
        }
    }
}
