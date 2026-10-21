using GrandmasHouse.DayCycle;
using UnityEngine;

namespace GrandmasHouse.Audio
{
    // The sound of the valley around whoever is listening: wind all year
    // (harder in winter and when it snows), birds by day from spring to
    // fall, crickets on summer and fall nights. Indoors it's all muffled.
    public class Outdoors : MonoBehaviour
    {
        [Tooltip("Wind on a still day, a winter day, and a snowy one.")]
        [SerializeField] float calmWind = 0.3f;
        [SerializeField] float winterWind = 0.6f;
        [SerializeField] float snowWind = 0.9f;
        [Tooltip("How much of it you hear indoors.")]
        [SerializeField] float indoors = 0.3f;
        [Tooltip("Seconds between birdcalls, at random in this range.")]
        [SerializeField] Vector2 birdEvery = new Vector2(2f, 7f);

        // Months by index: spring, summer, fall, winter.
        const int Winter = 3;

        SoundLoop wind;
        SoundLoop crickets;
        float nextCheck;
        float nextBird;
        bool inside;

        public float Wind { get; private set; }
        public float Crickets { get; private set; }
        public bool Birds { get; private set; }

        void Start()
        {
            wind = Sfx.Loop(SoundId.Wind, transform, flat: true);
            crickets = Sfx.Loop(SoundId.Crickets, transform, flat: true);
        }

        void Update()
        {
            var eyes = Camera.main;
            var weather = Weather.Instance;
            if (eyes == null || weather == null) return;
            transform.position = eyes.transform.position;

            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.5f;
                inside = false;
                foreach (var shelter in Shelter.All) if (shelter.Contains(eyes.transform.position)) { inside = true; break; }
                int month = weather.Calendar.DateOf(weather.Clock.Day).month;
                float dark = SunCycle.Darkness;
                float muffle = inside ? indoors : 1f;
                Wind = (weather.Snowing ? snowWind : month == Winter ? winterWind : calmWind) * muffle;
                Crickets = (month == 1 || month == 2) && dark > 0.5f ? muffle : 0f;
                Birds = month != Winter && !weather.Snowing && dark < 0.2f;
                wind.Set(Wind);
                crickets.Set(Crickets);
            }

            // A bird somewhere off in the trees.
            if (Birds && Time.time >= nextBird)
            {
                nextBird = Time.time + Random.Range(birdEvery.x, birdEvery.y);
                Vector2 around = Random.insideUnitCircle.normalized * Random.Range(3f, 6f);
                Vector3 at = eyes.transform.position + new Vector3(around.x, 2.5f, around.y);
                Sfx.Play(SoundId.Birdsong, at, inside ? indoors : 1f, 0.15f);
            }
        }
    }
}
