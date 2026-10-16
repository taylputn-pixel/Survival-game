using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Garden
{
    // A weed in the garden bed. It grows a little each night. Look at it
    // with empty hands and hold Interact to work it loose; it comes out
    // roots and all. Let go early and it settles back in.
    public class Weed : MonoBehaviour, IInteractable
    {
        [Tooltip("The leafy part above the soil.")]
        [SerializeField] Transform top;
        [Tooltip("The root, hidden in the soil until the weed is pulled.")]
        [SerializeField] Transform root;
        [Tooltip("How far the weed rises out of the soil as it comes loose, in meters.")]
        [SerializeField] float pullRise = 0.05f;
        [SerializeField] float tossSeconds = 0.45f;
        [Tooltip("Walk this far from the weed while pulling and you let go, in meters.")]
        [SerializeField] float letGoDistance = 3f;

        public int Age { get; private set; }
        public GardenBed Bed { get; set; }
        public float Size => Mathf.Lerp(0.9f, 1.8f, Age / (float)Mathf.Max(maxAge, 1));

        int maxAge = 3;
        float pull;
        float shownPull;
        bool held;
        bool pulledOut;
        InputAction interactAction;

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty && !pulledOut && !held;

        public string PromptVerb(PlayerHands hands) => "Hold to pull weed";

        public void Interact(PlayerHands hands)
        {
            interactAction ??= InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            StartCoroutine(Pulling(hands));
        }

        // Older weeds take a little longer. Walking off lets go.
        IEnumerator Pulling(PlayerHands hands)
        {
            float seconds = Bed.Settings.pullSeconds * Size;
            float amount = 0f;
            held = true;
            while (interactAction.IsPressed() && Vector3.Distance(hands.ViewPoint.position, transform.position) < letGoDistance)
            {
                amount += Time.deltaTime / seconds;
                SetPull(amount, holding: true);
                if (amount >= 1f)
                {
                    Bed.PullOut(this);
                    yield break;
                }
                yield return null;
            }
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
            if (pulledOut)
            {
                return;
            }
            // Let go and it settles back down; held, it follows your pull with a little give.
            float target = held ? pull : 0f;
            shownPull = Mathf.Lerp(shownPull, target, 1f - Mathf.Exp(-(held ? 20f : 8f) * Time.deltaTime));
            top.localPosition = Vector3.up * shownPull * pullRise;
            top.localScale = new Vector3(1f, 1f + shownPull * 0.4f, 1f);
            // A little shiver while you work it loose.
            float shake = held ? Mathf.Sin(Time.time * 40f) * shownPull * 4f : 0f;
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
                yield return null;
            }
            transform.SetPositionAndRotation(tossTo, endRotation);
        }
    }
}
