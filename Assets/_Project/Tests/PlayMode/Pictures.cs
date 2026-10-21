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

        // The containers around the homestead, a labeled jar up close, and the timer.
        [UnityTest]
        public IEnumerator TakeKitchenPictures()
        {
            yield return Load(newGame: false);
            yield return At(1, 11f);
            Shoot("7-crates", new Vector3(4.55f, 1.25f, 3.7f), new Vector3(5.0f, 0.1f, 5.25f));
            Shoot("8-sacks", new Vector3(1.4f, 1.2f, -2.3f), new Vector3(0.55f, 0.2f, -0.42f));
            Shoot("9-egg-basket", new Vector3(-4.9f, 1.1f, 3.3f), new Vector3(-6.0f, 0.05f, 2.2f));

            var jar = Homestead.Make<Kitchen.JarContents>("PintJar");
            jar.Restore("{\"slices\":20,\"cloves\":2,\"wedges\":0,\"lemon\":false,\"salsa\":false,\"badBatch\":false,\"kinds\":[\"Cucumber\",\"Clove\"],\"kindAmounts\":[20.0,2.0],\"peppercorns\":0,\"dill\":true,\"brine\":0.88,\"lid\":true,\"processed\":true,\"sealedUp\":true,\"labeled\":true,\"brineVinegar\":0.5,\"brineSalt\":2.0,\"poured\":0.88}");
            jar.GetComponent<Pickup>().SetResting(true, true);
            jar.transform.SetPositionAndRotation(new Vector3(2.6f, 0.9f, 6.55f), Quaternion.Euler(0f, 180f, 0f));
            var open = Homestead.Make<Kitchen.JarContents>("PintJar");
            open.Restore("{\"slices\":12,\"cloves\":0,\"wedges\":0,\"lemon\":false,\"salsa\":false,\"badBatch\":false,\"kinds\":[\"Cucumber\"],\"kindAmounts\":[12.0],\"peppercorns\":0,\"dill\":true,\"brine\":0.5,\"lid\":false,\"processed\":false,\"sealedUp\":false,\"labeled\":false}");
            open.GetComponent<Pickup>().SetResting(true, true);
            open.transform.SetPositionAndRotation(new Vector3(2.75f, 0.9f, 6.55f), Quaternion.identity);
            yield return null;
            Shoot("10-jars", new Vector3(2.68f, 1.12f, 6.18f), new Vector3(2.68f, 0.97f, 6.55f));
            Shoot("11-rosette", new Vector3(2.6f, 1.25f, 6.45f), new Vector3(2.6f, 1.04f, 6.55f));
            Shoot("12-timer", new Vector3(3.08f, 1.1f, 6.55f), new Vector3(3.08f, 0.93f, 6.78f));
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
            // From the driver's seat on a clear day: ahead, out the side, and back.
            yield return At(2, 12f);
            truck.SitIn(hands);
            yield return null;
            yield return null;
            Vector3 eye = eyes.position;
            Shoot("7-drive-ahead", eye, eye + truck.transform.forward * 10f - Vector3.up * 0.6f);
            Shoot("8-drive-side", eye, eye - truck.transform.right * 10f);
            Shoot("9-drive-back", eye, eye - truck.transform.forward * 10f);
            truck.Leave(hands);
            Debug.Log("[shots] eye " + (eye.y - truck.transform.position.y));
            Debug.Log("[shots] cover " + weather.Model.SnowCover(white) + " day " + white + ", snowing day " + falling + " " + weather.Model.DayOf(falling).snow);
        }
    }
}
