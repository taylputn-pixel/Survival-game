using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // The soil of one raised bed: its planting spots and the weeds that come
    // up between them overnight.
    public class GardenBed : MonoBehaviour
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
        public IReadOnlyList<Weed> Weeds => weeds;
        public Plane Soil => new Plane(transform.up, spots[0].transform.position);

        void Start()
        {
            // The garden sat untended for a while before you arrived.
            for (int i = 0; i < settings.startingWeeds; i++)
            {
                SpawnWeed(Random.Range(0, settings.weedMaxAge + 1));
            }
        }

        void OnEnable() => clock.NewDay += Overnight;
        void OnDisable() => clock.NewDay -= Overnight;

        void Overnight(int day)
        {
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
