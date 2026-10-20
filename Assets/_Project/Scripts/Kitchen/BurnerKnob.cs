using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A knob on the stove front. Look at it and press Interact to turn its
    // burner on or off, without stepping up to the stove.
    public class BurnerKnob : MonoBehaviour, IInteractable, GrandmasHouse.Interaction.ISharedUse
    {
        [SerializeField] Burner burner;

        public bool CanInteract(PlayerHands hands) => true;

        public string PromptVerb(PlayerHands hands) => burner.On ? "Turn the burner off" : "Turn the burner on";

        public void Interact(PlayerHands hands) => burner.Toggle();
    }
}
