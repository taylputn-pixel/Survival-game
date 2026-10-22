using System;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Net;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Food
{
    // A soup bowl or a dinner plate. Soup goes in a bowl: hold it empty and
    // look at a pot of soup to ladle some in. Everything else (the skillet's
    // fry-ups, sauerkraut) goes on a plate. Eat it from your hands; it's
    // still a bowl or a plate afterwards. Food left sitting goes off like
    // any food (keep it cold).
    [RequireComponent(typeof(Pickup))]
    public class Bowl : MonoBehaviour, IItemState, IEdible
    {
        [Tooltip("The soup's surface inside the bowl, hidden while it's empty.")]
        [SerializeField] Renderer soup;
        [Tooltip("A flat plate rather than a bowl: for anything that isn't soup.")]
        [SerializeField] bool plate;

        public string Dish { get; private set; }
        public float Food { get; private set; }
        public bool IsEmpty => Food <= 0f;
        public bool IsPlate => plate;

        // What you'd hold out at each: a bowl at the soup pot, a plate at the skillet.
        public static bool EmptyBowl(PlayerHands hands) => Held(hands, false);
        public static bool EmptyPlate(PlayerHands hands) => Held(hands, true);
        static bool Held(PlayerHands hands, bool plate) =>
            !hands.IsEmpty && hands.Held.TryGetComponent(out Bowl b) && b.plate == plate && b.IsEmpty;

        Perishable perishable;
        MaterialPropertyBlock block;
        Color color;

        void Awake()
        {
            perishable = GetComponent<Perishable>();
            if (perishable != null) perishable.CanSpoil = () => !IsEmpty;
            block = new MaterialPropertyBlock();
            Show();
        }

        // Ladled full from the pot.
        public void Fill(string dish, float food, Color soupColor, bool spoiled = false)
        {
            Dish = dish;
            Food = food;
            color = soupColor;
            if (perishable != null)
            {
                perishable.Refresh();
                // Scooped from something gone bad: it'll make you sick.
                if (spoiled) perishable.Restore("{\"freshness\":0}");
            }
            Show();
        }

        void Show()
        {
            if (soup == null) return;
            soup.enabled = !IsEmpty;
            soup.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            soup.SetPropertyBlock(block);
        }

        // ---- Eating ----

        public bool CanEat => !IsEmpty;
        public float Nutrition => Food;

        public float SickHours(SurvivalSettings settings) =>
            perishable != null && perishable.IsRotten ? settings.spoiledSickHours : 0f;

        public void Eaten(PlayerHands hands)
        {
            Dish = null;
            Food = 0f;
            Show();
            if (TryGetComponent(out ItemNet net)) net.Share();
        }

        // ---- Saving ----

        [Serializable]
        class State
        {
            public string dish;
            public float food;
            public Color color;
        }

        public string Capture() => JsonUtility.ToJson(new State { dish = Dish ?? "", food = Food, color = color });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Dish = string.IsNullOrEmpty(state.dish) ? null : state.dish;
            Food = state.food;
            color = state.color;
            Show();
        }
    }
}
