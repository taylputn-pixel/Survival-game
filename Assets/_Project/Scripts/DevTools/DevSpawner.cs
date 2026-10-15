using GrandmasHouse.Carrying;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.DevTools
{
    // Testing shortcut, Editor and development builds only: F5 drops a
    // cucumber in front of you, F6 a head of dill, F7 a head of garlic. Saves
    // regrowing the garden every time you want to test the kitchen.
    public class DevSpawner : MonoBehaviour
    {
        [SerializeField] PlayerHands hands;
        [SerializeField] Pickup cucumber;
        [SerializeField] Pickup dill;
        [SerializeField] Pickup garlic;
        [SerializeField] float distance = 0.6f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || !hands.Controller.InputEnabled)
            {
                return;
            }
            if (keys.f5Key.wasPressedThisFrame) Spawn(cucumber);
            if (keys.f6Key.wasPressedThisFrame) Spawn(dill);
            if (keys.f7Key.wasPressedThisFrame) Spawn(garlic);
        }

        void Spawn(Pickup prefab)
        {
            Transform eye = hands.ViewPoint;
            Vector3 ahead = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            Vector3 at = eye.position + ahead * distance + Vector3.down * 0.3f;
            Pickup item = Instantiate(prefab, at, Quaternion.Euler(0f, eye.eulerAngles.y, 0f) * prefab.transform.rotation);
            item.name = prefab.name;
            item.Drop();
        }
#else
        void Awake() => Destroy(this);
#endif
    }
}
