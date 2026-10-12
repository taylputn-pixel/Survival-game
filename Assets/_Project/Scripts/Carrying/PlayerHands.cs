using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Carrying
{
    // What the player is looking at, and the one thing they're holding.
    // Interact picks up what you look at, or sets down what you hold.
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
            Vector3 target = viewPoint.TransformPoint(settings.holdOffset);
            Quaternion targetRotation = viewPoint.rotation * Held.HeldRotation;
            float t = 1f - Mathf.Exp(-settings.holdFollowSharpness * Time.deltaTime);
            Held.transform.SetPositionAndRotation(
                Vector3.Lerp(Held.transform.position, target, t),
                Quaternion.Slerp(Held.transform.rotation, targetRotation, t));
        }

        public void Hold(Pickup item)
        {
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
                hasSurface = hit.normal.y >= settings.minSurfaceUpness;
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
            }
            else
            {
                Held.Drop();
            }
            Held = null;
        }
    }
}
