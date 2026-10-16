using UnityEngine;

namespace GrandmasHouse.Data
{
    // One thing to put up in jars: what goes in, how full, and how long it
    // processes. The card text is what Grandma wrote (or a stand-in until
    // the family recipe is added).
    [CreateAssetMenu(fileName = "Recipe", menuName = "Grandma's House/Recipe")]
    public class RecipeData : ScriptableObject
    {
        // What fills the jar, which is how a packed jar knows what it is.
        public enum Fill { Slices, Cloves, Wedges, Salsa }

        public string displayName = "Dill Pickle Chips";
        public Fill fill = Fill.Slices;
        [Tooltip("The label put on a sealed jar of this.")]
        public Material label;

        [Header("Packing a jar")]
        [Tooltip("Slices that fill one jar snugly.")]
        public int slicesPerJar = 20;
        [Tooltip("Tomato wedges that fill one jar, pressed down (four to a tomato).")]
        public int wedgesPerJar = 12;
        [Tooltip("A head of dill goes in each jar.")]
        public bool needsDill = true;
        [Tooltip("Peeled cloves that fill one jar packed with garlic alone. Write {cloves} in card or book text to show this number.")]
        public int clovesPerJar = 20;
        [Tooltip("Garlic cloves the recipe calls for in each jar (more is fine, just extra garlic).")]
        public int garlicPerJar = 2;
        [Tooltip("Pinches of peppercorns in each jar.")]
        public int peppercornPinches = 0;
        [Tooltip("How high the brine should come, as a share of the jar (the rest is headspace under the lid).")]
        [Range(0f, 1f)] public float brineLine = 0.88f;
        [Tooltip("Brine at least this high counts as filled and ready to can.")]
        [Range(0f, 1f)] public float brineEnough = 0.75f;
        [Tooltip("Brine is poured over what's packed. Off for tomatoes packed raw, which make their own juice as they're pressed in.")]
        public bool usesBrine = true;
        [Tooltip("A spoonful of lemon juice goes in each jar before the lid.")]
        public bool needsLemon;
        [Tooltip("What the liquid in the jar looks like (brine if empty).")]
        public Material liquid;

        [Header("Brine")]
        [Tooltip("Share of the brine that's vinegar; the rest is water.")]
        [Range(0f, 1f)] public float vinegarShare = 0.5f;
        [Tooltip("Scoops of salt for a pot of brine.")]
        public int saltScoops = 2;
        [Tooltip("Seconds on a lit burner to bring a full pot to a boil.")]
        public float heatSeconds = 8f;
        [Tooltip("How much of a full pot one pint of brine uses.")]
        [Range(0f, 1f)] public float potPerJar = 0.2f;

        [Header("Cooked in the pot (salsa)")]
        [Tooltip("Chopped pieces (tomato wedges, onion and pepper rings, cloves) that fill the pot.")]
        public int piecesPerPot = 44;
        [Tooltip("What the recipe calls for in a full pot, for the card and for tuning.")]
        public int tomatoesPerPot = 5;
        public int onionsPerPot = 1;
        public int peppersPerPot = 2;

        [Header("Canning")]
        [Tooltip("Minutes in the boiling water bath.")]
        public float processMinutes = 10f;

        [Header("Recipe card")]
        [TextArea(6, 20)] public string cardText;

        // Card or book text with this recipe's numbers filled in, so the
        // words always match how the game plays.
        public string FillIn(string text) => text?.Replace("{cloves}", clovesPerJar.ToString());
    }
}
