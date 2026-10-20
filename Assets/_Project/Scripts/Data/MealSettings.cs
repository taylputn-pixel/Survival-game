using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrandmasHouse.Data
{
    // What a pot of soup or stew is worth. Cooked vegetables feed far more
    // than the same vegetables eaten raw, a mix of kinds feeds more again,
    // and salt helps. A pot that never boiled is worth less.
    [CreateAssetMenu(fileName = "MealSettings", menuName = "Grandma's House/Meal Settings")]
    public class MealSettings : ScriptableObject
    {
        [Serializable]
        public struct PieceFood
        {
            [Tooltip("The first word of the piece's name: Potato, Carrot, Winter (squash), Onion, Tomato.")]
            public string kind;
            [Tooltip("Hunger one cooked piece fills, out of 100.")]
            public float food;
        }

        public List<PieceFood> pieces = new List<PieceFood>
        {
            new PieceFood { kind = "Potato", food = 2f },
            new PieceFood { kind = "Egg", food = 6f },
            new PieceFood { kind = "Winter", food = 1.5f },
            new PieceFood { kind = "Carrot", food = 1f },
            new PieceFood { kind = "Tomato", food = 2f },
            new PieceFood { kind = "Onion", food = 0.6f },
            new PieceFood { kind = "Cucumber", food = 0.4f },
            new PieceFood { kind = "Jalapeño", food = 0.2f },
            new PieceFood { kind = "Clove", food = 0.1f },
        };
        [Tooltip("Food per piece of anything not listed.")]
        public float otherPieceFood = 0.3f;
        [Tooltip("A pot that never came to a boil feeds this share of a cooked one.")]
        [Range(0f, 1f)] public float uncookedShare = 0.5f;
        [Tooltip("Extra hunger filled for each kind of vegetable past the first (up to four kinds).")]
        public float varietyBonus = 3f;
        [Tooltip("Salted soup feeds this much more (0.1 = 10%).")]
        public float saltBonus = 0.1f;
        [Tooltip("How much of a full pot one bowl holds.")]
        [Range(0.05f, 1f)] public float bowlShare = 0.25f;

        public float FoodPerPiece(string kind)
        {
            foreach (var piece in pieces)
            {
                if (piece.kind == kind) return piece.food;
            }
            return otherPieceFood;
        }

        // A whole pot: its pieces by kind, whether it boiled, whether it's salted.
        public float PotFood(IDictionary<string, int> counts, bool cooked, bool salted)
        {
            float food = 0f;
            int kinds = 0;
            foreach (var pair in counts)
            {
                if (pair.Value <= 0) continue;
                food += FoodPerPiece(pair.Key) * pair.Value;
                kinds++;
            }
            if (food <= 0f)
            {
                return 0f;
            }
            food += varietyBonus * Mathf.Clamp(kinds - 1, 0, 3);
            if (salted) food *= 1f + saltBonus;
            if (!cooked) food *= uncookedShare;
            return food;
        }

        // What the cook would call it.
        public static string DishName(IDictionary<string, int> counts, bool cooked)
        {
            bool Has(string kind) => counts.TryGetValue(kind, out int n) && n > 0;
            int kinds = 0;
            foreach (var pair in counts) if (pair.Value > 0) kinds++;
            string name =
                Has("Potato") && Has("Onion") && !Has("Tomato") && kinds <= 3 ? "Potato soup"
                : Has("Tomato") && kinds >= 3 ? "Garden stew"
                : (Has("Potato") || Has("Carrot")) && !Has("Tomato") ? "Root vegetable soup"
                : Has("Winter") && kinds == 1 ? "Squash soup"
                : Has("Egg") && kinds == 1 ? "Boiled eggs"
                : "Vegetable soup";
            return cooked ? name : "Uncooked " + name.ToLowerInvariant();
        }
    }
}
