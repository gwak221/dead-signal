using UnityEngine;

public class EscapeSystem : MonoBehaviour
{
    [SerializeField] HoldInteractable[] devices;
    [SerializeField] EscapeDoor escapeDoor;
    [SerializeField] int requiredCount = 4;

    int _activatedCount;

    void Start()
    {
        if (FindObjectsByType<BatteryPickup>(FindObjectsSortMode.None).Length == 0)
        {
            int count = devices != null && devices.Length > 0 ? devices.Length : requiredCount;
            BatteryPickup.SpawnClusterAtCenter(count);
        }
    }

    void OnEnable()
    {
        _activatedCount = 0;
        if (devices == null) return;

        foreach (var d in devices)
        {
            if (d == null) continue;
            if (d.IsActivated)
                _activatedCount++;
            else
                d.OnActivated += OnDeviceActivated;
        }

        TryOpenDoor();
    }

    void OnDisable()
    {
        if (devices == null) return;
        foreach (var d in devices)
        {
            if (d != null)
                d.OnActivated -= OnDeviceActivated;
        }
    }

    void OnDeviceActivated(HoldInteractable _)
    {
        _activatedCount++;
        TryOpenDoor();
    }

    void TryOpenDoor()
    {
        int need = devices != null && devices.Length > 0 ? devices.Length : requiredCount;
        if (_activatedCount >= need && escapeDoor != null)
            escapeDoor.Open();
    }
}
