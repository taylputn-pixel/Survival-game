using UnityEngine;

namespace GrandmasHouse.Data
{
    public enum NoteKind { Note, RecipeCard, DiaryPage, Book }

    // Something to read: a note, a recipe card, or a diary page in Grandma's
    // voice (short and practical, sometimes with a feeling slipped in), or a
    // book from the shelf, read a page at a time.
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

        [Tooltip("A book's pages, read in order. Leave empty for a single-page note.")]
        [TextArea(6, 20)] public string[] pages;

        public string Body => recipe != null && !string.IsNullOrEmpty(recipe.cardText) ? recipe.cardText : body;
        public int PageCount => pages != null && pages.Length > 0 ? pages.Length : 1;
        public string Page(int index) => pages != null && pages.Length > 0 ? pages[index] : Body;
    }
}
