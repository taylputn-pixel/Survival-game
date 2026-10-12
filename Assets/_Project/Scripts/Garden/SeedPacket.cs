using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // A packet of seeds you hold in your hand while planting a bed.
    public class SeedPacket : MonoBehaviour
    {
        [SerializeField] CropData crop;
        [SerializeField] int seeds = 20;

        public CropData Crop => crop;
        public bool HasSeeds => seeds > 0;

        public void TakeSeed() => seeds = Mathf.Max(seeds - 1, 0);
    }
}
