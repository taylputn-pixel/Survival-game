using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // A chick in a little cardboard box, from Ada. Carry it home, set it
    // down and open it: the chick hops out there and joins the flock (in
    // the run if that's where you let her out, loose if not), taking an
    // empty place in the coop. With the coop full the box stays shut.
    [RequireComponent(typeof(Pickup))]
    public class ChickBox : MonoBehaviour, IAltInteractable, ISharedUse
    {
        Pickup pickup;

        void Awake() => pickup = GetComponent<Pickup>();

        static Flock Flock => FindAnyObjectByType<Flock>();

        public bool CanAltInteract(PlayerHands hands)
        {
            var flock = Flock;
            return hands.IsEmpty && pickup.IsResting && flock != null && flock.HasRoom;
        }

        public string AltVerb(PlayerHands hands) => "Let the chick out";

        public void AltInteract(PlayerHands hands)
        {
            var flock = Flock;
            if (flock == null) return;
            flock.Adopt(Ground());
            Net.ItemNet.Remove(gameObject);
        }

        // The ground under the box.
        Vector3 Ground()
        {
            Vector3 at = transform.position;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 0.3f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.normal.y < 0.7f) continue;
                if (hit.collider.GetComponentInParent<Pickup>() != null || hit.collider.GetComponentInParent<Hen>() != null) continue;
                if (hit.distance < best) { best = hit.distance; at = hit.point; }
            }
            return at;
        }
    }
}
