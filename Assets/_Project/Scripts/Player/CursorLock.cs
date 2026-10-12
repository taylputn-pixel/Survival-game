using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Player
{
    // Hides and locks the cursor while walking around. Escape frees it
    // (handy in the editor); clicking the game view locks it again.
    public class CursorLock : MonoBehaviour
    {
        // Stations set this to false when they need a free cursor.
        public bool Locked { get; private set; }

        void Start() => SetLocked(true);

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (Locked && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetLocked(false);
            }
            else if (!Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetLocked(true);
            }
        }

        public void SetLocked(bool locked)
        {
            Locked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
