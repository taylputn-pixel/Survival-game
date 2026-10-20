using System.Collections;
using GrandmasHouse.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Proves play mode tests run and can see the game code.
    public class PlayModeSetupTests
    {
        [UnityTest]
        public IEnumerator FramesAdvance()
        {
            var crop = ScriptableObject.CreateInstance<CropData>();
            int frame = Time.frameCount;
            yield return null;
            Assert.Greater(Time.frameCount, frame);
            Object.Destroy(crop);
        }
    }
}
