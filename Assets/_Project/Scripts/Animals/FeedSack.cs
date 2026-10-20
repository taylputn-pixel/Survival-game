using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // A sack of chicken feed, a scoop at a time. When it's empty, more has
    // to come from trade.
    public class FeedSack : MonoBehaviour, IItemState
    {
        [SerializeField] int scoops = 24;

        public int Scoops => scoops;
        public bool HasFeed => scoops > 0;
        public int Capacity { get; private set; }

        void Awake() => Capacity = scoops;

        public void TakeScoop() => scoops = Mathf.Max(0, scoops - 1);

        public string Capture() => scoops.ToString();

        public void Restore(string json) => scoops = int.Parse(json);
    }
}
