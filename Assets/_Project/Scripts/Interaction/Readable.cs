using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Interaction
{
    // A note, card, or page of Grandma's lying somewhere in the house. Look
    // at it and press Interact to read it; it stays where it is.
    public class Readable : MonoBehaviour, IInteractable
    {
        [SerializeField] NoteData note;
        [SerializeField] NoteReader reader;
        [SerializeField] Discoveries discoveries;

        public bool CanInteract(PlayerHands hands) => !reader.IsOpen;

        public string PromptVerb(PlayerHands hands) => "Read";

        public void Interact(PlayerHands hands)
        {
            discoveries.Record(note);
            reader.Show(note, hands);
        }
    }
}
