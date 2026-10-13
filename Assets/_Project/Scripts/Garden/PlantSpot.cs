using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // One place in a garden bed where a single plant can grow. A watered plant
    // moves up one stage each night; a dry one just waits (it never dies in
    // Cozy). Once ready, it holds its harvest for you to pick by hand.
    public class PlantSpot : MonoBehaviour
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

        MaterialPropertyBlock block;
        GameObject mound;
        GameObject plant;
        readonly List<Transform> harvestPoints = new List<Transform>();
        readonly List<Pickup> ripe = new List<Pickup>();
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

        // Called once each night, before the soil dries out again.
        public void GrowOvernight(bool crowded)
        {
            if (Crop != null && Water >= settings.wateredEnough)
            {
                if (Stage < Crop.stages.Length - 1)
                {
                    ShowStage(Stage + 1);
                    if (IsReady)
                    {
                        Ripen(Crop.singleHarvest ? harvestPoints.Count : Crop.harvestPerNight + (crowded ? 0 : Crop.tendedBonus));
                    }
                }
                else if (!Crop.singleHarvest)
                {
                    Ripen(Crop.harvestPerNight + (crowded ? 0 : Crop.tendedBonus));
                }
            }
            Water = 0f;
            ShowWater();
        }

        // Press a hole into the soil, drop the seed in, and cover it over.
        public void Plant(CropData crop, float seconds)
        {
            Crop = crop;
            Stage = -1;
            harvested = false;
            marker.SetActive(false);
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
            plant = Instantiate(Crop.stages[stage], transform);
            plant.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            harvestPoints.Clear();
            foreach (var child in plant.GetComponentsInChildren<Transform>())
            {
                if (child.name.StartsWith("HarvestPoint"))
                {
                    harvestPoints.Add(child);
                }
            }
        }

        void Ripen(int count)
        {
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
            Crop = null;
            Stage = -1;
            harvested = false;
            marker.SetActive(true);
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

            mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mound.name = "Mound";
            Destroy(mound.GetComponent<Collider>());
            mound.transform.SetParent(transform, false);
            mound.GetComponent<Renderer>().sharedMaterial = soilMaterial;
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
