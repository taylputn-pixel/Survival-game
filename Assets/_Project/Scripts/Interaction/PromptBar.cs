using GrandmasHouse.Carrying;
using GrandmasHouse.Stations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Interaction
{
    // Decides which button prompts to show. Walking around: one small prompt
    // under the dot for what Interact would do. At a station: what Interact
    // does right now, and Back to step away. Reading a note: Back to put it
    // down. Every prompt asks ButtonGlyphs for its icon, so rebinding or a
    // new style shows up everywhere at once.
    public class PromptBar : MonoBehaviour
    {
        [SerializeField] PlayerHands hands;
        [SerializeField] NoteReader reader;
        [Tooltip("Under the reticle, for the thing you're looking at.")]
        [SerializeField] PromptView lookPrompt;
        [Tooltip("Along the bottom, while working at a station or reading.")]
        [SerializeField] PromptView usePrompt;
        [SerializeField] PromptView backPrompt;
        [Tooltip("Preview a style in Play mode. It's saved as the player's option.")]
        [SerializeField] PromptStyle style = PromptStyle.Auto;

        InputAction interactAction;
        InputAction backAction;

        void Awake()
        {
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
            style = PlayerOptions.PromptStyle;
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
            if (reader.IsOpen)
            {
                lookPrompt.Hide();
                usePrompt.Hide();
                backPrompt.Show(backAction, "Put down");
            }
            else if (station != null && station.Working)
            {
                lookPrompt.Hide();
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
