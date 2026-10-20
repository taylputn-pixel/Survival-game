using System;
using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Interaction
{
    // Everything of Grandma's you've found so far, and so what the journal
    // holds: the notes and recipe cards read, and each page of her books that
    // you've turned to. In a game with friends the household shares one
    // (a SharedState beside it keeps everyone's the same), and it's saved.
    public class Discoveries : MonoBehaviour, ISaveable
    {
        readonly List<NoteData> found = new List<NoteData>();
        // Book pages read, as "note name:page index".
        readonly List<string> pages = new List<string>();

        public IReadOnlyList<NoteData> Found => found;
        public event Action<NoteData> NoteFound;
        // Anything changed: found here, or taken in from a save or a friend.
        public event Action Changed;

        [Serializable]
        class State
        {
            public List<string> notes = new List<string>();
            public List<string> pages = new List<string>();
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
