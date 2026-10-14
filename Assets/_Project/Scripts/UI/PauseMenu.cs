using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using GrandmasHouse.Stations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GrandmasHouse.UI
{
    // Esc or Start while walking around stops the world and opens a small
    // menu: carry on, change options, or go back to the title. At a station
    // or reading a note, the same key steps back instead.
    [RequireComponent(typeof(MenuStack))]
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] PlayerHands hands;
        [SerializeField] NoteReader reader;
        [SerializeField] Reticle reticle;
        [SerializeField] string titleScene = "Title";

        MenuStack menus;
        InputAction pauseAction;
        CursorLock cursorLock;
        bool paused;
        int closedFrame = -1;

        void Awake()
        {
            menus = GetComponent<MenuStack>();
            menus.DimBehind = true;
            menus.Emptied += Resume;
            pauseAction = InputSystem.actions.FindAction("Player/Pause", throwIfNotFound: true);
            cursorLock = hands.GetComponent<CursorLock>();
        }

        void Update()
        {
            if (paused || !pauseAction.WasPressedThisFrame() || Time.frameCount == closedFrame)
            {
                return;
            }
            // Only when nothing else wants the key: no station, no note, not busy (asleep, filling the can).
            bool free = Station.Current == null && !reader.IsOpen && reader.ClosedFrame != Time.frameCount && hands.Controller.InputEnabled;
            if (free)
            {
                Pause();
            }
        }

        void Pause()
        {
            paused = true;
            Time.timeScale = 0f;
            // Simmering pots and pouring hush while paused.
            AudioListener.pause = true;
            hands.Controller.InputEnabled = false;
            cursorLock.Free = true;
            reticle.Hidden = true;

            RectTransform panel = UIKit.Panel("Paused", transform, 560f);
            UIKit.Label(panel, "Paused", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            var resume = UIKit.Button(panel, "Carry on", menus.Pop);
            UIKit.Button(panel, "Options", () => OptionsScreen.Open(menus, transform));
            UIKit.Button(panel, "Back to the title", QuitToTitle);
            menus.Push(panel.gameObject, resume);
        }

        void Resume()
        {
            paused = false;
            closedFrame = Time.frameCount;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            hands.Controller.InputEnabled = true;
            cursorLock.Free = false;
            reticle.Hidden = false;
        }

        void QuitToTitle()
        {
            AudioListener.pause = false;
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleScene);
        }
    }
}
