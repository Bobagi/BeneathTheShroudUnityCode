using UnityEngine;

// The colour the hall's stained glass throws (GDD concept art, 13/10: "a janela do hall de entrada é vitral; a
// iluminação é colorida"): a spot outside each window, its cookie the glass itself, faint with the night's glow and
// flaring with every lightning strike (LightningStorm.Flashed). It also sets how much the glass glows (the
// Horror/StainedGlass material) and how the frosted courtyard door blurs, all from GameTuning ("Vitral e vidro fosco").
[ExecuteAlways]
[RequireComponent(typeof(Light))]
public sealed class StainedGlassLight : MonoBehaviour
{
    [SerializeField]
    private Material glass;

    [SerializeField]
    private Material frosted;

    private float flash;

    private static readonly int Backlight = Shader.PropertyToID("_Backlight");
    private static readonly int FlashGain = Shader.PropertyToID("_FlashGain");
    private static readonly int BlurPerMetre = Shader.PropertyToID("_BlurPerMetre");

    public void Configure(Material stainedGlass, Material frostedGlass)
    {
        glass = stainedGlass;
        frosted = frostedGlass;
        Apply();
    }

    private void OnEnable()
    {
        GameTuning.Changed += Apply;
        LightningStorm.Flashed += OnFlash;
        Apply();
    }

    private void OnDisable()
    {
        GameTuning.Changed -= Apply;
        LightningStorm.Flashed -= OnFlash;
    }

    private void OnFlash(float amount)
    {
        flash = amount;
        Apply();
    }

    private void Apply()
    {
        GameTuning tuning = GameTuning.Instance;
        Light spot = GetComponent<Light>();
        spot.intensity = tuning.stainedGlassLight + tuning.stainedGlassLightFlash * flash;
        VolumetricAdditionalLight fog = GetComponent<VolumetricAdditionalLight>();
        if (fog != null)
        {
            fog.Scattering = tuning.stainedGlassFog;
        }

        // The materials are shared by both windows (and the door): setting them twice is harmless.
        if (glass != null)
        {
            glass.SetFloat(Backlight, tuning.stainedGlassGlow);
            glass.SetFloat(FlashGain, tuning.stainedGlassFlash);
        }

        if (frosted != null)
        {
            frosted.SetFloat(BlurPerMetre, tuning.frostedBlurPerMetre);
        }
    }
}
