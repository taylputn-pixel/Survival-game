using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

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

    // The kitchen sink. Bring a pot or jar and pour it out down the drain.
    public class Sink : MonoBehaviour, IInteractable
    {
        [Tooltip("Above the drain, where things are tipped out.")]
        [SerializeField] Transform drain;
        [SerializeField] float tipSeconds = 0.35f;
        [SerializeField] float pourSeconds = 0.8f;
        [SerializeField] float tipAngle = 115f;
        [SerializeField] float holdHeight = 0.22f;

        bool busy;

        public bool CanInteract(PlayerHands hands)
        {
            if (busy || hands.IsEmpty)
            {
                return false;
            }
            var emptyable = hands.Held.GetComponent<IEmptyable>();
            return emptyable != null && emptyable.HasContents;
        }

        public string PromptVerb(PlayerHands hands) => "Pour out";

        public void Interact(PlayerHands hands) => StartCoroutine(PourOut(hands));

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
