using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Garden;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Stations
{
    // Look closer at one plant in a bed. The view moves in beside it, and you
    // turn the plant (Previous/Next, or left and right) to find suckers and
    // hornworms hiding round the back. Point and press to pinch or pick them
    // off, or to pick what's ripe: into a basket you're holding, or into
    // your hands, which steps you back.
    public class PlantStation : Station, GrandmasHouse.Interaction.ISharedUse
    {
        [SerializeField] GardenBed bed;
        [Tooltip("How close to a plant's middle you must look to look closer at it, in meters.")]
        [SerializeField] float lookRadius = 0.18f;
        [Tooltip("How far from the plant's middle the view sits, in meters.")]
        [SerializeField] float viewDistance = 0.55f;
        [Tooltip("How far above the plant's middle the view sits, in meters.")]
        [SerializeField] float viewHeight = 0.22f;
        [Tooltip("How fast the plant turns, in degrees per second.")]
        [SerializeField] float turnSpeed = 140f;
        [Tooltip("How close the cursor must be to something to work on it, in screen heights.")]
        [SerializeField] float cursorRadius = 0.05f;

        PlantSpot spot;
        Transform view;
        PlayerHands hands;
        int enteredFrame;
        InputAction moveAction;
        InputAction previousAction;
        InputAction nextAction;

        protected override Transform ViewPoint => view;
        public override bool CanTurn => true;

        protected override void Awake()
        {
            base.Awake();
            view = new GameObject("Plant View").transform;
            view.SetParent(transform, false);
            moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
            previousAction = InputSystem.actions.FindAction("Player/Previous", throwIfNotFound: true);
            nextAction = InputSystem.actions.FindAction("Player/Next", throwIfNotFound: true);
        }

        public override bool CanInteract(PlayerHands player) =>
            (player.IsEmpty || Basket(player) != null) && PlantInView(player) != null;

        public override string PromptVerb(PlayerHands player) => "Look closer";

        public override void Interact(PlayerHands player)
        {
            hands = player;
            spot = PlantInView(player);
            PlaceView(player.ViewPoint.position);
            enteredFrame = Time.frameCount;
            Enter(player);
        }

        static Container Basket(PlayerHands player) => player.Held != null ? player.Held.OwnContainer : null;

        // The plant whose stem runs closest to your line of sight, if it's close.
        PlantSpot PlantInView(PlayerHands player)
        {
            var ray = new Ray(player.ViewPoint.position, player.ViewPoint.forward);
            PlantSpot closest = null;
            float best = lookRadius;
            foreach (var candidate in bed.Spots)
            {
                if (candidate.Stage < 0)
                {
                    continue;
                }
                Bounds bounds = candidate.PlantBounds;
                Vector3 foot = candidate.transform.position;
                for (int i = 0; i <= 4; i++)
                {
                    Vector3 point = foot + Vector3.up * (bounds.max.y - foot.y) * (i / 4f);
                    float along = Vector3.Dot(point - ray.origin, ray.direction);
                    if (along <= 0f || along > settings.reach)
                    {
                        continue;
                    }
                    float distance = Vector3.Cross(ray.direction, point - ray.origin).magnitude;
                    if (distance < best)
                    {
                        best = distance;
                        closest = candidate;
                    }
                }
            }
            return closest;
        }

        // Beside the plant, on the side you walked up from, looking at its middle.
        void PlaceView(Vector3 from)
        {
            Vector3 middle = spot.PlantBounds.center;
            Vector3 side = Vector3.ProjectOnPlane(from - middle, Vector3.up);
            side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.back;
            view.position = middle + side * viewDistance + Vector3.up * viewHeight;
            view.rotation = Quaternion.LookRotation(middle - view.position);
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            foreach (var thing in spot.Pinchables)
            {
                yield return thing.transform.position;
            }
            foreach (var item in spot.Ripe)
            {
                yield return item.transform.position;
            }
        }

        protected override void WhileWorking()
        {
            if (spot.Stage < 0)
            {
                // The plant was cut (dill) and is gone.
                Leave();
                return;
            }

            float turn = moveAction.ReadValue<Vector2>().x;
            if (nextAction.IsPressed()) turn += 1f;
            if (previousAction.IsPressed()) turn -= 1f;
            spot.Turn(Mathf.Clamp(turn, -1f, 1f) * turnSpeed * Time.deltaTime);

            Component target = Closest();
            reticle.Highlight = target != null;
            var pinchable = target as Pinchable;
            var ripe = target as Pickup;
            UseHint = pinchable != null ? pinchable.Verb : ripe != null ? "Pick " + ripe.DisplayName.ToLowerInvariant() : null;

            if (target == null || Time.frameCount == enteredFrame || !UseAction.WasPressedThisFrame())
            {
                return;
            }
            if (pinchable != null)
            {
                bed.TakeOff(spot, pinchable);
                return;
            }
            Container basket = Basket(hands);
            if (basket != null)
            {
                if (basket.Accepts(ripe))
                {
                    basket.Add(ripe, settings.placeGlideSeconds);
                }
                return;
            }
            // Empty hands: take it, and step back to carry it.
            hands.Give(ripe);
            Leave();
        }

        // Whatever on the plant is nearest the cursor, if it's close enough.
        Component Closest()
        {
            Component closest = null;
            float best = cursorRadius;
            float aspect = (float)Screen.width / Screen.height;
            foreach (var thing in Workables())
            {
                Vector3 screen = View.WorldToViewportPoint(thing.transform.position);
                Vector2 offset = (Vector2)screen - CursorPoint;
                float distance = new Vector2(offset.x * aspect, offset.y).magnitude;
                if (screen.z > 0f && distance < best)
                {
                    best = distance;
                    closest = thing;
                }
            }
            return closest;
        }

        IEnumerable<Component> Workables()
        {
            foreach (var thing in spot.Pinchables) yield return thing;
            foreach (var item in spot.Ripe) yield return item;
        }
    }
}
