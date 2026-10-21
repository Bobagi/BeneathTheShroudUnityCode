using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Pushes the GameTuning fog, bounce light (SSGI) and underwater blur values into this Volume's profile, in the editor too,
// so moving a slider in the tuning asset changes the picture right away.
[ExecuteAlways]
[RequireComponent(typeof(Volume))]
public sealed class TunedVolume : MonoBehaviour
{
    public enum Kind { House, Underwater }

    [SerializeField]
    private Kind kind;

    public void SetKind(Kind value)
    {
        kind = value;
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
        VolumeProfile profile = GetComponent<Volume>().sharedProfile;
        if (profile == null)
        {
            return;
        }

        GameTuning tuning = GameTuning.Instance;
        bool under = kind == Kind.Underwater;
        if (profile.TryGet(out VolumetricFogVolumeComponent fog))
        {
            fog.density.Override(under ? tuning.underwaterFogDensity : tuning.houseFogDensity);
            fog.scattering.Override(under ? tuning.underwaterFogScattering : tuning.houseFogScattering);
            fog.attenuationDistance.Override(under ? tuning.underwaterFogAttenuation : tuning.houseFogAttenuation);
        }

        // Screen-space bounce light (SSGI, MIT package): on/off and strength from the tuning asset.
        if (profile.TryGet(out ScreenSpaceGlobalIlluminationVolume ssgi))
        {
            ssgi.enable.Override(tuning.ssgi && !under);
            ssgi.indirectDiffuseLightingMultiplier.Override(tuning.ssgiIntensity);
        }

        if (under && profile.TryGet(out DepthOfField blur))
        {
            blur.gaussianStart.Override(tuning.sharpDistance);
            blur.gaussianEnd.Override(Mathf.Max(tuning.sharpDistance + 0.5f, tuning.blurEndDistance));
        }
    }
}
