using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Garden
{
    // The soil of a raised bed, worked by hand from wherever you stand, with
    // no station. Holding a seed packet, look at a marked spot and press to
    // plant it. Holding the watering can, hold Interact: the can tips over
    // where you look, and you sweep the water across the bed by looking
    // around (you can walk along the bed while you pour).
    [RequireComponent(typeof(GardenBed))]
    public class BedSoil : MonoBehaviour, IInteractable, ISharedUse
    {
        [SerializeField] GardenSettings garden;
        [Tooltip("How close to a marked spot you must look to plant there, in meters.")]
        [SerializeField] float spotReach = 0.2f;
        [SerializeField] float plantSeconds = 0.35f;
        [Tooltip("How far past the outer spots the water may land, in meters.")]
        [SerializeField] float wateringMargin = 0.15f;
        [Tooltip("Which way the spout points while watering, in degrees from straight ahead (negative is left).")]
        [SerializeField] float canYaw = -75f;
        [Tooltip("How far out from you the can reaches while watering, in meters.")]
        [SerializeField] float canReach = 1.1f;

        GardenBed bed;
        InputAction interactAction;
        bool watering;

        void Awake()
        {
            bed = GetComponent<GardenBed>();
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
        }

        public bool CanInteract(PlayerHands hands)
        {
            if (watering || hands.IsEmpty)
            {
                return false;
            }
            var can = hands.Held.GetComponent<WateringCan>();
            if (can != null)
            {
                return !can.IsEmpty;
            }
            var packet = hands.Held.GetComponent<SeedPacket>();
            // Frozen ground takes no seed, until spring.
            if (DayCycle.Weather.Instance != null && DayCycle.Weather.Instance.GroundFrozen) return false;
            return packet != null && packet.HasSeeds && SpotAt(hands) != null;
        }

        public string PromptVerb(PlayerHands hands)
        {
            if (hands.Held.GetComponent<WateringCan>() != null)
            {
                return "Hold to water";
            }
            return "Plant " + hands.Held.GetComponent<SeedPacket>().Crop.displayName.ToLowerInvariant();
        }

        public void Interact(PlayerHands hands)
        {
            var can = hands.Held.GetComponent<WateringCan>();
            if (can != null)
            {
                StartCoroutine(Water(hands, can));
                return;
            }
            var packet = hands.Held.GetComponent<SeedPacket>();
            PlantSpot spot = SpotAt(hands);
            packet.TakeSeed();
            bed.Plant(spot, packet.Crop, plantSeconds);
        }

        // The open spot (empty, or a dead plant) nearest where you're looking, if it's close.
        PlantSpot SpotAt(PlayerHands hands)
        {
            if (!hands.HasLookPoint)
            {
                return null;
            }
            PlantSpot closest = null;
            float best = spotReach;
            foreach (var spot in bed.Spots)
            {
                float distance = Vector3.ProjectOnPlane(spot.transform.position - hands.LookPoint, transform.up).magnitude;
                if (spot.IsOpen && distance < best)
                {
                    best = distance;
                    closest = spot;
                }
            }
            return closest;
        }

        // While Interact is held the bed holds the can, so the stream lands
        // where you look; let go and it eases back into your hand.
        IEnumerator Water(PlayerHands hands, WateringCan can)
        {
            watering = true;
            hands.HeldPosed = true;
            Pickup item = hands.Held;
            Transform t = item.transform;
            Vector3 spoutOffset = Quaternion.Inverse(t.rotation) * (can.Spout.position - t.position);
            Vector3 spoutForward = Quaternion.Inverse(t.rotation) * can.Spout.forward;
            Vector3 aimPoint = AimPoint(hands);
            float tilt = 0f;

            while (hands.Held == item && (interactAction.IsPressed() || tilt > 0f))
            {
                bool tipping = interactAction.IsPressed() && !can.IsEmpty;
                tilt = Mathf.MoveTowards(tilt, tipping ? garden.pourTilt : 0f, garden.pourTilt / 0.25f * Time.deltaTime);
                aimPoint = AimPoint(hands);
                MoveCan(hands, can, t, aimPoint, spoutOffset, spoutForward, tilt);

                bool pouring = tipping && tilt > garden.pourTilt * 0.8f && can.Pour(Time.deltaTime);
                can.SetPouring(pouring);
                if (pouring)
                {
                    WaterAround(can.LandingOn(bed.Soil));
                }
                yield return null;
            }
            can.SetPouring(false);
            hands.HeldPosed = false;
            watering = false;
        }

        // Where you're looking on the soil, kept to the bed and within arm's reach.
        Vector3 AimPoint(PlayerHands hands)
        {
            Transform eye = hands.ViewPoint;
            var ray = new Ray(eye.position, eye.forward);
            Vector3 point = bed.Soil.Raycast(ray, out float distance) ? ray.GetPoint(distance) : bed.Soil.ClosestPointOnPlane(eye.position + eye.forward);

            Vector3 flat = Vector3.ProjectOnPlane(point - eye.position, transform.up);
            if (flat.magnitude > canReach)
            {
                point -= flat - flat.normalized * canReach;
            }

            bed.GetSoilBounds(out Vector3 min, out Vector3 max, wateringMargin);
            Vector3 local = transform.InverseTransformPoint(point);
            local.x = Mathf.Clamp(local.x, min.x, max.x);
            local.z = Mathf.Clamp(local.z, min.z, max.z);
            return transform.TransformPoint(local);
        }

        void MoveCan(PlayerHands hands, WateringCan can, Transform t, Vector3 aimPoint, Vector3 spoutOffset, Vector3 spoutForward, float tilt)
        {
            // The spout points off to the side so you can watch the water;
            // tipping it forward lowers the spout.
            float yaw = hands.ViewPoint.eulerAngles.y + canYaw;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            float lift = garden.pourHeight + (1f - tilt / garden.pourTilt) * 0.08f;
            // Hold the can back so the stream, not the spout, lands where you look.
            Vector3 spoutTip = aimPoint + transform.up * lift;
            Vector3 drift = can.Landing(spoutTip, rotation * spoutForward * can.StreamSpeed, bed.Soil) - bed.Soil.ClosestPointOnPlane(spoutTip);
            spoutTip -= drift * Mathf.Clamp01(tilt / garden.pourTilt);
            Vector3 position = spoutTip - rotation * spoutOffset;

            float follow = 1f - Mathf.Exp(-garden.canFollowSharpness * Time.deltaTime);
            t.SetPositionAndRotation(Vector3.Lerp(t.position, position, follow), Quaternion.Slerp(t.rotation, rotation, follow));
        }

        void WaterAround(Vector3 landing)
        {
            foreach (var spot in bed.Spots)
            {
                Vector3 offset = spot.transform.position - landing;
                if (Vector3.ProjectOnPlane(offset, transform.up).magnitude < garden.pourRadius)
                {
                    bed.Water(spot, garden.waterPerSecond * Time.deltaTime);
                }
            }
        }
    }
}
