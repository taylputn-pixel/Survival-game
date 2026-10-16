using System;
using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Carrying
{
    // Something small enough to carry in your hands: produce, jars, tools,
    // or a container like a basket.
    [RequireComponent(typeof(Rigidbody))]
    public class Pickup : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "Item";
        [SerializeField] ItemCategory category = ItemCategory.Other;
        [Tooltip("Extra rotation while held, so long items like cucumbers sit naturally in view.")]
        [SerializeField] Vector3 heldRotation;
        [Tooltip("Stay level while carried instead of tilting with your view (baskets, crates).")]
        [SerializeField] bool keepUpright;
        [Tooltip("How many of these fit in one hand (jalapeños 4, tomatoes 2, a jar 1).")]
        [Min(1)] [SerializeField] int perHand = 1;
        [Tooltip("Takes both hands to carry. Baskets, cookware and tools always do.")]
        [SerializeField] bool bothHands;
        [Tooltip("Use a different hold spot than the default in PlayerSettings.")]
        [SerializeField] bool useCustomHoldOffset;
        [SerializeField] Vector3 customHoldOffset = new Vector3(0f, -0.45f, 0.55f);

        public string DisplayName => displayName;
        public ItemCategory Category => category;
        public Quaternion HeldRotation => Quaternion.Euler(heldRotation);
        public int PerHand => perHand;
        // Can be set down or dropped anywhere (a handful of slices can't).
        public bool CanSetDown { get; private set; } = true;

        // For things put together while playing (a handful of slices): what
        // the prefab would otherwise say.
        public void Configure(string name, ItemCategory itemCategory, bool bothHands, bool upright, bool settable)
        {
            displayName = name;
            category = itemCategory;
            this.bothHands = bothHands;
            keepUpright = upright;
            CanSetDown = settable;
        }
        public bool NeedsBothHands => bothHands || OwnContainer != null || category == ItemCategory.Cookware || category == ItemCategory.Tool;
        public bool KeepUpright => keepUpright;
        // How the item rests when set down (e.g. a cucumber lies on its side).
        public Quaternion RestTilt { get; private set; }
        // Distance from the item's center down to its bottom when resting.
        public float RestHalfHeight { get; private set; }
        public Container OwnContainer { get; private set; }
        public Container InContainer { get; private set; }

        // True when picked up, false once it's resting somewhere again.
        public event Action<bool> HeldChanged;

        Rigidbody body;
        Collider[] colliders;
        Renderer[] renderers;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.None;
            // Cached now, before anything is put inside, so a container's
            // contents are never treated as part of the container itself.
            colliders = GetComponentsInChildren<Collider>();
            // Only solid parts count toward its size; steam and water streams don't.
            renderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r => r is MeshRenderer || r is SkinnedMeshRenderer);
            OwnContainer = GetComponent<Container>();
            Vector3 euler = transform.rotation.eulerAngles;
            RestTilt = Quaternion.Euler(euler.x, 0f, euler.z);
            RestHalfHeight = transform.position.y - WorldBounds().min.y;
        }

        // Lying still where it was put (rather than tumbling with physics).
        public bool IsResting => body.isKinematic;
        public bool CollidersOn => colliders.Length > 0 && colliders[0].enabled;

        // Used when loading a save: puts the item back exactly as it was.
        public void SetResting(bool resting, bool collidersOn)
        {
            StopAllCoroutines();
            body.isKinematic = resting;
            body.interpolation = resting ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            // The physics body must move with it, or it pulls the item back to where it was.
            body.position = transform.position;
            body.rotation = transform.rotation;
            SetCollidersEnabled(collidersOn);
        }

        public Vector3 HoldOffset(Vector3 defaultOffset) => useCustomHoldOffset ? customHoldOffset : defaultOffset;

        public bool CanInteract(PlayerHands hands)
        {
            foreach (var guard in GetComponents<IPickupGuard>())
            {
                if (!guard.AllowPickup(hands))
                {
                    return false;
                }
            }
            return hands.CanHandle(this);
        }

        public void Interact(PlayerHands hands) => hands.Handle(this);

        public string PromptVerb(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                return "Pick up";
            }
            if (hands.CanCarryMore(this))
            {
                return hands.Held.DisplayName == DisplayName ? "Pick up another" : "Pick up";
            }
            return hands.Held.OwnContainer != null ? "Gather" : "Put in";
        }

        public void OnPickedUp()
        {
            StopAllCoroutines();
            body.isKinematic = true;
            // Interpolation would fight the code that moves the item by hand.
            body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);
            Sfx.Play(SoundId.PickUp, transform.position);
            HeldChanged?.Invoke(true);
        }

        // Sets the item down resting on a surface, facing the given direction.
        // It glides from the hand to its spot, then stays exactly where it's put.
        public void PlaceOn(Vector3 surfacePoint, float yaw, float glideSeconds)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            // Try the final pose to measure how far the item must sit above the surface.
            Quaternion endRotation = Quaternion.Euler(0f, yaw, 0f) * RestTilt;
            transform.SetPositionAndRotation(surfacePoint, endRotation);
            Vector3 endPosition = surfacePoint + Vector3.up * (surfacePoint.y - WorldBounds().min.y);
            transform.SetPositionAndRotation(startPosition, startRotation);

            StopAllCoroutines();
            StartCoroutine(Glide(endPosition, endRotation, glideSeconds, local: false, collidersOn: true));
        }

        // Lets go in mid-air, so the item falls and settles on its own.
        public void Drop()
        {
            SetCollidersEnabled(true);
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            HeldChanged?.Invoke(false);
        }

        // Called by a container: the item becomes part of it and glides into its slot.
        public void MoveInto(Container container, Vector3 localPosition, Quaternion localRotation, float glideSeconds, bool collidersOn)
        {
            InContainer = container;
            AttachTo(container.transform, localPosition, localRotation, glideSeconds, collidersOn);
        }

        // Glides the item onto something that holds it in place, like a cutting board.
        public void AttachTo(Transform parent, Vector3 localPosition, Quaternion localRotation, float glideSeconds, bool collidersOn)
        {
            StopAllCoroutines();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            SetCollidersEnabled(false);
            transform.SetParent(parent, worldPositionStays: true);
            StartCoroutine(Glide(localPosition, localRotation, glideSeconds, local: true, collidersOn: collidersOn));
        }

        public void LeaveContainer()
        {
            InContainer = null;
            transform.SetParent(null, worldPositionStays: true);
        }

        public void SetCollidersEnabled(bool enabled)
        {
            foreach (var c in colliders)
            {
                c.enabled = enabled;
            }
        }

        IEnumerator Glide(Vector3 endPosition, Quaternion endRotation, float seconds, bool local, bool collidersOn)
        {
            Vector3 startPosition = local ? transform.localPosition : transform.position;
            Quaternion startRotation = local ? transform.localRotation : transform.rotation;

            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                // Ease out: quick at first, gentle as it touches down.
                float t = 1f - Mathf.Pow(1f - time / seconds, 3f);
                SetPose(Vector3.LerpUnclamped(startPosition, endPosition, t), Quaternion.SlerpUnclamped(startRotation, endRotation, t), local);
                yield return null;
            }

            SetPose(endPosition, endRotation, local);
            SetCollidersEnabled(collidersOn);
            body.isKinematic = true;
            if (seconds > 0f)
            {
                // Softer when it's tucked into a basket.
                Sfx.Play(SoundId.SetDown, transform.position, InContainer != null ? 0.45f : 1f);
            }
            if (!local)
            {
                HeldChanged?.Invoke(false);
            }
        }

        void SetPose(Vector3 position, Quaternion rotation, bool local)
        {
            if (local)
            {
                transform.SetLocalPositionAndRotation(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }
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
    }
}
