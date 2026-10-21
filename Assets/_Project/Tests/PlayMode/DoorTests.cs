using System.Collections;
using GrandmasHouse.Kitchen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Doors, gates and lids swing open where you can see them.
    public class DoorTests
    {
        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [UnityTest]
        public IEnumerator EveryDoorSwingsWhereYouCanSeeIt()
        {
            foreach (var door in Object.FindObjectsByType<HingedDoor>())
            {
                var parts = door.GetComponentsInChildren<Renderer>();
                Assert.IsNotEmpty(parts, door.name + " has nothing to see.");
                foreach (var r in parts)
                {
                    // A static part is baked in place and never moves with its hinge.
                    Assert.IsFalse(r.isPartOfStaticBatch, door.name + "/" + r.name + " is static, so it won't swing.");
#if UNITY_EDITOR
                    Assert.IsFalse(r.gameObject.isStatic, door.name + "/" + r.name + " is marked static, so it won't swing.");
#endif
                }

                door.Set(false);
                yield return new WaitForSeconds(0.5f);
                Vector3 shut = parts[0].bounds.center;
                door.Set(true);
                yield return new WaitForSeconds(0.5f);
                Assert.Greater(Vector3.Distance(shut, parts[0].bounds.center), 0.05f, door.name + " should swing open.");
                Assert.IsTrue(parts[0].enabled && parts[0].gameObject.activeInHierarchy, door.name + " should still show when open.");
            }
        }
    }
}
