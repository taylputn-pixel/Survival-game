using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A handful of cut pieces picked up off the cutting board while walking
    // around: slices of one kind, tomato wedges, or peeled cloves. It's
    // carried in both hands like anything else. Look at an open jar or the
    // cooking pot to drop them in, or at the board to put them back.
    public class Handful : MonoBehaviour
    {
        readonly List<Transform> pieces = new List<Transform>();

        // The pieces' name ("Cucumber Slice", "Tomato Wedge", "Clove").
        public string Kind { get; private set; }
        public int Count => pieces.Count;
        public int Max { get; private set; }
        public int Room => Mathf.Max(0, Max - pieces.Count);
        public bool IsWedges => Kind == "Tomato Wedge";
        public bool IsCloves => Kind == "Clove";
        public Pickup Pickup { get; private set; }

        // What to call a handful of these ("cucumber slices").
        public static string Plural(string kind) => (kind == "Clove" ? "garlic cloves" : kind.ToLower() + "s");

        // Gathers the pieces into a new handful, ready to be put in your hands.
        public static Handful Make(List<Transform> first, int max)
        {
            string kind = first[0].name;
            var go = new GameObject("Handful of " + Plural(kind));
            go.transform.SetPositionAndRotation(first[0].position, Quaternion.identity);
            // Pieces first, so the pickup measures them; a box to look at and set down by.
            var handful = go.AddComponent<Handful>();
            handful.Kind = kind;
            handful.Max = max;
            handful.Add(first);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.012f, 0f);
            box.size = new Vector3(0.07f, 0.03f, 0.07f);
            handful.Pickup = go.AddComponent<Pickup>();
            handful.Pickup.Configure(go.name, ItemCategory.Other, bothHands: true, upright: true, settable: false);
            return handful;
        }

        // More of the same, heaped on top.
        public void Add(List<Transform> more)
        {
            foreach (var piece in more)
            {
                piece.SetParent(transform, worldPositionStays: true);
                int i = pieces.Count;
                Vector2 spot = Random.insideUnitCircle * 0.022f;
                piece.SetLocalPositionAndRotation(new Vector3(spot.x, 0.004f + (i / 4) * 0.006f, spot.y),
                    Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f)));
                pieces.Add(piece);
            }
        }

        // Hands over up to this many pieces, from the top of the heap.
        public List<Transform> Take(int count)
        {
            count = Mathf.Min(count, pieces.Count);
            var taken = pieces.GetRange(pieces.Count - count, count);
            pieces.RemoveRange(pieces.Count - count, count);
            foreach (var piece in taken)
            {
                piece.SetParent(null, worldPositionStays: true);
            }
            return taken;
        }

        // Once it's all been used, it's gone from your hands.
        public void DoneWith(PlayerHands hands)
        {
            if (pieces.Count > 0)
            {
                return;
            }
            hands.Take(Pickup);
            Destroy(gameObject);
        }

        public static Handful Held(PlayerHands hands) => hands.Held != null ? hands.Held.GetComponent<Handful>() : null;
    }
}
