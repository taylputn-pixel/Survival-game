using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Net;
using UnityEngine;

namespace GrandmasHouse.Food
{
    // Eating what's in a jar, all of it, which leaves the jar empty in your
    // hand. A sealed jar keeps; once it's open (or never sealed) it spoils
    // like fresh food. A jar that isn't one of Grandma's recipes, or was
    // made from a batch done wrong, makes you sick.
    [RequireComponent(typeof(JarContents))]
    public class JarFood : MonoBehaviour, IEdible
    {
        JarContents jar;
        Perishable perishable;

        void Awake()
        {
            jar = GetComponent<JarContents>();
            perishable = GetComponent<Perishable>();
            if (perishable != null)
            {
                perishable.CanSpoil = () => jar.HasContents && !jar.IsSealed;
            }
        }

        public bool CanEat => jar.HasContents;

        // A sealed jar of one of Grandma's recipes, eaten: how it was made
        // shows in the taste. Said on screen, and kept as a tasting note in
        // the journal for next time.
        void Taste()
        {
            RecipeData recipe = jar.Recipe;
            if (recipe == null || jar.IsInedible || jar.BadBatch || !jar.IsSealed) return;
            var marks = new List<string>();
            Ribbon ribbon = JarQuality.Grade(recipe, jar.Makings, marks);
            var caption = FindAnyObjectByType<UI.Caption>();
            if (caption != null) caption.Show(JarQuality.Tasting(recipe.displayName, ribbon, marks), 7f);
            var journal = FindAnyObjectByType<Interaction.Discoveries>();
            if (journal != null) journal.Taste(recipe.displayName, ribbon, marks);
        }

        // How much food is in it: the salsa's level, or how packed it is.
        float Share => jar.HasSalsa ? jar.BrineLevel : Mathf.Min(1f, jar.Fullness);

        public float Nutrition => jar.Recipe != null ? jar.Recipe.nutrition * Share : 0f;

        public float SickHours(SurvivalSettings settings)
        {
            if (jar.IsInedible || jar.BadBatch)
            {
                return settings.unsafeJarSickHours;
            }
            return perishable != null && perishable.IsRotten ? settings.spoiledSickHours : 0f;
        }

        public void Eaten(PlayerHands hands)
        {
            Taste();
            jar.Empty();
            if (TryGetComponent(out ItemNet net) && net.Live)
            {
                net.Share();
            }
        }
    }
}
