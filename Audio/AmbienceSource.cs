using UnityEngine;

// Every ambient sound of the world outside (rain, thunder, sea, wind) goes through this, so none can be too
// loud again: its level lives in GameTuning (Som), and when the listener is under cover it is heard through
// the roof and walls, quieter and duller (a low-pass). "Under cover" is a ray straight up from the listener
// that hits something within 12 m (a ceiling, the roof, the ground-floor slab seen from the basement), so a
// new building needs no zones. Put it next to the AudioSource; its volume is driven from here, and one-shots
// played on the same source (thunder) scale with it. Rain also has an inside layer: the recorded rain on the
// roof (SoundLibrary), heard only under cover at GameTuning.roofRainVolume. Sea with no clip of its own
// plays the recorded sea from the SoundLibrary.
[RequireComponent(typeof(AudioSource))]
public sealed class AmbienceSource : MonoBehaviour
{
    public enum Kind { Rain, Thunder, Sea, Wind }

    [SerializeField]
    private Kind kind;

    private const float CoverRayLength = 12f;

    private static int coverFrame = -1;
    private static bool coverHit;
    private static AudioListener listener;

    private AudioSource source;
    private AudioLowPassFilter lowPass;
    private float covered = -1f;
    private AudioSource insideLayer;

    // Rain sources playing in the scene (the mask's visor takes drops only while it rains and the sky is open).
    public static int RainSources { get; private set; }

    private void OnEnable()
    {
        if (kind == Kind.Rain)
        {
            RainSources++;
        }
    }

    private void OnDisable()
    {
        if (kind == Kind.Rain)
        {
            RainSources--;
        }
    }

    public void SetKind(Kind value)
    {
        kind = value;
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        lowPass = GetComponent<AudioLowPassFilter>();
        if (lowPass == null)
        {
            lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        }

        SoundLibrary library = SoundLibrary.Instance;
        if (kind == Kind.Sea && source.clip == null && library.seaLoop != null)
        {
            source.clip = library.seaLoop;
            source.loop = true;
            source.Play();
        }

        if (kind == Kind.Rain && library.rainRoofInsideLoop != null)
        {
            GameObject layer = new GameObject("RoofRain");
            layer.transform.SetParent(transform, false);
            insideLayer = layer.AddComponent<AudioSource>();
            insideLayer.clip = library.rainRoofInsideLoop;
            insideLayer.loop = true;
            insideLayer.spatialBlend = 0f;
            insideLayer.volume = 0f;
            insideLayer.time = Random.Range(0f, library.rainRoofInsideLoop.length);
            insideLayer.Play();
        }
    }

    // Whether the listener has a roof over it this frame (computed once per frame for all sources).
    public static bool ListenerCovered()
    {
        if (coverFrame == Time.frameCount)
        {
            return coverHit;
        }

        coverFrame = Time.frameCount;
        if (listener == null)
        {
            listener = FindAnyObjectByType<AudioListener>();
        }

        coverHit = listener != null && Physics.Raycast(listener.transform.position + Vector3.up * 0.05f, Vector3.up, CoverRayLength, ~0, QueryTriggerInteraction.Ignore);
        return coverHit;
    }

    private void Update()
    {
        GameTuning tuning = GameTuning.Instance;
        float target = ListenerCovered() ? 1f : 0f;
        // Stepping out of a door opens the sound up over a second, not in one frame.
        covered = covered < 0f ? target : Mathf.MoveTowards(covered, target, Time.deltaTime * 1.2f);
        source.volume = tuning.AmbienceLevel(kind) * Mathf.Lerp(1f, tuning.coveredAmbienceVolume, covered);
        lowPass.cutoffFrequency = Mathf.Lerp(22000f, tuning.coveredAmbienceCutoff, covered);
        if (insideLayer != null)
        {
            insideLayer.volume = tuning.roofRainVolume * covered;
        }
    }
}
