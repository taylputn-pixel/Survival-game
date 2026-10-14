using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // The canner's lid. Look at it with empty hands to put it on or take it off.
    public class CannerLid : MonoBehaviour, IInteractable
    {
        [SerializeField] Canner canner;

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty && canner.CanMoveLid;

        public string PromptVerb(PlayerHands hands) => canner.LidOn ? "Take the lid off" : "Put the lid on";

        public void Interact(PlayerHands hands) => canner.ToggleLid();
    }
}
