using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A round of a tree trunk, bought from Walt: split it on the chopping
    // block for firewood. It takes both hands and it's heavy: carrying one
    // you can only shuffle along, so it goes home in the truck.
    public class Log : MonoBehaviour
    {
        [Tooltip("Walking speed while carrying it, as a share of normal.")]
        [Range(0.1f, 1f)] [SerializeField] float carrySpeed = 0.35f;
        [Tooltip("Pieces of firewood it splits into.")]
        [SerializeField] int pieces = 4;

        public float CarrySpeed => carrySpeed;
        public int Pieces => pieces;
    }
}
