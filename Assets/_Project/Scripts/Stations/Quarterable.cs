using UnityEngine;

namespace GrandmasHouse.Stations
{
    // Produce cut into wedges on the cutting board (tomatoes): the first chop
    // halves it, the second quarters it. Its size is read from the model.
    public class Quarterable : MonoBehaviour
    {
        [Tooltip("The color of the inside, shown on the cut faces.")]
        [SerializeField] Material fleshMaterial;

        public Material FleshMaterial => fleshMaterial;
        public float Diameter { get; private set; }

        void Awake()
        {
            Vector3 size = Vector3.Scale(GetComponent<MeshFilter>().sharedMesh.bounds.size, transform.lossyScale);
            Diameter = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }
    }
}
