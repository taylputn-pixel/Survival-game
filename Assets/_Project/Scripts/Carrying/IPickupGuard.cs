namespace GrandmasHouse.Carrying
{
    // Lets something on an item say "not right now" to picking it up, like a
    // canner sitting on a lit burner (you lift it off from its station).
    public interface IPickupGuard
    {
        bool AllowPickup(PlayerHands hands);
    }
}
