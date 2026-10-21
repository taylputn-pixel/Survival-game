using System.Collections.Generic;
using UnityEngine;

namespace GrandmasHouse.Audio
{
    // Plays the game's sounds. Call Sfx.Play(SoundId.LidPing, position) for
    // a one-shot in the world, or Sfx.Loop(...) for a sound that runs while
    // something happens (pouring, boiling). Needs no scene setup: it builds
    // its own speakers the first time a sound plays.
    public static class Sfx
    {
        const int Voices = 16;
        const float Reach = 12f;

        static Transform root;
        static AudioSource[] voices;
        static int nextVoice;
        static SoundBank bank;
        static readonly Dictionary<SoundId, AudioClip> placeholders = new Dictionary<SoundId, AudioClip>();

        // How loud each placeholder plays, so none stands out.
        static readonly Dictionary<SoundId, float> loudness = new Dictionary<SoundId, float>
        {
            { SoundId.LidPing, 0.8f }, { SoundId.LidClink, 0.5f }, { SoundId.PotLid, 0.55f },
            { SoundId.KnifeChop, 0.6f }, { SoundId.SliceDrop, 0.35f }, { SoundId.DillRustle, 0.35f },
            { SoundId.SaltShake, 0.4f }, { SoundId.KnobClick, 0.5f }, { SoundId.SetDown, 0.45f },
            { SoundId.PickUp, 0.25f }, { SoundId.WeedPull, 0.5f }, { SoundId.SoilPat, 0.4f },
            { SoundId.StepWood, 0.3f }, { SoundId.StepGrass, 0.25f }, { SoundId.Paper, 0.45f },
            { SoundId.MenuClick, 0.35f }, { SoundId.RadioStatic, 0.3f }, { SoundId.Pour, 0.45f }, { SoundId.Boil, 0.4f }, { SoundId.Simmer, 0.25f },
            { SoundId.Engine, 0.5f }, { SoundId.Wind, 0.35f }, { SoundId.Crickets, 0.18f }, { SoundId.Birdsong, 0.9f }, { SoundId.Cluck, 0.4f }, { SoundId.TimerBell, 0.8f },
        };

        // Domain reload may be off in the editor, so start fresh each time Play begins.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            root = null;
            voices = null;
            nextVoice = 0;
            bank = null;
            placeholders.Clear();
        }

        // Reach is how far off it can still be heard (a timer's bell carries).
        public static void Play(SoundId id, Vector3 at, float volume = 1f, float pitchSpread = 0.06f, float reach = Reach)
        {
            Ensure();
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.maxDistance = reach;
            voice.transform.position = at;
            voice.spatialBlend = 1f;
            voice.ignoreListenerPause = false;
            voice.pitch = 1f + Random.Range(-pitchSpread, pitchSpread);
            voice.PlayOneShot(Clip(id, out float baseVolume), baseVolume * volume);
        }

        // For menus: not placed in the world.
        public static void PlayFlat(SoundId id, float volume = 1f)
        {
            Ensure();
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.spatialBlend = 0f;
            voice.pitch = 1f;
            // Menu sounds still play while the game is paused.
            voice.ignoreListenerPause = true;
            voice.PlayOneShot(Clip(id, out float baseVolume), baseVolume * volume);
        }

        // A looping sound attached to something; turn it on and off as needed.
        // Flat is all around you rather than from a place (wind, crickets).
        public static SoundLoop Loop(SoundId id, Transform on, bool flat = false)
        {
            Ensure();
            var go = new GameObject(id + " Sound");
            go.transform.SetParent(on, false);
            var source = go.AddComponent<AudioSource>();
            Setup(source);
            source.loop = true;
            if (flat) source.spatialBlend = 0f;
            source.clip = Clip(id, out float baseVolume);
            var loop = go.AddComponent<SoundLoop>();
            loop.Setup(source, baseVolume);
            return loop;
        }

        static AudioClip Clip(SoundId id, out float volume)
        {
            SoundBank.Entry entry = bank != null ? bank.Find(id) : null;
            if (entry != null)
            {
                volume = entry.volume;
                return entry.clips[Random.Range(0, entry.clips.Length)];
            }
            volume = loudness.TryGetValue(id, out float v) ? v : 0.5f;
            if (!placeholders.TryGetValue(id, out AudioClip clip))
            {
                clip = ProceduralSounds.Make(id);
                placeholders[id] = clip;
            }
            return clip;
        }

        static void Ensure()
        {
            if (root != null)
            {
                return;
            }
            bank = Resources.Load<SoundBank>("SoundBank");
            root = new GameObject("Sounds").transform;
            Object.DontDestroyOnLoad(root.gameObject);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(root, false);
                voices[i] = go.AddComponent<AudioSource>();
                Setup(voices[i]);
            }
        }

        // Soft falloff: clear within arm's reach, gone across the yard.
        static void Setup(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.6f;
            source.maxDistance = 12f;
            source.dopplerLevel = 0f;
        }
    }
}
