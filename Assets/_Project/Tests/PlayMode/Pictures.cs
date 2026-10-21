using System.Collections;
using System.IO;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Saving;
using GrandmasHouse.UI;
using GrandmasHouse.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Not a test: takes pictures of the opening, spring, night and snow for a
    // look by eye. Run on its own by name; it writes to GH_SHOTS (a folder).
    [Explicit]
    public class Pictures
    {
        string folder;
        DayClock clock;
        Weather weather;

        [TearDown]
        public void PutSavesBack()
        {
            SaveSlot.NewGame = false;
            Homestead.RestoreRealSaves();
        }

        IEnumerator At(int day, float hour)
        {
            clock.Restore("{\"day\":" + day + ",\"hour\":" + hour + "}");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (mb is IAfterLoad a) a.AfterLoad();
            yield return new WaitForSeconds(2.3f);
        }

        // Renders a view (and any overlay canvases, moved onto this camera) to a PNG.
        void Shoot(string name, Vector3 from, Vector3 lookAt, bool withCards = false)
        {
            var go = new GameObject("Shot");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            go.transform.position = from;
            go.transform.LookAt(lookAt);
            var target = new RenderTexture(1280, 720, 24);
            cam.targetTexture = target;
            var moved = new System.Collections.Generic.List<Canvas>();
            if (withCards)
            {
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isRootCanvas) continue;
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = 0.2f;
                    moved.Add(c);
                }
                Canvas.ForceUpdateCanvases();
            }
            cam.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            foreach (var c in moved) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(go);
            Object.Destroy(target);
        }

        IEnumerator Load(bool newGame)
        {
            folder = System.Environment.GetEnvironmentVariable("GH_SHOTS");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("Set GH_SHOTS to a folder.");
            Directory.CreateDirectory(folder);
            Homestead.UseMemorySaves();
            SaveSlot.NewGame = newGame;
            yield return Homestead.Load();
            clock = Homestead.Find<DayClock>();
            weather = Homestead.Find<Weather>();
        }

        [UnityTest]
        public IEnumerator TakePictures()
        {
            yield return Load(newGame: true);
            var eyes = Homestead.Find<PlayerHands>().ViewPoint;
            yield return new WaitForSeconds(3f);
            Shoot("1-opening", eyes.position, eyes.position + eyes.forward, withCards: true);

            // Spring after the first winter.
            var tally = Homestead.Find<YearTally>();
            YearTally.Add(YearTally.Count.Jar, 14);
            YearTally.Add(YearTally.Count.Firewood, 36);
            yield return new WaitForSeconds(16f);
            clock.Restore("{\"day\":" + weather.Calendar.DaysInYear + ",\"hour\":23.5}");
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if (mb is IAfterLoad a) a.AfterLoad();
            clock.SleepUntilMorning();
            yield return new WaitForSeconds(2.5f);
            Shoot("2-spring", eyes.position, eyes.position + eyes.forward, withCards: true);
            yield return new WaitForSeconds(12f);

            // Night: the house, the porch light, and the truck's headlights.
            var truck = Homestead.Find<Truck>();
            var hands = Homestead.Find<PlayerHands>();
            yield return At(2, 23f);
            truck.SitIn(hands);
            yield return null;
            Vector3 front = truck.transform.position + truck.transform.forward * 9f + Vector3.up * 2.5f + truck.transform.right * 3f;
            Shoot("3-night-truck", front, truck.transform.position + Vector3.up);
            Shoot("4-night-house", new Vector3(1.7f, 2.5f, -14f), new Vector3(1.7f, 2f, 0f));
            truck.Leave(hands);

            // Winter: snow on the ground, then falling.
            int start = 1;
            for (int m = 0; m < 3; m++) start += weather.Calendar.months[m].days;
            int white = start, falling = start;
            for (int d = start + weather.Calendar.months[3].days - 1; d >= start; d--) if (weather.Model.SnowCover(d) > weather.Model.SnowCover(white)) white = d;
            for (int d = start; d < start + weather.Calendar.months[3].days; d++) if (weather.Model.DayOf(d).snow) { falling = d; break; }
            yield return At(white, 13f);
            Shoot("5-snow-cover", new Vector3(1.7f, 3f, -16f), new Vector3(2f, 1f, 3f));
            var body = hands.GetComponent<CharacterController>();
            body.enabled = false;
            hands.transform.position = new Vector3(1.7f, 0.1f, -8f);
            body.enabled = true;
            yield return At(falling, 13f);
            yield return new WaitForSeconds(6f);
            Shoot("6-snowing", eyes.position, eyes.position + hands.transform.forward + Vector3.up * 0.1f);
            Debug.Log("[shots] cover " + weather.Model.SnowCover(white) + " day " + white + ", snowing day " + falling + " " + weather.Model.DayOf(falling).snow);
        }
    }
}
