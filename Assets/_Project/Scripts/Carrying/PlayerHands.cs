using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Carrying
{
    // What the player is looking at, and what they're carrying in their two
    // hands. How much fits depends on the thing: a hand holds a few small
    // things of one kind (jalapeños, garlic), a couple of tomatoes, or one
    // jar; a pot, the canner or a basket takes both hands. Whatever you
    // picked up last is what you use next. Interact picks up, gathers into a
    // held basket, puts into a basket, or sets down what you hold.
    public class PlayerHands : MonoBehaviour
    {
        const int RightHand = 0;
        const int LeftHand = 1;
        const int BothHands = -1;
        const int NoRoom = -2;

        public enum Focus { Nothing, CanInteract, CanPlace }

        [SerializeField] PlayerSettings settings;
        [SerializeField] Transform viewPoint;
        [SerializeField] FirstPersonController controller;

        // The thing you'd use next: the last one picked up.
        public Pickup Held => carried.Count > 0 ? carried[carried.Count - 1] : null;
        // Everything in your hands, in the order it was picked up.
        public IReadOnlyList<Pickup> Carried => carried;
        public bool IsEmpty => carried.Count == 0;
        public bool IsHolding(Pickup item) => carried.Contains(item);
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

        readonly List<Pickup> carried = new List<Pickup>();
        readonly Dictionary<Pickup, int> handOf = new Dictionary<Pickup, int>();

        // Hands the held item over to something else (a station, a board).
        public Pickup TakeHeld() => Take(Held);

        // Hands over one particular thing you're carrying.
        public Pickup Take(Pickup item)
        {
            if (item == null || !carried.Remove(item))
            {
                return null;
            }
            handOf.Remove(item);
            return item;
        }

        // Room in your hands for this as well as what you already carry.
        public bool CanCarryMore(Pickup target) => !IsEmpty && !carried.Contains(target) && HandFor(target) != NoRoom;

        // Puts something in your hands (a filled watering can, a jar back
        // off the towel), if there's room.
        public void Give(Pickup item)
        {
            if (carried.Contains(item) || HandFor(item) == NoRoom)
            {
                return;
            }
            item.transform.SetParent(null, worldPositionStays: true);
            Hold(item);
        }

        // Which hand it would go in: stacked with the same kind if that hand
        // has room, otherwise an empty hand; something big needs both.
        int HandFor(Pickup target)
        {
            if (target.NeedsBothHands)
            {
                return IsEmpty ? BothHands : NoRoom;
            }
            if (Held != null && Held.NeedsBothHands)
            {
                return NoRoom;
            }
            for (int hand = RightHand; hand <= LeftHand; hand++)
            {
                int count = 0;
                Pickup first = null;
                foreach (var item in carried)
                {
                    if (handOf[item] != hand) continue;
                    if (first == null) first = item;
                    count++;
                }
                if (first != null && first.DisplayName == target.DisplayName && count < target.PerHand)
                {
                    return hand;
                }
            }
            for (int hand = RightHand; hand <= LeftHand; hand++)
            {
                if (!handOf.ContainsValue(hand))
                {
                    return hand;
                }
            }
            return NoRoom;
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
            if (IsEmpty || HeldPosed)
            {
                return;
            }

            // Ease each held thing toward its spot in view so it feels carried,
            // not glued on: the right hand, the left mirrored, and a few of a
            // kind bunched together in one hand.
            float t = 1f - Mathf.Exp(-settings.holdFollowSharpness * Time.deltaTime);
            int[] inHand = new int[2];
            foreach (var item in carried)
            {
                int hand = handOf[item];
                Quaternion view = item.KeepUpright
                    ? Quaternion.Euler(0f, viewPoint.eulerAngles.y, 0f)
                    : viewPoint.rotation;
                Vector3 offset = item.HoldOffset(settings.holdOffset);
                Quaternion turn = Quaternion.identity;
                if (hand != BothHands)
                {
                    int k = inHand[hand]++;
                    float side = hand == RightHand ? 1f : -1f;
                    offset.x *= side;
                    // A loose handful: each one tucked in beside and a little above the last.
                    offset += new Vector3(-side * 0.018f * k, 0.012f * k, 0.008f * (k % 2));
                    turn = Quaternion.Euler(0f, k * 20f * side, k * 8f);
                }
                item.transform.SetPositionAndRotation(
                    Vector3.Lerp(item.transform.position, viewPoint.position + view * offset, t),
                    Quaternion.Slerp(item.transform.rotation, view * turn * item.HeldRotation, t));
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
            return (target.OwnContainer != null && target.OwnContainer.Accepts(Held)) || CanCarryMore(target);
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
                // Everything in your hands that goes in the basket goes in.
                foreach (var item in carried.ToArray())
                {
                    if (target.OwnContainer.Accepts(item))
                    {
                        target.OwnContainer.Add(Take(item), settings.placeGlideSeconds);
                    }
                }
            }
            else if (CanCarryMore(target))
            {
                Hold(target);
            }
        }

        void Hold(Pickup item)
        {
            int hand = HandFor(item);
            Lift(item);
            carried.Add(item);
            handOf[item] = hand;
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
