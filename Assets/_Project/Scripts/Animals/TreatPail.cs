using GrandmasHouse.Net;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // A pail by the garden. Hornworms picked off the tomatoes go in it
    // instead of being thrown away; tip it into the feeder as a treat for
    // the hens.
    public class TreatPail : MonoBehaviour, IItemState
    {
        [Tooltip("Shown inside the pail once there are worms in it.")]
        [SerializeField] GameObject wormsInside;

        public int Worms { get; private set; }

        void Update()
        {
            if (wormsInside != null) wormsInside.SetActive(Worms > 0);
        }

        public void AddWorm()
        {
            Worms++;
            if (TryGetComponent(out ItemNet net)) net.Share();
        }

        public void Empty() => Worms = 0;

        // The pail nearest a garden bed, if there is one close by.
        public static TreatPail Near(Vector3 place, float within = 12f)
        {
            TreatPail best = null;
            float bestDistance = within;
            foreach (var pail in FindObjectsByType<TreatPail>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(pail.transform.position, place);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = pail;
                }
            }
            return best;
        }

        public string Capture() => Worms.ToString();

        public void Restore(string json) => Worms = int.Parse(json);
    }
}
