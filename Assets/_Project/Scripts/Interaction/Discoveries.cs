using System;
using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Interaction
{
    // Everything of Grandma's you've found so far, and so what the journal
    // holds: the notes and recipe cards read, and each page of her books that
    // you've turned to; and your own tasting notes, the last jar of each
    // recipe eaten and what it needed. In a game with friends the household shares one
    // (a SharedState beside it keeps everyone's the same), and it's saved.
    public class Discoveries : MonoBehaviour, ISaveable
    {
        readonly List<NoteData> found = new List<NoteData>();
        // Book pages read, as "note name:page index".
        readonly List<string> pages = new List<string>();

        public IReadOnlyList<NoteData> Found => found;
        public IReadOnlyList<Tasting> Tastings => tastings;

        // How the last jar of a recipe tasted.
        [Serializable]
        public class Tasting
        {
            public string recipe;
            public Ribbon ribbon;
            public List<string> marks = new List<string>();
            public string Note => JarQuality.Tasting(recipe, ribbon, marks);
        }

        readonly List<Tasting> tastings = new List<Tasting>();
        public event Action<NoteData> NoteFound;
        // Anything changed: found here, or taken in from a save or a friend.
        public event Action Changed;

        [Serializable]
        class State
        {
            public List<string> notes = new List<string>();
            public List<string> pages = new List<string>();
            public List<Tasting> tastings = new List<Tasting>();
        }

        public string SaveKey => "discoveries";

        public string Capture()
        {
            var state = new State();
            foreach (var note in found)
            {
                state.notes.Add(note.name);
            }
            state.pages.AddRange(pages);
            state.tastings.AddRange(tastings);
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            var before = new List<NoteData>(found);
            found.Clear();
            pages.Clear();
            foreach (var name in state.notes)
            {
                NoteData note = SaveManager.Catalog.Note(name);
                if (note != null && !found.Contains(note))
                {
                    found.Add(note);
                }
            }
            if (state.pages != null) pages.AddRange(state.pages);
            tastings.Clear();
            if (state.tastings != null) tastings.AddRange(state.tastings);
            foreach (var note in found)
            {
                if (!before.Contains(note)) NoteFound?.Invoke(note);
            }
            Changed?.Invoke();
        }

        public bool HasFound(NoteData note) => found.Contains(note);

        public bool HasPage(NoteData book, int page) => pages.Contains(PageKey(book, page));

        static string PageKey(NoteData book, int page) => book.name + ":" + page;

        public void Record(NoteData note)
        {
            if (note == null || found.Contains(note))
            {
                return;
            }
            found.Add(note);
            NoteFound?.Invoke(note);
            Changed?.Invoke();
        }

        // A jar eaten: its tasting note replaces the last one for that recipe.
        public void Taste(string recipe, Ribbon ribbon, IEnumerable<string> marks)
        {
            if (string.IsNullOrEmpty(recipe)) return;
            tastings.RemoveAll(t => t.recipe == recipe);
            var tasting = new Tasting { recipe = recipe, ribbon = ribbon };
            if (marks != null) tasting.marks.AddRange(marks);
            tastings.Add(tasting);
            Changed?.Invoke();
        }

        public Tasting TastingOf(string recipe) => tastings.Find(t => t.recipe == recipe);

        // A page of a book, read.
        public void RecordPage(NoteData book, int page)
        {
            if (book == null) return;
            Record(book);
            string key = PageKey(book, page);
            if (pages.Contains(key)) return;
            pages.Add(key);
            Changed?.Invoke();
        }
    }
}
