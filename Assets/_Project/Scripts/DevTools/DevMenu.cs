using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using GrandmasHouse.Stations;
using GrandmasHouse.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandmasHouse.DevTools
{
    // Testing menu, Editor and development builds only. F1 while walking
    // around opens the kinds of items in the save catalog; click a kind to
    // see its items, pick how many and click one to drop them in front of you. New items show up here as
    // soon as they're added to the catalog.
    [RequireComponent(typeof(MenuStack))]
    public class DevMenu : MonoBehaviour, GrandmasHouse.Net.IPlayerBound
    {
        [SerializeField] PlayerHands hands;
        [SerializeField] NoteReader reader;
        [SerializeField] Reticle reticle;
        [SerializeField] SaveCatalog catalog;
        [SerializeField] float distance = 0.6f;

        static readonly int[] Amounts = { 1, 2, 4, 10 };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        MenuStack menus;
        CursorLock cursorLock;
        Text status;
        List<SavedItem> items;
        List<ItemCategory> categories;
        int amount = 1;
        bool open;
        int closedFrame = -1;

        void Awake()
        {
            // Its own canvas, laid out like the HUD.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            menus = GetComponent<MenuStack>();
            menus.DimBehind = true;
            menus.Emptied += Close;
            if (hands != null) Bind(hands);
        }

        public void Bind(PlayerHands player)
        {
            hands = player;
            cursorLock = hands.GetComponent<CursorLock>();
        }

        void Update()
        {
            if (hands == null)
            {
                return;
            }
            // Walking comes back a frame after closing, so the Esc that closed
            // this doesn't also open the pause menu.
            if (closedFrame >= 0 && Time.frameCount > closedFrame)
            {
                closedFrame = -1;
                hands.Controller.InputEnabled = true;
            }
            var keys = Keyboard.current;
            if (open || closedFrame >= 0 || keys == null || !keys.f1Key.wasPressedThisFrame)
            {
                return;
            }
            // Only while walking around: no station, no note, no other menu.
            if (Station.Current == null && !reader.IsOpen && hands.Controller.InputEnabled)
            {
                Open();
            }
        }

        // First screen: a button for each kind of item.
        void Open()
        {
            open = true;
            hands.Controller.InputEnabled = false;
            cursorLock.Free = true;
            reticle.Hidden = true;

            items = SpawnableItems();
            categories = new List<ItemCategory>();
            foreach (var item in items)
            {
                if (!categories.Contains(CategoryOf(item))) categories.Add(CategoryOf(item));
            }
            categories.Sort((a, b) => string.Compare(CategoryName(a), CategoryName(b), System.StringComparison.Ordinal));

            RectTransform panel = UIKit.Panel("Dev Menu", transform, 820f);
            UIKit.Label(panel, "Dev menu", 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            UIKit.Label(panel, "Spawn items", 24);
            RectTransform grid = Grid(panel, categories.Count);
            Button first = null;
            foreach (var category in categories)
            {
                ItemCategory kind = category;
                Button button = UIKit.Button(grid, CategoryName(kind), () => OpenCategory(kind), 52f);
                if (first == null) first = button;
            }
            UIKit.Button(panel, "Hunger, rest and time", OpenSurvival);
            Button close = UIKit.Button(panel, "Close", menus.Pop);
            menus.Push(panel.gameObject, first != null ? first : close);
        }

        // Second screen: how many, and the items of one kind. Back returns to the kinds.
        void OpenCategory(ItemCategory category)
        {
            RectTransform panel = UIKit.Panel(CategoryName(category), transform, 820f);
            UIKit.Label(panel, CategoryName(category), 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            var labels = new string[Amounts.Length];
            for (int i = 0; i < Amounts.Length; i++) labels[i] = Amounts[i].ToString();
            UIKit.ChooserRow(panel, "How many", labels, System.Array.IndexOf(Amounts, amount), i => amount = Amounts[i]);

            var ofKind = items.FindAll(item => CategoryOf(item) == category);
            UIKit.Label(panel, "Spawn in front of you", 24);
            RectTransform grid = Grid(panel, ofKind.Count);
            Button first = null;
            foreach (var item in ofKind)
            {
                SavedItem prefab = item;
                Button button = UIKit.Button(grid, Nice(prefab), () => Spawn(prefab), 52f);
                if (first == null) first = button;
            }

            status = UIKit.Label(panel, " ", 22, FontStyle.Italic, TextAnchor.MiddleCenter);
            UIKit.Button(panel, "Back", menus.Pop);
            menus.Push(panel.gameObject, first);
        }

        // Set hunger and rest, or hurry the clock on (food ages with it).
        void OpenSurvival()
        {
            var needs = hands.GetComponent<PlayerNeeds>();
            var clock = FindAnyObjectByType<DayClock>();
            RectTransform panel = UIKit.Panel("Survival", transform, 820f);
            UIKit.Label(panel, "Hunger, rest and time", 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform grid = Grid(panel, 9);
            Button first = UIKit.Button(grid, "Full and rested", () => Say(needs, n => n.Set(100f, 100f)), 52f);
            UIKit.Button(grid, "Hungry", () => Say(needs, n => n.Set(15f, n.State.rest)), 52f);
            UIKit.Button(grid, "Starving 2 days", () => Say(needs, n => n.Set(0f, n.State.rest, 48f)), 52f);
            UIKit.Button(grid, "Collapse", () => Say(needs, n => n.Set(0f, n.State.rest, n.Settings.collapseAfterHours)), 52f);
            UIKit.Button(grid, "Tired", () => Say(needs, n => n.Set(n.State.hunger, 25f)), 52f);
            UIKit.Button(grid, "Exhausted", () => Say(needs, n => n.Set(n.State.hunger, 3f)), 52f);
            UIKit.Button(grid, "Skip 6 hours", () => { clock.Advance(6f); Say(needs, null); }, 52f);
            UIKit.Button(grid, "Skip a day", () => { clock.Advance(24f); Say(needs, null); }, 52f);
            UIKit.Button(grid, "Skip a week", () => { clock.Advance(24f * 7f); Say(needs, null); }, 52f);
            status = UIKit.Label(panel, " ", 22, FontStyle.Italic, TextAnchor.MiddleCenter);
            UIKit.Button(panel, "Back", menus.Pop);
            menus.Push(panel.gameObject, first);
            Say(needs, null);

            void Say(PlayerNeeds n, System.Action<PlayerNeeds> change)
            {
                if (n == null)
                {
                    status.text = "No PlayerNeeds on the player.";
                    return;
                }
                change?.Invoke(n);
                status.text = $"Day {clock.Day}, {Mathf.FloorToInt(clock.Hour):00}:{Mathf.FloorToInt(clock.Hour % 1f * 60f):00}  ·  hunger {n.State.hunger:0}  ·  rest {n.State.rest:0}  ·  starving {n.State.starvingHours:0} h";
            }
        }

        void Close()
        {
            open = false;
            closedFrame = Time.frameCount;
            cursorLock.Free = false;
            reticle.Hidden = false;
        }

        // Buttons two to a row.
        static RectTransform Grid(Transform panel, int count)
        {
            RectTransform grid = UIKit.Rect("Buttons", panel);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(352f, 52f);
            layout.spacing = new Vector2(16f, 12f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            grid.gameObject.AddComponent<LayoutElement>().preferredHeight = (count + 1) / 2 * 64f;
            return grid;
        }

        static ItemCategory CategoryOf(SavedItem item)
        {
            var pickup = item.GetComponent<Pickup>();
            return pickup != null ? pickup.Category : ItemCategory.Other;
        }

        static string CategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Produce: return "Food";
                case ItemCategory.Jar: return "Jars";
                case ItemCategory.Egg: return "Eggs";
                case ItemCategory.Tool: return "Tools";
                case ItemCategory.Container: return "Containers";
                case ItemCategory.Seed: return "Seeds";
                case ItemCategory.Cookware: return "Cookware";
                case ItemCategory.Staple: return "Pantry";
                default: return "Other";
            }
        }

        // Every item in the catalog once (an old key can point at the same prefab).
        List<SavedItem> SpawnableItems()
        {
            var items = new List<SavedItem>();
            foreach (var entry in catalog.items)
            {
                if (entry.prefab != null && !items.Contains(entry.prefab))
                {
                    items.Add(entry.prefab);
                }
            }
            items.Sort((a, b) => string.Compare(Nice(a), Nice(b), System.StringComparison.Ordinal));
            return items;
        }

        static string Nice(SavedItem item)
        {
            var pickup = item.GetComponent<Pickup>();
            return pickup != null ? pickup.DisplayName : item.name;
        }

        // A little row of them on the floor ahead, so they don't land in a heap.
        void Spawn(SavedItem prefab)
        {
            Transform eye = hands.ViewPoint;
            Vector3 ahead = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            Quaternion facing = Quaternion.Euler(0f, eye.eulerAngles.y, 0f);
            for (int i = 0; i < amount; i++)
            {
                float across = (i % 5 - 2) * 0.12f;
                float along = (i / 5) * 0.15f;
                Vector3 at = eye.position + ahead * (distance + along) + side * across + Vector3.down * 0.3f;
                SavedItem made = Instantiate(prefab, at, facing * prefab.transform.rotation);
                made.name = prefab.name;
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer
                    && made.TryGetComponent(out Unity.Netcode.NetworkObject shared))
                {
                    shared.Spawn();
                }
                var pickup = made.GetComponent<Pickup>();
                if (pickup != null)
                {
                    pickup.Drop();
                }
            }
            status.text = "Spawned " + amount + " × " + Nice(prefab);
        }
#else
        void Awake() => Destroy(gameObject);
#endif
    }
}
