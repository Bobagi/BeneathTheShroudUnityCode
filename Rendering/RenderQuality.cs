using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// What each quality level changes beyond its URP asset (MSAA, render scale and shadows live there, set by
// PerformanceSettings). All the levels share one renderer, so without this the lower levels still paid the full
// volumetric fog and SSAO, the two biggest costs measured in the player sweep of 10/10 (PerfSweep):
//   - volumetric fog: a global volume caps its ray-march steps (the menu used the package default of 128);
//   - SSAO: fewer samples on the middle levels, off on Fast;
//   - SMAA on the game cameras whenever the level has no MSAA.
// Runs by itself after the first scene loads and again whenever the quality level changes.
public static class RenderQuality
{
    private struct Tier
    {
        public int FogSteps;
        public bool SsaoOn;
        public bool SsaoLowSamples;
    }

    private static Volume capVolume;
    private static VolumetricFogVolumeComponent fogCap;
    private static ScriptableRendererFeature ssao;
    private static object ssaoSettings;
    private static FieldInfo ssaoSamplesField;
    private static object ssaoSamplesOriginal;
    private static bool ssaoActiveOriginal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        GameObject holder = new GameObject("[RenderQuality]");
        Object.DontDestroyOnLoad(holder);
        capVolume = holder.AddComponent<Volume>();
        capVolume.isGlobal = true;
        capVolume.priority = 1000f;
        capVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        fogCap = capVolume.sharedProfile.Add<VolumetricFogVolumeComponent>();

        FindSsao();
        QualitySettings.activeQualityLevelChanged += (previous, current) => Apply();
        RenderPipelineManager.beginCameraRendering += SetCameraAntialiasing;
#if UNITY_EDITOR
        // The SSAO feature belongs to the renderer asset: put it back as it was when leaving Play.
        UnityEditor.EditorApplication.playModeStateChanged += state =>
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                RestoreSsao();
            }
        };
#endif
        Apply();
    }

    private static Tier Current()
    {
        switch (QualitySettings.names[QualitySettings.GetQualityLevel()])
        {
            case "Fantastic": return new Tier { FogSteps = 32, SsaoOn = true };
            case "Beautiful": return new Tier { FogSteps = 24, SsaoOn = true, SsaoLowSamples = true };
            case "Good": return new Tier { FogSteps = 24, SsaoOn = true, SsaoLowSamples = true };
            default: return new Tier { FogSteps = 16, SsaoOn = false };
        }
    }

    public static void Apply()
    {
        Tier tier = Current();
        if (fogCap != null)
        {
            fogCap.maxSteps.Override(tier.FogSteps);
        }

        if (ssao != null)
        {
            ssao.SetActive(tier.SsaoOn && ssaoActiveOriginal);
            if (ssaoSamplesField != null)
            {
                ssaoSamplesField.SetValue(ssaoSettings, tier.SsaoLowSamples ? System.Enum.Parse(ssaoSamplesField.FieldType, "Low") : ssaoSamplesOriginal);
            }
        }
    }

    private static void FindSsao()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset))
        {
            return;
        }

        FieldInfo listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        if (!(listField?.GetValue(asset) is ScriptableRendererData[] renderers))
        {
            return;
        }

        foreach (ScriptableRendererData data in renderers)
        {
            List<ScriptableRendererFeature> features = data != null ? data.rendererFeatures : null;
            if (features == null)
            {
                continue;
            }

            foreach (ScriptableRendererFeature feature in features)
            {
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    ssao = feature;
                    ssaoActiveOriginal = feature.isActive;
                    ssaoSettings = feature.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(feature);
                    ssaoSamplesField = ssaoSettings?.GetType().GetField("Samples", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    ssaoSamplesOriginal = ssaoSamplesField?.GetValue(ssaoSettings);
                    return;
                }
            }
        }
    }

    private static void RestoreSsao()
    {
        if (ssao == null)
        {
            return;
        }

        ssao.SetActive(ssaoActiveOriginal);
        if (ssaoSamplesField != null)
        {
            ssaoSamplesField.SetValue(ssaoSettings, ssaoSamplesOriginal);
        }
    }

    // MSAA comes from the URP asset of the level; with none, the game cameras get SMAA (measured: free next to MSAA).
    private static void SetCameraAntialiasing(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.Game || !(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset asset))
        {
            return;
        }

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (data == null || data.renderType != CameraRenderType.Base)
        {
            return;
        }

        AntialiasingMode wanted = asset.msaaSampleCount > 1 ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        if (data.antialiasing != wanted && (data.antialiasing == AntialiasingMode.None || data.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing))
        {
            data.antialiasing = wanted;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
