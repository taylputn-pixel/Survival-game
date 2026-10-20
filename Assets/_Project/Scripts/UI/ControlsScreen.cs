using System;
using GrandmasHouse.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // Rebind buttons, keyboard and controller separately. Pick one, press the
    // new key or button, and it's saved. Walking and looking stay on WASD,
    // the mouse, and the sticks.
    public static class ControlsScreen
    {
        static readonly (string action, string label)[] Rebindable =
        {
            ("Player/Interact", "Use / pick up"),
            ("Player/PickUp", "Second action (eat, pour out at the sink)"),
            ("Player/Back", "Step back"),
            ("Player/Sprint", "Walk briskly"),
            ("Player/Status", "Check yourself (hold)"),
            ("Player/Journal", "Open the journal"),
            ("Player/Pause", "Pause"),
        };

        public static void Open(MenuStack menus, Transform canvas)
        {
            RectTransform panel = UIKit.Panel("Controls", canvas, 880f);
            UIKit.Label(panel, "Controls", 44, FontStyle.Bold, TextAnchor.MiddleCenter);

            RectTransform header = UIKit.Row(panel, "");
            UIKit.Label(header, "", 22).GetComponent<LayoutElement>().preferredWidth = 290f;
            UIKit.Label(header, "Keyboard & mouse", 22, FontStyle.Italic, TextAnchor.MiddleCenter).GetComponent<LayoutElement>().flexibleWidth = 1f;
            UIKit.Label(header, "Controller", 22, FontStyle.Italic, TextAnchor.MiddleCenter).GetComponent<LayoutElement>().flexibleWidth = 1f;

            Selectable first = null;
            foreach (var (actionName, label) in Rebindable)
            {
                InputAction action = InputSystem.actions.FindAction(actionName, throwIfNotFound: true);
                RectTransform row = UIKit.Row(panel, label);
                Button keys = BindingButton(menus, row, action, gamepad: false);
                BindingButton(menus, row, action, gamepad: true);
                first ??= keys;
            }

            UIKit.Label(panel, "Walking and looking use WASD and the mouse, or the sticks.", 22, FontStyle.Italic, TextAnchor.MiddleCenter);
            UIKit.Button(panel, "Reset to defaults", () =>
            {
                PlayerOptions.ResetBindings();
                menus.Pop();
                Open(menus, canvas);
            });
            UIKit.Button(panel, "Back", menus.Pop);

            menus.Push(panel.gameObject, first);
        }

        // A button showing the current key or controller button; press it to choose a new one.
        static Button BindingButton(MenuStack menus, Transform row, InputAction action, bool gamepad)
        {
            RectTransform rect = UIKit.Rect(gamepad ? "Controller" : "Keyboard", row);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            UIKit.Style(button, image);

            RectTransform iconRect = UIKit.Rect("Icon", rect);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(44f, 44f);
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Text iconLabel = UIKit.Label(iconRect, "", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Stretch(iconLabel.rectTransform);
            iconLabel.resizeTextForBestFit = true;
            iconLabel.resizeTextMinSize = 8;
            iconLabel.resizeTextMaxSize = 22;
            Text waiting = UIKit.Label(rect, "", 22, FontStyle.Italic, TextAnchor.MiddleCenter);
            UIKit.Stretch(waiting.rectTransform);

            Action show = () =>
            {
                ButtonGlyphs.Glyph glyph = ButtonGlyphs.ForDevice(action, gamepad);
                icon.sprite = glyph.sprite;
                iconLabel.text = glyph.label;
                iconLabel.color = glyph.labelColor;
                icon.enabled = true;
                waiting.text = "";
            };
            show();

            button.onClick.AddListener(() =>
            {
                int index = BindingIndex(action, gamepad);
                if (index < 0 || menus.Busy)
                {
                    return;
                }
                icon.enabled = false;
                iconLabel.text = "";
                waiting.text = gamepad ? "Press a button..." : "Press a key...";
                menus.Busy = true;

                action.Disable();
                var rebind = action.PerformInteractiveRebinding(index)
                    .WithControlsHavingToMatchPath(gamepad ? "<Gamepad>" : "<Keyboard>")
                    .WithControlsExcluding("<Gamepad>/leftStick")
                    .WithControlsExcluding("<Gamepad>/rightStick")
                    .WithControlsExcluding("<Gamepad>/dpad")
                    .WithCancelingThrough(gamepad ? "<Gamepad>/select" : "<Keyboard>/backspace")
                    .OnMatchWaitForAnother(0.1f);
                rebind.OnComplete(op =>
                {
                    op.Dispose();
                    action.Enable();
                    PlayerOptions.SaveBindings();
                    show();
                    menus.Busy = false;
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    action.Enable();
                    show();
                    menus.Busy = false;
                })
                .Start();
            });
            return button;
        }

        // The binding that the button changes: the first key (or controller
        // button) for that action, leaving any extra mouse binding as is.
        static int BindingIndex(InputAction action, bool gamepad)
        {
            string prefix = gamepad ? "<Gamepad>" : "<Keyboard>";
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!binding.isComposite && !binding.isPartOfComposite && binding.effectivePath.StartsWith(prefix))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
