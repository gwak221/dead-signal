using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PipeTile : MonoBehaviour, IPointerClickHandler
{
    [Header("Pipe Info")]
    public string pipeType;
    public int currentRotation = 0; // 0, 90, 180, 270
    public int gridX;
    public int gridY; // 0이 맨 위
    public bool rotationLocked;

    [Header("Visuals")]
    public Image pipeImage;
    public Sprite normalSprite;
    public Sprite chargedSprite;
    public float spinSmoothTime = 0.12f;

    static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0)
    };

    static readonly Color LockedColor = new Color(0.62f, 0.7f, 0.82f, 1f);

    float visualZ;
    float targetZ;
    float spinVelocity;
    bool waitingForSettle;
    bool shaking;
    Vector2 shakeOrigin;

    void Awake()
    {
        visualZ = targetZ = -currentRotation;
    }

    void Update()
    {
        if (Mathf.Abs(visualZ - targetZ) <= 0.2f)
        {
            if (!waitingForSettle)
                return;

            visualZ = targetZ;
            spinVelocity = 0f;
            ApplyVisual();
            waitingForSettle = false;
            PuzzleManager manager = GetComponentInParent<PuzzleManager>();
            if (manager != null)
                manager.CheckConnection();
            return;
        }

        visualZ = Mathf.SmoothDamp(visualZ, targetZ, ref spinVelocity, spinSmoothTime, 720f);
        ApplyVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (rotationLocked)
        {
            PlayLockedFeedback();
            return;
        }

        RotatePipe();
    }

    public void RotatePipe()
    {
        if (rotationLocked)
            return;

        currentRotation = (currentRotation + 90) % 360;
        targetZ -= 90f;
        waitingForSettle = true;
    }

    public void SetRotationImmediate(int rotation)
    {
        currentRotation = ((rotation % 360) + 360) % 360;
        visualZ = targetZ = -currentRotation;
        spinVelocity = 0f;
        waitingForSettle = false;
        ApplyVisual();
    }

    public void SetLocked(bool locked)
    {
        rotationLocked = locked;
        if (pipeImage == null)
            return;

        pipeImage.color = locked ? LockedColor : Color.white;
    }

    public void SetCharged(bool charged)
    {
        if (pipeImage == null)
            return;

        pipeImage.sprite = charged && chargedSprite != null ? chargedSprite : normalSprite;
    }

    public bool HasOpening(int direction)
    {
        return Covers(TurnCount(), 1 << direction);
    }

    public bool Covers(int turns, int requiredMask)
    {
        int rotated = RotateMask(OpeningMask(), turns);
        return (rotated & requiredMask) == requiredMask;
    }

    public int OpeningMask()
    {
        switch (NormalizedType())
        {
            case "2way":
                return (1 << 0) | (1 << 2);
            case "2way_cha":
                return (1 << 0) | (1 << 3);
            case "3way":
                return (1 << 0) | (1 << 1) | (1 << 2);
            case "4way":
                return (1 << 0) | (1 << 1) | (1 << 2) | (1 << 3);
            default:
                return 0;
        }
    }

    public string NormalizedType()
    {
        if (string.IsNullOrEmpty(pipeType))
            return "";
        return pipeType.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "_");
    }

    public Vector2Int Step(int direction)
    {
        return new Vector2Int(gridX, gridY) + Directions[direction];
    }

    public static int Opposite(int direction)
    {
        return (direction + 2) % 4;
    }

    public static int RotateMask(int mask, int turns)
    {
        int rotated = 0;
        for (int direction = 0; direction < 4; direction++)
        {
            if ((mask & (1 << direction)) == 0)
                continue;
            rotated |= 1 << ((direction + turns) % 4);
        }
        return rotated;
    }

    int TurnCount()
    {
        return ((currentRotation % 360) + 360) % 360 / 90;
    }

    void ApplyVisual()
    {
        transform.localRotation = Quaternion.Euler(0, 0, visualZ);
    }

    void PlayLockedFeedback()
    {
        if (shaking)
            return;
        StartCoroutine(Shake());
    }

    IEnumerator Shake()
    {
        shaking = true;
        RectTransform rect = transform as RectTransform;
        shakeOrigin = rect.anchoredPosition;
        float time = 0f;
        const float duration = 0.16f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float damp = 1f - time / duration;
            float offset = Mathf.Sin(time * 72f) * 7f * damp;
            rect.anchoredPosition = shakeOrigin + new Vector2(offset, 0f);
            yield return null;
        }

        rect.anchoredPosition = shakeOrigin;
        shaking = false;
    }
}
