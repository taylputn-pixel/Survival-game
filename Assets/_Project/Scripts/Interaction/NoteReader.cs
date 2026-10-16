using System.Collections;
using GrandmasHouse.Audio;
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
    // in your hands. Back or Interact puts it down. A book opens to its first
    // page: Interact turns the page (and closes it after the last), Previous
    // turns back, Back puts it down. The clock waits.
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
        InputAction previousAction;
        NoteData showing;
        int page;
        FirstPersonController controller;
        int openedFrame;
        bool open;

        public bool IsOpen => open;
        public bool HasNextPage => open && showing != null && page < showing.PageCount - 1;
        public bool HasPreviousPage => open && showing != null && page > 0;
        // The frame the note was put down, so the same press doesn't also pause.
        public int ClosedFrame { get; private set; } = -1;

        void Awake()
        {
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
            previousAction = InputSystem.actions.FindAction("Player/Previous", throwIfNotFound: true);
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        public void Show(NoteData note, PlayerHands hands)
        {
            controller = hands.Controller;
            controller.InputEnabled = false;
            clock.PausedForWork = true;
            reticle.Hidden = true;

            showing = note;
            ShowPage(0);

            open = true;
            openedFrame = Time.frameCount;
            panel.gameObject.SetActive(true);
            Sfx.PlayFlat(SoundId.Paper);
            StopAllCoroutines();
            StartCoroutine(Fade(1f));
        }

        void Update()
        {
            if (!open || Time.frameCount == openedFrame)
            {
                return;
            }
            if (backAction.WasPressedThisFrame())
            {
                Close();
            }
            else if (interactAction.WasPressedThisFrame())
            {
                if (HasNextPage)
                {
                    TurnTo(page + 1);
                }
                else
                {
                    Close();
                }
            }
            else if (previousAction.WasPressedThisFrame() && page > 0)
            {
                TurnTo(page - 1);
            }
        }

        void TurnTo(int index)
        {
            Sfx.PlayFlat(SoundId.Paper, 0.7f);
            ShowPage(index);
        }

        // A book's page number sits where a note's margin writing would.
        void ShowPage(int index)
        {
            page = index;
            title.text = showing.title;
            body.text = showing.Page(page);
            string side = showing.PageCount > 1 ? "Page " + (page + 1) + " of " + showing.PageCount : showing.marginNote;
            margin.text = side;
            margin.gameObject.SetActive(!string.IsNullOrEmpty(side));
        }

        void Close()
        {
            open = false;
            ClosedFrame = Time.frameCount;
            Sfx.PlayFlat(SoundId.Paper, 0.6f);
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
