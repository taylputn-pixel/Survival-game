using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A rack against the house for split firewood, kept dry under the eaves.
    // Holding firewood, stack it (Interact on the rack, or the second action
    // anywhere on the pile); take pieces off like anything else. The pieces
    // are ordinary items sitting in its spots, so they save and share as
    // they are.
    public class Woodpile : MonoBehaviour, IInteractable, IAltInteractable, ISharedUse
    {
        [Tooltip("Where pieces lie, bottom row first.")]
        [SerializeField] Transform[] spots;

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var spot in spots) if (spot.GetComponentInChildren<Firewood>() != null) n++;
                return n;
            }
        }
        public int Capacity => spots.Length;

        Transform FreeSpot()
        {
            foreach (var spot in spots) if (spot.GetComponentInChildren<Pickup>() == null) return spot;
            return null;
        }

        static bool HoldingWood(PlayerHands hands) => !hands.IsEmpty && hands.Held.GetComponent<Firewood>() != null;

        public bool CanInteract(PlayerHands hands) => HoldingWood(hands) && FreeSpot() != null;
        public string PromptVerb(PlayerHands hands) => "Stack it on the woodpile";

        public void Interact(PlayerHands hands)
        {
            Transform spot = FreeSpot();
            if (!HoldingWood(hands) || spot == null) return;
            Pickup piece = hands.TakeHeld();
            piece.AttachTo(spot, Vector3.zero, Quaternion.identity, 0.25f, collidersOn: true);
            Sfx.Play(SoundId.SetDown, spot.position, 0.8f);
        }

        public bool CanAltInteract(PlayerHands hands) => CanInteract(hands);
        public void AltInteract(PlayerHands hands) => Interact(hands);
        public string AltVerb(PlayerHands hands) => "Stack it on the woodpile";
    }
}
