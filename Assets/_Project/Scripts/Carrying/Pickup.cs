using System.Collections;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Carrying
{
    // Something small enough to carry in your hands: produce, jars, tools.
    [RequireComponent(typeof(Rigidbody))]
    public class Pickup : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "Item";
        [Tooltip("Extra rotation while held, so long items like cucumbers sit naturally in view.")]
        [SerializeField] Vector3 heldRotation;

        public string DisplayName => displayName;
        public Quaternion HeldRotation => Quaternion.Euler(heldRotation);

        Rigidbody body;
        Collider[] colliders;
        Renderer[] renderers;
        // How the item rests when set down (e.g. a cucumber lies on its side).
        Quaternion restTilt;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            colliders = GetComponentsInChildren<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            Vector3 euler = transform.rotation.eulerAngles;
            restTilt = Quaternion.Euler(euler.x, 0f, euler.z);
        }

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty;

        public void Interact(PlayerHands hands) => hands.Hold(this);

        public void OnPickedUp()
        {
            StopAllCoroutines();
            body.isKinematic = true;
            // Interpolation would fight the code that moves the item by hand.
            body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);
        }

        // Sets the item down resting on a surface, facing the given direction.
        // It glides from the hand to its spot, then stays exactly where it's put.
        public void PlaceOn(Vector3 surfacePoint, float yaw, float glideSeconds)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            // Try the final pose to measure how far the item must sit above the surface.
            Quaternion endRotation = Quaternion.Euler(0f, yaw, 0f) * restTilt;
            transform.SetPositionAndRotation(surfacePoint, endRotation);
            Vector3 endPosition = surfacePoint + Vector3.up * (surfacePoint.y - WorldBounds().min.y);
            transform.SetPositionAndRotation(startPosition, startRotation);

            StopAllCoroutines();
            StartCoroutine(GlideTo(endPosition, endRotation, glideSeconds));
        }

        IEnumerator GlideTo(Vector3 endPosition, Quaternion endRotation, float seconds)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                // Ease out: quick at first, gentle as it touches down.
                float t = 1f - Mathf.Pow(1f - time / seconds, 3f);
                transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPosition, endPosition, t),
                    Quaternion.SlerpUnclamped(startRotation, endRotation, t));
                yield return null;
            }

            transform.SetPositionAndRotation(endPosition, endRotation);
            SetCollidersEnabled(true);
            body.isKinematic = true;
        }

        // Lets go in mid-air, so the item falls and settles on its own.
        public void Drop()
        {
            SetCollidersEnabled(true);
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        Bounds WorldBounds()
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        void SetCollidersEnabled(bool enabled)
        {
            foreach (var c in colliders)
            {
                c.enabled = enabled;
            }
        }
    }
}
