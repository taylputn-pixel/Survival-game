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
        // A second jar in the other hand. Whatever takes the first jar gets
        // it, and this one moves over to take its place.
        public Pickup Extra { get; private set; }
        public bool IsEmpty => Held == null;
        public Focus CurrentFocus { get; private set; }
        // What pressing Interact would do right now, for the button prompt.
        public string Verb { get; private set; }
        // What pressing Pick up would do right now, if anything.
        public string AltVerb { get; private set; }
        public Transform ViewPoint => viewPoint;
        // Where the line of sight meets something within reach.
        public bool HasLookPoint { get; private set; }
        public Vector3 LookPoint => surfaceHit.point;
        // Something else is moving the held item for now (the garden bed
        // tipping the watering can); the hands leave it be until it's done.
        public bool HeldPosed { get; set; }
        public FirstPersonController Controller => controller;

        // Hands the held item over to something else (a station, a board).
        public Pickup TakeHeld()
        {
            Pickup item = Held;
            Held = Extra;
            Extra = null;
            return item;
        }

        // Jars can be carried two at a time, one in each hand.
        public bool CanCarrySecond(Pickup target)
        {
            return Held != null && Extra == null && target != Held
                && Held.Category == ItemCategory.Jar && target.Category == ItemCategory.Jar;
        }

        // Puts something back in your empty hands (a filled watering can).
        public void Give(Pickup item)
        {
            if (Held != null)
            {
                return;
            }
            item.transform.SetParent(null, worldPositionStays: true);
            Hold(item);
        }

        InputAction interactAction;
        InputAction pickUpAction;
        IInteractable lookTarget;
        IAltInteractable altTarget;
        RaycastHit surfaceHit;
        bool hasSurface;

        void Awake()
        {
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            pickUpAction = InputSystem.actions.FindAction("Player/PickUp", throwIfNotFound: true);
        }

        void Update()
        {
            if (!controller.InputEnabled)
            {
                CurrentFocus = Focus.Nothing;
                Verb = null;
                AltVerb = null;
                return;
            }

            LookAround();

            if (interactAction.WasPressedThisFrame())
            {
                UseHands();
            }
            else if (pickUpAction.WasPressedThisFrame() && altTarget != null)
            {
                altTarget.AltInteract(this);
            }
        }

        void LateUpdate()
        {
            if (Held == null || HeldPosed)
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

            if (Extra != null)
            {
                // The other hand, mirrored to the left.
                Vector3 offset = Extra.HoldOffset(settings.holdOffset);
                offset.x = -offset.x;
                Extra.transform.SetPositionAndRotation(
                    Vector3.Lerp(Extra.transform.position, viewPoint.position + view * offset, t),
                    Quaternion.Slerp(Extra.transform.rotation, view * Extra.HeldRotation, t));
            }
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
            return (target.OwnContainer != null && target.OwnContainer.Accepts(Held)) || CanCarrySecond(target);
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
                target.OwnContainer.Add(TakeHeld(), settings.placeGlideSeconds);
            }
            else if (CanCarrySecond(target))
            {
                Lift(target);
                Extra = target;
                target.OnPickedUp();
            }
        }

        void Hold(Pickup item)
        {
            Lift(item);
            Held = item;
            item.OnPickedUp();
        }

        // Lifted off whatever it rested on (a vine, a towel, a shelf, a basket).
        static void Lift(Pickup item)
        {
            if (item.InContainer != null)
            {
                item.InContainer.Remove(item);
            }
            item.transform.SetParent(null, worldPositionStays: true);
        }

        void LookAround()
        {
            lookTarget = null;
            altTarget = null;
            hasSurface = false;
            HasLookPoint = false;

            var ray = new Ray(viewPoint.position, viewPoint.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, settings.reach, ~0, QueryTriggerInteraction.Ignore))
            {
                // Set first, so things deciding what you can do can see where you look.
                surfaceHit = hit;
                HasLookPoint = true;
                // The nearest thing up the hierarchy that can be used right now:
                // a jar on a shelf, or the shelf itself if you're holding a jar.
                foreach (var interactable in hit.collider.GetComponentsInParent<IInteractable>())
                {
                    if (interactable.CanInteract(this))
                    {
                        lookTarget = interactable;
                        break;
                    }
                }
                foreach (var alt in hit.collider.GetComponentsInParent<IAltInteractable>())
                {
                    if (alt.CanAltInteract(this))
                    {
                        altTarget = alt;
                        break;
                    }
                }
                hasSurface = hit.normal.y >= settings.minSurfaceUpness && IsFreeSurface(hit.collider);
            }

            AltVerb = altTarget?.AltVerb(this);
            if (lookTarget != null)
            {
                CurrentFocus = Focus.CanInteract;
                Verb = lookTarget.PromptVerb(this);
            }
            else if (Held != null && hasSurface)
            {
                CurrentFocus = Focus.CanPlace;
                Verb = "Set down";
            }
            else
            {
                CurrentFocus = Focus.Nothing;
                Verb = Held != null && Held.OwnContainer == null ? "Drop" : null;
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
                TakeHeld().PlaceOn(surfaceHit.point, transform.eulerAngles.y, settings.placeGlideSeconds);
            }
            else if (Held.OwnContainer == null)
            {
                // Loose items can be dropped; a full basket is only ever set down gently.
                TakeHeld().Drop();
            }
        }
    }
}
