using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // On the counter: holding the canning box, set up for canning here.
    public class CanningCounter : MonoBehaviour, IInteractable, ISharedUse
    {
        public bool CanInteract(PlayerHands hands) => CanningKit.Instance != null && !CanningKit.Instance.IsSetUp && CanningKit.IsBox(hands.Held);
        public string PromptVerb(PlayerHands hands) => "Set up for canning";
        public void Interact(PlayerHands hands) => CanningKit.Instance.SetUp(hands);
    }
}
