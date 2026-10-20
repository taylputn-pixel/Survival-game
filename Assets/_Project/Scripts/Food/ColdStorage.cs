using UnityEngine;

namespace GrandmasHouse.Food
{
    // The cold inside a refrigerator or freezer. Its trigger box marks the
    // space inside; food in there spoils at this rate instead of 1.
    public class ColdStorage : MonoBehaviour
    {
        [Tooltip("How fast food spoils in here compared with room temperature. Fridge about 0.3, freezer 0.")]
        [Range(0f, 1f)] [SerializeField] float spoilRate = 0.3f;

        public float SpoilRate => spoilRate;

        // The coldest storage at a point, or 1 (room temperature) if none.
        public static float RateAt(Vector3 point)
        {
            float rate = 1f;
            foreach (var hit in Physics.OverlapSphere(point, 0.01f, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.isTrigger && hit.TryGetComponent(out ColdStorage cold))
                {
                    rate = Mathf.Min(rate, cold.spoilRate);
                }
            }
            return rate;
        }
    }
}
