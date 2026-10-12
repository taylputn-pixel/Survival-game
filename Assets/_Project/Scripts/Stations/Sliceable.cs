using UnityEngine;

namespace GrandmasHouse.Stations
{
    // Produce that can be sliced into rounds on the cutting board (cucumbers,
    // carrots, zucchini). Length and thickness are read from the model.
    public class Sliceable : MonoBehaviour
    {
        [Tooltip("The color of the inside, shown on the cut slices.")]
        [SerializeField] Material fleshMaterial;

        public Material FleshMaterial => fleshMaterial;
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
