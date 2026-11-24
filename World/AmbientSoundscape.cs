using System.Collections;
using UnityEngine;

// Background bed of the battlefield: looping music and wind plus scattered one-shots (crows,
// distant machine guns, cries) placed around the listener so they come from all directions.
public sealed class AmbientSoundscape : MonoBehaviour
{
    [System.Serializable]
    private struct RandomLayer
    {
        public string label;
        public AudioClip[] clips;
        public Vector2 secondsBetween;
        public Vector2 volume;
        public Vector2 pitch;
        public float distance;
    }

    [SerializeField]
    private Transform listener;

    [SerializeField]
    private AudioSource musicSource;

    [SerializeField]
    private AudioSource windSource;

    [SerializeField]
    private float fadeInSeconds = 4f;

    [SerializeField]
    private RandomLayer[] randomLayers;

    private float musicTargetVolume;
    private float windTargetVolume;

    private void Start()
    {
        if (listener == null && Camera.main != null)
        {
            listener = Camera.main.transform;
        }

        musicTargetVolume = musicSource != null ? musicSource.volume : 0f;
        windTargetVolume = windSource != null ? windSource.volume : 0f;
        StartCoroutine(FadeIn());

        if (randomLayers == null)
        {
            return;
        }

        foreach (RandomLayer layer in randomLayers)
        {
            if (layer.clips != null && layer.clips.Length > 0)
            {
                StartCoroutine(LayerLoop(layer));
            }
        }
    }

    private IEnumerator FadeIn()
    {
        StartLoop(musicSource);
        StartLoop(windSource);
        float elapsed = 0f;
        while (elapsed < fadeInSeconds)
        {
            elapsed += Time.deltaTime;
            float k = elapsed / fadeInSeconds;
            if (musicSource != null)
            {
                musicSource.volume = musicTargetVolume * k;
            }

            if (windSource != null)
            {
                windSource.volume = windTargetVolume * k;
            }

            yield return null;
        }
    }

    private static void StartLoop(AudioSource source)
    {
        if (source != null && source.clip != null && !source.isPlaying)
        {
            source.volume = 0f;
            source.loop = true;
            source.Play();
        }
    }

    private IEnumerator LayerLoop(RandomLayer layer)
    {
        yield return new WaitForSeconds(Random.Range(0f, layer.secondsBetween.y));
        while (true)
        {
            if (listener != null)
            {
                Vector3 offset = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * layer.distance;
                GameObject go = new GameObject("Ambient_" + layer.label);
                go.transform.position = listener.position + offset + Vector3.up * Random.Range(0f, 8f);
                AudioSource source = go.AddComponent<AudioSource>();
                source.clip = layer.clips[Random.Range(0, layer.clips.Length)];
                source.spatialBlend = 1f;
                source.minDistance = layer.distance * 0.5f;
                source.maxDistance = layer.distance * 4f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                source.volume = Random.Range(layer.volume.x, layer.volume.y);
                source.pitch = Random.Range(layer.pitch.x, layer.pitch.y);
                source.Play();
                Destroy(go, source.clip.length / Mathf.Max(0.1f, source.pitch) + 0.5f);
            }

            yield return new WaitForSeconds(Random.Range(layer.secondsBetween.x, layer.secondsBetween.y));
        }
    }
}
