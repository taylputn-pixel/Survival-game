using System;
using UnityEngine;

namespace GrandmasHouse.Audio
{
    // Real recordings for the game's sounds. Any sound without clips here
    // uses its placeholder made in code. Keep this asset in a Resources
    // folder named "SoundBank" so it's found without any scene setup.
    [CreateAssetMenu(fileName = "SoundBank", menuName = "Grandma's House/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SoundId id;
            [Tooltip("One is picked at random each time, so repeats don't sound identical.")]
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
        }

        public Entry[] entries;

        public Entry Find(SoundId id)
        {
            if (entries == null) return null;
            foreach (var entry in entries)
            {
                if (entry.id == id && entry.clips != null && entry.clips.Length > 0)
                {
                    return entry;
                }
            }
            return null;
        }
    }
}
