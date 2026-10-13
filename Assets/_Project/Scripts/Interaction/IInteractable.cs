using GrandmasHouse.Carrying;

namespace GrandmasHouse.Interaction
{
    // Anything the player can use by looking at it and pressing Interact:
    // items to pick up, and later baskets, stations, doors, and notes.
    public interface IInteractable
    {
        bool CanInteract(PlayerHands hands);
        void Interact(PlayerHands hands);
        // The word shown next to the button icon, like "Pick up" or "Read".
        string PromptVerb(PlayerHands hands) => "Use";
    }
}
