using UnityEngine;

// One light of an electric fitting of the house (the key light at the bulb, or its bounce fill). Keeps the
// value the builder gave it and applies the GameTuning multipliers on top, in the editor too, so every
// fitting in every scene is tuned from one place (Horror > Ajustes do jogo > Luminárias da casa).
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public sealed class TunedLamp : MonoBehaviour
{
    [SerializeField]
    private float baseIntensity = 1f;

    [SerializeField]
    private float baseRange = 5f;

    [SerializeField]
    private bool bounce;

    // FillCulling: how much of a fill light the player sees (1 = all, eased to 0 before it is switched off).
    private float visibility = 1f;

    public bool IsBounce => bounce;

    // What the house's supply gives the fittings (PowerGrid: 1 on the mains, about a third on the emergency
    // generator the player starts in the generator room).
    public static float GridLevel { get; private set; } = 1f;

    public static void ResetGridLevel() => GridLevel = 1f;

    public static void SetGridLevel(float level)
    {
        GridLevel = level;
        foreach (TunedLamp lamp in FindObjectsByType<TunedLamp>(FindObjectsInactive.Include))
        {
            lamp.Apply();
        }
    }

    public float Range => baseRange * GameTuning.Instance.lampRange;

    public void SetVisibility(float weight)
    {
        if (visibility != weight)
        {
            visibility = weight;
            Apply();
        }
    }

    public void Configure(float intensity, float range, bool isBounce)
    {
        baseIntensity = intensity;
        baseRange = range;
        bounce = isBounce;
        Apply();
    }

    private void OnEnable()
    {
        GameTuning.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameTuning.Changed -= Apply;
    }

    private void Apply()
    {
        GameTuning tuning = GameTuning.Instance;
        Light lamp = GetComponent<Light>();
        float intensity = baseIntensity * tuning.lampBrightness * GridLevel * (bounce ? tuning.lampBounce * visibility : 1f);
        lamp.range = baseRange * tuning.lampRange;
        FlickerLight flicker = GetComponent<FlickerLight>();
        if (flicker != null && Application.isPlaying)
        {
            flicker.SetBaseIntensity(intensity);
        }
        else
        {
            lamp.intensity = intensity;
        }

        VolumetricAdditionalLight glow = GetComponent<VolumetricAdditionalLight>();
        if (glow != null)
        {
            glow.Scattering = tuning.lampFogGlow;
        }
    }
}
