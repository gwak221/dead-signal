using UnityEngine;

// 여닫는 문 (캐비닛·분전함·소화기함·약품장·천장 점검구).
// 피벗이 경첩 위치. openAmount 0 = 닫힘, 1 = 열림(가져온 상태).
[ExecuteAlways]
public class HingedDoor : MonoBehaviour
{
    [Range(0f, 1f)] public float openAmount = 1f;
    public Quaternion closedLocalRotation = Quaternion.identity;
    public Quaternion openLocalRotation = Quaternion.identity;

    public void Init(Quaternion importedOpenRotation)
    {
        openLocalRotation = importedOpenRotation;
        closedLocalRotation = Quaternion.identity;
        openAmount = 1f;
    }

    void OnValidate() => Apply();

    public void Apply() =>
        transform.localRotation = Quaternion.Slerp(closedLocalRotation, openLocalRotation, openAmount);

    public void SetOpen(bool open)
    {
        openAmount = open ? 1f : 0f;
        Apply();
    }

    public void Toggle() => SetOpen(openAmount < 0.5f);
}
