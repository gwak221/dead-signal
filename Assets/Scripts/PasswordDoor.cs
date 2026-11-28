using UnityEngine;
using System.Collections;

public class PasswordDoor : MonoBehaviour, IInteractable
{
    public Transform doorPivot;
    public string correctPassword = "1234";
    public float openAngle = 100f;
    public float openSpeed = 2f;

    private bool isUnlocked = false;
    private bool isOpen = false;
    private bool isMoving = false;
    private Quaternion closedRotation;
    private Quaternion openRotation;

    void Start()
    {
        if (doorPivot == null)
            return;

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public string GetInteractionText()
    {
        if (isMoving)
            return "";

        if (!isUnlocked)
            return "E : Enter Password";

        return isOpen ? "E : Close Door" : "E : Open Door";
    }

    public void Interact(PlayerInteraction player)
    {
        if (isMoving)
            return;

        if (!isUnlocked)
        {
            if (PasswordUIController.Instance != null)
                PasswordUIController.Instance.Open(this);
            return;
        }

        StartCoroutine(ToggleDoor());
    }

    public bool TryPassword(string value)
    {
        if (value != correctPassword)
            return false;

        isUnlocked = true;

        if (!isOpen && !isMoving)
            StartCoroutine(ToggleDoor());

        return true;
    }

    IEnumerator ToggleDoor()
    {
        if (doorPivot == null)
            yield break;

        isMoving = true;
        Quaternion startRotation = doorPivot.localRotation;
        Quaternion targetRotation = isOpen ? closedRotation : openRotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            doorPivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            yield return null;
        }

        doorPivot.localRotation = targetRotation;
        isOpen = !isOpen;
        isMoving = false;
    }
}
