using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // The towel beside the stove where canned jars cool. Jars set on it line
    // up in its spots.
    public class CoolingTowel : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform[] spots;
        [SerializeField] float glideSeconds = 0.3f;

        public Transform FreeSpot()
        {
            foreach (var spot in spots)
            {
                if (spot.childCount == 0)
                {
                    return spot;
                }
            }
            return null;
        }

        public bool CanInteract(PlayerHands hands)
        {
            return !hands.IsEmpty && hands.Held.GetComponent<JarContents>() != null && FreeSpot() != null;
        }

        public string PromptVerb(PlayerHands hands) => "Set down to cool";

        public void Interact(PlayerHands hands)
        {
            Pickup jar = hands.TakeHeld();
            jar.AttachTo(FreeSpot(), Vector3.zero, Quaternion.identity, glideSeconds, collidersOn: true);
        }
    }
}
