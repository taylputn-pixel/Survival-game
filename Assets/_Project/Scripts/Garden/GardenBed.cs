using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Net;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // The soil of one raised bed: its planting spots and the weeds that come
    // up between them overnight. Suckers and pests come up on the plants.
    // Every change to the bed goes through it (Plant, Water, PullOut,
    // TakeOff), so in a shared game a guest's work also goes to the host.
    public class GardenBed : MonoBehaviour, ISaveable
    {
        [SerializeField] GardenSettings settings;
        [SerializeField] DayClock clock;
        [SerializeField] PlantSpot[] spots;
        [SerializeField] Weed weedPrefab;
        [Tooltip("Where pulled weeds are tossed, beside the bed.")]
        [SerializeField] Transform weedPile;
        [Tooltip("Pulled weeds kept on the pile before the oldest are cleared away.")]
        [SerializeField] int pileSize = 12;

        readonly List<Weed> weeds = new List<Weed>();
        readonly Queue<Weed> pile = new Queue<Weed>();

        public IReadOnlyList<PlantSpot> Spots => spots;
        public GardenSettings Settings => settings;
        public IReadOnlyList<Weed> Weeds => weeds;
        public IEnumerable<Pinchable> Pinchables
        {
            get
            {
                foreach (var spot in spots)
                {
                    foreach (var thing in spot.Pinchables) yield return thing;
                }
            }
        }
        public Plane Soil => new Plane(transform.up, spots[0].transform.position);
        // Set in a shared game (GardenNet).
        public GardenNet Shared { get; set; }
        // On a guest's computer: the host grows this bed, and it only shows
        // what the host sends.
        public bool Following
        {
            get => following;
            set
            {
                following = value;
                foreach (var spot in spots) spot.Following = value;
            }
        }

        bool following;
        bool Guest => Shared != null && Shared.IsGuest;

        void Start()
        {
            // The garden sat untended for a while before you arrived.
            for (int i = 0; i < settings.startingWeeds; i++)
            {
                SpawnWeed(Random.Range(0, settings.weedMaxAge + 1));
            }
        }

        [System.Serializable]
        class WeedState
        {
            public Vector3 position;
            public float yaw;
            public int age;
        }

        [System.Serializable]
        class State
        {
            public List<PlantSpot.State> spots = new List<PlantSpot.State>();
            public List<WeedState> weeds = new List<WeedState>();
        }

        public string SaveKey => "garden:" + name;

        public string Capture()
        {
            var state = new State();
            foreach (var spot in spots)
            {
                state.spots.Add(spot.Capture());
            }
            foreach (var weed in weeds)
            {
                state.weeds.Add(new WeedState { position = weed.transform.localPosition, yaw = weed.transform.localEulerAngles.y, age = weed.Age });
            }
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            for (int i = 0; i < spots.Length && i < state.spots.Count; i++)
            {
                spots[i].Restore(state.spots[i]);
            }
            RebuildWeeds(state.weeds);
        }

        void RebuildWeeds(List<WeedState> saved)
        {
            foreach (var weed in weeds)
            {
                Destroy(weed.gameObject);
            }
            weeds.Clear();
            foreach (var one in saved)
            {
                Weed weed = Instantiate(weedPrefab, transform);
                weed.transform.SetLocalPositionAndRotation(one.position, Quaternion.Euler(0f, one.yaw, 0f));
                weed.Bed = this;
                weed.Sprout(one.age, settings.weedMaxAge);
                weeds.Add(weed);
            }
        }

        // The host's bed, on a guest's computer: only what changed is redone,
        // so a seed you just planted or a plant you've turned isn't reset.
        public void ApplyShared(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            for (int i = 0; i < spots.Length && i < state.spots.Count; i++)
            {
                spots[i].ApplyShared(state.spots[i]);
            }
            if (!SameWeeds(state.weeds))
            {
                RebuildWeeds(state.weeds);
            }
        }

        bool SameWeeds(List<WeedState> other)
        {
            if (other.Count != weeds.Count) return false;
            for (int i = 0; i < weeds.Count; i++)
            {
                if (weeds[i].Age != other[i].age || (weeds[i].transform.localPosition - other[i].position).sqrMagnitude > 0.0001f)
                {
                    return false;
                }
            }
            return true;
        }

        // A killing frost (the host): everything growing dies. Says how many plants were lost.
        public int Frost()
        {
            int killed = 0;
            foreach (var spot in spots) if (spot.Frost()) killed++;
            return killed;
        }

        // ---- Changes to the bed ----

        public void Plant(PlantSpot spot, CropData crop, float seconds)
        {
            spot.Plant(crop, seconds);
            if (Guest) Shared.SendPlant(System.Array.IndexOf(spots, spot), crop.name);
        }

        public void Water(PlantSpot spot, float amount)
        {
            spot.AddWater(amount);
            if (Guest) Shared.SendWater(System.Array.IndexOf(spots, spot), amount);
        }

        public void TakeOff(PlantSpot spot, Pinchable thing)
        {
            bool pest = spot.IsPest(thing);
            spot.TakeOff(thing);
            // Hornworms go in the hens' treat pail, if there's one by the garden.
            if (pest)
            {
                var pail = Animals.TreatPail.Near(spot.transform.position);
                if (pail != null) pail.AddWorm();
            }
            if (Guest) Shared.SendTakeOff(System.Array.IndexOf(spots, spot), pest);
        }

        // The host doing a guest's work.
        public void PlantFor(int spot, string crop)
        {
            if (spot < 0 || spot >= spots.Length || !spots[spot].IsEmpty) return;
            CropData data = SaveManager.Catalog.Crop(crop);
            if (data != null) spots[spot].Plant(data, 0.35f);
        }

        public void WaterFor(int spot, float amount)
        {
            if (spot >= 0 && spot < spots.Length) spots[spot].AddWater(amount);
        }

        public void TakeOffFor(int spot, bool pest)
        {
            if (spot >= 0 && spot < spots.Length) spots[spot].TakeOffAny(pest);
        }

        public void PullFor(Vector3 localPosition)
        {
            Weed nearest = null;
            float best = 0.1f;
            foreach (var weed in weeds)
            {
                float distance = Vector3.Distance(weed.transform.localPosition, localPosition);
                if (distance < best)
                {
                    best = distance;
                    nearest = weed;
                }
            }
            if (nearest != null) PullOut(nearest);
        }

        void OnEnable() => clock.NewDay += Overnight;

        void OnDisable() => clock.NewDay -= Overnight;

        void Overnight(int day)
        {
            // Plants grow first, judged by how you left the bed at bedtime.
            foreach (var spot in spots)
            {
                spot.GrowOvernight(!IsCrowded(spot) && !spot.HasTrouble);
            }
            foreach (var spot in spots)
            {
                spot.TroubleOvernight(HasCompanion(spot));
            }
            foreach (var weed in weeds)
            {
                weed.GrowOvernight();
            }
            for (int i = 0; i < settings.weedsPerNight; i++)
            {
                SpawnWeed(0);
            }
        }

        public void PullOut(Weed weed)
        {
            if (Guest) Shared.SendPull(weed.transform.localPosition);
            weeds.Remove(weed);
            Vector3 spot = weedPile.position + new Vector3(Random.Range(-0.08f, 0.08f), 0.01f * pile.Count, Random.Range(-0.08f, 0.08f));
            weed.transform.SetParent(weedPile, worldPositionStays: true);
            weed.PullOut(spot);

            pile.Enqueue(weed);
            if (pile.Count > pileSize)
            {
                Destroy(pile.Dequeue().gameObject);
            }
        }

        void SpawnWeed(int age)
        {
            if (weeds.Count >= settings.maxWeeds || !TryFindRoom(out Vector3 position))
            {
                return;
            }
            Weed weed = Instantiate(weedPrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            weed.Bed = this;
            weed.Sprout(age, settings.weedMaxAge);
            weeds.Add(weed);
        }

        // A random patch of open soil, clear of the planting spots and other weeds.
        bool TryFindRoom(out Vector3 position)
        {
            GetSoilBounds(out Vector3 min, out Vector3 max);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var local = new Vector3(Random.Range(min.x, max.x), min.y, Random.Range(min.z, max.z));
                position = transform.TransformPoint(local);
                if (IsClear(position))
                {
                    return true;
                }
            }
            position = default;
            return false;
        }

        // A companion blooming near this plant (marigolds by the tomatoes).
        bool HasCompanion(PlantSpot spot)
        {
            foreach (var other in spots)
            {
                if (other != spot && other.IsCompanionFor(spot)
                    && Vector3.Distance(other.transform.position, spot.transform.position) < settings.companionRadius)
                {
                    return true;
                }
            }
            return false;
        }

        bool IsCrowded(PlantSpot spot)
        {
            foreach (var weed in weeds)
            {
                if (Vector3.Distance(weed.transform.position, spot.transform.position) < settings.crowdingRadius)
                {
                    return true;
                }
            }
            return false;
        }

        bool IsClear(Vector3 position)
        {
            foreach (var spot in spots)
            {
                if (Vector3.Distance(spot.transform.position, position) < settings.weedSpacing)
                {
                    return false;
                }
            }
            foreach (var weed in weeds)
            {
                if (Vector3.Distance(weed.transform.position, position) < settings.weedSpacing)
                {
                    return false;
                }
            }
            return true;
        }

        // The area the spots cover, plus a little margin, in the bed's local space.
        public void GetSoilBounds(out Vector3 min, out Vector3 max, float margin = 0.15f)
        {
            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;
            foreach (var spot in spots)
            {
                Vector3 p = transform.InverseTransformPoint(spot.transform.position);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            min -= new Vector3(margin, 0f, margin);
            max += new Vector3(margin, 0f, margin);
        }
    }
}
