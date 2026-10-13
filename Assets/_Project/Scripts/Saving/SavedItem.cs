using UnityEngine;

namespace GrandmasHouse.Saving
{
    // Marks an item that's saved and rebuilt from its prefab on load. The key
    // names its entry in the save catalog.
    public class SavedItem : MonoBehaviour
    {
        [SerializeField] string key;

        public string Key => key;
    }
}
