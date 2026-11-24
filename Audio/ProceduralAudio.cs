using System.Collections.Generic;
using UnityEngine;

// Sounds synthesised at runtime from filtered noise and sines:
// no downloads, no licences, and each one is a few lines to retune. Clips are cached per name.
// Loops crossfade their tail into their head so they repeat without a click.
public static class ProceduralAudio
{
    private const int Rate = 44100;
    private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

    // Heavy rain on a roof: pink-ish noise with a slow swell and random fat drops.
    public static AudioClip RainLoop() => Get("Rain", () =>
    {
        float[] s = new float[Rate * 6];
        System.Random random = new System.Random(11);
        float low = 0f, band = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.08f;
            band += (white - band) * 0.45f;
            float swell = 0.8f + 0.2f * Mathf.Sin(i / (float)Rate * 0.9f);
            s[i] = (low * 1.6f + (band - low) * 0.35f) * swell;
            if (random.NextDouble() < 0.0009)
            {
                AddPing(s, i, 1800f + (float)random.NextDouble() * 2400f, 0.012f, 0.18f);
            }
        }

        return Loop("Rain", s, 0.5f);
    });

    // Close thunder: a sharp crack that rolls into a long low rumble.
    public static AudioClip Thunder() => Get("Thunder", () =>
    {
        float[] s = new float[Rate * 7];
        System.Random random = new System.Random(23);
        float low = 0f, lower = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.02f;
            lower += (low - lower) * 0.05f;
            float crack = Mathf.Exp(-t * 9f) * white * 0.5f;
            float roll = lower * 14f * Mathf.Exp(-t * 0.55f) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 3f, 0.5f)) * Mathf.Clamp01(t * 6f);
            s[i] = Mathf.Clamp(crack + roll, -1f, 1f);
        }

        return Make("Thunder", s);
    });

    // AM radio hiss with crackles; the voice lines ride on top as subtitles.
    public static AudioClip RadioStaticLoop() => Get("RadioStatic", () =>
    {
        float[] s = new float[Rate * 4];
        System.Random random = new System.Random(5);
        float band = 0f, prev = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            band += (white - band) * 0.6f;
            float hiss = (band - prev) * 0.5f;
            prev = band;
            float wobble = 0.55f + 0.45f * Mathf.PerlinNoise(i / (float)Rate * 4f, 1.3f);
            s[i] = hiss * wobble;
            if (random.NextDouble() < 0.0006)
            {
                int length = random.Next(200, 1400);
                for (int k = 0; k < length && i + k < s.Length; k++)
                {
                    s[i + k] += (float)(random.NextDouble() * 2.0 - 1.0) * 0.6f * (1f - k / (float)length);
                }
            }
        }

        return Loop("RadioStatic", s, 0.3f);
    });

    // A burst of a few rising bubbles.
    public static AudioClip Bubbles() => Get("Bubbles", () =>
    {
        float[] s = new float[(int)(Rate * 0.9f)];
        System.Random random = new System.Random(7);
        for (int b = 0; b < 9; b++)
        {
            int start = random.Next(0, s.Length - Rate / 6);
            float frequency = 300f + (float)random.NextDouble() * 700f;
            int length = Rate / 10;
            for (int k = 0; k < length; k++)
            {
                float t = k / (float)Rate;
                float env = Mathf.Exp(-t * 38f);
                s[start + k] += Mathf.Sin(2f * Mathf.PI * frequency * (1f + t * 6f) * t) * env * 0.35f;
            }
        }

        return Make("Bubbles", s);
    });

    // Wading step in shallow water.
    public static AudioClip Splash() => Get("Splash", () =>
    {
        float[] s = new float[(int)(Rate * 0.35f)];
        System.Random random = new System.Random(3);
        float band = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            band += (white - band) * 0.3f;
            s[i] = band * Mathf.Exp(-t * 14f) * Mathf.Clamp01(t * 200f) * 0.8f;
        }

        AddPing(s, (int)(Rate * 0.03f), 900f, 0.05f, 0.15f);
        return Make("Splash", s);
    });

    // Surfacing: a sharp breath in.
    public static AudioClip Gasp() => Get("Gasp", () =>
    {
        float[] s = new float[(int)(Rate * 0.8f)];
        System.Random random = new System.Random(9);
        float band = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            band += (white - band) * 0.25f;
            float env = Mathf.Clamp01(t * 12f) * Mathf.Exp(-Mathf.Max(0f, t - 0.18f) * 7f);
            s[i] = (white - band) * env * 0.5f;
        }

        return Make("Gasp", s);
    });

    // The pressure of deep water: a low, slowly breathing rumble.
    public static AudioClip UnderwaterLoop() => Get("Underwater", () =>
    {
        float[] s = new float[Rate * 8];
        System.Random random = new System.Random(13);
        float low = 0f, lower = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.01f;
            lower += (low - lower) * 0.02f;
            float breathe = 0.7f + 0.3f * Mathf.Sin(t * 0.8f);
            s[i] = lower * 22f * breathe + Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.03f;
        }

        return Loop("Underwater", s, 1f);
    });

    // Mains hum of old fluorescent tubes: 60 Hz and its harmonics with a buzzy edge.
    public static AudioClip ElectricHumLoop() => Get("ElectricHum", () =>
    {
        float[] s = new float[Rate * 2];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float hum = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.35f
                + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 180f * t)) * 0.08f;
            s[i] = hum;
        }

        return Make("ElectricHum", s);
    });

    // A short circuit: a hard crack followed by crackling sparks.
    public static AudioClip ShortCircuit() => Get("ShortCircuit", () =>
    {
        float[] s = new float[(int)(Rate * 1.2f)];
        System.Random random = new System.Random(31);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            float crack = white * Mathf.Exp(-t * 30f);
            float crackle = random.NextDouble() < 0.004 * Mathf.Exp(-t * 3f) * 40.0 ? white : 0f;
            s[i] = crack + crackle * 0.6f * Mathf.Exp(-t * 2.5f);
        }

        return Make("ShortCircuit", s);
    });

    // A single drop hitting still water.
    public static AudioClip Drip() => Get("Drip", () =>
    {
        float[] s = new float[(int)(Rate * 0.4f)];
        AddPing(s, 0, 1400f, 0.09f, 0.5f);
        return Make("Drip", s);
    });

    // A "lub-dub" from two decaying low sine thumps. Shared by the dread and the suffocation effects.
    public static AudioClip Heartbeat() => Get("Heartbeat", () =>
    {
        float[] s = new float[(int)(Rate * 0.6f)];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            s[i] = Thump(t, 0f, 55f) + Thump(t, 0.22f, 48f) * 0.7f;
        }

        return Make("Heartbeat", s);
    });

    // Dread under everything when something is near (Immersion): a low beating hum just above hearing's floor (two
    // detuned sines that swell and sink), a dull rumble of filtered noise, and far above, two dissonant thin tones
    // that drift in and out. 8 s, crossfaded into itself.
    public static AudioClip DreadDroneLoop() => Get("DreadDrone", () =>
    {
        float[] s = new float[Rate * 8];
        System.Random random = new System.Random(7);
        float rumble = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 8f);
            float low = (Mathf.Sin(2f * Mathf.PI * 41f * t) + Mathf.Sin(2f * Mathf.PI * 41.7f * t) * 0.8f
                + Mathf.Sin(2f * Mathf.PI * 55.2f * t) * 0.35f) * swell;
            rumble += ((float)random.NextDouble() * 2f - 1f - rumble) * 0.012f;
            float high = (Mathf.Sin(2f * Mathf.PI * 739f * t) + Mathf.Sin(2f * Mathf.PI * 783.5f * t))
                * 0.025f * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * t / 4f));
            s[i] = low * 0.5f + rumble * 2.2f + high;
        }

        return Loop("DreadDrone", s, 1.5f);
    });

    // The ringing in the ears before passing out: two close high tones beating slowly against each other.
    public static AudioClip RingingLoop() => Get("Ringing", () =>
    {
        float[] s = new float[Rate * 4];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            s[i] = Mathf.Sin(2f * Mathf.PI * 2900f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2903.5f * t) * 0.35f
                + Mathf.Sin(2f * Mathf.PI * 1450f * t) * 0.08f;
        }

        return Loop("Ringing", s, 0.5f);
    });

    // Keypad and alarm tones: a short square-ish beep at the given pitch.
    public static AudioClip Beep(float frequency) => Get("Beep" + (int)frequency, () =>
    {
        float[] s = new float[(int)(Rate * 0.12f)];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float tone = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t)) * 0.4f + Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.6f;
            s[i] = tone * Mathf.Clamp01(t * 400f) * Mathf.Clamp01((0.12f - t) * 200f);
        }

        return Make("Beep" + (int)frequency, s);
    });

    // Wrong code: a low double buzz.
    public static AudioClip Buzz() => Get("Buzz", () =>
    {
        float[] s = new float[(int)(Rate * 0.5f)];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float gate = (t < 0.2f || (t > 0.28f && t < 0.48f)) ? 1f : 0f;
            s[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 110f * t)) * gate * 0.6f;
        }

        return Make("Buzz", s);
    });

    // Something with claws behind the wall: bursts of scraped, band-limited noise.
    public static AudioClip Scratch() => Get("Scratch", () =>
    {
        float[] s = new float[(int)(Rate * 1.6f)];
        System.Random random = new System.Random(31);
        float band = 0f, low = 0f;
        for (int stroke = 0; stroke < 4; stroke++)
        {
            int start = (int)(Rate * (0.05f + stroke * 0.34f + (float)random.NextDouble() * 0.08f));
            int length = (int)(Rate * (0.16f + (float)random.NextDouble() * 0.1f));
            for (int k = 0; k < length && start + k < s.Length; k++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                band += (white - band) * 0.5f;
                low += (band - low) * 0.12f;
                float grain = random.NextDouble() < 0.08 ? 2.5f : 1f;
                float envelope = Mathf.Sin(Mathf.PI * k / length);
                s[start + k] += (band - low) * grain * envelope;
            }
        }

        return Make("Scratch", s);
    });

    // A dry cough through the throat: two noisy, falling bursts.
    public static AudioClip Cough() => Get("Cough", () =>
    {
        float[] s = new float[(int)(Rate * 0.9f)];
        System.Random random = new System.Random(41);
        float low = 0f;
        foreach (float start in new[] { 0f, 0.38f })
        {
            int a = (int)(Rate * start);
            int length = (int)(Rate * 0.3f);
            for (int k = 0; k < length && a + k < s.Length; k++)
            {
                float t = k / (float)Rate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * Mathf.Lerp(0.35f, 0.08f, t / 0.3f);
                float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(190f, 120f, t / 0.3f) * t) * 0.35f;
                s[a + k] += (low + body * low) * Mathf.Exp(-t * 12f) * Mathf.Clamp01(t * 300f);
            }
        }

        return Make("Cough", s);
    });

    // A wall giving way: a hard crack, splintering and a long collapse rumble.
    public static AudioClip Crash() => Get("Crash", () =>
    {
        float[] s = new float[Rate * 3];
        System.Random random = new System.Random(53);
        float low = 0f, mid = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.03f;
            mid += (white - mid) * 0.3f;
            float crack = Mathf.Exp(-t * 14f) * white;
            float splinter = random.NextDouble() < 0.004 * Mathf.Exp(-t * 2f) ? 1.5f : 0f;
            float rumble = low * 9f * Mathf.Exp(-t * 1.1f);
            s[i] = crack + mid * splinter + rumble;
        }

        return Make("Crash", s);
    });

    private static float Thump(float t, float start, float frequency)
    {
        float local = t - start;
        if (local < 0f)
        {
            return 0f;
        }

        float envelope = Mathf.Exp(-local * 18f) * Mathf.Clamp01(local * 400f);
        return Mathf.Sin(2f * Mathf.PI * frequency * local) * envelope * 0.9f;
    }

    private static void AddPing(float[] s, int start, float frequency, float decaySeconds, float gain)
    {
        int length = Mathf.Min(s.Length - start, (int)(Rate * decaySeconds * 6f));
        for (int k = 0; k < length; k++)
        {
            float t = k / (float)Rate;
            // Falling pitch is what makes a drop sound like water.
            float f = frequency * (1f + 1.5f * Mathf.Exp(-t / decaySeconds));
            s[start + k] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / decaySeconds) * gain;
        }
    }

    private static AudioClip Get(string name, System.Func<AudioClip> build)
    {
        if (!Cache.TryGetValue(name, out AudioClip clip) || clip == null)
        {
            clip = build();
            Cache[name] = clip;
        }

        return clip;
    }

    private static AudioClip Loop(string name, float[] samples, float crossfadeSeconds)
    {
        int fade = (int)(Rate * crossfadeSeconds);
        int length = samples.Length - fade;
        float[] looped = new float[length];
        System.Array.Copy(samples, looped, length);
        for (int i = 0; i < fade; i++)
        {
            float w = i / (float)fade;
            looped[i] = samples[i] * w + samples[length + i] * (1f - w);
        }

        return Make(name, looped);
    }

    private static AudioClip Make(string name, float[] samples)
    {
        float peak = 0.0001f;
        foreach (float v in samples)
        {
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }

        float gain = 0.9f / peak;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }

        AudioClip clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
