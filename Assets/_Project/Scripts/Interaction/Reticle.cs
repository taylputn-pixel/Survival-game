using GrandmasHouse.Carrying;
using UnityEngine;
using UnityEngine.UI;

namespace GrandmasHouse.Interaction
{
    // A small dot in the middle of the screen. It grows and brightens when
    // you can pick something up or set something down, and fades otherwise.
    public class Reticle : MonoBehaviour
    {
        [SerializeField] PlayerHands hands;
        [SerializeField] Image dot;
        [SerializeField] float idleSize = 6f;
        [SerializeField] float activeSize = 14f;
        [SerializeField, Range(0f, 1f)] float idleAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] float activeAlpha = 0.9f;

        void Update()
        {
            bool active = hands.CurrentFocus != PlayerHands.Focus.Nothing;
            float size = active ? activeSize : idleSize;
            float alpha = active ? activeAlpha : idleAlpha;
            float t = 1f - Mathf.Exp(-14f * Time.deltaTime);

            var rect = dot.rectTransform;
            rect.sizeDelta = Vector2.Lerp(rect.sizeDelta, new Vector2(size, size), t);
            Color c = dot.color;
            c.a = Mathf.Lerp(c.a, alpha, t);
            dot.color = c;
        }
    }
}
