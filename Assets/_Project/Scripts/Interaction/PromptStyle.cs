using System;
using UnityEngine;

namespace GrandmasHouse.Interaction
{
    // Which button icons to show for a controller. Auto matches the
    // controller in use; on consoles it's always the console's own.
    public enum PromptStyle { Auto, Xbox, PlayStation, Switch }

    // The player's own choices, kept between sessions on this machine.
    // The options menu edits these.
    public static class PlayerOptions
    {
        const string PromptStyleKey = "promptStyle";

        public static event Action Changed;

        public static PromptStyle PromptStyle
        {
            get => (PromptStyle)PlayerPrefs.GetInt(PromptStyleKey, (int)PromptStyle.Auto);
            set
            {
                PlayerPrefs.SetInt(PromptStyleKey, (int)value);
                Changed?.Invoke();
            }
        }
    }
}
