using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Carrying
{
    // What the player is looking at, and the one thing they're holding.
    // Interact picks up, gathers into a held basket, puts into a basket,
    // or sets down what you hold.
    public class PlayerHands : MonoBehaviour
    {
        public enum Focus { Nothing, CanInteract, CanPlace }

        [SerializeField] PlayerSettings settings;
        [SerializeField] Transform viewPoint;
        [SerializeField] FirstPersonController controller;

        public Pickup Held { get; private set; }
        public bool IsEmpty => Held == null;
        public Focus CurrentFocus { get; private set; }

        InputAction interactAction;
        IInteractable lookTarget;
        RaycastHit surfaceHit;
        bool hasSurface;

        void Awake()
        {
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
        }

        void Update()
        {
            if (!controller.InputEnabled)
            {
                CurrentFocus = Focus.Nothing;
                return;
            }

            LookAround();

            if (interactAction.WasPressedThisFrame())
            {
                UseHands();
            }
        }

        void LateUpdate()
        {
            if (Held == null)
            {
                return;
            }

            // Ease the held item toward its spot in view so it feels carried, not glued on.
            Quaternion view = Held.KeepUpright
                ? Quaternion.Euler(0f, viewPoint.eulerAngles.y, 0f)
                : viewPoint.rotation;
            Vector3 target = viewPoint.position + view * Held.HoldOffset(settings.holdOffset);
            Quaternion targetRotation = view * Held.HeldRotation;
            float t = 1f - Mathf.Exp(-settings.holdFollowSharpness * Time.deltaTime);
            Held.transform.SetPositionAndRotation(
                Vector3.Lerp(Held.transform.position, target, t),
                Quaternion.Slerp(Held.transform.rotation, targetRotation, t));
        }

        public bool CanHandle(Pickup target)
        {
            if (Held == null)
            {
                return true;
            }
            if (Held.OwnContainer != null)
            {
                return Held.OwnContainer.Accepts(target);
            }
            return target.OwnContainer != null && target.OwnContainer.Accepts(Held);
        }

        public void Handle(Pickup target)
        {
            if (Held == null)
            {
                Hold(target);
            }
            else if (Held.OwnContainer != null && Held.OwnContainer.Accepts(target))
            {
                // Carrying a basket: gather what you look at straight into it.
                Held.OwnContainer.Add(target, settings.placeGlideSeconds);
            }
            else if (target.OwnContainer != null && target.OwnContainer.Accepts(Held))
            {
                Pickup item = Held;
                Held = null;
                target.OwnContainer.Add(item, settings.placeGlideSeconds);
            }
        }

        void Hold(Pickup item)
        {
            if (item.InContainer != null)
            {
                item.InContainer.Remove(item);
            }
            Held = item;
            item.OnPickedUp();
        }

        void LookAround()
        {
            lookTarget = null;
            hasSurface = false;

            var ray = new Ray(viewPoint.position, viewPoint.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, settings.reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(this))
                {
                    lookTarget = interactable;
                }
                surfaceHit = hit;
                hasSurface = hit.normal.y >= settings.minSurfaceUpness && IsFreeSurface(hit.collider);
            }

            if (lookTarget != null)
            {
                CurrentFocus = Focus.CanInteract;
            }
            else if (Held != null && hasSurface)
            {
                CurrentFocus = Focus.CanPlace;
            }
            else
            {
                CurrentFocus = Focus.Nothing;
            }
        }

        // Things can't be stacked on top of a basket or on what's inside one.
        static bool IsFreeSurface(Collider surface)
        {
            var pickup = surface.GetComponentInParent<Pickup>();
            return pickup == null || (pickup.OwnContainer == null && pickup.InContainer == null);
        }

        void UseHands()
        {
            if (lookTarget != null)
            {
                lookTarget.Interact(this);
                return;
            }

            if (Held == null)
            {
                return;
            }

            if (hasSurface)
            {
                Held.PlaceOn(surfaceHit.point, transform.eulerAngles.y, settings.placeGlideSeconds);
                Held = null;
            }
            else if (Held.OwnContainer == null)
            {
                // Loose items can be dropped; a full basket is only ever set down gently.
                Held.Drop();
                Held = null;
            }
        }
    }
}
