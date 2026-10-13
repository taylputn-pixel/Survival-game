using System.IO;
using UnityEngine;

namespace GrandmasHouse.Saving
{
    // Where the save lives. PC writes a file; each console can supply its
    // own store later without touching the rest of the game.
    public interface ISaveStore
    {
        bool Exists();
        string Read();
        void Write(string text);
    }

    public class FileSaveStore : ISaveStore
    {
        readonly string path = Path.Combine(Application.persistentDataPath, "save.json");

        public bool Exists() => File.Exists(path);

        public string Read() => File.ReadAllText(path);

        public void Write(string text)
        {
            // Write to a temporary file first so a crash can't leave half a save.
            string temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);
        }
    }

    public static class SaveSlot
    {
        public static ISaveStore Store { get; set; } = new FileSaveStore();
        // Set by the title screen's Continue; the game scene loads it on start.
        public static bool LoadOnStart { get; set; }
    }
}
