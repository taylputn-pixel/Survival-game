using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // Opening the Homestead starts a game hosted here, unless this computer
    // joined someone else's. Runs before everything else's Start, so the
    // world is shared before a save loads into it.
    [DefaultExecutionOrder(-1000)]
    public class HomesteadSession : MonoBehaviour
    {
        bool started;

        void Start()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
            {
                return;
            }
            started = NetSession.Ensure().Host();
            if (!started)
            {
                Debug.LogError("Couldn't start the game session.");
            }
        }

        // Leaving the Homestead (back to the title, or a test reloading it)
        // ends the game this scene started.
        void OnDestroy()
        {
            if (started && NetSession.Instance != null)
            {
                NetSession.Instance.End();
            }
        }
    }
}
