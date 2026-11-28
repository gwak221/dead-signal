using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class PasswordUIController : MonoBehaviour
{
    public static PasswordUIController Instance { get; private set; }

    [Header("Scene UI")]
    public GameObject panel;
    public TMP_InputField inputField;
    public TMP_Text statusText;
    public Button confirmButton;

    private PasswordDoor activeDoor;
    private FirstPersonController playerController;
    private PlayerInteraction playerInteraction;
    private GameObject crosshair;
    private bool crosshairWasActive;
    private bool submitting;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        playerController = FindFirstObjectByType<FirstPersonController>();
        playerInteraction = FindFirstObjectByType<PlayerInteraction>();
        crosshair = GameObject.Find("Crosshair");

        if (confirmButton != null)
            confirmButton.onClick.AddListener(Submit);

        if (panel != null)
            panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Submit);

        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf || Keyboard.current == null)
            return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            Submit();
    }

    public void Open(PasswordDoor door)
    {
        if (door == null || panel == null || inputField == null)
            return;

        activeDoor = door;
        submitting = false;

        if (statusText != null)
            statusText.text = "";

        inputField.SetTextWithoutNotify("");
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        if (playerController != null)
            playerController.SetControlsEnabled(false);

        if (playerInteraction != null)
            playerInteraction.SetInteractionEnabled(false);

        if (crosshair != null)
        {
            crosshairWasActive = crosshair.activeSelf;
            crosshair.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        StartCoroutine(FocusInputNextFrame());
    }

    public void Close()
    {
        if (panel == null || !panel.activeSelf)
            return;

        panel.SetActive(false);
        activeDoor = null;
        submitting = false;

        if (playerController != null)
            playerController.SetControlsEnabled(true);

        if (playerInteraction != null)
            playerInteraction.SetInteractionEnabled(true);

        if (crosshair != null)
            crosshair.SetActive(crosshairWasActive);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Submit()
    {
        if (submitting || activeDoor == null || inputField == null)
            return;

        string entered = inputField.text.Trim();
        if (entered.Length == 0)
            return;

        submitting = true;

        if (activeDoor.TryPassword(entered))
        {
            Close();
            return;
        }

        if (statusText != null)
            statusText.text = "Wrong password";

        inputField.SetTextWithoutNotify("");
        submitting = false;
        inputField.ActivateInputField();
    }

    IEnumerator FocusInputNextFrame()
    {
        yield return null;
        inputField.Select();
        inputField.ActivateInputField();
    }
}
