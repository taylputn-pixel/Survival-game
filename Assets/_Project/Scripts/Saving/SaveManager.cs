using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.Saving
{
    // Saves the homestead when you go to bed or head back to the title, and
    // rebuilds it when you Continue. Systems save themselves (ISaveable);
    // items are saved with where they sit and rebuilt from their prefabs.
    public class SaveManager : MonoBehaviour
    {
        [SerializeField] SaveCatalog catalog;
        [SerializeField] PlayerHands hands;
        [SerializeField] ScreenFade fade;
        [SerializeField] float fadeInSeconds = 0.8f;

        public static SaveManager Instance { get; private set; }
        public static SaveCatalog Catalog => Instance.catalog;
        public bool IsLoading { get; private set; }

        void Awake() => Instance = this;

        IEnumerator Start()
        {
            if (!SaveSlot.LoadOnStart || !SaveSlot.Store.Exists())
            {
                yield break;
            }
            SaveSlot.LoadOnStart = false;
            IsLoading = true;
            yield return fade.FadeTo(1f, 0f);
            // Let every object finish its own setup before it's overwritten.
            yield return null;
            Load(JsonUtility.FromJson<SaveData>(SaveSlot.Store.Read()));
            IsLoading = false;
            yield return fade.FadeTo(0f, fadeInSeconds);
        }

        public void Save()
        {
            var data = new SaveData();
            foreach (var saveable in Find<ISaveable>())
            {
                data.systems.Add(new SystemEntry { key = saveable.SaveKey, json = saveable.Capture() });
            }

            List<SavedItem> items = FindObjectsByType<SavedItem>(FindObjectsSortMode.None).ToList();
            var index = new Dictionary<SavedItem, int>();
            for (int i = 0; i < items.Count; i++)
            {
                index[items[i]] = i;
            }
            foreach (var item in items)
            {
                data.items.Add(Record(item, index));
            }
            SaveSlot.Store.Write(JsonUtility.ToJson(data));
        }

        ItemRecord Record(SavedItem item, Dictionary<SavedItem, int> index)
        {
            Transform t = item.transform;
            var pickup = item.GetComponent<Pickup>();
            var record = new ItemRecord { key = item.Key };
            foreach (var state in item.GetComponents<IItemState>())
            {
                record.states.Add(state.Capture());
            }

            if (pickup != null && hands.Held == pickup)
            {
                // Whatever's in your hands is set down at your feet.
                Transform player = hands.transform;
                record.position = player.position + player.forward * 0.4f + Vector3.up * 0.1f;
                record.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
                record.kinematic = false;
                record.colliders = true;
                return record;
            }

            record.kinematic = pickup == null || pickup.IsResting;
            record.colliders = pickup == null || pickup.CollidersOn;
            if (pickup != null && pickup.InContainer != null)
            {
                record.containerIndex = index[pickup.InContainer.GetComponent<SavedItem>()];
                return record;
            }

            Transform parent = t.parent;
            if (parent == null)
            {
                record.position = t.position;
                record.rotation = t.rotation;
                return record;
            }

            record.position = t.localPosition;
            record.rotation = t.localRotation;
            SavedItem owner = parent.GetComponentInParent<SavedItem>();
            if (owner != null)
            {
                record.parentItem = index[owner];
                record.parentPath = PathBetween(owner.transform, parent);
            }
            else
            {
                record.parentPath = PathBetween(null, parent);
            }
            return record;
        }

        void Load(SaveData data)
        {
            var systems = data.systems.ToDictionary(e => e.key, e => e.json);
            foreach (var saveable in Find<ISaveable>())
            {
                if (systems.TryGetValue(saveable.SaveKey, out string json))
                {
                    saveable.Restore(json);
                }
            }

            // Clear out the items the scene started with; the save has its own.
            foreach (var old in FindObjectsByType<SavedItem>(FindObjectsSortMode.None))
            {
                old.transform.SetParent(null);
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            var made = new List<SavedItem>();
            foreach (var record in data.items)
            {
                SavedItem prefab = catalog.Item(record.key);
                SavedItem item = prefab != null ? Instantiate(prefab) : null;
                if (item != null)
                {
                    item.name = prefab.name;
                }
                made.Add(item);
            }

            for (int i = 0; i < data.items.Count; i++)
            {
                if (made[i] != null)
                {
                    Place(made[i], data.items[i], made);
                }
            }

            foreach (var after in Find<IAfterLoad>())
            {
                after.AfterLoad();
            }
        }

        void Place(SavedItem item, ItemRecord record, List<SavedItem> made)
        {
            var pickup = item.GetComponent<Pickup>();
            var states = item.GetComponents<IItemState>();
            for (int s = 0; s < states.Length && s < record.states.Count; s++)
            {
                states[s].Restore(record.states[s]);
            }

            if (record.containerIndex >= 0 && made[record.containerIndex] != null)
            {
                var container = made[record.containerIndex].GetComponent<Container>();
                container.Add(pickup, 0f);
                return;
            }

            Transform parent = null;
            if (record.parentItem >= 0 && made[record.parentItem] != null)
            {
                parent = string.IsNullOrEmpty(record.parentPath) ? made[record.parentItem].transform : made[record.parentItem].transform.Find(record.parentPath);
            }
            else if (!string.IsNullOrEmpty(record.parentPath))
            {
                parent = FindInScene(record.parentPath);
            }

            if (parent != null)
            {
                item.transform.SetParent(parent, false);
                item.transform.SetLocalPositionAndRotation(record.position, record.rotation);
            }
            else
            {
                item.transform.SetPositionAndRotation(record.position, record.rotation);
            }
            if (pickup != null)
            {
                pickup.SetResting(record.kinematic, record.colliders);
            }
        }

        // "Root/Child/Grandchild" from `from` (or the scene root) down to `to`.
        static string PathBetween(Transform from, Transform to)
        {
            var names = new List<string>();
            for (Transform t = to; t != null && t != from; t = t.parent)
            {
                names.Add(t.name);
            }
            names.Reverse();
            return string.Join("/", names);
        }

        static Transform FindInScene(string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
                }
            }
            return null;
        }

        static IEnumerable<T> Find<T>() where T : class
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<T>();
        }
    }
}
