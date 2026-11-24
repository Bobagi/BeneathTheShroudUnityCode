using UnityEngine;

// The courtyard's red palm (GDD gallery, Dayse 11/10: "pulsa como um coração", "veias e artérias, movimentação de
// líquido"): it swells in a double beat (lub-dub) and rests, a little wider than taller, at its own pace, with a deep
// wet thump on each swell when it has a beat sound.
public sealed class HeartbeatPulse : MonoBehaviour
{
    [SerializeField]
    private float beatsPerMinute = 38f;

    [SerializeField]
    private float swell = 0.05f;

    [SerializeField]
    private AudioClip beat;

    private Vector3 rest;
    private float phase;
    private float lastT;
    private AudioSource source;

    public void Configure(AudioClip beatClip)
    {
        beat = beatClip;
    }

    private void Awake()
    {
        rest = transform.localScale;
        phase = Random.value;
        if (beat != null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.minDistance = 0.8f;
            source.maxDistance = 9f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.playOnAwake = false;
        }
    }

    private void Update()
    {
        float t = Mathf.Repeat(Time.time * beatsPerMinute / 60f + phase, 1f);
        // Two quick swells (the second smaller) in the first third of the cycle, then rest.
        float pulse = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.045f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((t - 0.24f) / 0.05f, 2f));
        float s = swell * pulse;
        transform.localScale = new Vector3(rest.x * (1f + s), rest.y * (1f + s * 0.4f), rest.z * (1f + s));
        if (source != null)
        {
            if (Crossed(lastT, t, 0.05f))
            {
                source.PlayOneShot(beat, 0.55f);
            }
            else if (Crossed(lastT, t, 0.21f))
            {
                source.PlayOneShot(beat, 0.32f);
            }
        }

        lastT = t;
    }

    private static bool Crossed(float from, float to, float mark) => from < to ? from < mark && to >= mark : from < mark || to >= mark;
}
