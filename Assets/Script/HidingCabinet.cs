using System.Collections.Generic;
using UnityEngine;

/// <summary>Marks players inside a cabinet as hidden from MonsterChase.</summary>
public class HidingCabinet : MonoBehaviour
{
    static readonly HashSet<Transform> HiddenPlayers = new HashSet<Transform>();
    readonly HashSet<Transform> occupants = new HashSet<Transform>();

    public static bool IsHidden(Transform player)
    {
        return player != null && HiddenPlayers.Contains(player);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HiddenPlayers.Add(other.transform);
            occupants.Add(other.transform);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HiddenPlayers.Remove(other.transform);
            occupants.Remove(other.transform);
        }
    }

    void OnDisable()
    {
        foreach (var player in occupants)
            HiddenPlayers.Remove(player);
        occupants.Clear();
    }
}
