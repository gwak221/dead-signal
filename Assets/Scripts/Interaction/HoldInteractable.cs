using System;
using UnityEngine;

public class HoldInteractable : MonoBehaviour
{
    [SerializeField] string displayName = "장치";
    [SerializeField] float holdDuration = 3f;
    [SerializeField] Renderer indicatorRenderer;
    [SerializeField] Color noBatteryColor = new Color(0.25f, 0.25f, 0.25f);
    [SerializeField] Color idleColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] Color holdingColor = new Color(0.9f, 0.7f, 0.1f);
    [SerializeField] Color doneColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] bool requiresBattery = true;
    [SerializeField] bool hasBattery;
    [SerializeField] GameObject insertedBatteryVisual;

    float _holdTime;
    bool _activated;

    public string DisplayName => displayName;
    public bool IsActivated => _activated;
    public bool HasBattery => !requiresBattery || hasBattery;
    public bool NeedsBattery => requiresBattery && !hasBattery && !_activated;
    public float HoldProgressPercent => holdDuration > 0f ? Mathf.Clamp01(_holdTime / holdDuration) * 100f : 100f;

    public event Action<HoldInteractable> OnActivated;

    void Awake()
    {
        if (indicatorRenderer == null)
            indicatorRenderer = GetComponentInChildren<Renderer>();

        if (insertedBatteryVisual != null)
            insertedBatteryVisual.SetActive(hasBattery);

        ApplyColor(HasBattery ? idleColor : noBatteryColor);
    }

    public bool TryInsertBattery()
    {
        if (!NeedsBattery)
            return false;

        hasBattery = true;
        EnsureInsertedVisual();
        insertedBatteryVisual.SetActive(true);
        ApplyColor(idleColor);
        return true;
    }

    public void AddHoldTime(float delta)
    {
        if (_activated || NeedsBattery) return;

        _holdTime += delta;
        ApplyColor(Color.Lerp(idleColor, holdingColor, HoldProgressPercent / 100f));

        if (_holdTime >= holdDuration)
            Activate();
    }

    public void ResetHold()
    {
        if (_activated) return;
        _holdTime = 0f;
        ApplyColor(HasBattery ? idleColor : noBatteryColor);
    }

    void Activate()
    {
        _activated = true;
        _holdTime = holdDuration;
        ApplyColor(doneColor);
        OnActivated?.Invoke(this);
    }

    void EnsureInsertedVisual()
    {
        if (insertedBatteryVisual != null)
            return;

        var vis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        vis.name = "InsertedBattery";
        vis.transform.SetParent(transform, false);
        vis.transform.localPosition = new Vector3(0.45f, 0.7f, 0f);
        vis.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        vis.transform.localScale = new Vector3(0.14f, 0.22f, 0.14f);

        var col = vis.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        var rend = vis.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.15f, 0.85f, 0.35f);

        insertedBatteryVisual = vis;
    }

    void ApplyColor(Color c)
    {
        if (indicatorRenderer == null) return;
        indicatorRenderer.material.color = c;
    }
}
