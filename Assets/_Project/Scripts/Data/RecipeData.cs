using UnityEngine;

namespace GrandmasHouse.Data
{
    // One thing to put up in jars: what goes in, how full, and how long it
    // processes. The card text is what Grandma wrote (or a stand-in until
    // the family recipe is added).
    [CreateAssetMenu(fileName = "Recipe", menuName = "Grandma's House/Recipe")]
    public class RecipeData : ScriptableObject
    {
        public string displayName = "Dill Pickle Chips";

        [Header("Packing a jar")]
        [Tooltip("Slices that fill one jar snugly.")]
        public int slicesPerJar = 20;
        [Tooltip("A head of dill goes in each jar.")]
        public bool needsDill = true;
        [Tooltip("How high the brine should come, as a share of the jar (the rest is headspace under the lid).")]
        [Range(0f, 1f)] public float brineLine = 0.88f;
        [Tooltip("Brine at least this high counts as filled and ready to can.")]
        [Range(0f, 1f)] public float brineEnough = 0.75f;

        [Header("Canning")]
        [Tooltip("Minutes in the boiling water bath.")]
        public float processMinutes = 10f;

        [Header("Recipe card")]
        [TextArea(6, 20)] public string cardText;
    }
}
