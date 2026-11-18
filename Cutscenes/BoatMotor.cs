using UnityEngine;

// The outboard motor of the arrival boat: a two-stroke buzz whose pitch and loudness follow the boat's speed
// (idling when it stops at the pier). Synthesised until a recorded motor is found (game-audio skill: an outboard
// motor, CC0); then it goes in `recorded` and the same speed mapping applies to it.
[RequireComponent(typeof(AudioSource))]
public sealed class BoatMotor : MonoBehaviour
{
    [SerializeField]
    private AudioClip recorded;

    [SerializeField]
    private float fullSpeed = 6f;

    [SerializeField]
    private float volume = 0.55f;

    private const int Rate = 44100;

    private AudioSource source;
    private Vector3 last;
    private float speed;

    private void Start()
    {
        source = GetComponent<AudioSource>();
        source.clip = recorded != null ? recorded : Synthesise();
        source.loop = true;
        source.Play();
        last = transform.position;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        float now = (transform.position - last).magnitude / dt;
        last = transform.position;
        speed = Mathf.Lerp(speed, now, 1f - Mathf.Exp(-2f * dt));
        float throttle = Mathf.Clamp01(speed / fullSpeed);
        source.pitch = Mathf.Lerp(0.7f, 1.15f, throttle);
        source.volume = volume * Mathf.Lerp(0.35f, 1f, throttle);
    }

    // Two seconds that loop seamlessly: a firing pulse at a whole number of cycles (so the end meets the start),
    // its harmonics, exhaust rumble and a little mechanical noise.
    private static AudioClip Synthesise()
    {
        const int seconds = 2;
        const float firing = 46f;
        float[] s = new float[Rate * seconds];
        System.Random random = new System.Random(5);
        float noise = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)Rate;
            float phase = t * firing % 1f;
            float pulse = Mathf.Exp(-phase * 9f) - 0.12f;
            float harmonics = 0.35f * Mathf.Sin(2f * Mathf.PI * firing * 2f * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * firing * 3f * t);
            noise += ((float)(random.NextDouble() * 2.0 - 1.0) - noise) * 0.2f;
            s[i] = (pulse * 0.55f + harmonics * 0.4f + noise * 0.25f) * 0.5f;
        }

        AudioClip clip = AudioClip.Create("OutboardMotor", s.Length, 1, Rate, false);
        clip.SetData(s, 0);
        return clip;
    }
}
