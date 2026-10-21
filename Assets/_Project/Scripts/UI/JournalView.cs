using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Interaction;
using GrandmasHouse.Net;
using GrandmasHouse.Player;
using GrandmasHouse.Stations;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // The household's journal: what everyone has learned so far, copied out
    // from Grandma's recipe cards and books. Recipes, How-to (one entry per
    // book page read) and Notes, and Tasting: your own notes on the last jar
    // of each recipe you ate. Nothing shows for what nobody has found yet.
    // Open it with the Journal button; Next and Previous go through the
    // entries, Interact turns to the next section (or click them), Back or
    // Journal closes it.
    public class JournalView : MonoBehaviour, IPlayerBound
    {
        [SerializeField] Discoveries discoveries;
        [SerializeField] NoteReader reader;
        [SerializeField] Reticle reticle;
        [SerializeField] DayClock clock;
        [SerializeField] MenuStack menus;
        [Tooltip("How many entry titles fit down the left page.")]
        [SerializeField] int listLength = 13;

        public bool IsOpen { get; private set; }
        public int Tab { get; private set; }
        public int Selected { get; private set; }
        public IReadOnlyList<Entry> Entries => entries;
        public static readonly string[] Tabs = { "Recipes", "How-to", "Notes", "Tasting" };

        public class Entry
        {
            public string title;
            public string body;
            public string source;
        }

        PlayerHands hands;
        CursorLock cursorLock;
        InputAction journalAction, backAction, interactAction, nextAction, previousAction;
        int openedFrame;
        readonly List<Entry> entries = new List<Entry>();

        GameObject root;
        readonly List<Image> tabImages = new List<Image>();
        RectTransform list;
        Text title, body, source;

        void Awake()
        {
            journalAction = InputSystem.actions.FindAction("Player/Journal", throwIfNotFound: true);
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            nextAction = InputSystem.actions.FindAction("Player/Next", throwIfNotFound: true);
            previousAction = InputSystem.actions.FindAction("Player/Previous", throwIfNotFound: true);
            Build();
            root.SetActive(false);
            discoveries.Changed += Refresh;
        }

        void OnDestroy()
        {
            if (discoveries != null) discoveries.Changed -= Refresh;
        }

        public void Bind(PlayerHands player)
        {
            hands = player;
            cursorLock = hands.GetComponent<CursorLock>();
        }

        // Only when nothing else is going on: no station, note, menu, or sleep.
        bool Free => hands != null && Station.Current == null && !reader.IsOpen && reader.ClosedFrame != Time.frameCount
            && (menus == null || !menus.IsOpen) && hands.Controller.InputEnabled;

        void Update()
        {
            if (!IsOpen)
            {
                if (journalAction.WasPressedThisFrame() && Free) Open();
                return;
            }
            if (Time.frameCount == openedFrame) return;
            if (backAction.WasPressedThisFrame() || journalAction.WasPressedThisFrame()) Close();
            else if (interactAction.WasPressedThisFrame() && !OverButton()) ShowTab((Tab + 1) % Tabs.Length);
            else if (nextAction.WasPressedThisFrame()) Select(Selected + 1);
            else if (previousAction.WasPressedThisFrame()) Select(Selected - 1);
        }

        // A mouse click on a tab or entry is handled by the button, not as "next section".
        static bool OverButton() =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public void Open()
        {
            if (hands == null) return;
            IsOpen = true;
            openedFrame = Time.frameCount;
            hands.Controller.InputEnabled = false;
            if (cursorLock != null) cursorLock.Free = true;
            reticle.Hidden = true;
            clock.PausedForWork = true;
            root.SetActive(true);
            Sfx.PlayFlat(SoundId.Paper);
            ShowTab(Tab);
        }

        public void Close()
        {
            IsOpen = false;
            root.SetActive(false);
            Sfx.PlayFlat(SoundId.Paper, 0.6f);
            if (cursorLock != null) cursorLock.Free = false;
            reticle.Hidden = false;
            clock.PausedForWork = false;
            // A frame later, so the same press doesn't also act in the world (or pause).
            StartCoroutine(GiveBackControl());
        }

        IEnumerator GiveBackControl()
        {
            yield return null;
            hands.Controller.InputEnabled = true;
        }

        public void ShowTab(int tab)
        {
            if (tab != Tab) Sfx.PlayFlat(SoundId.Paper, 0.5f);
            Tab = tab;
            Selected = 0;
            Refresh();
        }

        public void Select(int index)
        {
            if (entries.Count == 0) return;
            int clamped = Mathf.Clamp(index, 0, entries.Count - 1);
            if (clamped == Selected) return;
            Selected = clamped;
            Sfx.PlayFlat(SoundId.Paper, 0.4f);
            Show();
        }

        // ---- What's in it ----

        void Refresh()
        {
            Gather(Tab, entries);
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, entries.Count - 1));
            if (root != null && root.activeSelf) Show();
        }

        public void Gather(int tab, List<Entry> into)
        {
            into.Clear();
            foreach (var note in discoveries.Found)
            {
                if (tab == 0 && note.kind == NoteKind.RecipeCard)
                {
                    into.Add(new Entry { title = note.title, body = note.Body, source = note.marginNote });
                }
                else if (tab == 1 && note.kind == NoteKind.Book)
                {
                    for (int page = 0; page < note.PageCount; page++)
                    {
                        if (discoveries.HasPage(note, page)) into.Add(PageEntry(note, page));
                    }
                }
                else if (tab == 2 && (note.kind == NoteKind.Note || note.kind == NoteKind.DiaryPage))
                {
                    into.Add(new Entry { title = note.title, body = note.Body, source = note.marginNote });
                }
            }
            if (tab == 3)
            {
                foreach (var tasting in discoveries.Tastings)
                {
                    into.Add(new Entry { title = tasting.recipe, body = tasting.Note, source = "From the last jar we ate" });
                }
            }
        }

        // A book page, titled by its heading line if it has one.
        static Entry PageEntry(NoteData book, int page)
        {
            string text = book.Page(page).Trim();
            int lineEnd = text.IndexOf('\n');
            string first = lineEnd > 0 ? text.Substring(0, lineEnd).Trim() : "";
            bool heading = first.Length > 0 && first.Length <= 40 && !first.EndsWith(".") && !first.EndsWith(":");
            return new Entry
            {
                title = heading ? first : book.title + (book.PageCount > 1 ? ", page " + (page + 1) : ""),
                body = heading ? text.Substring(lineEnd).Trim() : text,
                source = "From " + book.title,
            };
        }

        // ---- On screen ----

        void Show()
        {
            for (int i = 0; i < tabImages.Count; i++) tabImages[i].color = i == Tab ? UIKit.Selected : UIKit.Soft;

            for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
            int first = Mathf.Clamp(Selected - listLength / 2, 0, Mathf.Max(0, entries.Count - listLength));
            for (int i = first; i < Mathf.Min(entries.Count, first + listLength); i++)
            {
                int index = i;
                var button = UIKit.Button(list, entries[i].title, () => Select(index), 46f);
                button.GetComponentInChildren<Text>().fontSize = 22;
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                button.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(14f, 0f);
                button.transition = Selectable.Transition.None;
                button.GetComponent<Image>().color = i == Selected ? UIKit.Selected : UIKit.Soft;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            Entry entry = entries.Count > 0 ? entries[Selected] : null;
            title.text = entry != null ? entry.title : "";
            body.text = entry != null ? entry.body : "";
            source.text = entry != null ? entry.source : "";
        }

        void Build()
        {
            RectTransform rootRect = UIKit.Rect("Journal", transform);
            UIKit.Stretch(rootRect);
            root = rootRect.gameObject;
            rootRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            RectTransform book = UIKit.Rect("Book", rootRect);
            // A share of the screen, so it fits any shape of window.
            book.anchorMin = new Vector2(0.12f, 0.07f);
            book.anchorMax = new Vector2(0.88f, 0.93f);
            book.offsetMin = book.offsetMax = Vector2.zero;
            book.gameObject.AddComponent<Image>().color = UIKit.Paper;

            // Left page: the sections, then what's in this one.
            RectTransform left = UIKit.Rect("Left Page", book);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0.4f, 1f);
            left.offsetMin = new Vector2(44f, 40f);
            left.offsetMax = new Vector2(-24f, -40f);
            var leftLayout = left.gameObject.AddComponent<VerticalLayoutGroup>();
            leftLayout.spacing = 18f;
            leftLayout.childControlWidth = leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;
            UIKit.Label(left, "Journal", 40, FontStyle.Bold, TextAnchor.MiddleLeft);

            RectTransform tabs = UIKit.Rect("Tabs", left);
            var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 10f;
            tabLayout.childControlWidth = tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = true;
            var tabSize = tabs.gameObject.AddComponent<LayoutElement>();
            tabSize.minHeight = tabSize.preferredHeight = 54f;
            tabSize.flexibleHeight = 0f;
            tabLayout.childForceExpandHeight = false;
            for (int i = 0; i < Tabs.Length; i++)
            {
                int tab = i;
                var button = UIKit.Button(tabs, Tabs[i], () => ShowTab(tab), 54f);
                button.GetComponentInChildren<Text>().fontSize = 24;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                // Colored by which section is open, not by hover or selection.
                button.transition = Selectable.Transition.None;
                tabImages.Add(button.GetComponent<Image>());
            }

            list = UIKit.Rect("Entries", left);
            var listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 4f;
            listLayout.childControlWidth = listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            list.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // The fold of the book.
            RectTransform fold = UIKit.Rect("Fold", book);
            fold.anchorMin = new Vector2(0.4f, 0.04f);
            fold.anchorMax = new Vector2(0.4f, 0.96f);
            fold.sizeDelta = new Vector2(3f, 0f);
            fold.gameObject.AddComponent<Image>().color = UIKit.Soft;

            // Right page: the entry itself.
            RectTransform right = UIKit.Rect("Right Page", book);
            right.anchorMin = new Vector2(0.4f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.offsetMin = new Vector2(44f, 40f);
            right.offsetMax = new Vector2(-48f, -44f);

            title = UIKit.Label(right, "", 34, FontStyle.Bold, TextAnchor.UpperLeft);
            Place(title.rectTransform, 1f, 0.9f);
            body = UIKit.Label(right, "", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.1f;
            Place(body.rectTransform, 0.9f, 0.08f);
            source = UIKit.Label(right, "", 20, FontStyle.Italic, TextAnchor.LowerLeft);
            source.color = new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.7f);
            Place(source.rectTransform, 0.07f, 0f);
        }

        static void Place(RectTransform rect, float top, float bottom)
        {
            rect.anchorMin = new Vector2(0f, bottom);
            rect.anchorMax = new Vector2(1f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
