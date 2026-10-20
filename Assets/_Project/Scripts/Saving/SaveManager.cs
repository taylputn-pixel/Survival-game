using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.Saving
{
    // Saves the homestead when the night ends or the host heads back to the
    // title, and rebuilds it when you Continue. Systems save themselves
    // (ISaveable); items are saved with where they sit and rebuilt from
    // their prefabs; each player is saved by their key. Only the host saves.
    public class SaveManager : MonoBehaviour
    {
        [SerializeField] SaveCatalog catalog;
        [SerializeField] ScreenFade fade;
        [SerializeField] float fadeInSeconds = 0.8f;

        public static SaveManager Instance { get; private set; }
        public static SaveCatalog Catalog => Instance.catalog;
        public bool IsLoading { get; private set; }

        // Players from the save who haven't joined yet, by key.
        readonly Dictionary<string, PlayerRecord> waiting = new Dictionary<string, PlayerRecord>();

        // Joined someone else's game: their host keeps the save.
        static bool IsGuest => NetSession.Instance != null && NetSession.Instance.IsGuest;
        static bool Sharing => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void Awake() => Instance = this;

        IEnumerator Start()
        {
            if (IsGuest || !SaveSlot.LoadOnStart || !SaveSlot.Store.Exists())
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
            if (IsGuest)
            {
                return;
            }
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
            SavePlayers(data);
            SaveSlot.Store.Write(JsonUtility.ToJson(data));
        }

        // Everyone here now, plus anyone from the save who hasn't come back yet.
        void SavePlayers(SaveData data)
        {
            var seen = new HashSet<string>();
            foreach (var player in PlayerNet.All)
            {
                if (!player.IsSpawned) continue;
                string needs = player.IsOwner ? player.Needs.Capture() : player.NeedsJson;
                data.players.Add(new PlayerRecord { key = player.Key, position = player.transform.position, yaw = player.transform.eulerAngles.y, needs = needs });
                seen.Add(player.Key);
            }
            foreach (var record in waiting.Values)
            {
                if (!seen.Contains(record.key)) data.players.Add(record);
            }
        }

        // A player joined: put them back where they were.
        public void Arrived(PlayerNet player)
        {
            if (waiting.TryGetValue(player.Key, out PlayerRecord record))
            {
                waiting.Remove(player.Key);
                player.Restore(record);
            }
        }

        // Who is carrying this, if anyone, and which of their things it is.
        static Transform Carrier(Pickup pickup, out int slot)
        {
            slot = 0;
            foreach (var player in PlayerNet.All)
            {
                var hands = player.Hands;
                if (hands.IsHolding(pickup))
                {
                    while (hands.Carried[slot] != pickup) slot++;
                    return hands.transform;
                }
            }
            if (pickup.TryGetComponent(out ItemNet net) && net.Live && net.Holder != ItemNet.NoOne)
            {
                PlayerNet holder = PlayerNet.Owning(net.Holder);
                if (holder != null) return holder.transform;
            }
            return null;
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

            int slot = 0;
            Transform player = pickup != null ? Carrier(pickup, out slot) : null;
            if (player != null)
            {
                // Whatever's in someone's hands is set down at their feet, side by side.
                Vector3 side = -player.right * 0.12f * slot;
                record.position = player.position + player.forward * 0.4f + side + Vector3.up * 0.1f;
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

            LoadPlayers(data, systems);

            // Clear out the items the scene started with; the save has its own.
            foreach (var old in FindObjectsByType<SavedItem>(FindObjectsSortMode.None))
            {
                if (old.TryGetComponent(out NetworkObject shared) && shared.IsSpawned)
                {
                    shared.Despawn(true);
                    continue;
                }
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

            // Share them with everyone (and anyone who joins later).
            if (Sharing)
            {
                foreach (var item in made)
                {
                    if (item != null && item.TryGetComponent(out NetworkObject shared) && !shared.IsSpawned)
                    {
                        shared.Spawn();
                    }
                }
            }

            foreach (var after in Find<IAfterLoad>())
            {
                after.AfterLoad();
            }
        }

        void LoadPlayers(SaveData data, Dictionary<string, string> systems)
        {
            waiting.Clear();
            if (data.players != null)
            {
                foreach (var record in data.players)
                {
                    waiting[record.key] = record;
                }
            }
            // Saves from before shared games: the one player is the host.
            if (waiting.Count == 0 && systems.TryGetValue("player", out string where))
            {
                var old = JsonUtility.FromJson<OldPlayer>(where);
                systems.TryGetValue("needs", out string needs);
                waiting[NetSession.LocalHostKey] = new PlayerRecord { key = NetSession.LocalHostKey, position = old.position, yaw = old.yaw, needs = needs };
            }
            foreach (var player in PlayerNet.All.ToArray())
            {
                if (player.IsSpawned) Arrived(player);
            }
        }

        [System.Serializable]
        class OldPlayer
        {
            public Vector3 position;
            public float yaw;
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
