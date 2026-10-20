using GrandmasHouse.Carrying;
using GrandmasHouse.Player;
using GrandmasHouse.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Interaction
{
    // Decides which button prompts to show. Walking around: a small prompt
    // under the dot for what Interact would do, and beside it what Pick up
    // would do when that's something different. At a station: what Interact
    // does right now, and Back to step away. Reading a note: Back to put it
    // down, and in a book, Previous to turn back. At a station whose work can
    // be turned (a plant), Previous and Next to turn it. Every prompt asks ButtonGlyphs for its icon, so rebinding or a
    // new style shows up everywhere at once.
    public class PromptBar : MonoBehaviour
    {
        [SerializeField] PlayerHands hands;
        [SerializeField] NoteReader reader;
        [Tooltip("Under the reticle, for the thing you're looking at.")]
        [SerializeField] PromptView lookPrompt;
        [Tooltip("Next to the look prompt, for the Pick up button.")]
        [SerializeField] PromptView altPrompt;
        [Tooltip("Along the bottom, while working at a station or reading.")]
        [SerializeField] PromptView usePrompt;
        [SerializeField] PromptView backPrompt;
        [Tooltip("Preview a style in Play mode. It's saved as the player's option.")]
        [SerializeField] PromptStyle style = PromptStyle.Auto;

        InputAction interactAction;
        InputAction pickUpAction;
        InputAction backAction;
        InputAction previousAction;
        InputAction nextAction;
        // Copies of the use prompt, sitting before it in the row.
        PromptView previousPrompt;
        PromptView nextPrompt;
        PlayerNeeds needs;

        void Awake()
        {
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            needs = hands.GetComponent<PlayerNeeds>();
            pickUpAction = InputSystem.actions.FindAction("Player/PickUp", throwIfNotFound: true);
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
            previousAction = InputSystem.actions.FindAction("Player/Previous", throwIfNotFound: true);
            previousPrompt = Instantiate(usePrompt, usePrompt.transform.parent);
            previousPrompt.name = "Previous Prompt";
            previousPrompt.transform.SetSiblingIndex(usePrompt.transform.GetSiblingIndex());
            previousPrompt.Hide();
            style = PlayerOptions.PromptStyle;
            nextAction = InputSystem.actions.FindAction("Player/Next", throwIfNotFound: true);
            nextPrompt = Instantiate(usePrompt, usePrompt.transform.parent);
            nextPrompt.name = "Next Prompt";
            nextPrompt.transform.SetSiblingIndex(usePrompt.transform.GetSiblingIndex());
            nextPrompt.Hide();
        }

        void OnValidate()
        {
            if (Application.isPlaying && PlayerOptions.PromptStyle != style)
            {
                PlayerOptions.PromptStyle = style;
            }
        }

        void LateUpdate()
        {
            Station station = Station.Current;
            bool walking = !reader.IsOpen && !(station != null && station.Working) && hands.Controller.InputEnabled;
            if (walking && hands.AltVerb != null)
            {
                altPrompt.Show(pickUpAction, hands.AltVerb);
            }
            else
            {
                altPrompt.Hide();
            }

            if (needs != null && needs.Asleep)
            {
                lookPrompt.Hide();
                previousPrompt.Hide();
                nextPrompt.Hide();
                backPrompt.Hide();
                if (needs.CanGetUp)
                {
                    usePrompt.Show(interactAction, "Get up");
                }
                else
                {
                    usePrompt.Hide();
                }
            }
            else if (reader.IsOpen)
            {
                lookPrompt.Hide();
                if (reader.HasPreviousPage)
                {
                    previousPrompt.Show(previousAction, "Previous page");
                }
                else
                {
                    previousPrompt.Hide();
                }
                nextPrompt.Hide();
                if (reader.HasNextPage)
                {
                    usePrompt.Show(interactAction, "Turn the page");
                }
                else
                {
                    usePrompt.Hide();
                }
                backPrompt.Show(backAction, "Put down");
            }
            else if (station != null && station.Working)
            {
                lookPrompt.Hide();
                if (station.CanTurn)
                {
                    previousPrompt.Show(previousAction, "Turn left");
                    nextPrompt.Show(nextAction, "Turn right");
                }
                else
                {
                    previousPrompt.Hide();
                    nextPrompt.Hide();
                }
                if (station.UseHint != null)
                {
                    usePrompt.Show(interactAction, station.UseHint);
                }
                else
                {
                    usePrompt.Hide();
                }
                backPrompt.Show(backAction, "Step back");
            }
            else
            {
                usePrompt.Hide();
                previousPrompt.Hide();
                nextPrompt.Hide();
                backPrompt.Hide();
                if (hands.Controller.InputEnabled && hands.Verb != null)
                {
                    lookPrompt.Show(interactAction, hands.Verb);
                }
                else
                {
                    lookPrompt.Hide();
                }
            }
        }
    }
}
