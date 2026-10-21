using UnityEngine;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // Words heard in the world, written along the bottom of the screen for a
    // while: the radio's farm report.
    public class Caption : MonoBehaviour
    {
        Text text;
        Image back;
        float until;

        public string Showing => until > Time.time ? text.text : "";

        void Awake()
        {
            RectTransform rect = UIKit.Rect("Caption", transform);
            rect.anchorMin = new Vector2(0.2f, 0.04f);
            rect.anchorMax = new Vector2(0.8f, 0.2f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            back = rect.gameObject.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0f);
            back.raycastTarget = false;
            text = UIKit.Label(rect, "", 24, FontStyle.Italic, TextAnchor.MiddleCenter);
            text.color = new Color(1f, 1f, 1f, 0f);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24f, 8f);
            text.rectTransform.offsetMax = new Vector2(-24f, -8f);
        }

        public void Show(string words, float seconds)
        {
            text.text = words;
            until = Time.time + seconds;
        }

        void Update()
        {
            float a = Mathf.Clamp01((until - Time.time) / 0.6f);
            text.color = new Color(1f, 1f, 1f, a);
            back.color = new Color(0f, 0f, 0f, 0.45f * a);
        }
    }
}
