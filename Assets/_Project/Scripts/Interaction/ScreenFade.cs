using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GrandmasHouse.Interaction
{
    // A full-screen black overlay for gentle transitions like going to sleep.
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] Image overlay;

        public IEnumerator FadeTo(float alpha, float seconds)
        {
            float start = overlay.color.a;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                SetAlpha(Mathf.SmoothStep(start, alpha, time / seconds));
                yield return null;
            }
            SetAlpha(alpha);
        }

        void SetAlpha(float alpha)
        {
            Color c = overlay.color;
            c.a = alpha;
            overlay.color = c;
            overlay.raycastTarget = false;
        }
    }
}
