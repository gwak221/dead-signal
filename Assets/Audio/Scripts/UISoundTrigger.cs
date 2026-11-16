using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 버튼에 붙이면 마우스를 올릴 때 / 누를 때 소리를 낸다. 어떤 버튼에도 붙일 수 있다.
/// 메뉴(일시정지) 중에도 들리게 하려면 cue의 ignoreListenerPause를 켠다.
/// 버튼이 비활성(interactable = false)이면 소리를 내지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class UISoundTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public SoundCue hoverCue;
    public SoundCue clickCue;

    Selectable selectable;
    SoundEmitter emitter;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        emitter = GetComponent<SoundEmitter>();
        if (emitter == null) emitter = gameObject.AddComponent<SoundEmitter>();
    }

    bool CanPlay => selectable == null || selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData e)
    {
        if (CanPlay) emitter.Play(hoverCue);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (CanPlay && e.button == PointerEventData.InputButton.Left) emitter.Play(clickCue);
    }
}
