using System;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Saving
{
    // How a save finds things again by name: item prefabs, crops, and notes.
    [CreateAssetMenu(fileName = "SaveCatalog", menuName = "Grandma's House/Save Catalog")]
    public class SaveCatalog : ScriptableObject
    {
        [Serializable]
        public struct ItemEntry
        {
            public string key;
            public SavedItem prefab;
        }

        public ItemEntry[] items;
        public CropData[] crops;
        public NoteData[] notes;

        public SavedItem Item(string key)
        {
            foreach (var entry in items)
            {
                if (entry.key == key)
                {
                    return entry.prefab;
                }
            }
            return null;
        }

        public CropData Crop(string name) => Find(crops, name);

        public NoteData Note(string name) => Find(notes, name);

        static T Find<T>(T[] list, string name) where T : UnityEngine.Object
        {
            foreach (var entry in list)
            {
                if (entry != null && entry.name == name)
                {
                    return entry;
                }
            }
            return null;
        }
    }
}
