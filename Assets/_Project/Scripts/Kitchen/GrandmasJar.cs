using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // One of Grandma's own jars, put up before she passed: on a new game it
    // sits sealed and labeled in the pantry, part of what little food is
    // still good in the house. A loaded game replaces scene items with the
    // saved ones, so this only sets up the start.
    [RequireComponent(typeof(JarContents))]
    public class GrandmasJar : MonoBehaviour
    {
        [Tooltip("The jar as saved (JarContents' state).")]
        [TextArea(3, 8)]
        [SerializeField] string state;

        void Start()
        {
            if (!string.IsNullOrEmpty(state)) GetComponent<JarContents>().Restore(state);
        }
    }
}
