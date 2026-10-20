namespace GrandmasHouse.Interaction
{
    // Marks something that works for every player in a shared game: what
    // one player does with it, everyone sees. Players who joined someone
    // else's game can only use things marked this way; the rest (stations,
    // the garden, the stove) still only work for the host for now.
    public interface ISharedUse
    {
    }
}
