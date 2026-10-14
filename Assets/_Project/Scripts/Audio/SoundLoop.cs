using UnityEngine;

namespace GrandmasHouse.Audio
{
    // A looping sound that fades in and out rather than starting and
    // stopping sharply (a pour, a pot coming to the boil).
    public class SoundLoop : MonoBehaviour
    {
        [SerializeField] float fadeSeconds = 0.25f;

        AudioSource source;
        float baseVolume;
        float level;
        float target;

        public bool Playing => target > 0f;

        public void Setup(AudioSource audioSource, float volume)
        {
            source = audioSource;
            baseVolume = volume;
            source.volume = 0f;
        }

        // 0 is silent, 1 is full; in between for a gentle simmer.
        public void Set(float amount) => target = Mathf.Clamp01(amount);

        public void Set(bool on) => Set(on ? 1f : 0f);

        void Update()
        {
            if (Mathf.Approximately(level, target))
            {
                return;
            }
            level = Mathf.MoveTowards(level, target, Time.deltaTime / fadeSeconds);
            source.volume = level * baseVolume;
            if (level > 0f && !source.isPlaying)
            {
                // Start partway through so two loops of the same sound don't line up.
                source.time = Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
            }
            else if (level <= 0f && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
