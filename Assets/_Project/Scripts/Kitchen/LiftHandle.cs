using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A pot handle. Grab it to lift the pot, even off a lit burner, while the
    // rest of the pot still opens its station.
    public class LiftHandle : MonoBehaviour, IInteractable
    {
        Pickup pickup;

        void Awake() => pickup = GetComponentInParent<Pickup>();

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty;

        public string PromptVerb(PlayerHands hands) => "Pick up " + pickup.DisplayName.ToLowerInvariant();

        public void Interact(PlayerHands hands) => hands.Handle(pickup);
    }
}
