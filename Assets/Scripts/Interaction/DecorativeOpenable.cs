using UnityEngine;

/// <summary>
/// 장식용(쓰러져 있거나 잔해에 묻혀 실제로 열 수 없는) Openable / 약장에 붙이는 표시.
/// 붙은 오브젝트(와 그 자식)는 E 상호작용·열기/닫기·프롬프트·Aim Outline·Loot QTE 대상에서 모두 빠진다.
/// Collider / Mesh 는 그대로 유지되므로 물리적 장애물/시각 요소는 변하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class DecorativeOpenable : MonoBehaviour
{
    /// <summary>이 컴포넌트(또는 부모)가 장식용으로 표시돼 있는가.</summary>
    public static bool IsDecorative(Component c) => c != null && c.GetComponentInParent<DecorativeOpenable>(true) != null;
}
