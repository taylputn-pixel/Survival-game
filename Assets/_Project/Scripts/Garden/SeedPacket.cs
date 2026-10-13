using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // A packet of seeds you hold in your hand while planting a bed.
    public class SeedPacket : MonoBehaviour, IItemState
    {
        [SerializeField] CropData crop;
        [SerializeField] int seeds = 20;

        public CropData Crop => crop;
        public bool HasSeeds => seeds > 0;

        public string Capture() => seeds.ToString();

        public void Restore(string json) => seeds = int.Parse(json);

        public void TakeSeed() => seeds = Mathf.Max(seeds - 1, 0);
    }
}
