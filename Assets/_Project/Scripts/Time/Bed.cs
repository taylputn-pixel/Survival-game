using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // Grandma's bed. Lie down any time you like; the screen fades out and
    // you wake up the next morning, and the game is saved.
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
            clock.SleepUntilMorning();
            // A new morning is saved while the screen is dark.
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }
            yield return new WaitForSeconds(sleepSeconds);
            yield return fade.FadeTo(0f, fadeSeconds);

            hands.Controller.InputEnabled = true;
            sleeping = false;
        }
    }
}
