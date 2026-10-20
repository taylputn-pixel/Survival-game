using GrandmasHouse.Data;
using NUnit.Framework;
using UnityEngine;

namespace GrandmasHouse.Tests
{
    // Proves the test assembly can see the game code. Real tests live beside this.
    public class SetupTests
    {
        [Test]
        public void GameCodeIsReachable()
        {
            var crop = ScriptableObject.CreateInstance<CropData>();
            Assert.IsNotNull(crop);
            Object.DestroyImmediate(crop);
        }
    }
}
