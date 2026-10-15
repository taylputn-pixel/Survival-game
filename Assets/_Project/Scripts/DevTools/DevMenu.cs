using System.Collections.Generic;
using GrandmasHouse.Carrying;
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
    // around opens a list of every item in the save catalog; pick how many
    // and click one to drop them in front of you. New items show up here as
    // soon as they're added to the catalog.
    [RequireComponent(typeof(MenuStack))]
    public class DevMenu : MonoBehaviour
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
            cursorLock = hands.GetComponent<CursorLock>();
        }

        void Update()
        {
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

        void Open()
        {
            open = true;
            hands.Controller.InputEnabled = false;
            cursorLock.Free = true;
            reticle.Hidden = true;

            RectTransform panel = UIKit.Panel("Dev Menu", transform, 820f);
            UIKit.Label(panel, "Dev menu", 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            var labels = new string[Amounts.Length];
            for (int i = 0; i < Amounts.Length; i++) labels[i] = Amounts[i].ToString();
            var chooser = UIKit.ChooserRow(panel, "How many", labels, System.Array.IndexOf(Amounts, amount), i => amount = Amounts[i]);

            UIKit.Label(panel, "Spawn in front of you", 24);
            RectTransform grid = UIKit.Rect("Items", panel);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(352f, 52f);
            layout.spacing = new Vector2(16f, 12f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            List<SavedItem> items = SpawnableItems();
            int rows = (items.Count + 1) / 2;
            grid.gameObject.AddComponent<LayoutElement>().preferredHeight = rows * 64f;
            foreach (var item in items)
            {
                SavedItem prefab = item;
                UIKit.Button(grid, Nice(prefab), () => Spawn(prefab), 52f);
            }

            status = UIKit.Label(panel, " ", 22, FontStyle.Italic, TextAnchor.MiddleCenter);
            UIKit.Button(panel, "Close", menus.Pop);
            menus.Push(panel.gameObject, chooser);
        }

        void Close()
        {
            open = false;
            closedFrame = Time.frameCount;
            cursorLock.Free = false;
            reticle.Hidden = false;
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
