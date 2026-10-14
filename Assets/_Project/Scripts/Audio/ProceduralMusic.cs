using System;
using UnityEngine;

namespace GrandmasHouse.Audio
{
    // Placeholder radio music made in code: short, gentle, looping tunes in
    // a few old-time styles, until real recordings are added to a station.
    public static class ProceduralMusic
    {
        public enum Style { FrontPorch, Parlor, Waltz }

        const int Rate = 44100;

        // Semitones from A4 (440 Hz) for the notes used below.
        static double Hz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        public static AudioClip Make(Style style)
        {
            switch (style)
            {
                case Style.Parlor: return Parlor();
                case Style.Waltz: return Waltz();
                default: return FrontPorch();
            }
        }

        // ---- Styles ----

        // Easygoing picking in G: G C G D | G C D G, twice.
        static AudioClip FrontPorch()
        {
            const double bpm = 100;
            int[][] chords = { G, C, G, D, G, C, D, G };
            var song = new Song(bpm, beatsPerBar: 4, bars: 16);
            var rng = new System.Random(11);
            for (int bar = 0; bar < 16; bar++)
            {
                int[] chord = chords[bar % 8];
                double start = song.Bar(bar);
                // Bass on 1 and 3: root, then fifth.
                song.Bass(start, Hz(chord[0] - 12), song.Beat * 1.8);
                song.Bass(start + song.Beat * 2, Hz(chord[2] - 12), song.Beat * 1.8);
                // Eighth-note picking up and down the chord.
                int[] pattern = { 0, 1, 2, 1, 3, 1, 2, 1 };
                for (int i = 0; i < 8; i++)
                {
                    int note = chord[pattern[i] % chord.Length] + (pattern[i] == 3 ? 12 : 0);
                    song.Pluck(start + i * song.Beat / 2, Hz(note), 0.9, i % 2 == 0 ? 0.32 : 0.22, brightness: 0.55);
                }
                // A simple tune on top, now and then.
                if (bar % 2 == 1)
                {
                    song.Melody(start, chord, Major(67), rng, notes: 4, length: song.Beat);
                }
            }
            return song.Finish("Front Porch Strings");
        }

        // Slow parlor chords in F: F Dm Bb C, with a soft tune.
        static AudioClip Parlor()
        {
            const double bpm = 72;
            int[][] chords = { F, Dm, Bb, C7, F, Dm, Bb, F };
            var song = new Song(bpm, beatsPerBar: 4, bars: 16);
            var rng = new System.Random(23);
            for (int bar = 0; bar < 16; bar++)
            {
                int[] chord = chords[bar % 8];
                double start = song.Bar(bar);
                song.Bass(start, Hz(chord[0] - 12), song.Beat * 3.5);
                foreach (int note in chord)
                {
                    song.Piano(start, Hz(note), song.Beat * 3.6, 0.16);
                    song.Piano(start + song.Beat * 2, Hz(note), song.Beat * 1.8, 0.1);
                }
                song.Melody(start, chord, Major(65), rng, notes: 3, length: song.Beat * 1.33, piano: true);
            }
            return song.Finish("Parlor Piano");
        }

        // A slow waltz in D: D G A D, oom-pah-pah.
        static AudioClip Waltz()
        {
            const double bpm = 96;
            int[][] chords = { D, G, A7, D, D, G, A7, D };
            var song = new Song(bpm, beatsPerBar: 3, bars: 16);
            var rng = new System.Random(37);
            for (int bar = 0; bar < 16; bar++)
            {
                int[] chord = chords[bar % 8];
                double start = song.Bar(bar);
                song.Bass(start, Hz(chord[0] - 12), song.Beat * 0.95);
                for (int beat = 1; beat < 3; beat++)
                {
                    foreach (int note in chord)
                    {
                        song.Pluck(start + beat * song.Beat, Hz(note), 0.6, 0.12, brightness: 0.4);
                    }
                }
                song.Melody(start, chord, Major(62 + 12), rng, notes: 3, length: song.Beat, bowed: true);
            }
            return song.Finish("Evening Waltz");
        }

        // ---- Chords (MIDI notes) ----

        static readonly int[] G = { 55, 59, 62 };
        static readonly int[] C = { 60, 64, 67 };
        static readonly int[] D = { 62, 66, 69 };
        static readonly int[] F = { 53, 57, 60 };
        static readonly int[] Dm = { 62, 65, 69 };
        static readonly int[] Bb = { 58, 62, 65 };
        static readonly int[] C7 = { 60, 64, 67, 70 };
        static readonly int[] A7 = { 57, 61, 64, 67 };

