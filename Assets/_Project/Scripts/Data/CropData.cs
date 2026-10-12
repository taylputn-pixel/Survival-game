using UnityEngine;

namespace GrandmasHouse.Data
{
    // One kind of plant: what it's called and how it looks and grows.
    [CreateAssetMenu(fileName = "Crop", menuName = "Grandma's House/Crop")]
    public class CropData : ScriptableObject
    {
        public string displayName = "Cucumber";
        [Tooltip("The seed's color as it drops into the soil.")]
        public Color seedColor = new Color(0.93f, 0.88f, 0.7f);
        [Tooltip("Seed size, in meters.")]
        public float seedSize = 0.008f;
    }
}
