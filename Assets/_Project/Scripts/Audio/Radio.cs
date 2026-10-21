using System;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Audio
{
    // Grandma's tabletop radio. Press Interact to turn it on, again to tune
    // to the next station, and after the last station it clicks off. The
    // other button tunes in the farm report: the weather for the next few
    // days, written along the bottom of the screen for whoever's listening.
    // It sounds like an old radio, and you hear it best near the kitchen table.
    [RequireComponent(typeof(AudioSource))]
    public class Radio : MonoBehaviour, IInteractable, IAltInteractable, ISaveable, GrandmasHouse.Interaction.ISharedUse
    {
        [Serializable]
        public class Station
        {
            public string name;
            [Tooltip("Songs to play in turn. Leave empty to use the placeholder music made in code.")]
            public AudioClip[] songs;
            public ProceduralMusic.Style placeholder;
        }

        [SerializeField] List<Station> stations = new List<Station>
        {
            new Station { name = "Front Porch Strings", placeholder = ProceduralMusic.Style.FrontPorch },
            new Station { name = "Parlor Piano", placeholder = ProceduralMusic.Style.Parlor },
            new Station { name = "Evening Waltz", placeholder = ProceduralMusic.Style.Waltz },
        };
        [Range(0f, 1f)]
        [SerializeField] float volume = 0.55f;
        [Tooltip("Old radio speakers lose the deepest lows and highest highs.")]
        [SerializeField] float lowCut = 250f;
        [SerializeField] float highCut = 4200f;
        [SerializeField] float staticSeconds = 0.35f;

        AudioSource speaker;
        int current = -1;
        int song;
        float staticLeft;
        readonly Dictionary<ProceduralMusic.Style, AudioClip> made = new Dictionary<ProceduralMusic.Style, AudioClip>();

        bool IsOn => current >= 0;

        void Awake()
        {
            speaker = GetComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.spatialBlend = 1f;
            speaker.rolloffMode = AudioRolloffMode.Logarithmic;
            speaker.minDistance = 1.2f;
            speaker.maxDistance = 16f;
            speaker.dopplerLevel = 0f;
            speaker.volume = volume;

            var low = gameObject.AddComponent<AudioHighPassFilter>();
            low.cutoffFrequency = lowCut;
            var high = gameObject.AddComponent<AudioLowPassFilter>();
            high.cutoffFrequency = highCut;
        }

        public bool CanInteract(PlayerHands hands) => true;

        public bool CanAltInteract(PlayerHands hands) => DayCycle.Weather.Instance != null;
        public string AltVerb(PlayerHands hands) => "Hear the forecast";

        public void AltInteract(PlayerHands hands)
        {
            Sfx.Play(SoundId.RadioStatic, transform.position);
            var caption = FindAnyObjectByType<UI.Caption>();
            if (caption != null) caption.Show(DayCycle.Weather.Instance.Forecast(), 14f);
        }

        public string PromptVerb(PlayerHands hands)
        {
            if (!IsOn) return "Turn on the radio";
            int next = current + 1;
            return next < stations.Count ? "Tune to " + stations[next].name : "Turn off the radio";
        }

        public void Interact(PlayerHands hands)
        {
            Sfx.Play(SoundId.KnobClick, transform.position);
            int next = current + 1;
            Tune(next < stations.Count ? next : -1);
        }

        void Tune(int station)
        {
            current = station;
            song = 0;
            speaker.Stop();
            if (IsOn)
            {
                // A moment of static between stations.
                staticLeft = staticSeconds;
                Sfx.Play(SoundId.RadioStatic, transform.position);
            }
        }

        void Update()
        {
            if (!IsOn)
            {
                return;
            }
            if (staticLeft > 0f)
            {
                staticLeft -= Time.unscaledDeltaTime;
                return;
            }
            if (!speaker.isPlaying && !AudioListener.pause)
            {
                PlayNext();
            }
        }

        void PlayNext()
        {
            Station station = stations[current];
            if (station.songs != null && station.songs.Length > 0)
            {
                speaker.clip = station.songs[song % station.songs.Length];
                speaker.loop = station.songs.Length == 1;
                song++;
            }
            else
            {
                if (!made.TryGetValue(station.placeholder, out AudioClip clip))
                {
                    clip = ProceduralMusic.Make(station.placeholder);
                    made[station.placeholder] = clip;
                }
                speaker.clip = clip;
                speaker.loop = true;
            }
            speaker.Play();
        }

        public string SaveKey => "radio";
        public string Capture() => current.ToString();

        public void Restore(string json)
        {
            current = Mathf.Clamp(int.Parse(json), -1, stations.Count - 1);
            song = 0;
            speaker.Stop();
        }
    }
}
