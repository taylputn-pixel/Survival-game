using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // Moves the sun across the sky with the clock: up in the east, high at
    // noon, down in the west, and staying below the horizon through the night
    // so the sky stays dark. A separate moon light fades in over twilight.
    // The sky and overall fill light dim into night with no sudden steps.
    [RequireComponent(typeof(Light))]
    public class SunCycle : MonoBehaviour
    {
        [SerializeField] DayClock clock;
        [Tooltip("A second directional light that lights the world at night.")]
        [SerializeField] Light moon;
        [Tooltip("Compass direction the sun rises from, in degrees (90 = east).")]
        [SerializeField] float sunriseHeading = 90f;
        [Tooltip("How often the ambient lighting is refreshed, in seconds.")]
        [SerializeField] float lightingRefreshSeconds = 0.5f;
        [Tooltip("How much sun gets through on a snowy, overcast day.")]
        [Range(0f, 1f)] [SerializeField] float overcastSun = 0.4f;
        [Range(0f, 1f)] [SerializeField] float overcastSky = 0.55f;

        // How far into night it is (0 day, 1 full night), for lamps and headlights.
        public static float Darkness { get; private set; }

        Light sun;
        Material sky;
        float nextRefresh;

        void Awake()
        {
            sun = GetComponent<Light>();
            // Work on a copy so play mode never changes the sky material asset.
            if (RenderSettings.skybox != null)
            {
                sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = sky;
            }
            // The sky is drawn from the sun's direction, so it must stay the sun.
            RenderSettings.sun = sun;
        }

        void LateUpdate()
        {
            var settings = clock.Settings;
            float progress;
            float night;
            if (clock.Hour < settings.dawnHour)
            {
                // After midnight: night, lifting over the hour before dawn.
                progress = 0f;
                night = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(settings.dawnHour - 1f, settings.dawnHour, clock.Hour));
            }
            else
            {
                progress = clock.IsNight ? 1f : clock.DaylightProgress;
                // 0 before twilight, rising smoothly to 1 at nightfall.
                night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.twilightStart, 1f, progress));
            }

            // Rises just above the horizon at dawn, sets at dusk, and keeps sinking
            // through twilight so the afterglow on the horizon fades out too.
            float elevation = Mathf.Sin(progress * Mathf.PI) * settings.noonElevation + Mathf.Lerp(2f, -4f, progress) - 14f * night;
            Darkness = night;
            float heading = sunriseHeading + progress * 180f;
            transform.rotation = Quaternion.Euler(elevation, heading - 180f, 0f);
            sun.color = settings.sunColor.Evaluate(progress);
            // Kept just above zero so it remains the sky's sun even at night.
            // Snow comes with a gray sky.
            bool overcast = Weather.Instance != null && Weather.Instance.Snowing;
            sun.intensity = Mathf.Max(settings.sunIntensity.Evaluate(progress) * (1f - night) * (overcast ? overcastSun : 1f), 0.001f);

            moon.color = settings.moonColor;
            moon.intensity = settings.moonIntensity * night;
            moon.enabled = night > 0f;

            float exposure = Mathf.Lerp(settings.skyExposure.Evaluate(progress) * (overcast ? overcastSky : 1f), settings.nightSkyExposure, night);
            if (sky != null && sky.HasProperty("_Exposure"))
            {
                sky.SetFloat("_Exposure", exposure);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(settings.ambientIntensity.Evaluate(progress), settings.nightAmbientIntensity, night);

            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + lightingRefreshSeconds;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
