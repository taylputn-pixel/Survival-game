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

        public static Ribbon FromMarks(int marks) =>
            marks <= 0 ? Ribbon.Blue : marks == 1 ? Ribbon.Red : marks == 2 ? Ribbon.White : Ribbon.None;

        public static string Name(Ribbon ribbon) =>
            ribbon == Ribbon.Blue ? "blue ribbon" : ribbon == Ribbon.Red ? "red ribbon" : ribbon == Ribbon.White ? "white ribbon" : null;

        public static Color Color(Ribbon ribbon) =>
            ribbon == Ribbon.Blue ? new Color(0.16f, 0.3f, 0.72f) : ribbon == Ribbon.Red ? new Color(0.74f, 0.13f, 0.13f) : new Color(0.93f, 0.92f, 0.88f);
    }
}
