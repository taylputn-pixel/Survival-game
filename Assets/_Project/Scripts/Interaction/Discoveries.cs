using System;
using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Interaction
{
    // Everything of Grandma's you've found so far. The recipe box and diary
    // will read from this, and the save system will store it.
    public class Discoveries : MonoBehaviour, ISaveable
    {
        readonly List<NoteData> found = new List<NoteData>();

        public IReadOnlyList<NoteData> Found => found;
        public event Action<NoteData> NoteFound;

        [Serializable]
        class State
        {
            public List<string> notes = new List<string>();
        }

        public string SaveKey => "discoveries";

        public string Capture()
        {
            var state = new State();
            foreach (var note in found)
            {
                state.notes.Add(note.name);
            }
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            found.Clear();
            foreach (var name in JsonUtility.FromJson<State>(json).notes)
            {
                NoteData note = SaveManager.Catalog.Note(name);
                if (note != null)
                {
                    found.Add(note);
                }
            }
        }

        public bool HasFound(NoteData note) => found.Contains(note);

        public void Record(NoteData note)
        {
            if (note == null || found.Contains(note))
            {
                return;
            }
            found.Add(note);
            NoteFound?.Invoke(note);
        }
    }
}
