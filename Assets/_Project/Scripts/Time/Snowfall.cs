using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // Snow coming down around whoever is looking, on a snowy day. It stops
    // at the door: indoors (in a shelter) there's none.
    [RequireComponent(typeof(ParticleSystem))]
    public class Snowfall : MonoBehaviour
    {
        [SerializeField] Weather weather;
        [Tooltip("Flakes a second at full snowfall.")]
        [SerializeField] float rate = 400f;
        [Tooltip("How high above the eyes the flakes start.")]
        [SerializeField] float above = 8f;

        ParticleSystem flakes;
        float nextCheck;
        bool falling;

        void Awake() => flakes = GetComponent<ParticleSystem>();

        void LateUpdate()
        {
            var eyes = Camera.main;
            if (eyes == null || weather == null) return;
            transform.position = eyes.transform.position + Vector3.up * above;

            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            bool inside = false;
            foreach (var shelter in Shelter.All) if (shelter.Contains(eyes.transform.position)) { inside = true; break; }
            bool fall = weather.Snowing && !inside;
            var emission = flakes.emission;
            emission.rateOverTime = fall ? rate : 0f;
            // Step inside and the snow is gone at once, not drifting through the roof.
            if (falling && inside) flakes.Clear();
            if (fall && !flakes.isPlaying) flakes.Play();
            falling = fall;
        }
    }
}
