using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float runSpeed = 9f;
    public float gravity = -20f;

    [Header("Mouse Look")]
    public Transform cameraPivot;
    public Camera playerCamera;
    public float mouseSensitivity = 0.05f;

    [Header("Camera")]
    public Vector3 firstPersonPosition = Vector3.zero;
    public Vector3 thirdPersonPosition = new Vector3(0f, 0.8f, -5f);

    [Header("UI")]
    public GameObject sprintIndicator;

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraRotationX;
    private bool isThirdPerson = false;
    private bool controlsEnabled = true;

    // 신체 파손(다리) 등 외부 시스템이 제어하는 런타임 값.
    // Inspector의 moveSpeed / runSpeed 원본 값은 건드리지 않는다.
    private bool sprintAllowed = true;
    private float speedMultiplier = 1f;

    // 심박수 Sprint Lock (히스테리시스): lockBpm 이상이면 잠금, releaseBpm 이하로 회복돼야 해제. 그 사이 구간은 현재 상태 유지.
    // 코드 상수(직렬화 안 함): 씬에 저장된 옛 값이 새 기준을 덮어쓰지 않게 한다.
    /// <summary>이 BPM 이상이면 달리기 금지.</summary>
    [System.NonSerialized] public float sprintLockBpm = 160f;
    /// <summary>잠긴 뒤 이 BPM 이하로 내려가야 다시 달릴 수 있음 (121~159 는 현재 상태 유지).</summary>
    [System.NonSerialized] public float sprintReleaseBpm = 120f;
    private bool heartSprintLocked;

    /// <summary>심박수로 인한 Sprint Lock 상태 (다리 부상 SprintAllowed 와 별개).</summary>
    public bool HeartSprintLocked => heartSprintLocked;

    void UpdateHeartSprintLock()
    {
        var heart = HeartRateController.Instance;
        if (heart == null) return;
        // 주사기 효과 중: 심박수와 상관없이 달릴 수 있다 (끝나는 순간 아래 판정으로 BPM 이 높으면 바로 다시 잠김)
        if (heart.IsSprintUnlocked) { heartSprintLocked = false; return; }
        float bpm = heart.CurrentBpm;
        if (!heartSprintLocked && bpm >= sprintLockBpm) heartSprintLocked = true;
        else if (heartSprintLocked && bpm <= sprintReleaseBpm) heartSprintLocked = false;
    }

    public bool ControlsEnabled => controlsEnabled;
    public bool IsThirdPerson => isThirdPerson;
    public bool SprintAllowed => sprintAllowed;
    /// <summary>이번 프레임 실제로 달리는 중 (심박수 시스템 등이 읽음).</summary>
    public bool IsSprinting { get; private set; }
    /// <summary>카메라 상하 시선(도, 아래가 +). 잡힘 연출 등 외부 시선 제어용.</summary>
    public float LookPitch => cameraRotationX;
    public void SetLookPitch(float pitch)
    {
        cameraRotationX = Mathf.Clamp(pitch, -80f, 80f);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
    }
    public float SpeedMultiplier => speedMultiplier;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetCameraMode();

        if (sprintIndicator != null)
            sprintIndicator.SetActive(false);
    }

    void Update()
    {
        if (!controlsEnabled)
        {
            IsSprinting = false;
            return;
        }

        Move();
        Look();
        // V키 1/3인칭 전환은 폐기 (1인칭 고정).
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;

        if (!enabled && sprintIndicator != null)
            sprintIndicator.SetActive(false);
    }

    // 다리 파손 등으로 달리기를 막을 때 사용.
    public void SetSprintAllowed(bool allowed)
    {
        sprintAllowed = allowed;

        if (!allowed && sprintIndicator != null)
            sprintIndicator.SetActive(false);
    }

    // 이동 속도 배율 (1 = 정상). 걷기/달리기 모두에 적용.
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0f, multiplier);
    }

    void Move()
    {
        if (Keyboard.current == null)
            return;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1;
        if (Keyboard.current.sKey.isPressed) input.y -= 1;
        if (Keyboard.current.aKey.isPressed) input.x -= 1;
        if (Keyboard.current.dKey.isPressed) input.x += 1;

        input = Vector2.ClampMagnitude(input, 1f);

        bool isMoving = input.sqrMagnitude > 0.01f;
        UpdateHeartSprintLock();
        bool shiftHeld = Keyboard.current.leftShiftKey.isPressed;
        // 다리 부상(sprintAllowed) 또는 심박수 잠금 중 하나라도 걸려 있으면 달리기 불가.
        bool isRunning = sprintAllowed && !heartSprintLocked && shiftHeld && isMoving;
        if (heartSprintLocked && shiftHeld && isMoving)
            HudToast.Show("TooExhausted", "TOO EXHAUSTED", 1.0f, 150f, 0);
        IsSprinting = isRunning;
        float currentSpeed = (isRunning ? runSpeed : moveSpeed) * speedMultiplier;

        if (sprintIndicator != null)
            sprintIndicator.SetActive(isRunning);

        Vector3 move = transform.right * input.x + transform.forward * input.y;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0)
                verticalVelocity = -2f;

        }

        verticalVelocity += gravity * Time.deltaTime;
        move *= currentSpeed;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    void Look()
    {
        if (Mouse.current == null || cameraPivot == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraRotationX -= mouseY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -80f, 80f);
        cameraPivot.localRotation = Quaternion.Euler(cameraRotationX, 0f, 0f);
    }

    void SetCameraMode()
    {
        if (playerCamera == null)
            return;

        playerCamera.transform.localPosition = isThirdPerson ? thirdPersonPosition : firstPersonPosition;
        playerCamera.transform.localRotation = Quaternion.identity;
    }
}
