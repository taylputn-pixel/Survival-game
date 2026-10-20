using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Interaction
{
    // A note, card, or book of Grandma's lying somewhere in the house. Look
    // at it and press Interact to read it. Notes and books stay where they
    // are; a recipe card is taken: it goes into the journal, so once anyone
    // in the household has read it, it's gone from where it lay.
    public class Readable : MonoBehaviour, IInteractable, GrandmasHouse.Interaction.ISharedUse
    {
        [SerializeField] NoteData note;
        [SerializeField] NoteReader reader;
        [SerializeField] Discoveries discoveries;

        bool Taken => note != null && note.kind == NoteKind.RecipeCard && discoveries.HasFound(note);

        void Awake() => discoveries.Changed += Refresh;
        void Start() => Refresh();
        void OnDestroy() { if (discoveries != null) discoveries.Changed -= Refresh; }

        // Gone once it's been taken (it comes back if a save from before is loaded).
        void Refresh() => gameObject.SetActive(!Taken);

        public bool CanInteract(PlayerHands hands) => !reader.IsOpen;

        public string PromptVerb(PlayerHands hands) => note != null && note.kind == NoteKind.RecipeCard ? "Read and keep" : "Read";

        public void Interact(PlayerHands hands)
        {
            reader.Show(note, hands);
            discoveries.Record(note);
        }
    }
}
