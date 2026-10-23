using System;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Trade
{
    // Someone you trade with: the shopkeeper at the general store, or a
    // neighbor on their porch. Set your goods on their counter, then talk to
    // them: they weigh what they'll take against what they have. The store
    // takes most things (for less than they're worth) and keeps a ledger on
    // Grandma's account for anything left over; a neighbor only wants
    // certain things, keeps the difference, and asks a little more for their
    // own. They're out by day and gone in at night. In a shared game the
    // host makes the trade.
    public class Trader : MonoBehaviour, IInteractable, ISharedUse, ISaveable
    {
        [Serializable]
        public class Ware
        {
            [Tooltip("An item's save key, or empty for a recipe.")]
            public string key;
            [Tooltip("A recipe card they'll let you copy into the journal.")]
            public NoteData recipe;
            public int stock = 3;
            public int maxStock = 3;
            [Tooltip("How many come back each day, up to the most they keep.")]
            public float restockPerDay = 0.5f;
            [Tooltip("Their own price; 0 uses the trade settings.")]
            public float worth;
            [Tooltip("Where they set these out, if not the usual spots (logs on the log pile).")]
            public Transform[] setOutAt;
        }

        [SerializeField] string traderName = "June";
        [SerializeField] string placeName = "the general store";
        [Tooltip("What they say when you come to trade.")]
        [TextArea(1, 3)] [SerializeField] string greeting = "What've you got?";
        [SerializeField] DayClock clock;
        [SerializeField] CalendarSettings calendar;
        [SerializeField] TradeSettings settings;
        [SerializeField] Discoveries discoveries;
        [Tooltip("The store: takes most goods, keeps a ledger. A neighbor: only takes what they want.")]
        [SerializeField] bool isStore;
        [Tooltip("What a neighbor will take, by save key (\"Jar\" for any sealed jar). Ignored for the store.")]
        [SerializeField] List<string> wants = new List<string>();
        [SerializeField] List<Ware> wares = new List<Ware>();
        [Tooltip("Given the first time you come to them (a save key), set out with the rest of their goods. Empty for none.")]
        [SerializeField] string welcomeGift = "";
        [Tooltip("What they say as they hand it over.")]
        [TextArea(1, 3)] [SerializeField] string welcomeLine = "";
        [Tooltip("Restocked only when a delivery comes in (the store, in hard times): the chance of one each day, by month. Empty restocks a little every day.")]
        [SerializeField] float[] deliveryChanceByMonth = new float[0];
        [Tooltip("How much a delivery brings of each thing, as a share of the most they keep (at least, at most). Rounded, so it can bring none.")]
        [SerializeField] Vector2 deliveryShare = new Vector2(0.2f, 0.8f);
        [Tooltip("Where your goods go: the counter top, as a box.")]
        [SerializeField] BoxCollider counter;
        [Tooltip("Where their goods are set out for you.")]
        [SerializeField] Transform[] handOver;
        [Tooltip("Their figure, shown while they're out.")]
        [SerializeField] GameObject figure;
        [SerializeField] string saveKey = "trader";

        public string Name => traderName;
        public string Place => placeName;
        public string Greeting => greeting;
        public bool IsStore => isStore;
        public IReadOnlyList<Ware> Wares => wares;
        public float Credit { get; private set; }
        public bool IsOpen => clock != null && !clock.IsNight;
        public int Month => calendar != null && clock != null ? calendar.DateOf(clock.Day).month : 0;
        public event Action Traded;
        // Met you already (and gave the welcome gift, if they have one).
        public bool Welcomed { get; private set; }

        readonly float[] restock = new float[32];

        void OnEnable() { if (clock != null) clock.NewDay += Restock; }
        void OnDisable() { if (clock != null) clock.NewDay -= Restock; }

        void Update()
        {
            if (figure != null && figure.activeSelf != IsOpen) figure.SetActive(IsOpen);
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        public bool HasDeliveries => deliveryChanceByMonth != null && deliveryChanceByMonth.Length > 0;
        // The day the last delivery came in (-1 for none yet).
        public int LastDelivery { get; private set; } = -1;

        void Restock(int day)
        {
            if (!Hosting) return;
            if (HasDeliveries)
            {
                int month = calendar != null ? calendar.DateOf(day).month : 0;
                float chance = deliveryChanceByMonth[Mathf.Clamp(month, 0, deliveryChanceByMonth.Length - 1)];
                if (UnityEngine.Random.value < chance) Deliver(day);
                return;
            }
            for (int i = 0; i < wares.Count && i < restock.Length; i++)
            {
                var w = wares[i];
                if (w.stock >= w.maxStock) { restock[i] = 0f; continue; }
                restock[i] += w.restockPerDay;
                int whole = Mathf.FloorToInt(restock[i]);
                if (whole > 0) { w.stock = Mathf.Min(w.maxStock, w.stock + whole); restock[i] -= whole; }
            }
        }

        // The truck came in: each thing comes back by some share of the most
        // they keep, never sure how much (or whether any).
        public void Deliver(int day)
        {
            LastDelivery = day;
            foreach (var w in wares)
            {
                if (w.recipe != null) continue;
                int brought = Mathf.RoundToInt(w.maxStock * UnityEngine.Random.Range(deliveryShare.x, deliveryShare.y));
                w.stock = Mathf.Min(w.maxStock, w.stock + brought);
            }
            Traded?.Invoke();
        }

        // ---- Talking to them ----

        public bool CanInteract(PlayerHands hands) => IsOpen && hands.IsEmpty;
        public string PromptVerb(PlayerHands hands) => "Trade with " + traderName;

        public void Interact(PlayerHands hands)
        {
            if (!Welcomed && !string.IsNullOrEmpty(welcomeGift))
            {
                var caption = FindAnyObjectByType<UI.Caption>();
                if (caption != null && !string.IsNullOrEmpty(welcomeLine)) caption.Show(traderName + ": " + welcomeLine, 6f);
                if (!Hosting && TryGetComponent(out TraderNet net)) net.Welcome();
                else Welcome();
            }
            var view = FindAnyObjectByType<UI.TradeView>();
            if (view != null) view.Open(this, hands);
        }

        // ---- Weighing ----

        public struct Offer
        {
            public Pickup item;
            public float worth;
        }

        // What's on the counter that they'll take, and what each is worth to them.
        public List<Offer> OnCounter()
        {
            var offers = new List<Offer>();
            var seen = new HashSet<Pickup>();
            Bounds area = counter.bounds;
            foreach (var hit in Physics.OverlapBox(area.center, area.extents, counter.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                var item = hit.GetComponentInParent<Pickup>();
                if (item == null || !seen.Add(item) || !item.gameObject.activeInHierarchy) continue;
                if (item.TryGetComponent(out Container basket))
                {
                    foreach (var inside in basket.Contents) if (seen.Add(inside)) Weigh(inside, offers);
                    continue;
                }
                Weigh(item, offers);
            }
            return offers;
        }

        void Weigh(Pickup item, List<Offer> offers)
        {
            if (!Takes(item)) return;
            float worth = settings.WorthOf(item, Month) * (isStore ? settings.storeTakes : 1f);
            if (worth > 0.01f) offers.Add(new Offer { item = item, worth = worth });
        }

        bool Takes(Pickup item)
        {
            if (isStore) return true;
            if (item.TryGetComponent(out Kitchen.JarContents _)) return wants.Contains("Jar");
            return item.TryGetComponent(out SavedItem saved) && wants.Contains(saved.Key);
        }

        // What one of their wares costs you.
        public float Price(Ware ware)
        {
            float worth = ware.worth > 0f ? ware.worth : settings.BaseWorth(ware.key, Month);
            return isStore ? worth : worth * settings.neighborAsks;
        }

        public string WareName(Ware ware)
        {
            if (ware.recipe != null) return "Recipe: " + ware.recipe.title;
            var prefab = SaveManager.Catalog.Item(ware.key);
            return prefab != null && prefab.TryGetComponent(out Pickup p) ? p.DisplayName : ware.key;
        }

        // Fair enough to shake on: their side doesn't outweigh yours (plus
        // Grandma's credit, at the store).
        public bool CanDeal(float yours, float theirs)
        {
            if (yours <= 0f && theirs <= 0f) return false;
            return theirs <= yours + (isStore ? Credit : 0f) + 0.001f;
        }

        // ---- The trade (the host makes it) ----

        public void Deal(int[] counts)
        {
            if (!Hosting && TryGetComponent(out TraderNet net))
            {
                net.Deal(counts);
                return;
            }
            DealHere(counts, null);
        }

        // Returns what happened, or null if the trade didn't add up.
        public string DealHere(int[] counts, Vector3? near)
        {
            if (!IsOpen || counts == null || counts.Length != wares.Count) return null;
            var offers = OnCounter();
            float yours = 0f, theirs = 0f;
            foreach (var o in offers) yours += o.worth;
            for (int i = 0; i < wares.Count; i++)
            {
                if (counts[i] < 0 || counts[i] > wares[i].stock) return null;
                theirs += Price(wares[i]) * counts[i];
            }
            if (!CanDeal(yours, theirs)) return null;

            foreach (var o in offers)
            {
                if (o.item.TryGetComponent(out Net.ItemNet _)) Net.ItemNet.Remove(o.item.gameObject);
                else Destroy(o.item.gameObject);
            }
            int spot = 0;
            var learned = new List<string>();
            for (int i = 0; i < wares.Count; i++)
            {
                var w = wares[i];
                for (int n = 0; n < counts[i]; n++)
                {
                    if (w.recipe != null)
                    {
                        if (discoveries != null) discoveries.Record(w.recipe);
                        learned.Add(w.recipe.title);
                        continue;
                    }
                    if (w.setOutAt != null && w.setOutAt.Length > 0) HandOver(w.key, n, w.setOutAt);
                    else HandOver(w.key, spot++, handOver);
                }
                w.stock -= counts[i];
            }
            if (isStore) Credit = Mathf.Max(0f, Credit + yours - theirs);
            Traded?.Invoke();
            return learned.Count > 0 ? "Copied into the journal: " + string.Join(", ", learned) : "Done.";
        }

        void HandOver(string key, int index, Transform[] spots)
        {
            var prefab = SaveManager.Catalog.Item(key);
            if (prefab == null || spots.Length == 0) return;
            Transform at = spots[index % spots.Length];
            Vector3 position = at.position + Vector3.up * (0.05f + 0.4f * (index / spots.Length));
            var item = Instantiate(prefab, position, at.rotation);
            item.name = prefab.name;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && item.TryGetComponent(out NetworkObject shared))
            {
                shared.Spawn();
            }
        }

        // The first meeting (the host): their gift goes out with the rest.
        public void Welcome()
        {
            if (Welcomed) return;
            Welcomed = true;
            if (!string.IsNullOrEmpty(welcomeGift)) HandOver(welcomeGift, 0, handOver);
            Traded?.Invoke();
        }

        // ---- Saving (and sharing) ----

        [Serializable]
        class State
        {
            public List<int> stock = new List<int>();
            public float credit;
            public int lastDelivery = -1;
            public bool welcomed;
        }

        public string SaveKey => saveKey;

        public string Capture()
        {
            var state = new State { credit = Mathf.Round(Credit * 100f) / 100f, lastDelivery = LastDelivery, welcomed = Welcomed };
            foreach (var w in wares) state.stock.Add(w.stock);
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Credit = state.credit;
            LastDelivery = state.lastDelivery;
            Welcomed = state.welcomed;
            for (int i = 0; i < wares.Count && i < state.stock.Count; i++) wares[i].stock = state.stock[i];
            Traded?.Invoke();
        }
    }
}
