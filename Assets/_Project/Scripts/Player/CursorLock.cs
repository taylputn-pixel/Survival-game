using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Player
{
    // Hides and locks the cursor while playing. A menu frees it so the mouse
    // can click buttons; clicking the game view locks it again otherwise
    // (handy when the editor or another window took the cursor).
    public class CursorLock : MonoBehaviour
    {
        public bool Locked { get; private set; }
        // Set by menus while they're open.
        public bool Free
        {
            get => free;
            set
            {
                free = value;
                SetLocked(!value);
            }
        }

        bool free;

        void Start() => SetLocked(true);

        void Update()
        {
            var mouse = Mouse.current;
            if (!Locked && !free && mouse != null && mouse.leftButton.wasPressedThisFrame)
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
