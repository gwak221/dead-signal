using DeadSignal.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙이면 마우스를 올릴 때 / 왼쪽 클릭할 때 소리를 낸다. 비활성(interactable = false) 버튼은 소리 없음.
/// 어떤 버튼에도 직접 붙여 쓸 수 있다.
/// </summary>
[DisallowMultipleComponent]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Tooltip("비우면 UISounds 기본값 (ui_hover)")] public string hoverSound = "";
    [Tooltip("비우면 UISounds 기본값 (ui_click)")] public string clickSound = "";

    Selectable selectable;

    void Awake() => selectable = GetComponent<Selectable>();

    bool CanPlay => selectable == null || selectable.IsInteractable();

    string Hover => !string.IsNullOrEmpty(hoverSound) ? hoverSound : UISounds.Instance != null ? UISounds.Instance.hoverSound : "ui_hover";
    string Click => !string.IsNullOrEmpty(clickSound) ? clickSound : UISounds.Instance != null ? UISounds.Instance.clickSound : "ui_click";

    public void OnPointerEnter(PointerEventData e)
    {
        if (CanPlay) AudioManager.Play(Hover);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (CanPlay && e.button == PointerEventData.InputButton.Left) AudioManager.Play(Click);
    }
}
