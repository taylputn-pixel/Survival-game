using UnityEngine;

namespace GrandmasHouse.Stations
{
    // Produce that can be cut on the cutting board: into rounds (cucumbers,
    // carrots, jalapeños), cubes (potatoes, winter squash) or a small dice
    // (onions). Each cut makes one piece either way. Length and thickness are
    // read from the model.
    public class Sliceable : MonoBehaviour
    {
        public enum Cut { Rounds, Cubes, Diced }

        [Tooltip("The color of the inside, shown on the cut slices.")]
        [SerializeField] Material fleshMaterial;
        [Tooltip("Rounds lie in a stack; cubes and dice heap up small.")]
        [SerializeField] Cut cut = Cut.Rounds;

        public Material FleshMaterial => fleshMaterial;
        public Cut Style => cut;

        // A cut piece's name says what it is: "Potato Cube", "Cucumber Slice".
        public string PieceName(string produce) => produce + (cut == Cut.Rounds ? " Slice" : " Cube");

        // How big a cube or a bit of dice is (onion is diced finer).
        public static float CubeEdge(string pieceName) =>
            pieceName.StartsWith("Onion") ? 0.012f : pieceName.StartsWith("Winter") ? 0.024f : 0.02f;

        public static bool IsCube(string pieceName) => pieceName.EndsWith(" Cube");
        public float Length { get; private set; }
        public float Diameter { get; private set; }

        void Awake()
        {
            Vector3 size = Vector3.Scale(GetComponent<MeshFilter>().sharedMesh.bounds.size, transform.lossyScale);
            Length = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            Diameter = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        }
    }
}
