using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // The towel beside the stove where canned jars cool.
    public class CoolingTowel : MonoBehaviour
    {
        [SerializeField] Transform[] spots;

        public Transform FreeSpot()
        {
            foreach (var spot in spots)
            {
                if (spot.childCount == 0)
                {
                    return spot;
                }
            }
            return null;
        }
    }
}
