using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Garden;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // A raised garden bed. Step up holding a seed packet to plant seeds one at
    // a time in the marked spots, or holding the watering can to water: hold
    // Interact to tip the can and sweep the stream across the bed.
    public class GardenBedStation : Station
    {
        [SerializeField] GardenSettings garden;
        [SerializeField] PlantSpot[] spots;
        [Tooltip("How close the cursor must be to a spot to plant there, in screen heights.")]
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

        SeedPacket Packet => hands != null && hands.Held != null ? hands.Held.GetComponent<SeedPacket>() : null;
        Plane Soil => new Plane(transform.up, spots[0].transform.position);

        public override bool CanInteract(PlayerHands player)
        {
            if (player.IsEmpty)
            {
                return false;
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
            if (player.Held.GetComponent<WateringCan>() != null)
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
            foreach (var spot in spots)
            {
                if (spot.IsEmpty)
                {
                    yield return spot.transform.position;
                }
            }
        }

        protected override void OnLeave()
        {
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
            if (Soil.Raycast(ray, out float distance))
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
            Vector3 landing = Soil.ClosestPointOnPlane(can.Spout.position);
            foreach (var spot in spots)
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
            foreach (var spot in spots)
            {
                sum += spot.transform.position;
            }
            return sum / spots.Length;
        }

        Vector3 ClampToBed(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            foreach (var spot in spots)
            {
                Vector3 p = transform.InverseTransformPoint(spot.transform.position);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            local.x = Mathf.Clamp(local.x, min.x - wateringMargin, max.x + wateringMargin);
            local.z = Mathf.Clamp(local.z, min.z - wateringMargin, max.z + wateringMargin);
            return transform.TransformPoint(local);
        }

        PlantSpot SpotUnderCursor()
        {
            PlantSpot closest = null;
            float best = spotRadius;
            float aspect = (float)Screen.width / Screen.height;
            foreach (var spot in spots)
            {
                if (!spot.IsEmpty)
                {
                    continue;
                }
                Vector3 screen = View.WorldToViewportPoint(spot.transform.position);
                Vector2 offset = (Vector2)screen - CursorPoint;
                float distance = new Vector2(offset.x * aspect, offset.y).magnitude;
                if (screen.z > 0f && distance < best)
                {
                    best = distance;
                    closest = spot;
                }
            }
            return closest;
        }

        bool HasEmptySpot()
        {
            foreach (var spot in spots)
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
