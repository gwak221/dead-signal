public interface IInteractable
{
    string GetInteractionText();

    void Interact(PlayerInteraction player);
}

/// <summary>
/// 줍기 전용 표식. 이 인터페이스를 가진 IInteractable 은 F 로만 줍고, E 로는 반응하지 않는다.
/// (E = 문/가구/비콘/은신 등 환경 상호작용)
/// </summary>
public interface IPickupable { }
