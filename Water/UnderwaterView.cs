using UnityEngine;
using UnityEngine.Rendering;

// Sits on the camera. When the lens drops under a water surface: the underwater Volume fades in
// (murky teal grade, lens warp, heavy vignette), scene fog closes to a few metres, everything heard
// goes through a low-pass filter, a pressure drone fades up, motes and dirt flakes drift past the lens
// (taking turns with the dust and falling specks of the air) and the diver exhales a stream of bubbles
// every few seconds. Surfacing reverses it with a splash.
[RequireComponent(typeof(Camera))]
public sealed class UnderwaterView : MonoBehaviour
{
    [SerializeField]
    private Volume underwaterVolume;

    [SerializeField]
    private ParticleSystem motes;

    [SerializeField]
    private ParticleSystem exhaleBubbles;

    [Tooltip("Emit only under water (contamination flakes).")]
    [SerializeField]
    private ParticleSystem[] waterParticles = new ParticleSystem[0];

    [Tooltip("Emit only in air (dust, falling specks).")]
    [SerializeField]
    private ParticleSystem[] airParticles = new ParticleSystem[0];

    [SerializeField]
    private AudioLowPassFilter lowPass;

    [SerializeField]
    private AudioSource droneSource;

    [SerializeField]
    private AudioSource splashSource;

    [Header("Fog")]
    // Exponential fog: things dissolve past about 8 m, as the GDD asks (6 to 8 m of visibility).
    [SerializeField]
    private float underwaterFogDensity = 0.13f;

    [SerializeField]
    private float blendSpeed = 7f;

    private Color airFogColor;
    private float airFogDensity;
    private FogMode airFogMode;
    private float blend;
    private float visibility = 12f;
    private float exhaleTimer = 2f;
    private bool wasUnder;

    public bool IsUnder { get; private set; }

    // 0..1 extra deafness on top of the water's low-pass: set by SuffocationEffects as the player fades.
    public float Muffle { get; set; }

    // Nothing left to breathe out: the calm exhale stream stops (choking bursts come from Exhale).
    public bool OutOfBreath { get; set; }

    public void Exhale(int bubbleCount, float volume)
    {
        if (exhaleBubbles != null)
        {
            exhaleBubbles.Emit(bubbleCount);
        }

        if (splashSource != null)
        {
            splashSource.PlayOneShot(ProceduralAudio.Bubbles(), volume);
        }
    }

    private void Start()
    {
        airFogColor = RenderSettings.fogColor;
        airFogDensity = RenderSettings.fogDensity;
        airFogMode = RenderSettings.fogMode;
        if (droneSource != null)
        {
            AudioClip recorded = SoundLibrary.Instance.underwaterLoop;
            droneSource.clip = recorded != null ? recorded : ProceduralAudio.UnderwaterLoop();
            droneSource.loop = true;
            droneSource.volume = 0f;
            droneSource.Play();
        }

        Apply(0f);
    }

    private void LateUpdate()
    {
        IsUnder = WaterVolume.IsUnderwater(transform.position);
        WaterVolume water = IsUnder ? WaterVolume.Find(transform.position) : null;
        if (water != null)
        {
            // Ease between rooms of different murk instead of popping at a doorway.
            visibility = Mathf.MoveTowards(visibility, GameTuning.Instance.VisibilityFor(water.name), Time.deltaTime * 6f);
        }
        // Snap across the surface quickly, but never pop.
        blend = Mathf.MoveTowards(blend, IsUnder ? 1f : 0f, Time.deltaTime * blendSpeed);
        Apply(blend);

        if (IsUnder != wasUnder && splashSource != null)
        {
            AudioClip surfacing = SoundLibrary.Instance.emergeHeardUnderwater;
            splashSource.PlayOneShot(IsUnder ? ProceduralAudio.Bubbles() : surfacing != null ? surfacing : ProceduralAudio.Splash(), IsUnder ? 0.7f : 0.45f);
        }

        if (motes != null)
        {
            ParticleSystem.EmissionModule emission = motes.emission;
            emission.enabled = IsUnder;
        }

        SetEmission(waterParticles, IsUnder);
        SetEmission(airParticles, !IsUnder);

        if (IsUnder)
        {
            exhaleTimer -= Time.deltaTime;
            if (exhaleTimer <= 0f)
            {
                exhaleTimer = Random.Range(3.5f, 5.5f);
                if (!OutOfBreath)
                {
                    Exhale(Random.Range(10, 18), 0.35f);
                }
            }
        }

        wasUnder = IsUnder;
    }

    private static void SetEmission(ParticleSystem[] systems, bool on)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
            {
                ParticleSystem.EmissionModule emission = systems[i].emission;
                emission.enabled = on;
            }
        }
    }

    private void Apply(float t)
    {
        if (underwaterVolume != null)
        {
            underwaterVolume.weight = t;
        }

        // Under water the fog is linear: past the room's visibility distance it is fully opaque, so
        // murky water hides everything beyond a few metres (exponential fog never quite gets there).
        bool under = t > 0.01f;
        RenderSettings.fogMode = under ? FogMode.Linear : airFogMode;
        RenderSettings.fogColor = Color.Lerp(airFogColor, GameTuning.Instance.underwaterFogColor, t);
        RenderSettings.fogDensity = Mathf.Lerp(airFogDensity, underwaterFogDensity, t);
        RenderSettings.fogStartDistance = 0.5f;
        RenderSettings.fogEndDistance = Mathf.Lerp(60f, visibility, t);

        if (lowPass != null)
        {
            lowPass.cutoffFrequency = Mathf.Lerp(Mathf.Lerp(22000f, 650f, t), 220f, Muffle);
        }

        if (droneSource != null)
        {
            droneSource.volume = t * GameTuning.Instance.underwaterAmbienceVolume;
        }
    }
}
