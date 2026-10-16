using UnityEngine;

namespace GrandmasHouse.Data
{
    // How a cut piece of one kind looks, for rebuilding saved pieces.
    [System.Serializable]
    public struct PieceLook
    {
        [Tooltip("The kind, as the first word of the piece's name (Onion).")]
        public string kind;
        public Material material;
        [Tooltip("Diameter and thickness of a slice, in meters.")]
        public Vector2 size;
    }
}
