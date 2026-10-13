using UnityEngine;

namespace GrandmasHouse.Data
{
    public enum NoteKind { Note, RecipeCard, DiaryPage }

    // Something Grandma wrote: a note, a recipe card, or a diary page. Her
    // voice is short and practical, sometimes with a feeling slipped in.
    [CreateAssetMenu(fileName = "Note", menuName = "Grandma's House/Note")]
    public class NoteData : ScriptableObject
    {
        public NoteKind kind = NoteKind.Note;
        public string title;
        [Tooltip("For a recipe card, the card text comes from the recipe instead.")]
        public RecipeData recipe;
        [TextArea(6, 20)] public string body;
        [Tooltip("Her handwriting in the margin: how she changed it, or a thought.")]
        [TextArea(2, 8)] public string marginNote;

        public string Body => recipe != null && !string.IsNullOrEmpty(recipe.cardText) ? recipe.cardText : body;
    }
}
