using GrandmasHouse.Carrying;
using GrandmasHouse.Net;
using UnityEngine;
using UnityEngine.UI;

namespace GrandmasHouse.Interaction
{
    // A small dot in the middle of the screen. It grows and brightens when
    // you can pick something up or set something down, and fades otherwise.
    // Stations can move it around as their cursor, hide it, or highlight it.
    public class Reticle : MonoBehaviour, IPlayerBound
    {
        [SerializeField] PlayerHands hands;
        [SerializeField] Image dot;
        [SerializeField] float idleSize = 6f;
        [SerializeField] float activeSize = 14f;
        [SerializeField, Range(0f, 1f)] float idleAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] float activeAlpha = 0.9f;

        // Where the dot sits, in viewport coordinates (0..1). Null = screen center.
        public Vector2? ScreenPoint { get; set; }
        // Forces the highlighted or plain look. Null = follow what the hands see.
        public bool? Highlight { get; set; }
        public bool Hidden { get; set; }

        RectTransform canvasRect;

        void Awake() => canvasRect = (RectTransform)transform;

        public void Bind(PlayerHands player) => hands = player;

        void Update()
        {
            bool active = Highlight ?? (hands != null && hands.CurrentFocus != PlayerHands.Focus.Nothing);
            float size = active ? activeSize : idleSize;
            float alpha = Hidden ? 0f : active ? activeAlpha : idleAlpha;
            float t = 1f - Mathf.Exp(-14f * Time.deltaTime);

            var rect = dot.rectTransform;
            rect.sizeDelta = Vector2.Lerp(rect.sizeDelta, new Vector2(size, size), t);
            Color c = dot.color;
            c.a = Mathf.Lerp(c.a, alpha, t);
            dot.color = c;

            Vector2 point = ScreenPoint ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.Scale(point - new Vector2(0.5f, 0.5f), canvasRect.rect.size);
        }
    }
}
