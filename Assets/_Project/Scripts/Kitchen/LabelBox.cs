using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A little box of Grandma's jar labels and a pot of paste on the kitchen
    // table. Bring a sealed jar (or two) and put a label on each.
    public class LabelBox : MonoBehaviour, IInteractable
    {
        public bool CanInteract(PlayerHands hands) => Unlabeled(hands) != null;

        public string PromptVerb(PlayerHands hands) => "Put on a label";

        public void Interact(PlayerHands hands) => Unlabeled(hands).PutOnLabel();

        // The jar in your main hand first, then the one in your other hand.
        static JarContents Unlabeled(PlayerHands hands)
        {
            foreach (var held in new[] { hands.Held, hands.Extra })
            {
                var jar = held != null ? held.GetComponent<JarContents>() : null;
                if (jar != null && jar.CanLabel)
                {
                    return jar;
                }
            }
            return null;
        }
    }
}
