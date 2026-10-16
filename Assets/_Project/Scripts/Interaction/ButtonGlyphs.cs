using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Interaction
{
    // The icon for an action's button on the device you're using: a keycap
    // with the bound key, a mouse button, or a controller face button in the
    // chosen style. Icons are drawn in code until real art replaces them.
    public static class ButtonGlyphs
    {
        public struct Glyph
        {
            public Sprite sprite;
            public string label;
            public Color labelColor;
        }

        const int Size = 96;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static readonly Color PadBase = new Color(0.16f, 0.16f, 0.17f);
        static readonly Color KeyBase = new Color(0.95f, 0.92f, 0.85f);
        static readonly Color KeyInk = new Color(0.22f, 0.18f, 0.14f);

        public static Glyph For(InputAction action) => ForDevice(action, InputDevices.UsingGamepad);

        // For a specific kind of device, whichever is in use (the controls screen shows both).
        public static Glyph ForDevice(InputAction action, bool gamepad)
        {
            return gamepad ? GamepadGlyph(action) : KeyboardGlyph(action);
        }

        static Glyph GamepadGlyph(InputAction action)
        {
            string control = FindBinding(action, "<Gamepad>/");
            PadFamily family = InputDevices.ShownPad;
            int face = FaceIndex(control);
            if (face < 0)
            {
                // Shoulders, triggers, sticks: a dark cap with the name printed on that controller.
                return new Glyph { sprite = Cap(PadBase, "pad-cap"), label = PadName(control, family), labelColor = Color.white };
            }

            switch (family)
            {
                case PadFamily.PlayStation:
                    return new Glyph { sprite = PlayStationShape(face), label = "", labelColor = Color.white };
                case PadFamily.Switch:
                    // Nintendo's layout: B is the bottom button, A the right.
                    string[] switchLetters = { "B", "A", "Y", "X" };
                    return new Glyph { sprite = Circle(PadBase, "pad-circle"), label = switchLetters[face], labelColor = Color.white };
                default:
                    string[] xboxLetters = { "A", "B", "X", "Y" };
                    Color[] xboxColors =
                    {
                        new Color(0.45f, 0.78f, 0.33f), new Color(0.92f, 0.33f, 0.27f),
                        new Color(0.3f, 0.58f, 0.92f), new Color(0.96f, 0.77f, 0.24f),
                    };
                    return new Glyph { sprite = Circle(PadBase, "pad-circle"), label = xboxLetters[face], labelColor = xboxColors[face] };
            }
        }

        static Glyph KeyboardGlyph(InputAction action)
        {
            string key = FindBinding(action, "<Keyboard>/");
            if (key != null)
            {
                return new Glyph { sprite = Cap(KeyBase, "key-cap"), label = ShortName(action, "<Keyboard>/"), labelColor = KeyInk };
            }
            string mouse = FindBinding(action, "<Mouse>/");
            bool right = mouse != null && mouse.Contains("right");
            return new Glyph { sprite = Mouse(right), label = "", labelColor = KeyInk };
        }

        // The control path of the first binding on that device, after rebinding.
        static string FindBinding(InputAction action, string devicePrefix)
        {
            foreach (var binding in action.bindings)
            {
                if (!binding.isComposite && !binding.isPartOfComposite && binding.effectivePath.StartsWith(devicePrefix))
                {
                    return binding.effectivePath;
                }
            }
            return null;
        }

        static string ShortName(InputAction action, string devicePrefix)
        {
            string path = FindBinding(action, devicePrefix);
            if (path == null)
            {
                return "?";
            }
            string name = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            switch (name)
            {
                case "Escape": return "Esc";
                case "Left Shift": return "Shift";
                case "Space": return "Space";
                default: return name.Length > 5 ? name.Substring(0, 5) : name;
            }
        }

        // What each controller family prints on its other buttons.
        static string PadName(string control, PadFamily family)
        {
            if (control == null) return "?";
            string name = control.Substring(control.LastIndexOf('/') + 1);
            int f = (int)family; // Xbox, PlayStation, Switch
            switch (name)
            {
                case "leftShoulder": return new[] { "LB", "L1", "L" }[f];
                case "rightShoulder": return new[] { "RB", "R1", "R" }[f];
                case "leftTrigger": return new[] { "LT", "L2", "ZL" }[f];
                case "rightTrigger": return new[] { "RT", "R2", "ZR" }[f];
                case "leftStickPress": return new[] { "LS", "L3", "LS" }[f];
                case "rightStickPress": return new[] { "RS", "R3", "RS" }[f];
                case "start": return new[] { "Menu", "Opt", "+" }[f];
                case "select": return new[] { "View", "Share", "-" }[f];
                // The d-pad's directions, as arrows.
                case "left": return "←";
                case "right": return "→";
                case "up": return "↑";
                case "down": return "↓";
                default: return name.Length > 5 ? name.Substring(0, 5) : name;
            }
        }

        // South, East, West, North; -1 for anything else.
        static int FaceIndex(string control)
        {
            if (control == null) return -1;
            if (control.EndsWith("buttonSouth")) return 0;
            if (control.EndsWith("buttonEast")) return 1;
            if (control.EndsWith("buttonWest")) return 2;
            if (control.EndsWith("buttonNorth")) return 3;
            return -1;
        }

        // ---- Drawing ----

        delegate float Shape(Vector2 p);

        static Sprite Circle(Color color, string key) => Draw(key, p => Disc(p, 0.46f), color, null, Color.clear);

        static Sprite Cap(Color color, string key) => Draw(key, p => RoundRect(p, new Vector2(0.44f, 0.4f), 0.12f), color, null, Color.clear);

        static Sprite PlayStationShape(int face)
        {
            Color[] colors =
            {
                new Color(0.55f, 0.7f, 0.95f), new Color(0.93f, 0.42f, 0.42f),
                new Color(0.86f, 0.55f, 0.8f), new Color(0.35f, 0.8f, 0.66f),
            };
            Shape[] shapes =
            {
                p => Mathf.Min(Segment(p, new Vector2(-0.2f, -0.2f), new Vector2(0.2f, 0.2f)), Segment(p, new Vector2(-0.2f, 0.2f), new Vector2(0.2f, -0.2f))) - 0.045f,
                p => Mathf.Abs(p.magnitude - 0.21f) - 0.045f,
                p => Mathf.Abs(RoundRect(p, new Vector2(0.19f, 0.19f), 0.02f)) - 0.04f,
                p => Mathf.Abs(Triangle(p, 0.25f)) - 0.04f,
            };
            return Draw("ps-" + face, p => Disc(p, 0.46f), PadBase, shapes[face], colors[face]);
        }

        static Sprite Mouse(bool rightButton)
        {
            Shape body = p => RoundRect(p, new Vector2(0.26f, 0.38f), 0.24f);
            Shape button = p =>
            {
                bool onSide = rightButton ? p.x > 0.015f : p.x < -0.015f;
                return onSide && p.y > 0.04f ? body(p) + 0.05f : 1f;
            };
            return Draw(rightButton ? "mouse-r" : "mouse-l", body, KeyBase, button, new Color(0.85f, 0.5f, 0.35f));
        }

        static Sprite Draw(string key, Shape background, Color backgroundColor, Shape mark, Color markColor)
        {
            if (cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[Size * Size];
            float pixel = 1f / Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f);
                    float fill = Coverage(background(p), pixel);
                    Color c = backgroundColor;
                    c.a *= fill;
                    if (mark != null)
                    {
                        float m = Coverage(mark(p), pixel) * fill;
                        c = Color.Lerp(c, new Color(markColor.r, markColor.g, markColor.b, c.a), m);
                    }
                    pixels[y * Size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            cache[key] = sprite;
            return sprite;
        }

        // Soft edge one pixel wide, so the shapes don't look jagged.
        static float Coverage(float distance, float pixel) => Mathf.Clamp01(0.5f - distance / pixel);

        static float Disc(Vector2 p, float radius) => p.magnitude - radius;

        static float RoundRect(Vector2 p, Vector2 half, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static float Triangle(Vector2 p, float r)
        {
            // Equilateral triangle pointing up, centered a touch low.
            p.y += 0.04f;
            const float k = 1.7320508f;
            p.x = Mathf.Abs(p.x) - r;
            p.y = p.y + r / k;
            if (p.x + k * p.y > 0f)
            {
                p = new Vector2(p.x - k * p.y, -k * p.x - p.y) / 2f;
            }
            p.x -= Mathf.Clamp(p.x, -2f * r, 0f);
            return -p.magnitude * Mathf.Sign(p.y);
        }
    }
}
