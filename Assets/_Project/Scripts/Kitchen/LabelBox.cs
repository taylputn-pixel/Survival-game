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

        // The jar you picked up last first, then the one in your other hand.
        static JarContents Unlabeled(PlayerHands hands)
        {
            for (int i = hands.Carried.Count - 1; i >= 0; i--)
            {
                var jar = hands.Carried[i].GetComponent<JarContents>();
                if (jar != null && jar.CanLabel)
                {
                    return jar;
                }
            }
            return null;
        }
    }
}
