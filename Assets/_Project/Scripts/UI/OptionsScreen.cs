using GrandmasHouse.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // The Options screen, shared by the title and pause menus: how looking
    // feels, button icons, volume, and a way into the controls.
    public static class OptionsScreen
    {
        public static void Open(MenuStack menus, Transform canvas)
        {
            RectTransform panel = UIKit.Panel("Options", canvas, 820f);
            UIKit.Label(panel, "Options", 44, FontStyle.Bold, TextAnchor.MiddleCenter);

            Slider first = UIKit.SliderRow(panel, "Mouse look", 0.25f, 3f, PlayerOptions.MouseSensitivity, v => PlayerOptions.MouseSensitivity = v);
            UIKit.SliderRow(panel, "Controller look", 0.25f, 3f, PlayerOptions.StickSensitivity, v => PlayerOptions.StickSensitivity = v);
            UIKit.ChooserRow(panel, "Invert look up/down", new[] { "Off", "On" }, PlayerOptions.InvertY ? 1 : 0, i => PlayerOptions.InvertY = i == 1);
            UIKit.ChooserRow(panel, "Button icons", new[] { "Auto", "Xbox", "PlayStation", "Switch" }, (int)PlayerOptions.PromptStyle, i => PlayerOptions.PromptStyle = (PromptStyle)i);
            UIKit.SliderRow(panel, "Volume", 0f, 1f, PlayerOptions.Volume, v => PlayerOptions.Volume = v);
            UIKit.Button(panel, "Controls", () => ControlsScreen.Open(menus, canvas));
            UIKit.Button(panel, "Back", menus.Pop);

            menus.Push(panel.gameObject, first);
        }
    }
}
