using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Net;
using UnityEngine;

namespace GrandmasHouse.Food
{
    // Produce you can eat as it is: a tomato, a cucumber. Rotten food makes
    // you sick instead of feeding you, and so does raw meat that hasn't been
    // smoked, salted or dried (cook it in the pot instead).
    public class FreshFood : MonoBehaviour, IEdible
    {
        [Tooltip("Hunger it fills, out of 100. A day's eating is about 100.")]
        [Range(0f, 100f)] [SerializeField] float nutrition = 8f;
        [Tooltip("Raw meat: makes you sick eaten as it is, unless it's been cured.")]
        [SerializeField] bool meat;

        Perishable perishable;

        void Awake() => perishable = GetComponent<Perishable>();

        public bool CanEat => true;
        public float Nutrition => nutrition;
        public bool IsMeat => meat;

        public float SickHours(SurvivalSettings settings)
        {
            if (perishable != null && perishable.IsRotten) return settings.spoiledSickHours;
            if (meat && (perishable == null || !perishable.Cured)) return settings.spoiledSickHours;
            return 0f;
        }

        public void Eaten(PlayerHands hands)
        {
            var pickup = GetComponent<Pickup>();
            if (pickup != null)
            {
                hands.Take(pickup);
            }
            if (TryGetComponent(out ItemNet net) && net.Live)
            {
                net.Consume();
                return;
            }
            Destroy(gameObject);
        }
    }
}
