using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // A lamp that's on from dusk to dawn: the porch light, the kitchen lamp.
    [RequireComponent(typeof(Light))]
    public class NightLight : MonoBehaviour
    {
        [Tooltip("How dark it gets before it comes on (0 day, 1 full night).")]
        [Range(0f, 1f)] [SerializeField] float onAt = 0.3f;

        Light lamp;

        void Awake() => lamp = GetComponent<Light>();

        void Update() => lamp.enabled = SunCycle.Darkness >= onAt;
    }
}
