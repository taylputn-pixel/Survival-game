using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Garden
{
    // A weed in the garden bed. It grows a little each night. Look at it
    // with empty hands, hold Interact to grab it, then drag the mouse (or
    // the right stick) back toward you: it stretches and stretches, then
    // pops out roots and all. Let go early and it settles back in.
    public class Weed : MonoBehaviour, IInteractable
    {
        [Tooltip("The leafy part above the soil.")]
        [SerializeField] Transform top;
        [Tooltip("The root, hidden in the soil until the weed is pulled.")]
        [SerializeField] Transform root;
        [Tooltip("How far the weed rises out of the soil as it comes loose, in meters.")]
        [SerializeField] float pullRise = 0.05f;
        [SerializeField] float tossSeconds = 0.45f;

        public int Age { get; private set; }
        public GardenBed Bed { get; set; }
        public float Size => Mathf.Lerp(0.9f, 1.8f, Age / (float)Mathf.Max(maxAge, 1));

        int maxAge = 3;
        float pull;
        float shownPull;
        bool held;
        bool pulledOut;
        InputAction interactAction;
        InputAction lookAction;

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty && !pulledOut && !held;

        public string PromptVerb(PlayerHands hands) => "Hold and drag back to pull weed";

        public void Interact(PlayerHands hands)
        {
            interactAction ??= InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            lookAction ??= InputSystem.actions.FindAction("Player/Look", throwIfNotFound: true);
            StartCoroutine(Pulling(hands));
        }

        // While you hold the weed your view stays put and the mouse or stick
        // pulls instead. Only pulling back counts; ease off and it gives a
        // little. Older weeds take a little more pulling.
        IEnumerator Pulling(PlayerHands hands)
        {
            GardenSettings settings = Bed.Settings;
            float amount = 0f;
            held = true;
            hands.Controller.InputEnabled = false;
            while (interactAction.IsPressed())
            {
                Vector2 input = lookAction.ReadValue<Vector2>();
                float back = Mathf.Max(0f, -input.y);
                float gained = lookAction.activeControl?.device is Gamepad
                    ? back * Time.deltaTime / settings.pullStickSeconds
                    : back * PlayerOptions.MouseSensitivity / settings.pullMouseDistance;
                amount = gained > 0f
                    ? amount + gained / Size
                    : Mathf.Max(0f, amount - settings.pullSlackPerSecond * Time.deltaTime);
                SetPull(amount, holding: true);
                if (amount >= 1f)
                {
                    hands.Controller.InputEnabled = true;
                    Bed.PullOut(this);
                    yield break;
                }
                yield return null;
            }
            hands.Controller.InputEnabled = true;
            SetPull(0f, holding: false);
        }

        public void Sprout(int startAge, int oldest)
        {
            maxAge = oldest;
            Age = Mathf.Clamp(startAge, 0, maxAge);
            transform.localScale = Vector3.one * Size;
        }

        public void GrowOvernight()
        {
            Age = Mathf.Min(Age + 1, maxAge);
            transform.localScale = Vector3.one * Size;
        }

        // How loose the weed is, from 0 (in the ground) to 1 (out).
        public void SetPull(float amount, bool holding)
        {
            pull = Mathf.Clamp01(amount);
            held = holding;
        }

        public void PullOut(Vector3 tossTo)
        {
            pulledOut = true;
            held = false;
            foreach (var hit in GetComponentsInChildren<Collider>())
            {
                hit.enabled = false;
            }
            Sfx.Play(SoundId.WeedPull, transform.position);
            StartCoroutine(Toss(tossTo));
        }

        void Update()
        {
            if (pulledOut || Bed == null)
            {
                return;
            }
            // Let go and it springs back down; held, it follows your pull with a little give.
            float target = held ? pull : 0f;
            shownPull = Mathf.Lerp(shownPull, target, 1f - Mathf.Exp(-(held ? 20f : 8f) * Time.deltaTime));
            top.localPosition = Vector3.up * shownPull * pullRise;
            // It stretches tall and thin, stretching easier at first and stiffer near the end.
            float stretch = 1f + Mathf.Sqrt(shownPull) * Bed.Settings.pullStretch;
            float thin = 1f / Mathf.Sqrt(stretch);
            top.localScale = new Vector3(thin, stretch, thin);
            // It trembles harder the closer it gets to giving way.
            float shake = held ? Mathf.Sin(Time.time * 45f) * shownPull * shownPull * 6f : 0f;
            top.localRotation = Quaternion.Euler(shake, 0f, shake * 0.5f);
        }

        IEnumerator Toss(Vector3 tossTo)
        {
            // Pops free with its root showing, then is tossed onto the pile.
            root.gameObject.SetActive(true);
            Vector3 start = transform.position + Vector3.up * pullRise * Size;
            top.localPosition = Vector3.zero;
            top.localScale = Vector3.one;
            top.localRotation = Quaternion.identity;
            Quaternion startRotation = transform.rotation;
            Quaternion endRotation = Quaternion.Euler(80f, Random.Range(0f, 360f), 0f);

            for (float time = 0f; time < tossSeconds; time += Time.deltaTime)
            {
                float t = time / tossSeconds;
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;
                transform.SetPositionAndRotation(Vector3.Lerp(start, tossTo, t) + arc, Quaternion.Slerp(startRotation, endRotation, t));
                // The stretched leaves snap short and wobble as it flies.
                float boing = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 0.35f;
                top.localScale = new Vector3(1f + boing * 0.5f, 1f - boing, 1f + boing * 0.5f);
                yield return null;
            }
            top.localScale = Vector3.one;
            transform.SetPositionAndRotation(tossTo, endRotation);
        }
    }
}
