using GrandmasHouse.Carrying;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A whole head of garlic in its papery skin. Lay it on the cutting board,
    // press down to break it into cloves, then peel each clove.
    [RequireComponent(typeof(Pickup))]
    public class GarlicBulb : MonoBehaviour
    {
        [Tooltip("Cloves in one head.")]
        [SerializeField] int cloves = 10;

        public int Cloves => cloves;
    }
}
