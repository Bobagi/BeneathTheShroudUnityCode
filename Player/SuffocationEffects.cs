using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Sits on the camera and makes running out of air unmistakable, so the player goes for air at once:
// - air under 25%: a faint red rim and the heartbeat starting;
// - air at zero: the last breath escapes in a burst of bubbles, the rim flashes red on every heartbeat,
//   colour drains, the image fringes and throbs, the world goes muffled, choking bubbles break out and
//   a ringing in the ears climbs as health runs down (AirSupply.Health);
// - breathing again: it all eases off as health comes back;
// - health gone: Blackout() closes the view to black while the heart slows and stops.
// Its own global Volume (runtime profile, higher priority than the house and underwater ones) blends in
// with the stress, so at rest it touches nothing. No allocations per frame.
[RequireComponent(typeof(Camera))]
public sealed class SuffocationEffects : MonoBehaviour
{
    [SerializeField]
    private AirSupply air;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private UnderwaterView view;

    private static readonly Color BloodRed = new Color(0.42f, 0f, 0.02f);
    private static readonly Color RedTint = new Color(1f, 0.76f, 0.72f);

    private const float WarnBelowAir = 0.25f;

    private VolumeProfile profile;
    private Volume volume;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;
    private ChromaticAberration aberration;
    private LensDistortion distortion;
    private AudioSource heart;
    private AudioSource ringing;
    private AudioClip thump;

    private float stress;
    private float blackout;
    private float beatTimer;
    private float beatPulse;
    private float chokeTimer;
    private bool wasSuffocating;

    private void Awake()
    {
        BuildVolume();
        thump = ProceduralAudio.Heartbeat();

        // Inner sounds: they skip the listener's low-pass so they stay clear while the world goes dull.
        GameObject audioObject = new GameObject("SuffocationAudio");
        audioObject.transform.SetParent(transform, false);
        heart = InnerSource(audioObject);
        ringing = InnerSource(audioObject);
        ringing.clip = ProceduralAudio.RingingLoop();
        ringing.loop = true;
        ringing.volume = 0f;
        ringing.Play();
    }

    private void OnDestroy()
    {
        if (profile != null)
        {
            Destroy(profile);
        }
    }

    // Losing consciousness: the view closes to black and the heartbeat slows to nothing.
    public IEnumerator Blackout(float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            blackout = t;
            yield return null;
        }

        blackout = 1f;
        Apply();
    }

    // Back to a clean view (retry from the checkpoint).
    public void Clear()
    {
        stress = 0f;
        blackout = 0f;
        beatPulse = 0f;
        beatTimer = 0f;
        wasSuffocating = false;
        Apply();
    }

    private void Update()
    {
        if (air == null)
        {
            return;
        }

        float warn = air.IsUnderwater ? Mathf.InverseLerp(WarnBelowAir, 0f, air.Normalized) : 0f;
        // On land a creature's blow lowers the same health: a lighter red than choking (Immersion already flashes the hit).
        float target = air.IsSuffocating ? 0.35f + 0.65f * air.Suffocation : Mathf.Max(warn * 0.3f, air.Suffocation * (air.IsUnderwater ? 0.7f : 0.3f));
        stress = Mathf.MoveTowards(stress, target, Time.deltaTime * (target > stress ? 1.5f : 0.35f));

        UpdateChoking();
        UpdateHeartbeat();
        Apply();
    }

    private void UpdateChoking()
    {
        bool suffocating = air.IsSuffocating && !air.IsUnconscious;
        if (view != null)
        {
            view.OutOfBreath = air.IsSuffocating;
        }

        if (suffocating && !wasSuffocating)
        {
            // The last of the air leaves the lungs at once.
            hud.ShowMessage(Loc.T("msg.no_air"), 4f);
            chokeTimer = Random.Range(1.2f, 2f);
            if (view != null)
            {
                view.Exhale(40, 0.9f);
            }
        }
        else if (suffocating)
        {
            chokeTimer -= Time.deltaTime;
            if (chokeTimer <= 0f)
            {
                // Convulsive swallows of water, closer together as the body fails.
                chokeTimer = Random.Range(1.4f, 2.8f) * Mathf.Lerp(1f, 0.5f, air.Suffocation);
                if (view != null)
                {
                    view.Exhale(Random.Range(6, 14), Mathf.Lerp(0.4f, 0.75f, air.Suffocation));
                }
            }
        }

        wasSuffocating = suffocating;
    }

    private void UpdateHeartbeat()
    {
        beatPulse = Mathf.MoveTowards(beatPulse, 0f, Time.deltaTime * 4f);
        if ((stress < 0.06f && blackout <= 0f) || blackout > 0.85f)
        {
            return;
        }

        beatTimer -= Time.deltaTime;
        if (beatTimer > 0f)
        {
            return;
        }

        // Racing while fighting for air; slowing to a stop while passing out.
        beatTimer = Mathf.Lerp(Mathf.Lerp(1f, 0.4f, stress), 1.8f, blackout);
        beatPulse = 1f;
        heart.PlayOneShot(thump, Mathf.Lerp(0.3f, 1f, stress) * (1f - blackout * 0.6f));
    }

    private void Apply()
    {
        float suffocation = air != null ? air.Suffocation : 0f;
        bool suffocating = air != null && air.IsSuffocating;
        float flash = beatPulse * (suffocating ? Mathf.Lerp(0.15f, 0.6f, stress) : 0.15f * stress);
        hud.SetDamageEdge((stress * 0.3f + flash) * (1f - blackout));

        volume.weight = Mathf.Clamp01(Mathf.Max(stress * 2.5f, blackout));
        vignette.intensity.value = Mathf.Clamp01(Mathf.Lerp(0.3f, 0.6f, stress) + beatPulse * 0.1f * stress + blackout * 0.6f);
        vignette.color.value = Color.Lerp(BloodRed, Color.black, blackout);
        vignette.smoothness.value = Mathf.Lerp(0.8f, 1f, blackout);
        colorAdjustments.saturation.value = Mathf.Max(-100f, -85f * stress - 15f * blackout);
        colorAdjustments.contrast.value = 20f * stress;
        colorAdjustments.colorFilter.value = Color.Lerp(Color.white, RedTint, stress) * (1f - blackout);
        aberration.intensity.value = Mathf.Clamp01(stress * 0.7f + beatPulse * 0.3f * stress);
        distortion.intensity.value = -0.3f * stress * beatPulse;

        if (view != null)
        {
            view.Muffle = Mathf.Clamp01(stress * 0.6f + blackout);
        }

        float ring = Mathf.InverseLerp(0.35f, 1f, suffocation) * 0.22f + blackout * 0.25f;
        ringing.volume = ring * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, blackout)));
    }

    private void BuildVolume()
    {
        GameObject volumeObject = new GameObject("SuffocationVolume");
        volumeObject.transform.SetParent(transform, false);
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;

        // Only the parameters driven here override; exposure stays with the brightness setting.
        vignette = profile.Add<Vignette>();
        vignette.intensity.overrideState = true;
        vignette.color.overrideState = true;
        vignette.smoothness.overrideState = true;
        colorAdjustments = profile.Add<ColorAdjustments>();
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.colorFilter.overrideState = true;
        aberration = profile.Add<ChromaticAberration>();
        aberration.intensity.overrideState = true;
        distortion = profile.Add<LensDistortion>();
        distortion.intensity.overrideState = true;
    }

    private static AudioSource InnerSource(GameObject host)
    {
        AudioSource source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.bypassListenerEffects = true;
        return source;
    }
}
