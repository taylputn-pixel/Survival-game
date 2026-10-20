using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Food
{
    // Produce you can eat as it is: a tomato, a cucumber. Rotten food makes
    // you sick instead of feeding you.
    public class FreshFood : MonoBehaviour, IEdible
    {
        [Tooltip("Hunger it fills, out of 100. A day's eating is about 100.")]
        [Range(0f, 100f)] [SerializeField] float nutrition = 8f;

        Perishable perishable;

        void Awake() => perishable = GetComponent<Perishable>();

        public bool CanEat => true;
        public float Nutrition => nutrition;

        public float SickHours(SurvivalSettings settings) =>
            perishable != null && perishable.IsRotten ? settings.spoiledSickHours : 0f;

        public void Eaten(PlayerHands hands)
        {
            var pickup = GetComponent<Pickup>();
            if (pickup != null)
            {
                hands.Take(pickup);
            }
            Destroy(gameObject);
        }
    }
}
