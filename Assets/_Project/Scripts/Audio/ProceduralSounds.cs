using System;
using UnityEngine;

namespace GrandmasHouse.Audio
{
    // Simple sounds made in code, used until real recordings replace them.
    // Each is a few lines of tones and filtered noise: soft, short, and
    // quiet enough not to wear on the ear.
    public static class ProceduralSounds
    {
        const int Rate = 44100;

        public static AudioClip Make(SoundId id)
        {
            switch (id)
            {
                case SoundId.LidPing: return Clip(id, 0.6f, Ping);
                case SoundId.LidClink: return Clip(id, 0.25f, t => Bell(t, new[] { 1250f, 2930f, 4170f }, 18f) * 0.5f);
                case SoundId.PotLid: return Clip(id, 0.45f, t => Bell(t, new[] { 520f, 1370f, 2610f }, 9f) * 0.55f + Noise() * Decay(t, 40f) * 0.15f);
                case SoundId.KnifeChop: return Clip(id, 0.12f, Chop);
                case SoundId.SliceDrop: return Clip(id, 0.1f, t => Sweep(t, 420f, 160f, 0.07f) * Decay(t, 45f) * 0.6f);
                case SoundId.DillRustle: return Filtered(id, 0.3f, 0.35f, t => Envelope(t, 0.03f, 0.3f) * Jitter(t, 40f));
                case SoundId.SaltShake: return Filtered(id, 0.4f, 0.55f, t => Pulses(t, 3, 0.11f, 0.05f) * 0.6f);
                case SoundId.KnobClick: return Clip(id, 0.05f, t => (Math.Sin(2 * Math.PI * 3200 * t) * 0.4f + Noise() * 0.3f) * Decay(t, 160f));
                case SoundId.SetDown: return Clip(id, 0.14f, t => Sweep(t, 140f, 70f, 0.1f) * Decay(t, 30f) * 0.8f + Noise() * Decay(t, 90f) * 0.08f);
                case SoundId.PickUp: return Filtered(id, 0.16f, 0.25f, t => Envelope(t, 0.05f, 0.16f) * 0.35f);
                case SoundId.WeedPull: return Filtered(id, 0.35f, 0.3f, t => Envelope(t, 0.2f, 0.35f) * Jitter(t, 90f) * 0.7f + PopAt(t, 0.24f));
                case SoundId.SoilPat: return Filtered(id, 0.12f, 0.08f, t => Decay(t, 35f) * 0.8f);
                case SoundId.StepWood: return Clip(id, 0.12f, t => Sweep(t, 160f, 90f, 0.08f) * Decay(t, 38f) * 0.5f + Noise() * Decay(t, 120f) * 0.12f);
                case SoundId.StepGrass: return Filtered(id, 0.18f, 0.22f, t => Envelope(t, 0.02f, 0.18f) * Jitter(t, 60f) * 0.5f);
                case SoundId.Paper: return Filtered(id, 0.35f, 0.6f, t => Envelope(t, 0.02f, 0.35f) * Jitter(t, 120f) * 0.45f);
                case SoundId.RadioStatic: return Filtered(id, 0.35f, 0.8f, t => Envelope(t, 0.02f, 0.35f) * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 23 * t)) * 0.5f);
                case SoundId.MenuClick: return Clip(id, 0.04f, t => Math.Sin(2 * Math.PI * 1800 * t) * Decay(t, 140f) * 0.35f);
                case SoundId.Eat: return Filtered(id, 0.5f, 0.4f, t => Pulses(t, 3, 0.16f, 0.07f) * Jitter(t, 70f) * 0.7f);
                case SoundId.StomachGrowl: return Clip(id, 1.1f, Growl);
                case SoundId.Birdsong: return Clip(id, 0.7f, Birdsong);
                case SoundId.Cluck: return Clip(id, 0.42f, Cluck);
                case SoundId.TimerBell: return Clip(id, 0.5f, t => Bell(t, new[] { 2480f, 3610f, 5230f }, 7f) * 0.7f);
                case SoundId.Engine: return EngineLoop(id);
                case SoundId.Wind: return Filtered(id, 8f, 0.05f, t => 0.55 + 0.3 * Math.Sin(2 * Math.PI * 0.25 * t) + 0.15 * Math.Sin(2 * Math.PI * 0.625 * t + 1.3), loop: true);
                case SoundId.Crickets: return CricketsLoop(id);
                case SoundId.Pour:return Filtered(id, 2f, 0.45f, t => 0.35f + 0.08f * Math.Sin(2 * Math.PI * 3 * t), loop: true);
                case SoundId.Boil: return BubblesLoop(id, 3f, 14f);
                case SoundId.Simmer: return BubblesLoop(id, 3f, 5f);
                default: return Clip(id, 0.05f, t => 0f);
            }
        }

        // ---- Shapes ----

        static readonly System.Random random = new System.Random(7);

        static float Noise() => (float)(random.NextDouble() * 2.0 - 1.0);

        static double Decay(double t, double speed) => Math.Exp(-t * speed);

        static double Envelope(double t, double attack, double length)
        {
            if (t < attack) return t / attack;
            return Math.Max(0.0, 1.0 - (t - attack) / (length - attack));
        }

        // Sound of a jar lid sealing: a bright tick that rings briefly and dips in pitch.
        static double Ping(double t)
        {
            double bend = 1.0 - 0.06 * Math.Min(t / 0.05, 1.0);
            double ring = Math.Sin(2 * Math.PI * 2350 * bend * t) * 0.6 + Math.Sin(2 * Math.PI * 3720 * bend * t) * 0.25;
            return ring * Decay(t, 9) * Math.Min(t / 0.002, 1.0) * 0.5;
        }

        // A low, wobbling gurgle that rises and falls.
        static double Growl(double t)
        {
            double pitch = 70 + 25 * Math.Sin(2 * Math.PI * 1.3 * t) + 10 * Math.Sin(2 * Math.PI * 7 * t);
            double body = Math.Sin(2 * Math.PI * pitch * t) + 0.4 * Math.Sin(4 * Math.PI * pitch * t);
            return body * Envelope(t, 0.25, 1.1) * (0.7 + 0.3 * Jitter(t, 30)) * 0.45;
        }

        // A few quick rising whistles, like a songbird off in the trees.
        static double Birdsong(double t)
        {
            const double note = 0.13;
            int n = (int)(t / note);
            if (n >= 4) return 0;
            double u = t - n * note;
            if (u > 0.09) return 0;
            double from = n == 3 ? 3400 : 2600 + n * 180, to = n == 3 ? 2500 : 3900;
            double f = from + (to - from) * u / 0.09;
            return Math.Sin(2 * Math.PI * f * u) * Math.Sin(Math.PI * u / 0.09) * 0.45;
        }

        // "Buk-buk": two short, nasal bursts that drop in pitch.
        static double Cluck(double t)
        {
            double sum = 0;
            foreach (double at in new[] { 0.0, 0.16 })
            {
                double u = t - at;
                if (u < 0 || u > 0.12) continue;
                double f = 520 - 900 * u;
                double voice = Math.Sin(2 * Math.PI * f * u) + 0.5 * Math.Sin(4 * Math.PI * f * u) + 0.3 * Math.Sin(6 * Math.PI * f * u);
                sum += voice * Math.Min(u / 0.008, 1.0) * Decay(u, 28) * 0.4;
            }
            return sum;
        }

        static double Chop(double t)
        {
            // A knock on the wooden board under a short slice.
            double knock = Math.Sin(2 * Math.PI * 180 * t) * Decay(t, 45);
            return knock * 0.55 + Noise() * Decay(t, 70) * 0.25;
        }

        static double Bell(double t, float[] partials, double speed)
        {
            double sum = 0;
            for (int i = 0; i < partials.Length; i++)
            {
                sum += Math.Sin(2 * Math.PI * partials[i] * t) * Decay(t, speed * (1 + i * 0.6)) / (i + 1);
            }
            return sum * Math.Min(t / 0.002, 1.0);
        }

        static double Sweep(double t, double from, double to, double over)
        {
            double k = Math.Min(t / over, 1.0);
            double f = from + (to - from) * k;
            return Math.Sin(2 * Math.PI * f * t);
        }

        // Rough, crackly amplitude for rustles and rips.
        static double Jitter(double t, double rate)
        {
            double step = Math.Floor(t * rate);
            var r = new System.Random((int)step * 7919);
            return 0.4 + r.NextDouble() * 0.6;
        }

        static double Pulses(double t, int count, double spacing, double width)
        {
            double phase = t % spacing;
            return t < count * spacing && phase < width ? Math.Sin(Math.PI * phase / width) : 0;
        }

        static double PopAt(double t, double at)
        {
            double d = t - at;
            return d < 0 ? 0 : Math.Sin(2 * Math.PI * 240 * d) * Decay(d, 60) * 0.5;
        }

        // ---- Building clips ----

        static AudioClip Clip(SoundId id, float seconds, Func<double, double> shape)
        {
            int count = (int)(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = (float)shape((double)i / Rate);
            }
            FadeEnds(data, 0.003f);
            var clip = AudioClip.Create(id.ToString(), count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Noise shaped by an envelope, then softened: brightness 0 is a dull
        // thud, 1 is a hiss.
        static AudioClip Filtered(SoundId id, float seconds, float brightness, Func<double, double> envelope, bool loop = false)
        {
            int count = (int)(seconds * Rate);
            var data = new float[count];
            float low = 0f;
            float smoothing = Mathf.Lerp(0.02f, 0.6f, brightness);
            for (int i = 0; i < count; i++)
            {
                low += (Noise() - low) * smoothing;
                // Take away a little of the lowest rumble so it isn't boomy.
                data[i] = low * (float)envelope((double)i / Rate);
            }
            Normalize(data, 0.6f);
            if (loop)
            {
                data = LoopSeam(data, 0.05f);
            }
            else
            {
                FadeEnds(data, 0.003f);
            }
            var clip = AudioClip.Create(id.ToString(), data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A pot of water: a low rumble with bubbles popping at random.
        static AudioClip BubblesLoop(SoundId id, float seconds, float bubblesPerSecond)
        {
            int count = (int)(seconds * Rate);
            var data = new float[count];
            float low = 0f;
            for (int i = 0; i < count; i++)
            {
                low += (Noise() - low) * 0.03f;
                data[i] = low * 0.35f;
            }
            int bubbles = (int)(seconds * bubblesPerSecond);
            for (int b = 0; b < bubbles; b++)
            {
                int start = random.Next(count);
                double pitch = 250 + random.NextDouble() * 500;
                double size = 0.15 + random.NextDouble() * 0.25;
                for (int i = 0; i < Rate / 25 && start + i < count; i++)
                {
                    double t = (double)i / Rate;
                    double f = pitch * (1 + t * 12);
                    data[start + i] += (float)(Math.Sin(2 * Math.PI * f * t) * Decay(t, 70) * size);
                }
            }
            Normalize(data, 0.55f);
            data = LoopSeam(data, 0.08f);
            var clip = AudioClip.Create(id.ToString(), data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // An old six-cylinder idling: a low throb with a rough edge. The
        // truck raises its pitch as it speeds up.
        static AudioClip EngineLoop(SoundId id)
        {
            const float seconds = 2f;
            int count = (int)(seconds * Rate);
            var data = new float[count];
            float low = 0f;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / Rate;
                // 36 Hz firing, whole cycles in two seconds so the loop meets itself.
                double throb = Math.Sin(2 * Math.PI * 36 * t) + 0.6 * Math.Sin(2 * Math.PI * 72 * t) + 0.3 * Math.Sin(2 * Math.PI * 108 * t);
                double pulse = 0.75 + 0.25 * Math.Sin(2 * Math.PI * 12 * t);
                low += (Noise() - low) * 0.05f;
                data[i] = (float)(throb * pulse * 0.5 + low * 0.6);
            }
            Normalize(data, 0.6f);
            data = LoopSeam(data, 0.05f);
            var clip = AudioClip.Create(id.ToString(), data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Two crickets trilling out of step on a summer night.
        static AudioClip CricketsLoop(SoundId id)
        {
            const float seconds = 3f;
            int count = (int)(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / Rate;
                data[i] = (float)(Cricket(t, 4400, 0.6, 0.0) + Cricket(t, 4700, 0.75, 0.31) * 0.7);
            }
            Normalize(data, 0.5f);
            data = LoopSeam(data, 0.05f);
            var clip = AudioClip.Create(id.ToString(), data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Three quick pulses of a high tone, every so often.
        static double Cricket(double t, double pitch, double every, double offset)
        {
            double u = (t + offset) % every;
            double gate = Pulses(u, 3, 0.045, 0.025);
            return Math.Sin(2 * Math.PI * pitch * t) * gate;
        }

        static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        static void FadeEnds(float[] data, float seconds)
        {
            int n = Mathf.Min((int)(seconds * Rate), data.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }

        // Blend the tail into the start and drop it, so a loop has no click where it wraps.
        static float[] LoopSeam(float[] data, float seconds)
        {
            int n = Mathf.Min((int)(seconds * Rate), data.Length / 2);
            int end = data.Length - n;
            for (int i = 0; i < n; i++)
            {
                float k = (float)i / n;
                data[i] = data[i] * k + data[end + i] * (1f - k);
            }
            Array.Resize(ref data, end);
            return data;
        }
    }
}
