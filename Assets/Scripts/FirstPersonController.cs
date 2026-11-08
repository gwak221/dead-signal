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

    public bool ControlsEnabled => controlsEnabled;
    public bool IsThirdPerson => isThirdPerson;
    public bool SprintAllowed => sprintAllowed;
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
            return;

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
        bool isRunning = sprintAllowed && Keyboard.current.leftShiftKey.isPressed && isMoving;
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
