using System;
using System.Collections.Generic;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Interaction
{
    // Everything of Grandma's you've found so far. The recipe box and diary
    // will read from this, and the save system will store it.
    public class Discoveries : MonoBehaviour
    {
        readonly List<NoteData> found = new List<NoteData>();

        public IReadOnlyList<NoteData> Found => found;
        public event Action<NoteData> NoteFound;

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
