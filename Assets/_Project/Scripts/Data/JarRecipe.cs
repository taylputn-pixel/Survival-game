using System.Collections.Generic;

namespace GrandmasHouse.Data
{
    // Everything about a jar's insides that decides what it is.
    public struct JarFacts
    {
        // How much of each kind: slices and wedges by count, salsa by share
        // of the jar ("Tomato", "Onion", "Jalapeño").
        public IDictionary<string, float> amounts;
        public int slices;
        public int cloves;
        public int wedges;
        public bool salsa;
        public bool dill;
        public bool lemon;
        // Poured from a pot that wasn't salted, vinegared and boiled.
        public bool badBatch;
        // 0 is empty, 1 is right to the rim.
        public float brine;
    }

    // What a jar is being put up as. What fills it picks the family (slices,
    // cloves, wedges, or something ladled from the pot); then the first
    // recipe of that family whose ingredients it has, in the order the
    // recipes are listed, so the real recipes come before plainer ones.
    // Anything else is the inedible one. Nothing in it at all is null.
    public static class JarRecipe
    {
        public static RecipeData Resolve(IList<RecipeData> recipes, RecipeData inedible, JarFacts jar)
        {
            RecipeData.Fill fill;
            if (jar.salsa) fill = RecipeData.Fill.Salsa;
            else if (jar.wedges > 0) fill = RecipeData.Fill.Wedges;
            else if (jar.slices > 0) fill = RecipeData.Fill.Slices;
            else if (jar.cloves > 0) fill = RecipeData.Fill.Cloves;
            else return null;

            var counted = jar.amounts != null ? new Dictionary<string, float>(jar.amounts) : new Dictionary<string, float>();
            // Extras with the slices (garlic, dill) don't count toward what it mostly is.
            float main = 0f;
            foreach (var kind in counted)
            {
                if (fill != RecipeData.Fill.Slices || kind.Key != "Clove") main += kind.Value;
            }
            if (jar.dill) counted["Dill"] = 1f;
            if (jar.lemon) counted["Lemon"] = 1f;
            if (jar.badBatch)
            {
                return inedible;
            }
            foreach (var recipe in recipes)
            {
                if (recipe != null && recipe.fill == fill && recipe.Matches(counted, main))
                {
                    // Pickles need their brine up over them.
                    return recipe.usesBrine && jar.brine < recipe.brineEnough ? inedible : recipe;
                }
            }
            return inedible;
        }
    }
}
