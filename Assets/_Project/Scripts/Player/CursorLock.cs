using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Player
{
    // Hides and locks the cursor while walking around. Escape frees it
    // (handy in the editor); clicking the game view locks it again.
    public class CursorLock : MonoBehaviour
    {
        public bool Locked { get; private set; }
        // Stations use Escape to leave, so they turn off Escape-to-unlock while active.
        public bool BlockUnlock { get; set; }

        void Start() => SetLocked(true);

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (Locked && !BlockUnlock && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
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
