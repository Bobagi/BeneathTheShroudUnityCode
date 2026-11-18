using UnityEngine;

// What is heard in the menu besides the rain and the sea: now and then something calls from under the water. One
// component per voice (the near growls, the colossal roar far below), each with its own clips, rhythm and pitch.
// Quiet and rare: the menu should unsettle, not startle.
public sealed class MenuAmbience : MonoBehaviour
{
    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private AudioClip[] calls = new AudioClip[0];

    [SerializeField]
    private Vector2 intervalSeconds = new Vector2(18f, 34f);

    [SerializeField]
    [Range(0f, 1f)]
    private float volume = 1f;

    [Tooltip("Seconds after the menu opens before the first call.")]
    [SerializeField]
    private Vector2 firstDelay = new Vector2(6f, 11f);

    [SerializeField]
    private Vector2 pitchRange = new Vector2(0.94f, 1.04f);

    private float next;

    private void Start()
    {
        next = Time.unscaledTime + Random.Range(firstDelay.x, firstDelay.y);
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        if (t < next || calls.Length == 0 || source == null)
        {
            return;
        }

        next = t + Random.Range(intervalSeconds.x, intervalSeconds.y);
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(calls[Random.Range(0, calls.Length)], volume);
    }
}
