using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class AlternateFirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float walkSpeed = 5f;
    [SerializeField] float gravity = -20f;

    [Header("Look")]
    [SerializeField] Transform cameraRoot;
    [SerializeField] float mouseSensitivity = 0.15f;
    [SerializeField] float maxPitch = 85f;

    CharacterController _controller;
    float _verticalVelocity;
    float _pitch;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (cameraRoot == null && Camera.main != null)
            cameraRoot = Camera.main.transform;
    }

    void Start()
    {
        LockCursor();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor();

        HandleLook();
        HandleMove();
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void HandleLook()
    {
        if (cameraRoot == null || Mouse.current == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        float mouseX = delta.x * mouseSensitivity;
        float mouseY = delta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);
        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    void HandleMove()
    {
        Vector2 moveInput = ReadMoveInput();
        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y);
        if (move.sqrMagnitude > 1f)
            move.Normalize();
        move *= walkSpeed;

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;
        move.y = _verticalVelocity;

        _controller.Move(move * Time.deltaTime);
    }

    static Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;

        return new Vector2(x, y);
    }
}
