using UnityEngine;

public class BatteryPickup : MonoBehaviour
{
    public static BatteryPickup Create(Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Battery";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.18f, 0.28f, 0.18f);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.15f, 0.85f, 0.35f);

        return go.AddComponent<BatteryPickup>();
    }

    public static void SpawnClusterAtCenter(int count)
    {
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            var pos = new Vector3(Mathf.Cos(angle) * 0.45f, 0.28f, Mathf.Sin(angle) * 0.45f);
            Create(pos);
        }
    }

    public bool Collect(PlayerInteraction player)
    {
        if (player == null || player.Inventory == null)
            return false;

        return player.Inventory.TryAddWorldItem(InventoryItemType.Battery, gameObject);
    }
}
