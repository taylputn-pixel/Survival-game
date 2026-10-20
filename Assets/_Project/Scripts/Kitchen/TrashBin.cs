using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // The kitchen trash bin. Bring a jar (sealed or not), the cooking pot,
    // or a handful of cut pieces and throw out what's in it: a "Don't eat!"
    // jar, a pot of something that went wrong. Jars and the pot come back
    // empty, ready to use again.
    public class TrashBin : MonoBehaviour, IInteractable, GrandmasHouse.Interaction.ISharedUse
    {
        [Tooltip("The open top of the bin, where things are tipped out.")]
        [SerializeField] Transform opening;
        [SerializeField] float tipSeconds = 0.3f;
        [SerializeField] float pourSeconds = 0.5f;
        [SerializeField] float tipAngle = 125f;
        [SerializeField] float holdHeight = 0.18f;

        bool busy;

        public bool CanInteract(PlayerHands hands)
        {
            if (busy || hands.IsEmpty)
            {
                return false;
            }
            if (Handful.Held(hands) != null)
            {
                return true;
            }
            var emptyable = hands.Held.GetComponent<IEmptyable>();
            return emptyable != null && emptyable.HasContents;
        }

        public string PromptVerb(PlayerHands hands) => "Throw out";

        public void Interact(PlayerHands hands)
        {
            if (Handful.Held(hands) is Handful handful)
            {
                // Tossed in; nothing to hand back.
                foreach (var piece in handful.Take(handful.Count))
                {
                    Destroy(piece.gameObject);
                }
                handful.DoneWith(hands);
                Sfx.Play(SoundId.SliceDrop, opening.position);
                return;
            }
            StartCoroutine(ThrowOut(hands));
        }

        IEnumerator ThrowOut(PlayerHands hands)
        {
            busy = true;
            hands.Controller.InputEnabled = false;
            Pickup item = hands.TakeHeld();
            var emptyable = item.GetComponent<IEmptyable>();
            yield return TipOut.Over(hands, item.transform, opening.position, holdHeight, tipAngle, tipSeconds, pourSeconds, tipped =>
            {
                if (tipped)
                {
                    Sfx.Play(SoundId.SliceDrop, opening.position, 1f, 0.1f);
                }
                else
                {
                    emptyable.Empty();
                }
                emptyable.SetPouring(tipped);
            });
            hands.Give(item);
            hands.Controller.InputEnabled = true;
            busy = false;
        }
    }
}