        static int[] Major(int root) => new[] { root, root + 2, root + 4, root + 5, root + 7, root + 9, root + 11, root + 12 };

        // ---- A little synthesizer ----

        class Song
        {
            readonly float[] data;
            public double Beat { get; }
            readonly int beatsPerBar;

            public Song(double bpm, int beatsPerBar, int bars)
            {
                Beat = 60.0 / bpm;
                this.beatsPerBar = beatsPerBar;
                data = new float[(int)(Beat * beatsPerBar * bars * Rate)];
            }

            public double Bar(int bar) => bar * beatsPerBar * Beat;

            // A plucked string (Karplus-Strong): a burst of noise that rings at a pitch.
            public void Pluck(double at, double hz, double seconds, double loudness, double brightness)
            {
                int period = Math.Max(2, (int)(Rate / hz));
                var ring = new float[period];
                var rng = new System.Random((int)(at * 1000) ^ period);
                for (int i = 0; i < period; i++) ring[i] = (float)(rng.NextDouble() * 2 - 1);
                int start = (int)(at * Rate);
                int count = (int)(seconds * Rate);
                float keep = (float)(0.996 - (1 - brightness) * 0.006);
                for (int i = 0; i < count && start + i < data.Length; i++)
                {
                    int j = i % period;
                    float next = ring[(j + 1) % period];
                    float sample = ring[j];
                    ring[j] = (sample + next) * 0.5f * keep;
                    data[start + i] += (float)(sample * loudness * Fade(i, count));
                }
            }

            // A soft piano-like tone: a few harmonics that fade.
            public void Piano(double at, double hz, double seconds, double loudness)
            {
                int start = (int)(at * Rate);
                int count = (int)(seconds * Rate);
                for (int i = 0; i < count && start + i < data.Length; i++)
                {
                    double t = (double)i / Rate;
                    double tone = Math.Sin(2 * Math.PI * hz * t) + 0.4 * Math.Sin(4 * Math.PI * hz * t) + 0.15 * Math.Sin(6 * Math.PI * hz * t);
                    data[start + i] += (float)(tone * Math.Exp(-t * 2.2) * Math.Min(t / 0.004, 1) * loudness * Fade(i, count));
                }
            }

            public void Bass(double at, double hz, double seconds)
            {
                int start = (int)(at * Rate);
                int count = (int)(seconds * Rate);
                for (int i = 0; i < count && start + i < data.Length; i++)
                {
                    double t = (double)i / Rate;
                    double tone = Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(4 * Math.PI * hz * t);
                    data[start + i] += (float)(tone * Math.Exp(-t * 2.5) * Math.Min(t / 0.01, 1) * 0.3 * Fade(i, count));
                }
            }

            // A bowed note (fiddle-like): slow swell, a little vibrato.
            void Bowed(double at, double hz, double seconds, double loudness)
            {
                int start = (int)(at * Rate);
                int count = (int)(seconds * Rate);
                double phase = 0;
                for (int i = 0; i < count && start + i < data.Length; i++)
                {
                    double t = (double)i / Rate;
                    double f = hz * (1 + 0.004 * Math.Sin(2 * Math.PI * 5.5 * t));
                    phase += 2 * Math.PI * f / Rate;
                    double tone = Math.Sin(phase) + 0.35 * Math.Sin(2 * phase) + 0.15 * Math.Sin(3 * phase);
                    double swell = Math.Min(t / 0.12, 1) * Fade(i, count);
                    data[start + i] += (float)(tone * swell * loudness);
                }
            }

            // A few notes from the scale over a bar, leaning on the chord's own notes.
            public void Melody(double barStart, int[] chord, int[] scale, System.Random rng, int notes, double length, bool piano = false, bool bowed = false)
            {
                for (int n = 0; n < notes; n++)
                {
                    int note = n == 0 ? chord[rng.Next(chord.Length)] + 12 : scale[rng.Next(scale.Length)];
                    while (note > 81) note -= 12;
                    while (note < 62) note += 12;
                    double at = barStart + n * length;
                    if (piano) Piano(at, Hz(note), length * 1.2, 0.14);
                    else if (bowed) Bowed(at, Hz(note), length * 0.95, 0.12);
                    else Pluck(at, Hz(note), length * 1.4, 0.3, brightness: 0.75);
                }
            }

            static double Fade(int i, int count)
            {
                int tail = Math.Min(2000, count / 4);
                return i > count - tail ? (double)(count - i) / tail : 1;
            }

            public AudioClip Finish(string name)
            {
                float max = 0.0001f;
                foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
                float gain = 0.5f / max;
                for (int i = 0; i < data.Length; i++) data[i] *= gain;
                var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }
        }
    }
}
