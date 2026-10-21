using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Saving;
using GrandmasHouse.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // After dark the lamps come on and the truck's headlights with someone in
    // it; in winter snow falls outdoors and piles up on the ground.
    public class NightAndSnowTests
    {
        PlayerHands hands;
        DayClock clock;
        Weather weather;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            hands = Homestead.Find<PlayerHands>();
            clock = Homestead.Find<DayClock>();
            weather = Homestead.Find<Weather>();
        }

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        IEnumerator At(int day, float hour)
        {
            clock.Restore("{\"day\":" + day + ",\"hour\":" + hour + "}");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (mb is IAfterLoad a) a.AfterLoad();
            yield return null;
            yield return null;
        }

        void Stand(Vector3 at)
        {
            var body = hands.GetComponent<CharacterController>();
            body.enabled = false;
            hands.transform.position = at;
            body.enabled = true;
        }

        // A winter day this game's weather snows on (and one it has snow lying).
        int FirstWinterDay(System.Func<int, bool> wanted)
        {
            int start = 1;
            for (int m = 0; m < 3; m++) start += weather.Calendar.months[m].days;
            for (int d = start; d < start + 60; d++) if (wanted(d)) return d;
            Assert.Inconclusive("No such day in this game's weather.");
            return 0;
        }

        [UnityTest]
        public IEnumerator HeadlightsComeOnWithADriverAfterDark()
        {
            var truck = Homestead.Find<Truck>();
            var lights = truck.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(2, lights.Length, "The truck should have two headlights.");

            yield return At(1, 12f);
            Stand(truck.transform.position + truck.transform.right * -2f);
            truck.SitIn(hands);
            yield return null;
            foreach (var l in lights) Assert.IsFalse(l.enabled, "No headlights at noon.");

            yield return At(1, 23f);
            yield return null;
            foreach (var l in lights) Assert.IsTrue(l.enabled, "Headlights on at night with someone driving.");

            truck.Leave(hands);
            yield return null;
            foreach (var l in lights) Assert.IsFalse(l.enabled, "Off once you get out.");
        }

        [UnityTest]
        public IEnumerator ThePorchLightIsOnAtNightOnly()
        {
            var porch = GameObject.Find("House/Porch Light").GetComponent<Light>();
            yield return At(1, 12f);
            Assert.IsFalse(porch.enabled);
            yield return At(1, 23f);
            Assert.IsTrue(porch.enabled);
        }

        [UnityTest]
        public IEnumerator SnowFallsOutsideButNotIndoors()
        {
            int day = FirstWinterDay(d => weather.Model.DayOf(d).snow);
            var snow = Homestead.Find<Snowfall>().GetComponent<ParticleSystem>();
            Stand(new Vector3(1.7f, 0.1f, -6f));
            yield return At(day, 12f);
            yield return new WaitForSeconds(0.7f);
            Assert.Greater(snow.emission.rateOverTime.constant, 0f, "Snow should be falling in the yard.");

            Stand(new Vector3(2.25f, 0.1f, 3f));
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(0f, snow.emission.rateOverTime.constant, "No snow in the kitchen.");
        }

        [UnityTest]
        public IEnumerator SnowWhitensTheGroundAndMeltsInSpring()
        {
            int day = FirstWinterDay(d => weather.Model.SnowCover(d) > 0.4f);
            var cover = Homestead.Find<SnowCover>();
            var ground = GameObject.Find("House/Ground South").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();

            yield return At(day, 12f);
            yield return new WaitForSeconds(2.2f);
            ground.GetPropertyBlock(block);
            Color white = block.GetColor("_BaseColor");
            Assert.Greater(cover.Cover, 0.3f);
            Assert.Greater(white.r, 0.55f, "The grass should be paler under snow (bare is 0.45).");

            yield return At(weather.Calendar.DaysInYear + weather.Calendar.months[0].days, 12f);
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(0f, cover.Cover, 0.001f, "Melted by spring.");
        }
    }
}
