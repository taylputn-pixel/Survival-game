using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Kitchen
{
    // Something whose contents can be poured out to start over (a pot of
    // brine with too much water, a jar packed wrong).
    public interface IEmptyable
    {
        bool HasContents { get; }
        void Empty();
        // Show a stream while it's tipped, if it has one.
        void SetPouring(bool pouring);
    }

    // The kitchen sink. Hold the cooking pot under the faucet to fill it
    // with water (Interact, held), or pour a pot or jar out down the drain
    // (Interact for a jar; the second button for a pot, since Interact fills it).
    public class Sink : MonoBehaviour, IInteractable, IAltInteractable
    {
        [Tooltip("Above the drain, where things are tipped out.")]
        [SerializeField] Transform drain;
        [Tooltip("Where a pot sits under the faucet to fill.")]
        [SerializeField] Transform fillSpot;
        [Tooltip("The column of water from the spout, shown while filling.")]
        [SerializeField] Transform faucetStream;
        [Tooltip("The spout's opening, where the stream starts.")]
        [SerializeField] Transform spoutTip;
        [Tooltip("How fast the pot fills (share of the pot per second).")]
        [SerializeField] float fillRate = 0.25f;
        [SerializeField] float tipSeconds = 0.35f;
        [SerializeField] float pourSeconds = 0.8f;
        [SerializeField] float tipAngle = 115f;
        [SerializeField] float holdHeight = 0.22f;

        bool busy;
        InputAction interactAction;
        SoundLoop tapSound;

        void Awake()
        {
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            tapSound = Sfx.Loop(SoundId.Pour, spoutTip);
            faucetStream.gameObject.SetActive(false);
        }

        public bool CanInteract(PlayerHands hands)
        {
            if (busy || hands.IsEmpty)
            {
                return false;
            }
            var pot = hands.Held.GetComponent<CookingPot>();
            if (pot != null)
            {
                return !pot.IsFull;
            }
            return CanPourOut(hands);
        }

        public string PromptVerb(PlayerHands hands) => hands.Held.GetComponent<CookingPot>() != null ? "Hold to fill with water" : "Pour out";

        public void Interact(PlayerHands hands)
        {
            var pot = hands.Held.GetComponent<CookingPot>();
            StartCoroutine(pot != null ? Fill(hands, pot) : PourOut(hands));
        }

        // A pot can always be poured out on the second button.
        public bool CanAltInteract(PlayerHands hands) => !busy && !hands.IsEmpty && hands.Held.GetComponent<CookingPot>() != null && CanPourOut(hands);

        public string AltVerb(PlayerHands hands) => "Pour out";

        public void AltInteract(PlayerHands hands) => StartCoroutine(PourOut(hands));

        bool CanPourOut(PlayerHands hands)
        {
            var emptyable = hands.Held.GetComponent<IEmptyable>();
            return emptyable != null && emptyable.HasContents;
        }

        // The pot goes under the faucet and fills while you hold the button.
        IEnumerator Fill(PlayerHands hands, CookingPot pot)
        {
            busy = true;
            hands.Controller.InputEnabled = false;
            Pickup item = hands.TakeHeld();
            Transform t = item.transform;
            Vector3 startPosition = t.position;
            Quaternion startRotation = t.rotation;
            yield return Move(t, startPosition, startRotation, fillSpot.position, fillSpot.rotation, tipSeconds);

            tapSound.Set(true);
            faucetStream.gameObject.SetActive(true);
            while (interactAction.IsPressed() && !pot.IsFull)
            {
                pot.AddWater(fillRate * Time.deltaTime);
                ShowStream(pot.SurfaceWorld);
                yield return null;
            }
            faucetStream.gameObject.SetActive(false);
            tapSound.Set(false);
            pot.LogBrine();

            hands.Give(item);
            hands.Controller.InputEnabled = true;
            busy = false;
        }

        // A thin column from the spout down to the water.
        void ShowStream(Vector3 water)
        {
            Vector3 top = spoutTip.position;
            float length = Mathf.Max(top.y - water.y, 0.01f);
            faucetStream.position = new Vector3(top.x, top.y - length / 2f, top.z);
            Vector3 size = faucetStream.localScale;
            faucetStream.localScale = new Vector3(size.x, length / 2f, size.z);
        }

        IEnumerator PourOut(PlayerHands hands)
        {
            busy = true;
            hands.Controller.InputEnabled = false;
            Pickup item = hands.TakeHeld();
            var emptyable = item.GetComponent<IEmptyable>();
            Transform t = item.transform;

            // Over the drain, tipped away from you.
            Vector3 startPosition = t.position;
            Quaternion startRotation = t.rotation;
            Vector3 away = Vector3.ProjectOnPlane(drain.position - hands.ViewPoint.position, Vector3.up).normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            Quaternion upright = Quaternion.LookRotation(away, Vector3.up);
            Quaternion tipped = Quaternion.AngleAxis(tipAngle, axis) * upright;
            Vector3 over = drain.position + Vector3.up * holdHeight - away * 0.05f;

            yield return Move(t, startPosition, startRotation, over, upright, tipSeconds);
            yield return Move(t, over, upright, over, tipped, tipSeconds);
            emptyable.SetPouring(true);
            if (emptyable is JarContents)
            {
                // A jar has no pouring sound of its own.
                Sfx.Play(SoundId.Pour, drain.position, 0.8f);
            }
            yield return new WaitForSeconds(pourSeconds);
            emptyable.Empty();
            emptyable.SetPouring(false);
            yield return Move(t, over, tipped, over, upright, tipSeconds);

            hands.Give(item);
            hands.Controller.InputEnabled = true;
            busy = false;
        }

        static IEnumerator Move(Transform t, Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, float seconds)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, time / seconds);
                t.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, k), Quaternion.Slerp(fromRotation, toRotation, k));
                yield return null;
            }
            t.SetPositionAndRotation(toPosition, toRotation);
        }
    }
}
