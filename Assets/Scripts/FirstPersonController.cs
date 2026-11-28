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

    public bool ControlsEnabled => controlsEnabled;

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
        CameraSwitch();
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;

        if (!enabled && sprintIndicator != null)
            sprintIndicator.SetActive(false);
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
        bool isRunning = Keyboard.current.leftShiftKey.isPressed && isMoving;
        float currentSpeed = isRunning ? runSpeed : moveSpeed;

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

    void CameraSwitch()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            isThirdPerson = !isThirdPerson;
            SetCameraMode();
        }
    }

    void SetCameraMode()
    {
        if (playerCamera == null)
            return;

        playerCamera.transform.localPosition = isThirdPerson ? thirdPersonPosition : firstPersonPosition;
        playerCamera.transform.localRotation = Quaternion.identity;
    }
}
