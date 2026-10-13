using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandmasHouse.Interaction
{
    // Holds one of Grandma's notes up to read, filling the screen like paper
    // in your hands. Back or Interact puts it down. The clock waits.
    public class NoteReader : MonoBehaviour
    {
        [SerializeField] CanvasGroup panel;
        [SerializeField] Text title;
        [SerializeField] Text body;
        [SerializeField] Text margin;
        [SerializeField] Reticle reticle;
        [SerializeField] DayClock clock;
        [SerializeField] float fadeSeconds = 0.2f;

        InputAction interactAction;
        InputAction backAction;
        FirstPersonController controller;
        CursorLock cursorLock;
        int openedFrame;
        bool open;

        public bool IsOpen => open;

        void Awake()
        {
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        public void Show(NoteData note, PlayerHands hands)
        {
            controller = hands.Controller;
            controller.InputEnabled = false;
            // Back closes the note here, so it mustn't also free the mouse.
            cursorLock = hands.GetComponent<CursorLock>();
            cursorLock.BlockUnlock = true;
            clock.PausedForWork = true;
            reticle.Hidden = true;

            title.text = note.title;
            body.text = note.Body;
            margin.text = note.marginNote;
            margin.gameObject.SetActive(!string.IsNullOrEmpty(note.marginNote));

            open = true;
            openedFrame = Time.frameCount;
            panel.gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Fade(1f));
        }

        void Update()
        {
            if (!open || Time.frameCount == openedFrame)
            {
                return;
            }
            if (backAction.WasPressedThisFrame() || interactAction.WasPressedThisFrame())
            {
                Close();
            }
        }

        void Close()
        {
            open = false;
            reticle.Hidden = false;
            clock.PausedForWork = false;
            // Wait a frame so the same press doesn't also act in the world.
            StartCoroutine(GiveBackControl());
            StartCoroutine(Fade(0f));
        }

        IEnumerator GiveBackControl()
        {
            yield return null;
            controller.InputEnabled = true;
            cursorLock.BlockUnlock = false;
        }

        IEnumerator Fade(float to)
        {
            float from = panel.alpha;
            for (float time = 0f; time < fadeSeconds; time += Time.deltaTime)
            {
                panel.alpha = Mathf.Lerp(from, to, time / fadeSeconds);
                yield return null;
            }
            panel.alpha = to;
            if (to <= 0f)
            {
                panel.gameObject.SetActive(false);
            }
        }
    }
}
