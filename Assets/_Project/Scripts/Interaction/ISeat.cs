using GrandmasHouse.Carrying;

namespace GrandmasHouse.Interaction
{
    // Somewhere you sit and are carried along: the truck's driver seat. While
    // seated your hands don't reach for things; Interact gets you out.
    public interface ISeat
    {
        string LeaveVerb { get; }
        void Leave(PlayerHands hands);
    }
}
