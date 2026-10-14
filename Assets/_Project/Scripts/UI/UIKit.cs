using System;
using GrandmasHouse.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // Builds the cozy menu widgets in code (paper panels, buttons, sliders,
    // left-right choosers) so every menu looks alike and works by mouse or
    // controller. Real art can restyle these in one place later.
    public static class UIKit
    {
        public static readonly Color Paper = new Color(0.96f, 0.93f, 0.85f, 0.97f);
        public static readonly Color Ink = new Color(0.25f, 0.19f, 0.13f);
        public static readonly Color Soft = new Color(0.88f, 0.82f, 0.7f);
        public static readonly Color Selected = new Color(0.95f, 0.73f, 0.47f);
        public static readonly Color Pressed = new Color(0.86f, 0.6f, 0.38f);

        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // A sheet of paper in the middle of the screen that stacks its rows.
        public static RectTransform Panel(string name, Transform parent, float width)
        {
            RectTransform panel = Rect(name, parent);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(width, 0f);
            panel.gameObject.AddComponent<Image>().color = Paper;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 40, 44);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        public static Text Label(Transform parent, string text, int size, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect("Label", parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Ink;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Height comes from the text itself, so wrapped lines fit.
            rect.gameObject.AddComponent<LayoutElement>().minHeight = size * 1.4f;
            return label;
        }

        public static Button Button(Transform parent, string text, UnityAction onClick, float height = 62f)
        {
            RectTransform rect = Rect(text, parent);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            Style(button, image);
            button.onClick.AddListener(() => Sfx.PlayFlat(SoundId.MenuClick));
            button.onClick.AddListener(onClick);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;

            Text label = Label(rect, text, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return button;
        }

        // Label on the left, a slider on the right, and its value as a percent.
        public static Slider SliderRow(Transform parent, string text, float min, float max, float value, Action<float> onChange)
        {
            RectTransform row = Row(parent, text);

            RectTransform holder = Rect("Slider", row);
            var holderElement = holder.gameObject.AddComponent<LayoutElement>();
            holderElement.preferredWidth = 300f;
            holderElement.flexibleWidth = 1f;

            var background = holder.gameObject.AddComponent<Image>();
            background.color = Soft;
            var slider = holder.gameObject.AddComponent<Slider>();
            Style(slider, background);

            RectTransform fillArea = Rect("Fill Area", holder);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(6f, 18f);
            fillArea.offsetMax = new Vector2(-6f, -18f);
            RectTransform fill = Rect("Fill", fillArea);
            fill.gameObject.AddComponent<Image>().color = Pressed;
            fill.sizeDelta = Vector2.zero;

            RectTransform handleArea = Rect("Handle Area", holder);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);
            RectTransform handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(20f, 0f);
            handle.gameObject.AddComponent<Image>().color = Ink;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = background;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            Text readout = Label(row, Percent(value), 26, FontStyle.Normal, TextAnchor.MiddleRight);
            readout.GetComponent<LayoutElement>().preferredWidth = 90f;
            slider.onValueChanged.AddListener(v =>
            {
                readout.text = Percent(v);
                onChange(v);
            });
            return slider;
        }

        // Label on the left and a value you change with left/right (or by clicking).
        public static Chooser ChooserRow(Transform parent, string text, string[] options, int index, Action<int> onChange)
        {
            RectTransform row = Row(parent, text);
            RectTransform holder = Rect("Chooser", row);
            var element = holder.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 390f;
            element.flexibleWidth = 1f;
            var image = holder.gameObject.AddComponent<Image>();
            var chooser = holder.gameObject.AddComponent<Chooser>();
            Style(chooser, image);
            Text label = Label(holder, "", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            chooser.Setup(label, options, index, onChange);
            return chooser;
        }

        // A labelled row that lays its controls out left to right.
        public static RectTransform Row(Transform parent, string text)
        {
            RectTransform row = Rect(text + " Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
            if (!string.IsNullOrEmpty(text))
            {
                Text label = Label(row, text, 26);
                label.GetComponent<LayoutElement>().preferredWidth = 290f;
            }
            return row;
        }

        public static void Style(Selectable selectable, Image image)
        {
            image.color = Color.white;
            selectable.targetGraphic = image;
            ColorBlock colors = selectable.colors;
            colors.normalColor = Soft;
            colors.highlightedColor = Selected;
            colors.selectedColor = Selected;
            colors.pressedColor = Pressed;
            colors.disabledColor = new Color(Soft.r, Soft.g, Soft.b, 0.4f);
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
        }

        static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";
    }
}
