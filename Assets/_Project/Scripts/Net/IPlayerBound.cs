using GrandmasHouse.Carrying;

namespace GrandmasHouse.Net
{
    // Screen things that follow the player on this computer (the prompts,
    // the reticle, the menus). They're handed that player when it appears.
    public interface IPlayerBound
    {
        void Bind(PlayerHands hands);
    }
}
