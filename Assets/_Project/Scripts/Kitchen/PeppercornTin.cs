using GrandmasHouse.Carrying;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Grandma's tin of whole peppercorns, kept in the pantry. Set it on the
    // packing towel and take a pinch at a time for a jar.
    [RequireComponent(typeof(Pickup))]
    public class PeppercornTin : MonoBehaviour
    {
        [SerializeField] Material peppercornMaterial;
        [SerializeField] float peppercornSize = 0.005f;
        [Tooltip("Peppercorns in one pinch.")]
        [SerializeField] int pinch = 8;

        // A pinch comes up out of the tin.
        public System.Collections.Generic.List<Transform> TakePinch()
        {
            var corns = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < pinch; i++)
            {
                Transform corn = JarContents.MakePeppercorn(peppercornMaterial, peppercornSize);
                corn.position = transform.position + Vector3.up * 0.06f + Random.insideUnitSphere * 0.006f;
                corns.Add(corn);
            }
            return corns;
        }
    }
}
