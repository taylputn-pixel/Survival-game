using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A cork hot pad on the counter beside the packing towel. Set a hot pot
    // here and the brine is right at hand while you pack jars.
    public class HotPad : MonoBehaviour, IInteractable
    {
        [Tooltip("Where the pot sits.")]
        [SerializeField] Transform spot;
        [SerializeField] float glideSeconds = 0.35f;

        public bool CanInteract(PlayerHands hands)
        {
            return spot.childCount == 0 && !hands.IsEmpty && hands.Held.GetComponent<Cookware>() != null;
        }

        public string PromptVerb(PlayerHands hands) => "Set on the hot pad";

        public void Interact(PlayerHands hands)
        {
            Pickup item = hands.TakeHeld();
            item.AttachTo(spot, Vector3.zero, Quaternion.identity, glideSeconds, collidersOn: true);
        }
    }
}
