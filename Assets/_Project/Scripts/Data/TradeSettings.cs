using System;
using System.Collections.Generic;
using GrandmasHouse.Animals;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using GrandmasHouse.Garden;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Data
{
    // What things are worth in trade, and how that shifts with the seasons.
    // Worth is a plain number with no money behind it: the store and the
    // neighbors weigh your goods against theirs with it.
    [CreateAssetMenu(menuName = "Grandma's House/Trade Settings")]
    public class TradeSettings : ScriptableObject
    {
        [Serializable]
        public class Worth
        {
            public string key;
            public float worth = 1f;
            [Tooltip("Times its worth in each month of the calendar, in order (eggs are dear in winter). Empty is the same all year.")]
            public float[] byMonth = new float[0];
        }

        [Serializable]
        public class JarWorth
        {
            [Tooltip("The recipe's display name, like \"Dill Pickle Chips\".")]
            public string recipe;
            public float worth = 6f;
        }

        [Tooltip("Worth of one of each item, by save key.")]
        public List<Worth> items = new List<Worth>();
        [Tooltip("A sealed jar of one of Grandma's recipes.")]
        public List<JarWorth> jars = new List<JarWorth>();
        public float sealedJarDefault = 5f;
        [Tooltip("An empty, clean jar.")]
        public float emptyJar = 0.5f;
        [Tooltip("Dried, smoked or salted food is worth this many times as much.")]
        public float curedBonus = 1.5f;
        [Tooltip("Food that's turned (past half fresh) is worth this share.")]
        [Range(0f, 1f)] public float turnedShare = 0.4f;
        [Tooltip("The store gives this share of an item's worth when it takes your goods.")]
        [Range(0f, 1f)] public float storeTakes = 0.6f;
        [Tooltip("Neighbors ask this many times an item's worth for their own things.")]
        public float neighborAsks = 1.2f;

        public float BaseWorth(string key, int month)
        {
            foreach (var w in items)
            {
                if (w.key != key) continue;
                float season = w.byMonth != null && month >= 0 && month < w.byMonth.Length ? w.byMonth[month] : 1f;
                return w.worth * season;
            }
            return 0f;
        }

        // One item as it is now: how full, how fresh, whether it's a good jar.
        public float WorthOf(Pickup item, int month)
        {
            if (item == null || !item.TryGetComponent(out SavedItem saved)) return 0f;
            if (item.TryGetComponent(out JarContents jar))
            {
                if (jar.IsSealed && jar.Recipe != null && !jar.IsInedible)
                {
                    foreach (var j in jars) if (j.recipe == jar.Recipe.displayName) return j.worth;
                    return sealedJarDefault;
                }
                return jar.Contents == null && !jar.HasLid ? emptyJar : 0f;
            }
            float worth = BaseWorth(saved.Key, month);
            if (worth <= 0f) return 0f;
            if (item.TryGetComponent(out Perishable food))
            {
                if (food.IsRotten) return 0f;
                if (food.Cured) worth *= curedBonus;
                else if (food.Freshness < 0.5f) worth *= turnedShare;
            }
            if (item.TryGetComponent(out Seasoning seasoning)) worth *= seasoning.Capacity > 0f ? seasoning.Left / seasoning.Capacity : 0f;
            if (item.TryGetComponent(out FeedSack sack)) worth *= sack.Scoops / (float)Mathf.Max(1, sack.Capacity);
            if (item.TryGetComponent(out SeedPacket packet)) worth *= packet.Seeds / (float)Mathf.Max(1, packet.Capacity);
            return worth;
        }
    }
}
