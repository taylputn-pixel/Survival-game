using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Net;
using GrandmasHouse.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // How the body's needs show. Nothing on screen normally: the edges of
    // your view darken as you wear down, your stomach growls when you're
    // hungry, and food that's off tastes wrong. Hold Status to check
    // yourself: hunger, rest and how you're doing, in words and bars.
    public class NeedsView : MonoBehaviour, IPlayerBound
    {
        [SerializeField] PlayerNeeds needs;
        [Tooltip("Seconds between stomach growls while hungry, at random in this range.")]
        [SerializeField] Vector2 growlSeconds = new Vector2(45f, 90f);
        [SerializeField] float messageSeconds = 3.5f;
        [SerializeField] float deathSeconds = 6f;
        [SerializeField] string titleScene = "Title";

        InputAction statusAction;
        Image vignette;
        GameObject panel;
        Text hungerWord, restWord, conditionWord, message;
        RectTransform hungerBar, restBar;
        Image deathScreen;
        Text deathText;
        float nextGrowl;
        float messageUntil;

        void Awake()
        {
            statusAction = InputSystem.actions.FindAction("Player/Status", throwIfNotFound: true);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            vignette = UIKit.Rect("Vignette", transform).gameObject.AddComponent<Image>();
            UIKit.Stretch(vignette.rectTransform);
            vignette.sprite = VignetteSprite();
            vignette.color = Color.clear;
            vignette.raycastTarget = false;

            BuildPanel();

            message = UIKit.Label(transform, "", 30, FontStyle.Italic, TextAnchor.MiddleCenter);
            message.color = Color.white;
            Place(message.rectTransform, new Vector2(0.5f, 0.22f), new Vector2(900f, 60f));

            deathScreen = UIKit.Rect("Death", transform).gameObject.AddComponent<Image>();
            UIKit.Stretch(deathScreen.rectTransform);
            deathScreen.color = Color.clear;
            deathScreen.raycastTarget = false;
            deathText = UIKit.Label(deathScreen.transform, "", 56, FontStyle.Normal, TextAnchor.MiddleCenter);
            deathText.color = new Color(0.9f, 0.86f, 0.78f, 0f);
            UIKit.Stretch(deathText.rectTransform);

            nextGrowl = Time.time + Random.Range(growlSeconds.x, growlSeconds.y);
        }

        void OnEnable() => Listen(true);

        void OnDisable() => Listen(false);

        public void Bind(PlayerHands player)
        {
            Listen(false);
            needs = player.GetComponent<PlayerNeeds>();
            Listen(isActiveAndEnabled);
        }

        void Listen(bool on)
        {
            if (needs == null) return;
            needs.Ate -= OnAte;
            needs.FellDown -= OnFellDown;
            needs.CameRound -= OnCameRound;
            needs.GameOver -= OnGameOver;
            if (on)
            {
                needs.Ate += OnAte;
                needs.FellDown += OnFellDown;
                needs.CameRound += OnCameRound;
                needs.GameOver += OnGameOver;
            }
        }

        void BuildPanel()
        {
            RectTransform rect = UIKit.Rect("Status", transform);
            Place(rect, new Vector2(0.5f, 0.78f), new Vector2(560f, 200f));
            var paper = rect.gameObject.AddComponent<Image>();
            paper.color = UIKit.Paper;
            paper.raycastTarget = false;
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 20, 20);
            layout.spacing = 10f;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            hungerWord = Line(rect, "Hunger", out hungerBar);
            restWord = Line(rect, "Rest", out restBar);
            conditionWord = Line(rect, "Health", out _);
            panel = rect.gameObject;
            panel.SetActive(false);
        }

        // "Hunger   Peckish   [=====     ]"
        static Text Line(Transform parent, string name, out RectTransform bar)
        {
            RectTransform row = UIKit.Rect(name, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
            var across = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            across.spacing = 16f;
            across.childControlHeight = across.childControlWidth = true;
            across.childForceExpandWidth = false;
            UIKit.Label(row, name, 26, FontStyle.Bold).gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;
            Text word = UIKit.Label(row, "", 26);
            word.gameObject.AddComponent<LayoutElement>().preferredWidth = 170f;
            bar = null;
            if (name != "Health")
            {
                RectTransform track = UIKit.Rect("Bar", row);
                var element = track.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = 1f;
                var back = track.gameObject.AddComponent<Image>();
                back.color = UIKit.Soft;
                back.raycastTarget = false;
                bar = UIKit.Rect("Fill", track);
                bar.anchorMin = new Vector2(0f, 0.3f);
                bar.anchorMax = new Vector2(1f, 0.7f);
                bar.offsetMin = bar.offsetMax = Vector2.zero;
                var fill = bar.gameObject.AddComponent<Image>();
                fill.color = UIKit.Ink;
                fill.raycastTarget = false;
            }
            return word;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        void Update()
        {
            if (needs == null)
            {
                return;
            }
            var settings = needs.Settings;
            Needs state = needs.State;
            bool on = needs.isActiveAndEnabled && state != null && !needs.Collapsed && !needs.Asleep;

            panel.SetActive(on && statusAction.IsPressed());
            if (panel.activeSelf)
            {
                hungerWord.text = Words(state.Hunger(settings));
                restWord.text = Words(state.Rest(settings));
                conditionWord.text = Words(state.Condition(settings));
                hungerBar.anchorMax = new Vector2(state.hunger / 100f, hungerBar.anchorMax.y);
                restBar.anchorMax = new Vector2(state.rest / 100f, restBar.anchorMax.y);
            }

            // The edges close in as you tire, starve or fall sick.
            float worn = 0f;
            if (on)
            {
                worn = Mathf.Max(worn, Mathf.InverseLerp(settings.tiredBelow, 0f, state.rest) * 0.75f);
                worn = Mathf.Max(worn, state.hunger <= 0f ? 0.35f + 0.5f * Mathf.InverseLerp(0f, settings.collapseAfterHours, state.starvingHours) : 0f);
                worn = Mathf.Max(worn, state.IsSick ? 0.4f : 0f);
            }
            Color target = new Color(0f, 0f, 0f, worn);
            vignette.color = Color.Lerp(vignette.color, target, 1f - Mathf.Exp(-2f * Time.deltaTime));

            if (on && state.hunger < settings.hungryBelow && Time.time >= nextGrowl)
            {
                Sfx.PlayFlat(SoundId.StomachGrowl, 0.6f);
                nextGrowl = Time.time + Random.Range(growlSeconds.x, growlSeconds.y);
            }

            var c = message.color;
            c.a = Mathf.Clamp01((messageUntil - Time.time) / 0.5f);
            message.color = c;
        }

        void OnAte(bool sick)
        {
            if (sick)
            {
                message.text = "That didn't taste right.";
                messageUntil = Time.time + messageSeconds;
            }
        }

        // Down on the floor: the world goes dim, and you wait to be fed.
        void OnFellDown()
        {
            panel.SetActive(false);
            StopAllCoroutines();
            StartCoroutine(Dim(0.75f, "You collapsed from hunger.\nSomeone has to feed you."));
        }

        void OnCameRound()
        {
            StopAllCoroutines();
            deathScreen.color = Color.clear;
            var c = deathText.color;
            c.a = 0f;
            deathText.color = c;
            message.text = "You came round. Eat something soon.";
            messageUntil = Time.time + messageSeconds;
        }

        IEnumerator Dim(float darkness, string words)
        {
            float from = deathScreen.color.a;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                deathScreen.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, darkness, t / 2f));
                yield return null;
            }
            deathText.text = words;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                var c = deathText.color;
                c.a = t / 1.5f;
                deathText.color = c;
                yield return null;
            }
        }

        void OnGameOver()
        {
            StopAllCoroutines();
            StartCoroutine(Die());
        }

        IEnumerator Die()
        {
            panel.SetActive(false);
            bool shared = Net.PlayerNet.All.Count > 1;
            float from = deathScreen.color.a;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                deathScreen.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, 1f, t / 2f));
                yield return null;
            }
            deathText.text = shared ? "Everyone starved." : "You starved.";
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                var c = deathText.color;
                c.a = t / 1.5f;
                deathText.color = c;
                yield return null;
            }
            yield return new WaitForSeconds(deathSeconds);
            if (NetSession.Instance != null)
            {
                NetSession.Instance.Leave();
                yield break;
            }
            SceneManager.LoadScene(titleScene);
        }

        static string Words(HungerLevel level) => level switch
        {
            HungerLevel.Full => "Full",
            HungerLevel.Fed => "Fed",
            HungerLevel.Peckish => "Peckish",
            HungerLevel.Hungry => "Hungry",
            _ => "Starving",
        };

        static string Words(RestLevel level) => level switch
        {
            RestLevel.Rested => "Rested",
            RestLevel.Tired => "Tired",
            RestLevel.VeryTired => "Very tired",
            _ => "Exhausted",
        };

        static string Words(Condition condition) => condition switch
        {
            Condition.Healthy => "Fine",
            Condition.Sick => "Sick",
            Condition.Malnourished => "Malnourished",
            _ => "Dead",
        };

        // Clear in the middle, darkening toward the edges.
        static Sprite VignetteSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) / (size - 1) * 2f - Vector2.one;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.35f, p.magnitude));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
