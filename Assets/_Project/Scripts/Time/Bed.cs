using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.DayCycle
{
    // Grandma's bed. Lie down any time you like and the screen fades out.
    // When the clock stops at night, you wake the next morning. When it runs
    // all night, time hurries by while you sleep until you're rested, and
    // you go on getting hungry. Once the sun is up you can press Interact
    // to get up early.
    // The game is saved as you wake.
    public class Bed : MonoBehaviour, IInteractable
    {
        [SerializeField] DayClock clock;
        [SerializeField] ScreenFade fade;
        [SerializeField] float fadeSeconds = 1.2f;
        [SerializeField] float sleepSeconds = 1.5f;

        bool sleeping;

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty && !sleeping;

        public void Interact(PlayerHands hands) => StartCoroutine(Sleep(hands));

        public string PromptVerb(PlayerHands hands) => "Sleep";

        IEnumerator Sleep(PlayerHands hands)
        {
            sleeping = true;
            hands.Controller.InputEnabled = false;

            yield return fade.FadeTo(1f, fadeSeconds);
            var needs = hands.GetComponent<PlayerNeeds>();
            if (clock.Mode.clockStopsAtNight || needs == null || !clock.Mode.hungerHasPenalties)
            {
                clock.SleepUntilMorning();
                yield return new WaitForSeconds(sleepSeconds);
            }
            else
            {
                yield return SleepUntilRested(needs);
            }
            // Saved while the screen is dark.
            if (SaveManager.Instance != null && !(needs != null && needs.IsDead))
            {
                SaveManager.Instance.Save();
            }
            yield return fade.FadeTo(0f, fadeSeconds);

            if (needs == null || !needs.IsDead)
            {
                hands.Controller.InputEnabled = true;
            }
            sleeping = false;
        }

        IEnumerator SleepUntilRested(PlayerNeeds needs)
        {
            var interact = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            needs.Asleep = true;
            clock.FastForward = needs.Settings.sleepHoursPerSecond;
            // A moment of dark before you can get up again.
            float earliestWake = Time.time + sleepSeconds;
            while (needs.State.rest < 100f && !needs.IsDead)
            {
                needs.CanGetUp = Time.time >= earliestWake && !clock.IsNight;
                if (needs.CanGetUp && interact.WasPressedThisFrame())
                {
                    break;
                }
                yield return null;
            }
            clock.FastForward = 0f;
            needs.CanGetUp = false;
            needs.Asleep = false;
        }
    }
}
