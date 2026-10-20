using UnityEngine;

namespace GrandmasHouse.Net
{
    // Where players appear when they join: one child per player, in order.
    public class SpawnPoints : MonoBehaviour
    {
        static SpawnPoints current;

        void Awake() => current = this;

        void OnDestroy()
        {
            if (current == this) current = null;
        }

        public static Transform Pick(int index)
        {
            if (current == null || current.transform.childCount == 0)
            {
                return null;
            }
            return current.transform.GetChild(index % current.transform.childCount);
        }
    }
}
