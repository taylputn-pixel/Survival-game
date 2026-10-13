using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Interaction
{
    public enum PadFamily { Xbox, PlayStation, Switch }

    // Keeps track of whether you're playing on keyboard and mouse or a
    // controller (and which kind), from the last action you used.
    public static class InputDevices
    {
        public static bool UsingGamepad { get; private set; }
        public static PadFamily LastPad { get; private set; } = PadFamily.Xbox;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            UsingGamepad = false;
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;
        }

        static void OnActionChange(object target, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed || target is not InputAction action || action.activeControl == null)
            {
                return;
            }
            InputDevice device = action.activeControl.device;
            bool gamepad = device is Gamepad;
            PadFamily family = gamepad ? FamilyOf((Gamepad)device) : LastPad;
            if (gamepad != UsingGamepad || family != LastPad)
            {
                UsingGamepad = gamepad;
                LastPad = family;
                Changed?.Invoke();
            }
        }

        static PadFamily FamilyOf(Gamepad pad)
        {
            // Layout names work on every platform, unlike the device classes.
            string layout = pad.layout;
            if (layout.Contains("DualShock") || layout.Contains("DualSense") || layout.Contains("PS4") || layout.Contains("PS5"))
            {
                return PadFamily.PlayStation;
            }
            if (layout.Contains("Switch") || layout.Contains("Npad"))
            {
                return PadFamily.Switch;
            }
            return PadFamily.Xbox;
        }

        // The controller icons to show, after the player's style choice.
        public static PadFamily ShownPad
        {
            get
            {
                switch (PlayerOptions.PromptStyle)
                {
                    case PromptStyle.Xbox: return PadFamily.Xbox;
                    case PromptStyle.PlayStation: return PadFamily.PlayStation;
                    case PromptStyle.Switch: return PadFamily.Switch;
                    default: return LastPad;
                }
            }
        }
    }
}
