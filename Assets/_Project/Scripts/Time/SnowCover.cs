using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // Whitens the ground (and roofs) as snow piles up through the winter,
    // and lets it go again as it melts.
    public class SnowCover : MonoBehaviour
    {
        [SerializeField] Weather weather;
        [SerializeField] Renderer[] covered;
        [SerializeField] Color snow = new Color(0.92f, 0.94f, 0.97f);
        [Tooltip("How white a full cover gets (some ground still shows).")]
        [Range(0f, 1f)] [SerializeField] float most = 0.85f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Color[] bare;
        MaterialPropertyBlock block;
        float nextCheck;
        float shown = -1f;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            bare = new Color[covered.Length];
            for (int i = 0; i < covered.Length; i++)
            {
                var material = covered[i] != null ? covered[i].sharedMaterial : null;
                bare[i] = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
            }
        }

        public float Cover => shown;

        void Update()
        {
            if (weather == null || Time.time < nextCheck) return;
            nextCheck = Time.time + 2f;
            float cover = weather.SnowCover * most;
            if (Mathf.Approximately(cover, shown)) return;
            shown = cover;
            for (int i = 0; i < covered.Length; i++)
            {
                if (covered[i] == null) continue;
                covered[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(bare[i], snow, cover));
                covered[i].SetPropertyBlock(block);
            }
        }
    }
}
