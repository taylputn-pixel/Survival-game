using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandmasHouse.Interaction
{
    // One button prompt: the button's icon (with its key or letter on it)
    // and a short word for what it does.
    public class PromptView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text iconLabel;
        [SerializeField] Text verb;

        public void Show(InputAction action, string word)
        {
            ButtonGlyphs.Glyph glyph = ButtonGlyphs.For(action);
            if (icon.sprite != glyph.sprite)
            {
                icon.sprite = glyph.sprite;
            }
            if (iconLabel.text != glyph.label)
            {
                iconLabel.text = glyph.label;
            }
            iconLabel.color = glyph.labelColor;
            if (verb.text != word)
            {
                verb.text = word;
            }
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
