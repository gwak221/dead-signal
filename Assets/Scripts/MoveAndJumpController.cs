using UnityEngine;
using UnityEngine.InputSystem;

public class MoveAndJumpController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -20f;
    public float jumpHeight = 1.5f;

    [Header("Mouse Look")]
    public Transform cameraPivot;
    public Camera playerCamera;
    public float mouseSensitivity = 0.05f;

    [Header("Camera")]
    public Vector3 firstPersonPosition = Vector3.zero;
    public Vector3 thirdPersonPosition = new Vector3(0f, 0.4f, -5f);

    private CharacterController controller;

    private float verticalVelocity;
    private float cameraRotationX;

    private bool isThirdPerson = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SetCameraMode();
    }

    void Update()
    {
        Move();
        Look();
        CameraSwitch();
    }

    void Move()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
            input.y += 1;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1;

        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        // 바닥에 있을 때
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0)
                verticalVelocity = -2f;

            // 점프
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity =
                    Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;

        move *= moveSpeed;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    void Look()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        // 좌우 = 플레이어 전체 회전
        transform.Rotate(Vector3.up * mouseX);

        // 위아래 = 카메라 Pivot 회전
        cameraRotationX -= mouseY;
        cameraRotationX = Mathf.Clamp(cameraRotationX, -80f, 80f);

        cameraPivot.localRotation =
            Quaternion.Euler(cameraRotationX, 0f, 0f);
    }

    void CameraSwitch()
    {
        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            isThirdPerson = !isThirdPerson;

            SetCameraMode();
        }
    }

    void SetCameraMode()
    {
        if (isThirdPerson)
        {
            playerCamera.transform.localPosition =
                thirdPersonPosition;
        }
        else
        {
            playerCamera.transform.localPosition =
                firstPersonPosition;
        }

        playerCamera.transform.localRotation =
            Quaternion.identity;
    }
}