using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Pantry shelves for finished jars. Bring a sealed jar and press Interact:
    // it slides into the next open spot, eye-level shelf first, left to right.
    public class PantryShelf : MonoBehaviour, IInteractable
    {
        [Tooltip("Spots for jars, in the order they fill.")]
        [SerializeField] Transform[] slots;
        [SerializeField] float glideSeconds = 0.35f;

        public int JarCount
        {
            get
            {
                int count = 0;
                foreach (var slot in slots)
                {
                    if (slot.childCount > 0)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                return false;
            }
            var jar = hands.Held.GetComponent<JarContents>();
            return jar != null && jar.IsSealed && FreeSlot() != null;
        }

        public string PromptVerb(PlayerHands hands) => "Shelve";

        public void Interact(PlayerHands hands)
        {
            Transform slot = FreeSlot();
            Pickup jar = hands.TakeHeld();
            jar.AttachTo(slot, Vector3.zero, Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f), glideSeconds, collidersOn: true);
        }

        Transform FreeSlot()
        {
            foreach (var slot in slots)
            {
                if (slot.childCount == 0)
                {
                    return slot;
                }
            }
            return null;
        }
    }
}
