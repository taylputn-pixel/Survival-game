using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Garden;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // A raised garden bed. Step up holding a seed packet to plant seeds one at
    // a time in the marked spots, holding the watering can to water (hold
    // Interact to tip the can and sweep the stream across the bed), or with
    // empty hands to weed (grab a weed and pull back to get it out by the root).
    public class GardenBedStation : Station
    {
        [SerializeField] GardenSettings garden;
        [SerializeField] GardenBed bed;
        [Tooltip("How close the cursor must be to a spot or weed to work on it, in screen heights.")]
        [SerializeField] float spotRadius = 0.06f;
        [SerializeField] float plantSeconds = 0.35f;
        [Tooltip("How far past the outer spots the can may reach, in meters.")]
        [SerializeField] float wateringMargin = 0.15f;
        [Tooltip("Which way the spout points while watering, in degrees from straight ahead (negative is left).")]
        [SerializeField] float canYaw = -75f;

        PlayerHands hands;
        int enteredFrame;

        Pickup canItem;
        WateringCan can;
        Vector3 spoutOffset;
        Vector3 aimPoint;
        float tilt;
        bool waitForRelease;

        Weed grabbed;
        float grabCursorY;

        SeedPacket Packet => hands != null && hands.Held != null ? hands.Held.GetComponent<SeedPacket>() : null;
        IReadOnlyList<PlantSpot> Spots => bed.Spots;

        public override bool CanInteract(PlayerHands player)
        {
            if (player.IsEmpty)
            {
                return bed.Weeds.Count > 0;
            }
            if (player.Held.GetComponent<WateringCan>() != null)
            {
                return true;
            }
            var packet = player.Held.GetComponent<SeedPacket>();
            return packet != null && packet.HasSeeds && HasEmptySpot();
        }

        public override void Interact(PlayerHands player)
        {
            hands = player;
            enteredFrame = Time.frameCount;
            if (!player.IsEmpty && player.Held.GetComponent<WateringCan>() != null)
            {
                TakeCan();
            }
            Enter(player);
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (can != null)
            {
                yield break;
            }
            if (hands.IsEmpty)
            {
                if (grabbed == null)
                {
                    foreach (var weed in bed.Weeds)
                    {
                        yield return weed.transform.position;
                    }
                }
                yield break;
            }
            foreach (var spot in Spots)
            {
                if (spot.IsEmpty)
                {
                    yield return spot.transform.position;
                }
            }
        }

        protected override void OnLeave()
        {
            LetGoOfWeed();
            if (can != null)
            {
                can.SetPouring(false);
                hands.Give(canItem);
                can = null;
                canItem = null;
            }
        }

        protected override void WhileWorking()
        {
            if (can != null)
            {
                Water();
                return;
            }
            if (hands.IsEmpty)
            {
                PullWeeds();
                return;
            }

            PlantSpot target = SpotUnderCursor();
            reticle.Highlight = target != null;

            // The press that opened the station shouldn't also plant.
            if (Time.frameCount == enteredFrame || target == null || !UseAction.WasPressedThisFrame())
            {
                return;
            }

            SeedPacket packet = Packet;
            if (packet == null || !packet.HasSeeds)
            {
                return;
            }
            packet.TakeSeed();
            target.Plant(packet.Crop, plantSeconds);
        }

        protected override void Update()
        {
            base.Update();
            if (can != null && IsWorking)
            {
                MoveCan();
            }
        }

        // The bed holds the can while you water, so it can follow the cursor.
        void TakeCan()
        {
            canItem = hands.TakeHeld();
            can = canItem.GetComponent<WateringCan>();
            spoutOffset = Quaternion.Inverse(canItem.transform.rotation) * (can.Spout.position - canItem.transform.position);
            aimPoint = BedCenter();
            tilt = 0f;
            waitForRelease = true;
            reticle.Hidden = true;
        }

        void Water()
        {
            Ray ray = CursorRay();
            if (bed.Soil.Raycast(ray, out float distance))
            {
                aimPoint = ClampToBed(ray.GetPoint(distance));
            }

            // Wait for the press that opened the station to be let go.
            bool pressed = UseAction.IsPressed();
            if (!pressed)
            {
                waitForRelease = false;
            }
            bool pouring = pressed && !waitForRelease && tilt > garden.pourTilt * 0.8f && can.Pour(Time.deltaTime);
            can.SetPouring(pouring);
            if (!pouring)
            {
                return;
            }

            // Water lands under the spout.
            Vector3 landing = bed.Soil.ClosestPointOnPlane(can.Spout.position);
            foreach (var spot in Spots)
            {
                Vector3 offset = spot.transform.position - landing;
                if (Vector3.ProjectOnPlane(offset, transform.up).magnitude < garden.pourRadius)
                {
                    spot.AddWater(garden.waterPerSecond * Time.deltaTime);
                }
            }
        }

        void MoveCan()
        {
            bool tipping = UseAction.IsPressed() && !waitForRelease && !can.IsEmpty;
            float step = garden.pourTilt / 0.25f * Time.deltaTime;
            tilt = Mathf.MoveTowards(tilt, tipping ? garden.pourTilt : 0f, step);

            // The spout points off to the side so you can watch the water;
            // tipping it forward lowers the spout.
            float yaw = View.transform.eulerAngles.y + canYaw;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            float lift = garden.pourHeight + (1f - tilt / garden.pourTilt) * 0.08f;
            Vector3 spoutTip = aimPoint + transform.up * lift;
            Vector3 position = spoutTip - rotation * spoutOffset;

            Transform t = canItem.transform;
            float follow = 1f - Mathf.Exp(-garden.canFollowSharpness * Time.deltaTime);
            t.SetPositionAndRotation(Vector3.Lerp(t.position, position, follow), Quaternion.Slerp(t.rotation, rotation, follow));
        }

        Vector3 BedCenter()
        {
            Vector3 sum = Vector3.zero;
            foreach (var spot in Spots)
            {
                sum += spot.transform.position;
            }
            return sum / Spots.Count;
        }

        Vector3 ClampToBed(Vector3 point)
        {
            bed.GetSoilBounds(out Vector3 min, out Vector3 max, wateringMargin);
            Vector3 local = bed.transform.InverseTransformPoint(point);
            local.x = Mathf.Clamp(local.x, min.x, max.x);
            local.z = Mathf.Clamp(local.z, min.z, max.z);
            return bed.transform.TransformPoint(local);
        }

        void PullWeeds()
        {
            if (grabbed == null)
            {
                Weed target = Closest(bed.Weeds, w => w.transform.position);
                reticle.Highlight = target != null;
                if (Time.frameCount != enteredFrame && target != null && UseAction.WasPressedThisFrame())
                {
                    grabbed = target;
                    grabCursorY = CursorPoint.y;
                }
                return;
            }

            if (!UseAction.IsPressed())
            {
                LetGoOfWeed();
                return;
            }

            // Pull back toward you (mouse or stick down) to work it loose.
            float pulled = (grabCursorY - CursorPoint.y) / (garden.pullDistance * grabbed.Size);
            grabbed.SetPull(pulled, holding: true);
            if (pulled >= 1f)
            {
                bed.PullOut(grabbed);
                grabbed = null;
            }
        }

        void LetGoOfWeed()
        {
            if (grabbed != null)
            {
                grabbed.SetPull(0f, holding: false);
                grabbed = null;
            }
        }

        PlantSpot SpotUnderCursor()
        {
            var empty = new List<PlantSpot>();
            foreach (var spot in Spots)
            {
                if (spot.IsEmpty)
                {
                    empty.Add(spot);
                }
            }
            return Closest(empty, spot => spot.transform.position);
        }

        // The thing nearest the cursor on screen, if it's close enough.
        T Closest<T>(IEnumerable<T> things, System.Func<T, Vector3> positionOf) where T : class
        {
            T closest = null;
            float best = spotRadius;
            float aspect = (float)Screen.width / Screen.height;
            foreach (var thing in things)
            {
                Vector3 screen = View.WorldToViewportPoint(positionOf(thing));
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

        bool HasEmptySpot()
        {
            foreach (var spot in Spots)
            {
                if (spot.IsEmpty)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
