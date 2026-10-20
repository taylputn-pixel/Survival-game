using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    // Checks on the project's assets, so a forgotten catalog entry or label
    // fails here instead of losing something from a save. Each test lists
    // every problem it finds, not just the first.
    public class DataChecks
    {
        const string Project = "Assets/_Project";

        SaveCatalog catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<SaveCatalog>(Project + "/Data/SaveCatalog.asset");
            Assert.IsNotNull(catalog, "The save catalog has moved.");
        }

        static IEnumerable<T> All<T>(string filter) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { Project }))
            {
                yield return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        static IEnumerable<GameObject> Prefabs() => All<GameObject>("t:Prefab");

        static void Report(List<string> problems)
        {
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void CatalogKeysAreUniqueAndFilledIn()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (var entry in catalog.items)
            {
                if (string.IsNullOrEmpty(entry.key)) problems.Add("An item entry has no key.");
                else if (!seen.Add(entry.key)) problems.Add("Key listed twice: " + entry.key);
                if (entry.prefab == null) problems.Add("No prefab for key: " + entry.key);
            }
            Report(problems);
        }

        // Old keys (BrinePot from before the rename) may point at a prefab
        // too, so older saves still load; its own key must be there as well.
        [Test]
        public void EverySavedPrefabIsInTheCatalogUnderItsOwnKey()
        {
            var problems = new List<string>();
            foreach (var prefab in Prefabs())
            {
                var saved = prefab.GetComponent<SavedItem>();
                if (saved == null) continue;
                if (string.IsNullOrEmpty(saved.Key)) problems.Add(prefab.name + " has a SavedItem with no key.");
                else if (catalog.Item(saved.Key) != saved) problems.Add(prefab.name + " isn't in the catalog as " + saved.Key + ".");
            }
            Report(problems);
        }

        [Test]
        public void EveryPickupPrefabIsSaved()
        {
            var problems = new List<string>();
            foreach (var prefab in Prefabs())
            {
                if (prefab.GetComponent<Pickup>() != null && prefab.GetComponent<SavedItem>() == null)
                {
                    problems.Add(prefab.name + " can be picked up but has no SavedItem.");
                }
            }
            Report(problems);
        }

        [Test]
        public void EveryCropIsInTheCatalogOnce()
        {
            var problems = new List<string>();
            var names = new HashSet<string>();
            foreach (var crop in catalog.crops)
            {
                if (crop == null) problems.Add("An empty crop slot.");
                else if (!names.Add(crop.name)) problems.Add("Crop named twice: " + crop.name);
            }
            foreach (var crop in All<CropData>("t:CropData"))
            {
                if (System.Array.IndexOf(catalog.crops, crop) < 0) problems.Add(crop.name + " isn't in the catalog.");
            }
            Report(problems);
        }

        [Test]
        public void EveryNoteIsInTheCatalogOnce()
        {
            var problems = new List<string>();
            var names = new HashSet<string>();
            foreach (var note in catalog.notes)
            {
                if (note == null) problems.Add("An empty note slot.");
                else if (!names.Add(note.name)) problems.Add("Note named twice: " + note.name);
            }
            foreach (var note in All<NoteData>("t:NoteData"))
            {
                if (System.Array.IndexOf(catalog.notes, note) < 0) problems.Add(note.name + " isn't in the catalog.");
            }
            Report(problems);
        }

        [Test]
        public void EveryCropHasStagesToGrowThrough()
        {
            var problems = new List<string>();
            foreach (var crop in All<CropData>("t:CropData"))
            {
                if (crop.stages == null || crop.stages.Length == 0) problems.Add(crop.name + " has no growth stages.");
                else if (System.Array.IndexOf(crop.stages, null) >= 0) problems.Add(crop.name + " has an empty growth stage.");
            }
            Report(problems);
        }

        [Test]
        public void EveryJarRecipeHasANameAndALabel()
        {
            var problems = new List<string>();
            foreach (var prefab in Prefabs())
            {
                var jar = prefab.GetComponent<JarContents>();
                if (jar == null) continue;
                var contents = new SerializedObject(jar);
                var recipes = contents.FindProperty("recipes");
                var all = new List<Object> { contents.FindProperty("inedible").objectReferenceValue };
                for (int i = 0; i < recipes.arraySize; i++) all.Add(recipes.GetArrayElementAtIndex(i).objectReferenceValue);
                foreach (var item in all)
                {
                    var recipe = item as RecipeData;
                    if (recipe == null) { problems.Add(prefab.name + " has an empty recipe slot."); continue; }
                    if (string.IsNullOrEmpty(recipe.displayName)) problems.Add(recipe.name + " has no name.");
                    if (recipe.label == null) problems.Add(recipe.name + " has no label.");
                }
            }
            Report(problems);
        }
    }
}
