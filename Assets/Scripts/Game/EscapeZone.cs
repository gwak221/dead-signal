using UnityEngine;

/// <summary>
/// EscapeWall 바깥쪽 Trigger. 비콘 4개가 모두 활성화된 상태에서 플레이어가 들어오면 클리어.
/// (문이 열리는 것만으로는 클리어되지 않는다.)
/// </summary>
[RequireComponent(typeof(Collider))]
public class EscapeZone : MonoBehaviour
{
    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other) => Check(other);
    void OnTriggerStay(Collider other) => Check(other);

    static void Check(Collider other)
    {
        if (!GameProgress.EscapeUnlocked || GameProgress.HasEscaped) return;
        if (other.GetComponentInParent<FirstPersonController>() == null) return;
        GameProgress.TriggerEscape();
    }
}
