using System.Collections;
using GrandmasHouse.Stations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // How produce is cut: rounds for cucumbers and carrots, cubes for
    // potatoes and squash, a small dice for onions; cubes heap up small.
    public class CuttingTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [TestCase("Potato", "Potato Cube")]
        [TestCase("WinterSquash", "Winter Squash Cube")]
        [TestCase("Onion", "Onion Cube")]
        [TestCase("Cucumber", "Cucumber Slice")]
        [TestCase("Carrot", "Carrot Slice")]
        public void EachIsCutItsOwnWay(string key, string piece)
        {
            var produce = Homestead.Make<Sliceable>(key);
            string name = produce.GetComponent<Carrying.Pickup>().DisplayName;
            Assert.AreEqual(piece, produce.PieceName(name));
            Assert.AreEqual(key, Data.RecipeData.KindOf(piece) == "Winter" ? "WinterSquash" : Data.RecipeData.KindOf(piece), "Still counts as its kind.");
            Object.Destroy(produce.gameObject);
        }

        [UnityTest]
        public IEnumerator CubesHeapUpSmall()
        {
            var board = Homestead.Find<CuttingBoardStation>();
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 12; i++) names.Add("Potato Cube");
            board.Restore(JsonUtility.ToJson(new Saved { slices = 12, sliceNames = names }));
            yield return null;
            Assert.AreEqual(12, board.SliceCount);
            Bounds heap = new Bounds();
            bool first = true;
            foreach (var r in board.GetComponentsInChildren<MeshFilter>())
            {
                if (r.name != "Potato Cube") continue;
                Assert.AreEqual("Cube", r.sharedMesh.name, "A potato is cubed, not sliced in rounds.");
                var b = r.GetComponent<Renderer>().bounds;
                if (first) { heap = b; first = false; } else heap.Encapsulate(b);
            }
            Assert.Less(Mathf.Max(heap.size.x, heap.size.z), 0.09f, "Twelve cubes heap in a small mound.");
        }

        [System.Serializable]
        class Saved
        {
            public int slices;
            public System.Collections.Generic.List<string> sliceNames;
        }
    }
}
