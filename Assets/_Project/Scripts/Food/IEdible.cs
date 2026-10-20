using GrandmasHouse.Carrying;

namespace GrandmasHouse.Food
{
    // Something you can eat from your hands with the Pick up button.
    public interface IEdible
    {
        bool CanEat { get; }
        // Hunger it fills, out of 100.
        float Nutrition { get; }
        // Hours of sickness it gives instead of feeding you (0 if it's fine).
        float SickHours(Data.SurvivalSettings settings);
        // Called once it's eaten: gone, or an empty jar left in your hand.
        void Eaten(PlayerHands hands);
    }
}
