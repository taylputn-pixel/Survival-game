using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // One place in a garden bed where a single plant can grow. A watered plant
    // moves up one stage each night; a dry one just waits (it never dies in
    // Cozy). Once ready, it holds its harvest for you to pick by hand. Some
    // crops grow suckers or draw pests overnight, to take off by hand.
    public class PlantSpot : MonoBehaviour, IAfterLoad
    {
        [SerializeField] Material soilMaterial;
        [Tooltip("Shows where to plant while the spot is empty.")]
        [SerializeField] GameObject marker;
        [Tooltip("A patch of soil that darkens as the spot is watered.")]
        [SerializeField] Renderer wetPatch;
        [SerializeField] GardenSettings settings;

        public CropData Crop { get; private set; }
        public bool IsEmpty => Crop == null;
        // 0 is dry, 1 is well watered.
        public float Water { get; private set; }
        // -1 while it's still a seed; the last stage means ready to harvest.
        public int Stage { get; private set; } = -1;
        public bool IsReady => Crop != null && Stage == Crop.stages.Length - 1;
        public int RipeCount => ripe.Count;
        public IReadOnlyList<Pickup> Ripe => ripe;
        // The space the plant takes up, for looking closer at it.
        public Bounds PlantBounds
        {
            get
            {
                var bounds = new Bounds(transform.position, Vector3.one * 0.05f);
                if (plant != null)
                {
                    foreach (var part in plant.GetComponentsInChildren<Renderer>())
                    {
                        bounds.Encapsulate(part.bounds);
                    }
                }
                return bounds;
            }
        }
        // Suckers or pests on it, so it isn't tended.
        public bool HasTrouble => suckers.Count > 0 || pests.Count > 0;
        public IEnumerable<Pinchable> Pinchables
        {
            get
            {
                foreach (var sucker in suckers) yield return sucker;
                foreach (var pest in pests) yield return pest;
            }
        }

        MaterialPropertyBlock block;
        GameObject mound;
        GameObject plant;
        readonly List<Transform> harvestPoints = new List<Transform>();
        readonly List<Pickup> ripe = new List<Pickup>();
        readonly List<Transform> suckerPoints = new List<Transform>();
        readonly List<Transform> pestPoints = new List<Transform>();
        readonly List<Pinchable> suckers = new List<Pinchable>();
        readonly List<Pinchable> pests = new List<Pinchable>();
        bool harvested;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            ShowWater();
        }

        void Update()
        {
            // Notice anything picked off the plant, by hand or into a basket.
            for (int i = ripe.Count - 1; i >= 0; i--)
            {
                Pickup item = ripe[i];
                if (item == null || !harvestPoints.Contains(item.transform.parent))
                {
                    ripe.RemoveAt(i);
                    harvested = true;
                }
            }
            if (Crop != null && Crop.singleHarvest && harvested && ripe.Count == 0)
            {
                Clear();
            }
        }

        public void AddWater(float amount)
        {
            Water = Mathf.Clamp01(Water + amount);
            ShowWater();
        }

        void ShowWater()
        {
            wetPatch.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(soilMaterial.color, settings.wetSoilColor, Water));
            wetPatch.SetPropertyBlock(block);
        }

        [System.Serializable]
        public class State
        {
            public string crop;
            public int stage = -1;
            public float water;
            public bool harvested;
            public float yaw;
            public int suckers;
            public int pests;
        }

        public State Capture() => new State
        {
            crop = Crop != null ? Crop.name : "",
            stage = Stage,
            water = Water,
            harvested = harvested,
            yaw = plant != null ? plant.transform.localEulerAngles.y : 0f,
            suckers = suckers.Count,
            pests = pests.Count,
        };

        // Puts the plant back as it was; the picking on it comes back with the items.
        public void Restore(State state)
        {
            StopAllCoroutines();
            if (plant != null) Destroy(plant);
            if (mound != null) Destroy(mound);
            plant = null;
            mound = null;
            harvestPoints.Clear();
            ripe.Clear();
            suckers.Clear();
            pests.Clear();

            Crop = string.IsNullOrEmpty(state.crop) ? null : SaveManager.Catalog.Crop(state.crop);
            Water = state.water;
            harvested = state.harvested;
            Stage = -1;
            marker.SetActive(Crop == null);
            if (Crop != null)
            {
                if (state.stage >= 0)
                {
                    ShowStage(state.stage);
                    plant.transform.localRotation = Quaternion.Euler(0f, state.yaw, 0f);
                    for (int i = 0; i < state.suckers; i++) AddSucker();
                    for (int i = 0; i < state.pests; i++) AddPest();
                }
                else
                {
                    MakeMound(1f);
                }
            }
            ShowWater();
        }

        public void AfterLoad()
        {
            ripe.Clear();
            foreach (var point in harvestPoints)
            {
                var item = point.GetComponentInChildren<Pickup>();
                if (item != null)
                {
                    ripe.Add(item);
                }
            }
        }

        // Called once each night, before the soil dries out again. A tended
        // plant (no weeds crowding it, no suckers or pests) yields more.
        public void GrowOvernight(bool tended)
        {
            if (Crop != null && Water >= settings.wateredEnough)
            {
                if (Stage < Crop.stages.Length - 1)
                {
                    ShowStage(Stage + 1);
                    if (IsReady)
                    {
                        Ripen(Crop.singleHarvest ? harvestPoints.Count : Crop.harvestPerNight + (tended ? Crop.tendedBonus : 0));
                    }
                }
                else if (!Crop.singleHarvest)
                {
                    Ripen(Crop.harvestPerNight + (tended ? Crop.tendedBonus : 0));
                }
            }
            Water = 0f;
            ShowWater();
        }

        // After growing: new suckers sprout, and pests may turn up, fewer with
        // a companion blooming nearby.
        public void TroubleOvernight(bool companionNearby)
        {
            if (Crop == null || Stage < 0)
            {
                return;
            }
            if (Crop.sucker != null)
            {
                for (int i = 0; i < settings.suckersPerNight && suckers.Count < Crop.maxSuckers; i++)
                {
                    AddSucker();
                }
            }
            if (Crop.pest != null && Random.value < (companionNearby ? Crop.companionPestChance : Crop.pestChance))
            {
                AddPest();
            }
        }

        // Blooming and the kind of plant that draws the other's pests away.
        public bool IsCompanionFor(PlantSpot other) =>
            IsReady && other.Crop != null && other.Crop.companions != null && System.Array.IndexOf(other.Crop.companions, Crop) >= 0;

        // Turned round to see the other side; it stays turned.
        public void Turn(float degrees)
        {
            if (plant != null)
            {
                plant.transform.Rotate(0f, degrees, 0f, Space.Self);
            }
        }

        // Pinched or picked off by hand.
        public void TakeOff(Pinchable thing)
        {
            if (suckers.Remove(thing) || pests.Remove(thing))
            {
                thing.Remove(transform.position);
            }
        }

        void AddSucker() => Place(Crop.sucker, suckerPoints, suckers);
        void AddPest() => Place(Crop.pest, pestPoints, pests);

        // On a free point of the plant as it looks now, if there is one.
        void Place(Pinchable prefab, List<Transform> points, List<Pinchable> into)
        {
            var free = new List<Transform>();
            foreach (var point in points)
            {
                if (point.GetComponentInChildren<Pinchable>() == null)
                {
                    free.Add(point);
                }
            }
            if (prefab == null || free.Count == 0)
            {
                return;
            }
            Pinchable thing = Instantiate(prefab, free[Random.Range(0, free.Count)]);
            thing.name = prefab.name;
            thing.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            thing.Owner = this;
            into.Add(thing);
        }

        // Press a hole into the soil, drop the seed in, and cover it over.
        public void Plant(CropData crop, float seconds)
        {
            Crop = crop;
            Stage = -1;
            harvested = false;
            marker.SetActive(false);
            Sfx.Play(SoundId.SoilPat, transform.position);
            StartCoroutine(Planting(crop, seconds));
        }

        void ShowStage(int stage)
        {
            Stage = stage;
            if (mound != null)
            {
                Destroy(mound);
            }
            if (plant != null)
            {
                Destroy(plant);
            }
            // Suckers and pests carry over to the grown plant.
            int keepSuckers = suckers.Count;
            int keepPests = pests.Count;
            suckers.Clear();
            pests.Clear();
            plant = Instantiate(Crop.stages[stage], transform);
            plant.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            harvestPoints.Clear();
            suckerPoints.Clear();
            pestPoints.Clear();
            foreach (var child in plant.GetComponentsInChildren<Transform>())
            {
                if (child.name.StartsWith("HarvestPoint"))
                {
                    harvestPoints.Add(child);
                }
                else if (child.name.StartsWith("SuckerPoint"))
                {
                    suckerPoints.Add(child);
                }
                else if (child.name.StartsWith("PestPoint"))
                {
                    pestPoints.Add(child);
                }
            }
            for (int i = 0; i < keepSuckers; i++) AddSucker();
            for (int i = 0; i < keepPests; i++) AddPest();
        }

        void Ripen(int count)
        {
            if (Crop.harvest == null)
            {
                return;
            }
            foreach (var point in harvestPoints)
            {
                if (count <= 0)
                {
                    return;
                }
                if (point.childCount > 0)
                {
                    continue;
                }
                Pickup item = Instantiate(Crop.harvest, point.position, point.rotation);
                item.name = Crop.harvest.name;
                item.AttachTo(point, Vector3.zero, Quaternion.identity, 0f, collidersOn: true);
                // Once in your hands it no longer belongs to the plant.
                item.HeldChanged += held =>
                {
                    if (held && item.transform.parent != null && harvestPoints.Contains(item.transform.parent))
                    {
                        item.transform.SetParent(null, worldPositionStays: true);
                    }
                };
                ripe.Add(item);
                count--;
            }
        }

        // The plant is finished (dill cut); the spot is ready to plant again.
        void Clear()
        {
            Destroy(plant);
            plant = null;
            harvestPoints.Clear();
            suckerPoints.Clear();
            pestPoints.Clear();
            suckers.Clear();
            pests.Clear();
            Crop = null;
            Stage = -1;
            harvested = false;
            marker.SetActive(true);
        }

        void MakeMound(float grown)
        {
            mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mound.name = "Mound";
            Destroy(mound.GetComponent<Collider>());
            mound.transform.SetParent(transform, false);
            mound.GetComponent<Renderer>().sharedMaterial = soilMaterial;
            mound.transform.localScale = new Vector3(0.07f, 0.025f, 0.07f) * grown;
        }

        IEnumerator Planting(CropData crop, float seconds)
        {
            var seed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            seed.name = crop.displayName + " Seed";
            Destroy(seed.GetComponent<Collider>());
            seed.transform.SetParent(transform, false);
            seed.transform.localScale = Vector3.one * crop.seedSize;
            var seedMaterial = new Material(soilMaterial) { color = crop.seedColor };
            seed.GetComponent<Renderer>().sharedMaterial = seedMaterial;

            MakeMound(0f);
            var moundSize = new Vector3(0.07f, 0.025f, 0.07f);

            // The seed falls from a little above, then the soil closes over it.
            float fall = seconds * 0.5f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float drop = Mathf.Clamp01(time / fall);
                seed.transform.localPosition = Vector3.up * Mathf.Lerp(0.06f, 0f, drop * drop);
                float cover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fall, seconds, time));
                mound.transform.localScale = moundSize * cover;
                yield return null;
            }
            mound.transform.localScale = moundSize;
            Destroy(seed);
            Destroy(seedMaterial);
        }
    }
}
