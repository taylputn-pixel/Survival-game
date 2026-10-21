using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Net;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // A few words over the screen at the turning points of the game: why you
    // left the city (a new game), and making it through the winter (the first
    // morning of spring). Any button moves it along.
    public class StoryCard : MonoBehaviour, IPlayerBound
    {
        [TextArea(4, 12)]
        [SerializeField] string opening =
            "The plant closed in March, and the rent kept coming.\n\n" +
            "When the lawyer called about Grandma's place in Star Valley (paid off, empty since she passed), there wasn't much to decide. You packed what fit in the car and left the city behind.\n\n" +
            "The garden's gone to weeds and the pantry is thin. Winters here don't forgive.\n\nBut it's yours.";
        [SerializeField] float openingSeconds = 14f;
        [SerializeField] float springSeconds = 10f;

        public bool Showing { get; private set; }
        public string Text => body != null ? body.text : "";

        PlayerHands hands;
        PlayerNeeds needs;
        Image back;
        Text title, body;
        InputAction interactAction, backAction;
        int springShown;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            back = UIKit.Rect("Card", transform).gameObject.AddComponent<Image>();
            UIKit.Stretch(back.rectTransform);
            back.raycastTarget = false;
            title = UIKit.Label(back.transform, "", 52, FontStyle.Bold, TextAnchor.LowerCenter);
            Place(title.rectTransform, 0.62f, 0.78f);
            body = UIKit.Label(back.transform, "", 30, FontStyle.Normal, TextAnchor.UpperCenter);
            body.lineSpacing = 1.15f;
            Place(body.rectTransform, 0.2f, 0.6f);
            Fade(0f);

            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
        }

        static void Place(RectTransform rect, float bottom, float top)
        {
            rect.anchorMin = new Vector2(0.22f, bottom);
            rect.anchorMax = new Vector2(0.78f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void Bind(PlayerHands player)
        {
            hands = player;
            needs = player.GetComponent<PlayerNeeds>();
            // A new game opens with why you came; so does joining a friend's first day.
            bool fresh = SaveSlot.NewGame || (NetSession.Instance != null && NetSession.Instance.IsGuest && FirstMorning());
            SaveSlot.NewGame = false;
            if (fresh) StartCoroutine(Play("", opening, openingSeconds, fromBlack: true));
            if (YearTally.Instance != null) springShown = YearTally.Instance.Closed;
        }

        static bool FirstMorning()
        {
            var clock = FindAnyObjectByType<DayClock>();
            return clock != null && clock.Day == 1;
        }

        void Update()
        {
            var tally = YearTally.Instance;
            if (tally == null || Showing || hands == null) return;
            // Spring came: once you're up and about, the year's tally.
            if (tally.Closed > springShown && tally.LastYear != null && (needs == null || !needs.Asleep))
            {
                springShown = tally.Closed;
                StartCoroutine(Play("Spring", Spring(tally.LastYear), springSeconds, fromBlack: false));
            }
        }

        public static string Spring(YearTally.Year y)
        {
            string down = y.wentDown == 0 ? "Nobody went down." : "Someone went down " + (y.wentDown == 1 ? "once." : y.wentDown + " times.");
            return "You made it through the winter.\n\n" +
                "Year " + y.year + ", " + y.days + " days\n" +
                y.jars + (y.jars == 1 ? " jar" : " jars") + " put up  ·  " +
                y.eggs + (y.eggs == 1 ? " egg" : " eggs") + "  ·  " +
                y.firewood + " firewood split\n" + down;
        }

        IEnumerator Play(string heading, string words, float seconds, bool fromBlack)
        {
            Showing = true;
            title.text = heading;
            body.text = words;
            bool held = hands != null && hands.Controller.InputEnabled;
            if (held) hands.Controller.InputEnabled = false;
            float dark = fromBlack ? 1f : 0.8f;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                Fade(t / 1.2f, dark);
                yield return null;
            }
            Fade(1f, dark);
            float until = Time.time + seconds;
            yield return new WaitForSeconds(1f);
            while (Time.time < until && !interactAction.WasPressedThisFrame() && !backAction.WasPressedThisFrame()) yield return null;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                Fade(1f - t / 1.5f, dark);
                yield return null;
            }
            Fade(0f);
            // A frame later, so the same press doesn't also act in the world.
            yield return null;
            if (held && hands != null) hands.Controller.InputEnabled = true;
            Showing = false;
        }

        void Fade(float amount, float dark = 1f)
        {
            back.color = new Color(0.05f, 0.05f, 0.05f, amount * dark);
            title.color = new Color(0.95f, 0.9f, 0.8f, amount);
            body.color = new Color(0.92f, 0.88f, 0.8f, amount);
        }
    }
}
