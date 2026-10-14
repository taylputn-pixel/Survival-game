using GrandmasHouse.Carrying;

namespace GrandmasHouse.Interaction
{
    // A second thing you can do to what you're looking at, on its own button
    // (Pick up), for things whose Interact already does something else, like
    // a pot on the stove that opens its station.
    public interface IAltInteractable
    {
        bool CanAltInteract(PlayerHands hands);
        void AltInteract(PlayerHands hands);
        string AltVerb(PlayerHands hands) => "Pick up";
    }
}
