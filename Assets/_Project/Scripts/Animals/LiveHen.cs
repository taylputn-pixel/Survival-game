using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // A hen held in your arms: a copy of her to carry, while the hen herself
    // is out of the run. Lay her on the chopping block and take the axe to
    // her (ChoppingBlock). Set her down or drop her anywhere else and she
    // lands there: back in the run if that's where, loose if not. She isn't
    // saved: a game saved with her in your arms loads with her where you
    // picked her up.
    public class LiveHen : MonoBehaviour
    {
        public Hen Hen { get; private set; }
        public Pickup Pickup { get; private set; }

        Flock flock;
        PlayerHands carrier;

        public static LiveHen Make(Hen hen, Flock flock)
        {
            var go = new GameObject("Hen");
            go.transform.SetPositionAndRotation(hen.transform.position, hen.transform.rotation);
            // A copy of how she looks.
            var bounds = new Bounds(hen.transform.position, Vector3.zero);
            foreach (var part in hen.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh = part.GetComponent<MeshFilter>();
                if (mesh == null) continue;
                var copy = new GameObject(part.name);
                copy.transform.SetParent(go.transform, false);
                copy.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
                copy.transform.localScale = part.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = part.sharedMaterials;
                bounds.Encapsulate(part.bounds);
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(Mathf.Max(0.2f, bounds.size.x), Mathf.Max(0.2f, bounds.size.y), Mathf.Max(0.2f, bounds.size.z));
            var live = go.AddComponent<LiveHen>();
            live.Hen = hen;
            live.flock = flock;
            live.Pickup = go.AddComponent<Pickup>();
            live.Pickup.Configure("Hen", ItemCategory.Other, bothHands: true, upright: true, settable: true);
            live.Pickup.HeldChanged += held => { if (held) live.carrier = live.FindCarrier(); };
            return live;
        }

        PlayerHands FindCarrier()
        {
            foreach (var hands in FindObjectsByType<PlayerHands>(FindObjectsSortMode.None)) if (hands.IsHolding(Pickup)) return hands;
            return null;
        }

        public bool OnBlock => transform.parent != null && transform.parent.GetComponentInParent<Kitchen.ChoppingBlock>() != null;

        void LateUpdate()
        {
            if (carrier == null) carrier = FindCarrier();
            if ((carrier != null && carrier.IsHolding(Pickup)) || OnBlock) return;
            // Let go of: she lands on the ground under where she was and stays there.
            flock.LetGo(Hen, Ground());
            Destroy(gameObject);
        }

        Vector3 Ground()
        {
            Vector3 from = transform.position + Vector3.up * 0.1f;
            float best = float.MaxValue;
            Vector3 at = transform.position;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Hen>() != null) continue;
                if (hit.collider.GetComponentInParent<Pickup>() != null || hit.normal.y < 0.7f) continue;
                if (hit.distance < best) { best = hit.distance; at = hit.point; }
            }
            return at;
        }

        // The axe falls.
        public void Cull()
        {
            flock.Butcher(Hen, transform.position);
            Destroy(gameObject);
        }
    }
}
