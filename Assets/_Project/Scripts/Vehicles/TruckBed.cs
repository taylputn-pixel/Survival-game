using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Kitchen;
using UnityEngine;

namespace GrandmasHouse.Vehicles
{
    // The pickup's bed. Look at it holding a log (or firewood) to load it;
    // what's in the bed rides along wherever the truck goes. Take things out
    // by picking them up like anything else.
    public class TruckBed : MonoBehaviour, IInteractable, ISharedUse
    {
        [SerializeField] Transform[] spots;

        static bool Hauls(Pickup item) => item.GetComponent<Log>() != null || item.GetComponent<Firewood>() != null;

        Transform FreeSpot()
        {
            foreach (var spot in spots) if (spot.GetComponentInChildren<Pickup>() == null) return spot;
            return null;
        }

        public int Load
        {
            get
            {
                int n = 0;
                foreach (var spot in spots) if (spot.GetComponentInChildren<Pickup>() != null) n++;
                return n;
            }
        }

        public bool CanInteract(PlayerHands hands) => !hands.IsEmpty && Hauls(hands.Held) && FreeSpot() != null;
        public string PromptVerb(PlayerHands hands) => "Load it in the truck";

        public void Interact(PlayerHands hands)
        {
            Transform spot = FreeSpot();
            Pickup item = hands.TakeHeld();
            item.AttachTo(spot, Vector3.up * item.RestHalfHeight, item.RestTilt, 0.3f, collidersOn: true);
        }
    }
}
