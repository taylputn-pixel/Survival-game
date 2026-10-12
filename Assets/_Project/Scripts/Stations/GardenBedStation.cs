using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Garden;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // A raised garden bed. Step up holding a seed packet and plant seeds one
    // at a time in the marked spots.
    public class GardenBedStation : Station
    {
        [SerializeField] PlantSpot[] spots;
        [Tooltip("How close the cursor must be to a spot to plant there, in screen heights.")]
        [SerializeField] float spotRadius = 0.06f;
        [SerializeField] float plantSeconds = 0.35f;

        PlayerHands hands;
        int enteredFrame;

        SeedPacket Packet => hands != null && hands.Held != null ? hands.Held.GetComponent<SeedPacket>() : null;

        public override bool CanInteract(PlayerHands player)
        {
            if (player.IsEmpty)
            {
                return false;
            }
            var packet = player.Held.GetComponent<SeedPacket>();
            return packet != null && packet.HasSeeds && HasEmptySpot();
        }

        public override void Interact(PlayerHands player)
        {
            hands = player;
            enteredFrame = Time.frameCount;
            Enter(player);
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            foreach (var spot in spots)
            {
                if (spot.IsEmpty)
                {
                    yield return spot.transform.position;
                }
            }
        }

        protected override void WhileWorking()
        {
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
