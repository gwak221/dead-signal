public interface IInteractable
{
    string GetInteractionText();

    void Interact(PlayerInteraction player);
}