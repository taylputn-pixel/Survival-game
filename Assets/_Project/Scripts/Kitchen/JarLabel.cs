using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A paper label wrapped around the front of a jar: a curved strip just
    // outside the glass, open at the back, with its picture stretched across.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class JarLabel : MonoBehaviour
    {
        [SerializeField] float radius = 0.0432f;
        [SerializeField] float height = 0.05f;
        [Tooltip("How far around the jar the label reaches, in degrees.")]
        [SerializeField] float arc = 150f;
        [SerializeField] int segments = 16;

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = Build();
            GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Mesh Build()
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                // Centered on the jar's front (-Z), from its left edge to its right
                // as you face it, so the picture reads the right way round.
                float angle = Mathf.Deg2Rad * (-90f - arc / 2f + u * arc);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = outward * radius + Vector3.down * height / 2f;
                vertices[i * 2 + 1] = outward * radius + Vector3.up * height / 2f;
                normals[i * 2] = normals[i * 2 + 1] = outward;
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                // Wound to face outward.
                triangles[i * 6 + 0] = a;
                triangles[i * 6 + 1] = a + 1;
                triangles[i * 6 + 2] = a + 2;
                triangles[i * 6 + 3] = a + 2;
                triangles[i * 6 + 4] = a + 1;
                triangles[i * 6 + 5] = a + 3;
            }
            var mesh = new Mesh { name = "Jar Label" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
