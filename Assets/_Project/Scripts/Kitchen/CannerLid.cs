using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // The canner's lid. Look at it with empty hands to put it on or take it off.
    // The prompt says when the canner needs more water, so one that won't
    // boil tells you why.
    public class CannerLid : MonoBehaviour, IInteractable
    {
        [SerializeField] Canner canner;

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty && canner.CanMoveLid;

        public string PromptVerb(PlayerHands hands)
        {
            string verb = canner.LidOn ? "Take the lid off" : "Put the lid on";
            return canner.NeedsWater ? verb + " (needs more water)" : verb;
        }

        public void Interact(PlayerHands hands) => canner.ToggleLid();
    }
}
