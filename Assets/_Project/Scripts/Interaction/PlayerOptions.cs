using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Interaction
{
    // Which button icons to show for a controller. Auto matches the
    // controller in use; on consoles it's always the console's own.
    public enum PromptStyle { Auto, Xbox, PlayStation, Switch }

    // The player's own choices, kept between sessions on this machine.
    // The options menu edits these; the game reads them live.
    public static class PlayerOptions
    {
        const string PromptStyleKey = "promptStyle";
        const string MouseKey = "mouseSensitivity";
        const string StickKey = "stickSensitivity";
        const string InvertKey = "invertY";
        const string VolumeKey = "volume";
        const string BindingsKey = "bindingOverrides";

        public static event Action Changed;

        // Loads saved controls and volume before the first scene starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            string overrides = PlayerPrefs.GetString(BindingsKey, "");
            if (!string.IsNullOrEmpty(overrides))
            {
                InputSystem.actions.LoadBindingOverridesFromJson(overrides);
            }
            AudioListener.volume = Volume;
        }

        public static PromptStyle PromptStyle
        {
            get => (PromptStyle)PlayerPrefs.GetInt(PromptStyleKey, (int)PromptStyle.Auto);
            set { PlayerPrefs.SetInt(PromptStyleKey, (int)value); Changed?.Invoke(); }
        }

        // Multiplies the tuned mouse look and station cursor speed (1 = as designed).
        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat(MouseKey, 1f);
            set { PlayerPrefs.SetFloat(MouseKey, value); Changed?.Invoke(); }
        }

        // Multiplies the tuned controller look and station cursor speed.
        public static float StickSensitivity
        {
            get => PlayerPrefs.GetFloat(StickKey, 1f);
            set { PlayerPrefs.SetFloat(StickKey, value); Changed?.Invoke(); }
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt(InvertKey, 0) == 1;
            set { PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); Changed?.Invoke(); }
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, value);
                AudioListener.volume = value;
                Changed?.Invoke();
            }
        }

        // Call after the player rebinds a button, so it's remembered.
        public static void SaveBindings()
        {
            PlayerPrefs.SetString(BindingsKey, InputSystem.actions.SaveBindingOverridesAsJson());
            Changed?.Invoke();
        }

        public static void ResetBindings()
        {
            InputSystem.actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingsKey);
            Changed?.Invoke();
        }
    }
}
