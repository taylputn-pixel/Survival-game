using System.Collections.Generic;
using UnityEngine;

namespace GrandmasHouse.Data
{
    // A fair's ribbon for a jar: blue for one made just as the card says,
    // red for one thing off, white for two, and none for more.
    public enum Ribbon { None, White, Red, Blue }

    // How a jar was made, for judging it against its recipe.
    public struct JarMakings
    {
        // How packed it is (1 is full), and how high the brine or salsa is
        // (1 is right to the rim).
        public float fullness;
        public float level;
        public int cloves;
        public int peppercorns;
        // The brine poured over it: share that's vinegar, and scoops of salt
        // for a full pot's worth. Negative when no brine went in.
        public float vinegarShare;
        public float saltPerPot;
    }

    // Judges a jar the way a county fair would. Each thing that isn't as the
    // recipe card says is one mark against it: not packed full, the brine
    // (or salsa) not up to the line, brine too weak or too sharp, too little
    // or too much salt, short on garlic, no peppercorns. What a jar is (dill
    // pickles need dill, tomatoes need lemon) is JarRecipe's business; this
    // only says how well it was made.
    public static class JarQuality
    {
        public static Ribbon Grade(RecipeData recipe, JarMakings jar, List<string> marks = null)
        {
            if (recipe == null) return Ribbon.None;
            int count = 0;
            void Mark(string why)
            {
                count++;
                marks?.Add(why);
            }

            bool packed = recipe.fill != RecipeData.Fill.Salsa;
            if (packed && jar.fullness < recipe.packedEnough) Mark("not packed full");

            // Brine, tomato juice or salsa, up to the line under the lid.
            if (jar.level < recipe.brineLine - recipe.lineTolerance) Mark("too much headspace");
            else if (jar.level > recipe.brineLine + recipe.lineTolerance) Mark("filled too high");

            if (recipe.usesBrine && jar.vinegarShare >= 0f)
            {
                if (jar.vinegarShare < recipe.vinegarShare - recipe.vinegarTolerance) Mark("weak brine");
                else if (jar.vinegarShare > recipe.vinegarShare + recipe.vinegarTolerance) Mark("sharp brine");
            }
            if ((recipe.usesBrine || recipe.fill == RecipeData.Fill.Salsa) && jar.saltPerPot >= 0f)
            {
                if (jar.saltPerPot < recipe.saltScoops - recipe.saltTolerance) Mark("needs salt");
                else if (jar.saltPerPot > recipe.saltScoops + recipe.saltTolerance) Mark("too salty");
            }

            if (recipe.fill == RecipeData.Fill.Slices && recipe.garlicPerJar > 0 && jar.cloves < recipe.garlicPerJar) Mark("short on garlic");
            if (recipe.peppercornPinches > 0 && jar.peppercorns <= 0) Mark("no peppercorns");

            return FromMarks(count);
        }

        // What a mark means for next time, as you'd say it tasting the jar.
        public static string Advice(string mark)
        {
            switch (mark)
            {
                case "not packed full": return "Pack the jars fuller.";
                case "too much headspace": return "Fill it up to the line.";
                case "filled too high": return "Only fill to the line; leave room under the lid.";
                case "weak brine": return "Needs more vinegar.";
                case "sharp brine": return "Too sharp; less vinegar, more water.";
                case "needs salt": return "Needs more salt.";
                case "too salty": return "Too salty; go easier on the salt.";
                case "short on garlic": return "Needs more garlic.";
                case "no peppercorns": return "Needs peppercorns.";
                default: return mark;
            }
        }

        // A tasting note: what it was, its ribbon, and what it needs.
        public static string Tasting(string recipe, Ribbon ribbon, IList<string> marks)
        {
            string ribbonName = Name(ribbon);
            string verdict = ribbonName != null ? recipe + ": a " + ribbonName + " jar." : recipe + ": no ribbon for this one.";
            if (marks == null || marks.Count == 0) return verdict + " Just right.";
            var advice = new List<string>();
            foreach (var mark in marks) advice.Add(Advice(mark));
            return verdict + " " + string.Join(" ", advice);
        }

        public static Ribbon FromMarks(int marks) =>
            marks <= 0 ? Ribbon.Blue : marks == 1 ? Ribbon.Red : marks == 2 ? Ribbon.White : Ribbon.None;

        public static string Name(Ribbon ribbon) =>
            ribbon == Ribbon.Blue ? "blue ribbon" : ribbon == Ribbon.Red ? "red ribbon" : ribbon == Ribbon.White ? "white ribbon" : null;

        public static Color Color(Ribbon ribbon) =>
            ribbon == Ribbon.Blue ? new Color(0.16f, 0.3f, 0.72f) : ribbon == Ribbon.Red ? new Color(0.74f, 0.13f, 0.13f) : new Color(0.93f, 0.92f, 0.88f);
    }
}
