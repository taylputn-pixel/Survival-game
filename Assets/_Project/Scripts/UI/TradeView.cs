using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Net;
using GrandmasHouse.Player;
using GrandmasHouse.Trade;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // Haggling with a trader: your goods on their counter on one side, what
    // they have on the other, and whether it balances. Pick how many of each
    // of their things you want, then shake on it. At the store anything over
    // goes on Grandma's account, and the account can make up a shortfall.
    public class TradeView : MonoBehaviour, IPlayerBound
    {
        [SerializeField] Reticle reticle;

        public bool IsOpen => trader != null;
        public Trader Trader => trader;
        public int[] Counts => counts;

        Trader trader;
        PlayerHands hands;
        CursorLock cursorLock;
        InputAction backAction;
        int openedFrame;
        int[] counts;
        float nextWeigh;
        string result;

        GameObject root;
        Text title, greeting, yoursList, yoursTotal, balance, resultLine;
        RectTransform wareRows;
        readonly List<Text> countLabels = new List<Text>();
        readonly List<Text> wareLabels = new List<Text>();
        Button deal;

        void Awake()
        {
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
            Build();
            root.SetActive(false);
        }

        public void Bind(PlayerHands player)
        {
            hands = player;
            cursorLock = hands.GetComponent<CursorLock>();
        }

        public void Open(Trader with, PlayerHands by)
        {
            if (IsOpen) return;
            if (hands == null) Bind(by);
            trader = with;
            counts = new int[trader.Wares.Count];
            result = null;
            openedFrame = Time.frameCount;
            hands.Controller.InputEnabled = false;
            if (cursorLock != null) cursorLock.Free = true;
            if (reticle != null) reticle.Hidden = true;
            trader.Traded += Refresh;
            BuildWares();
            root.SetActive(true);
            Sfx.PlayFlat(SoundId.Paper);
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(deal.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            trader.Traded -= Refresh;
            trader = null;
            root.SetActive(false);
            if (cursorLock != null) cursorLock.Free = false;
            if (reticle != null) reticle.Hidden = false;
            StartCoroutine(GiveBackControl());
        }

        System.Collections.IEnumerator GiveBackControl()
        {
            yield return null;
            hands.Controller.InputEnabled = true;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (!trader.IsOpen || (Time.frameCount != openedFrame && backAction.WasPressedThisFrame()))
            {
                Close();
                return;
            }
            // Someone may set more down on the counter, or take something off.
            if (Time.time >= nextWeigh) Refresh();
        }

        public void Change(int ware, int by)
        {
            int stock = trader.Wares[ware].stock;
            counts[ware] = Mathf.Clamp(counts[ware] + by, 0, stock);
            Sfx.PlayFlat(SoundId.MenuClick, 0.6f);
            result = null;
            Refresh();
        }

        public void Shake()
        {
            float yours = Yours(out _), theirs = Theirs();
            if (!trader.CanDeal(yours, theirs)) return;
            var sent = (int[])counts.Clone();
            trader.Deal(sent);
            result = "Done.";
            for (int i = 0; i < counts.Length; i++)
            {
                if (trader.Wares[i].recipe != null && sent[i] > 0) result = "Copied into the journal: " + trader.Wares[i].recipe.title;
                counts[i] = 0;
            }
            Sfx.PlayFlat(SoundId.Paper);
            Refresh();
        }

        float Yours(out List<Trader.Offer> offers)
        {
            offers = trader.OnCounter();
            float total = 0f;
            foreach (var o in offers) total += o.worth;
            return total;
        }

        float Theirs()
        {
            float total = 0f;
            for (int i = 0; i < counts.Length; i++) total += trader.Price(trader.Wares[i]) * counts[i];
            return total;
        }

        static string Plural(string name) =>
            name.EndsWith("s") ? name : name.EndsWith("o") ? name + "es" : name + "s";

        static string W(float worth) => worth < 10f ? worth.ToString("0.#") : worth.ToString("0");

        void Refresh()
        {
            if (!IsOpen) return;
            nextWeigh = Time.time + 0.5f;
            title.text = trader.Name + ", at " + trader.Place;
            greeting.text = "“" + trader.Greeting + "”";

            float yours = Yours(out var offers);
            var lines = new System.Text.StringBuilder();
            var tally = new SortedDictionary<string, (int n, float w)>();
            foreach (var o in offers)
            {
                tally.TryGetValue(o.item.DisplayName, out var t);
                tally[o.item.DisplayName] = (t.n + 1, t.w + o.worth);
            }
            foreach (var kv in tally) lines.AppendLine((kv.Value.n > 1 ? kv.Value.n + " " + Plural(kv.Key) : kv.Key) + "   " + W(kv.Value.w));
            yoursList.text = lines.Length > 0 ? lines.ToString() : "Nothing they'll take.\nSet your goods on the counter.";
            yoursTotal.text = "Your side: " + W(yours) + (trader.IsStore ? "\nOn Grandma's account: " + W(trader.Credit) : "");

            for (int i = 0; i < counts.Length && i < wareLabels.Count; i++)
            {
                var w = trader.Wares[i];
                counts[i] = Mathf.Min(counts[i], w.stock);
                wareLabels[i].text = trader.WareName(w) + "   " + W(trader.Price(w)) + (w.stock > 0 ? "   (" + w.stock + " left)" : "   (none left)");
                countLabels[i].text = counts[i].ToString();
            }

            float theirs = Theirs();
            bool fair = trader.CanDeal(yours, theirs);
            deal.interactable = fair;
            string side = "Their side: " + W(theirs) + "   against yours: " + W(yours) + ".\n";
            if (!fair) side += theirs > 0f ? "That's more than your side is worth to them." : "";
            else if (trader.IsStore && yours > theirs) side += W(yours - theirs) + " goes on Grandma's account.";
            else if (trader.IsStore && theirs > yours) side += W(theirs - yours) + " comes off Grandma's account.";
            else if (yours > theirs) side += "They'll keep the difference.";
            balance.text = side;
            resultLine.text = result ?? "";
        }

        // ---- Building it ----

        void BuildWares()
        {
            for (int i = wareRows.childCount - 1; i >= 0; i--) Destroy(wareRows.GetChild(i).gameObject);
            wareLabels.Clear();
            countLabels.Clear();
            for (int i = 0; i < trader.Wares.Count; i++)
            {
                int index = i;
                RectTransform row = UIKit.Rect("Ware", wareRows);
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

                var label = UIKit.Label(row, "", 22);
                label.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
                wareLabels.Add(label);
                var less = UIKit.Button(row, "-", () => Change(index, -1), 46f);
                less.gameObject.GetComponent<LayoutElement>().preferredWidth = 46f;
                var count = UIKit.Label(row, "0", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
                count.gameObject.GetComponent<LayoutElement>().preferredWidth = 40f;
                countLabels.Add(count);
                var more = UIKit.Button(row, "+", () => Change(index, 1), 46f);
                more.gameObject.GetComponent<LayoutElement>().preferredWidth = 46f;
            }
        }

        void Build()
        {
            RectTransform rootRect = UIKit.Rect("Trade", transform);
            UIKit.Stretch(rootRect);
            root = rootRect.gameObject;
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            RectTransform sheet = UIKit.Rect("Sheet", rootRect);
            sheet.anchorMin = new Vector2(0.14f, 0.08f);
            sheet.anchorMax = new Vector2(0.86f, 0.92f);
            sheet.offsetMin = sheet.offsetMax = Vector2.zero;
            sheet.gameObject.AddComponent<Image>().color = UIKit.Paper;

            title = UIKit.Label(sheet, "", 36, FontStyle.Bold);
            Place(title.rectTransform, 0.04f, 0.96f, 0.9f, 0.98f);
            greeting = UIKit.Label(sheet, "", 22, FontStyle.Italic);
            Place(greeting.rectTransform, 0.04f, 0.96f, 0.85f, 0.9f);

            var yoursHead = UIKit.Label(sheet, "On the counter", 26, FontStyle.Bold);
            Place(yoursHead.rectTransform, 0.04f, 0.42f, 0.78f, 0.84f);
            yoursList = UIKit.Label(sheet, "", 22, FontStyle.Normal, TextAnchor.UpperLeft);
            yoursList.verticalOverflow = VerticalWrapMode.Overflow;
            Place(yoursList.rectTransform, 0.04f, 0.42f, 0.36f, 0.77f);
            yoursTotal = UIKit.Label(sheet, "", 22, FontStyle.Bold, TextAnchor.LowerLeft);
            Place(yoursTotal.rectTransform, 0.04f, 0.42f, 0.24f, 0.35f);

            var theirsHead = UIKit.Label(sheet, "What they have", 26, FontStyle.Bold);
            Place(theirsHead.rectTransform, 0.46f, 0.96f, 0.78f, 0.84f);
            // A long list scrolls (the store has a lot on its shelves).
            RectTransform viewport = UIKit.Rect("Wares View", sheet);
            Place(viewport, 0.46f, 0.96f, 0.24f, 0.77f);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            wareRows = UIKit.Rect("Wares", viewport);
            wareRows.anchorMin = new Vector2(0f, 1f);
            wareRows.anchorMax = new Vector2(1f, 1f);
            wareRows.pivot = new Vector2(0.5f, 1f);
            wareRows.offsetMin = wareRows.offsetMax = Vector2.zero;
            var rows = wareRows.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.spacing = 4f;
            rows.childControlWidth = rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            wareRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = wareRows;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            balance = UIKit.Label(sheet, "", 22, FontStyle.Normal, TextAnchor.UpperLeft);
            balance.verticalOverflow = VerticalWrapMode.Overflow;
            Place(balance.rectTransform, 0.04f, 0.96f, 0.12f, 0.22f);
            resultLine = UIKit.Label(sheet, "", 20, FontStyle.Italic, TextAnchor.MiddleLeft);
            Place(resultLine.rectTransform, 0.04f, 0.5f, 0.03f, 0.1f);

            deal = UIKit.Button(sheet, "Shake on it", Shake, 56f);
            Place((RectTransform)deal.transform, 0.52f, 0.73f, 0.03f, 0.11f);
            var leave = UIKit.Button(sheet, "Leave", Close, 56f);
            Place((RectTransform)leave.transform, 0.75f, 0.96f, 0.03f, 0.11f);
        }

        static void Place(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
