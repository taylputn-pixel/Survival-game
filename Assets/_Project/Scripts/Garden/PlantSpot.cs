using System.Collections;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // One place in a garden bed where a single plant can grow.
    public class PlantSpot : MonoBehaviour
    {
        [SerializeField] Material soilMaterial;
        [Tooltip("Shows where to plant while the spot is empty.")]
        [SerializeField] GameObject marker;

        public CropData Crop { get; private set; }
        public bool IsEmpty => Crop == null;

        // Press a hole into the soil, drop the seed in, and cover it over.
        public void Plant(CropData crop, float seconds)
        {
            Crop = crop;
            if (marker != null)
            {
                marker.SetActive(false);
            }
            StartCoroutine(Planting(crop, seconds));
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

            var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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
