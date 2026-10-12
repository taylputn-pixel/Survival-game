using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // A clock in the world that shows the time. The game never shows a clock
    // on screen, so this is how you check the hour.
    public class WallClock : MonoBehaviour
    {
        [SerializeField] DayClock clock;
        [Tooltip("Rotates around its local Z axis, pointing up at 12. The clock's local +Z faces the viewer.")]
        [SerializeField] Transform hourHand;
        [SerializeField] Transform minuteHand;

        void LateUpdate()
        {
            float hours = clock.Hour % 12f;
            float minutes = (clock.Hour % 1f) * 60f;
            // Seen from the +Z side, a positive turn around Z reads as clockwise.
            hourHand.localRotation = Quaternion.Euler(0f, 0f, hours * 30f);
            minuteHand.localRotation = Quaternion.Euler(0f, 0f, minutes * 6f);
        }
    }
}
