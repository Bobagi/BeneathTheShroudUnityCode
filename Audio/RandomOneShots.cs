using UnityEngine;

// Plays a sound now and then from this spot: drips in the basement, a creak in the walls. With no
// clip assigned it plays a water drip from the SoundLibrary.
[RequireComponent(typeof(AudioSource))]
public sealed class RandomOneShots : MonoBehaviour
{
    [SerializeField]
    private AudioClip[] clips;

    [SerializeField]
    private Vector2 intervalSeconds = new Vector2(1.5f, 5f);

    [SerializeField]
    private Vector2 volume = new Vector2(0.3f, 0.6f);

    [SerializeField]
    private Vector2 pitch = new Vector2(0.85f, 1.15f);

    private AudioSource source;
    private float timer;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        timer = Random.Range(intervalSeconds.x, intervalSeconds.y);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }

        timer = Random.Range(intervalSeconds.x, intervalSeconds.y);
        AudioClip clip = clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : SoundLibrary.Instance.Drip();
        source.pitch = Random.Range(pitch.x, pitch.y);
        source.PlayOneShot(clip, Random.Range(volume.x, volume.y));
    }
}
