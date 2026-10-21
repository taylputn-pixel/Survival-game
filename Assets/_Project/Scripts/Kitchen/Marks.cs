using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Small marks built in code: a measuring line around the inside of a pot
    // or the outside of a jar, and a fair rosette for a jar's lid.
    public static class Marks
    {
        static Material plain;

        static Material Plain()
        {
            if (plain == null) plain = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Mark" };
            return plain;
        }

        static GameObject Piece(PrimitiveType shape, string name, Transform parent, Vector3 at, Vector3 size, Color color, Quaternion turn = default)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            // Gone at once, not at the end of the frame: a jar notes its own
            // colliders as it wakes, and a mark's must never be among them.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = turn == default ? Quaternion.identity : turn;
            go.transform.localScale = size;
            var look = go.GetComponent<Renderer>();
            look.sharedMaterial = Plain();
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(look, color);
            return go;
        }

        static void Tint(Renderer look, Color color)
        {
            var block = new MaterialPropertyBlock();
            look.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            look.SetPropertyBlock(block);
        }

        // A thin line all the way round, centered here in the parent's space.
        public static GameObject Ring(Transform parent, string name, Vector3 center, float radius, float height, float depth, Color color, int segments = 28)
        {
            var ring = new GameObject(name).transform;
            ring.SetParent(parent, false);
            ring.localPosition = center;
            float length = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                Quaternion turn = Quaternion.Euler(0f, angle, 0f);
                Piece(PrimitiveType.Cube, "Line", ring, turn * Vector3.forward * radius, new Vector3(length, height, depth), color, turn);
            }
            return ring.gameObject;
        }

        // A rosette lying on top of a jar's lid: a ribbon disc, a gold button
        // and two tails. Tint the ribbon with Tint.
        public static GameObject Rosette(Transform jar, Renderer lid)
        {
            var root = new GameObject("Ribbon Rosette").transform;
            root.SetParent(jar, false);
            Bounds top = lid != null ? lid.bounds : new Bounds(jar.position + jar.up * 0.14f, Vector3.zero);
            root.position = top.center + jar.up * (top.extents.y + 0.0008f);
            root.rotation = jar.rotation;
            var gold = new Color(0.86f, 0.7f, 0.25f);
            Piece(PrimitiveType.Cylinder, "Ribbon", root, Vector3.zero, new Vector3(0.04f, 0.0012f, 0.04f), Color.white);
            Piece(PrimitiveType.Cylinder, "Button", root, Vector3.up * 0.0015f, new Vector3(0.017f, 0.0015f, 0.017f), gold);
            foreach (float side in new[] { -1f, 1f })
            {
                Piece(PrimitiveType.Cube, "Ribbon", root, new Vector3(0.007f * side, 0.0004f, -0.022f), new Vector3(0.011f, 0.0016f, 0.028f), Color.white, Quaternion.Euler(0f, 14f * side, 0f));
            }
            return root.gameObject;
        }

        // Colors every ribbon piece of a rosette (the button stays gold).
        public static void Tint(GameObject rosette, Color color)
        {
            foreach (var look in rosette.GetComponentsInChildren<Renderer>(true))
            {
                if (look.name == "Ribbon") Tint(look, color);
            }
        }
    }
}
