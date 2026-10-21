using UnityEngine;
using UnityEngine.Rendering;

public sealed class DayNightCycleController : MonoBehaviour
{
    #region Properties and Fields
    [Header("Time")]
    [SerializeField]
    private bool stopTime = false;

    [SerializeField]
    private float fullDayDurationInSeconds = 900f;

    [Range(0f, 1f)]
    [SerializeField]
    private float timeOfDayNormalized;

    [Header("Sun")]
    [SerializeField]
    private Light sunDirectionalLight;

    [SerializeField]
    private Vector3 sunRotationOffsetEuler = new Vector3(-90f, 170f, 0f);

    [SerializeField]
    private Gradient sunColorByTime;

    [SerializeField]
    private AnimationCurve sunIntensityByTime;

    [SerializeField]
    private float sunIntensityMultiplier = 1f;

    [SerializeField]
    private LightShadows sunShadows = LightShadows.Soft;

    [SerializeField]
    private float sunShadowStrength = 1f;

    [Header("Moon")]
    [SerializeField]
    private Light moonDirectionalLight;

    [SerializeField]
    private Vector3 moonRotationOffsetEuler = new Vector3(-90f, 170f, 0f);

    [SerializeField]
    private Gradient moonColorByTime;

    [SerializeField]
    private AnimationCurve moonIntensityByTime;

    [SerializeField]
    private float moonIntensityMultiplier = 0.12f;

    [SerializeField]
    private LightShadows moonShadows = LightShadows.None;

    [SerializeField]
    private float moonShadowStrength = 1f;

    [Header("Environment")]
    [SerializeField]
    private AmbientMode ambientMode = AmbientMode.Flat;

    [SerializeField]
    private Gradient ambientColorByTime;

    [SerializeField]
    private AnimationCurve ambientIntensityByTime;

    [Header("Skybox Materials")]
    [SerializeField]
    private Material dawnSkyboxMaterial;

    [SerializeField]
    private Material daySkyboxMaterial;

    [SerializeField]
    private Material duskSkyboxMaterial;

    [SerializeField]
    private Material nightSkyboxMaterial;

    [Header("Skybox Blend")]
    [SerializeField]
    private Material skyboxBlendMaterial;

    [SerializeField]
    private float skyboxRotationOffsetDegrees;

    private string fromCubemapTexturePropertyName = "_DayCubemap";
    private string toCubemapTexturePropertyName = "_NightCubemap";
    private string blendNormalizedPropertyName = "_Blend";
    private string rotationDegreesPropertyName = "_Rotation";
    private string tintColorPropertyName = "_Tint";
    private string exposurePropertyName = "_Exposure";

    [Header("Skybox Appearance")]
    [SerializeField]
    private Color skyboxTintColor = Color.white;

    [SerializeField]
    private float skyboxExposure = 1f;

    [Header("Dynamic GI")]
    [SerializeField]
    private bool updateDynamicGI = true;

    [SerializeField]
    private float dynamicGIUpdateIntervalSeconds = 1.5f;

    private float dynamicGIUpdateElapsedSeconds;
    #endregion
    private void Reset()
    {
        timeOfDayNormalized = 0.25f;

        sunIntensityMultiplier = 1f;
        sunShadows = LightShadows.Soft;
        sunShadowStrength = 1f;

        moonIntensityMultiplier = 0.12f;
        moonShadows = LightShadows.None;
        moonShadowStrength = 1f;

        skyboxTintColor = Color.white;
        skyboxExposure = 1f;
        dynamicGIUpdateIntervalSeconds = 1.5f;

        sunIntensityByTime = CreateSunIntensityPreset();
        moonIntensityByTime = CreateMoonIntensityPreset();
        ambientIntensityByTime = CreateAmbientIntensityPreset();

        sunColorByTime = CreateSunColorPreset();
        moonColorByTime = CreateMoonColorPreset();
        ambientColorByTime = CreateAmbientColorPreset();
    }

    private void Update()
    {
        if (stopTime || fullDayDurationInSeconds <= 0.1f)
        {
            return;
        }

        timeOfDayNormalized = Mathf.Repeat(
            timeOfDayNormalized + Time.deltaTime / fullDayDurationInSeconds,
            1f
        );

        ApplySun(timeOfDayNormalized);
        ApplyMoon(timeOfDayNormalized);
        ApplyEnvironment(timeOfDayNormalized);
        ApplySkybox(timeOfDayNormalized);
        ApplyDynamicGIUpdate();
    }

    private void ApplySun(float timeNormalized)
    {
        if (sunDirectionalLight == null)
        {
            return;
        }

        float sunAngle = timeNormalized * 360f;
        sunDirectionalLight.transform.rotation = Quaternion.Euler(
            sunRotationOffsetEuler + new Vector3(sunAngle, 0f, 0f)
        );
        sunDirectionalLight.color = sunColorByTime.Evaluate(timeNormalized);
        sunDirectionalLight.intensity = Mathf.Max(
            0f,
            sunIntensityByTime.Evaluate(timeNormalized) * sunIntensityMultiplier
        );
        sunDirectionalLight.shadows = sunShadows;
        sunDirectionalLight.shadowStrength = Mathf.Clamp01(sunShadowStrength);
    }

    private void ApplyMoon(float timeNormalized)
    {
        if (moonDirectionalLight == null)
        {
            return;
        }

        float moonAngle = (timeNormalized * 360f) + 180f;
        moonDirectionalLight.transform.rotation = Quaternion.Euler(
            moonRotationOffsetEuler + new Vector3(moonAngle, 0f, 0f)
        );
        moonDirectionalLight.color = moonColorByTime.Evaluate(timeNormalized);
        moonDirectionalLight.intensity = Mathf.Max(
            0f,
            moonIntensityByTime.Evaluate(timeNormalized) * moonIntensityMultiplier
        );
        moonDirectionalLight.shadows = moonShadows;
        moonDirectionalLight.shadowStrength = Mathf.Clamp01(moonShadowStrength);
    }

    private void ApplyEnvironment(float timeNormalized)
    {
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambientColorByTime.Evaluate(timeNormalized);
        RenderSettings.ambientIntensity = Mathf.Max(
            0f,
            ambientIntensityByTime.Evaluate(timeNormalized)
        );
    }

    private void ApplySkybox(float timeNormalized)
    {
        if (skyboxBlendMaterial == null)
        {
            return;
        }

        if (
            dawnSkyboxMaterial == null
            || daySkyboxMaterial == null
            || duskSkyboxMaterial == null
            || nightSkyboxMaterial == null
        )
        {
            return;
        }

        if (!TryResolveCubemapFromSkyboxMaterial(dawnSkyboxMaterial, out Cubemap dawnCubemap))
        {
            return;
        }

        if (!TryResolveCubemapFromSkyboxMaterial(daySkyboxMaterial, out Cubemap dayCubemap))
        {
            return;
        }

        if (!TryResolveCubemapFromSkyboxMaterial(duskSkyboxMaterial, out Cubemap duskCubemap))
        {
            return;
        }

        if (!TryResolveCubemapFromSkyboxMaterial(nightSkyboxMaterial, out Cubemap nightCubemap))
        {
            return;
        }

        ResolveSkyboxBlendPair(
            timeNormalized,
            dawnCubemap,
            dayCubemap,
            duskCubemap,
            nightCubemap,
            out Cubemap fromCubemap,
            out Cubemap toCubemap,
            out float blendNormalized
        );

        float skyboxRotationDegrees = Mathf.Repeat(
            (timeNormalized * 360f) + skyboxRotationOffsetDegrees,
            360f
        );

        RenderSettings.skybox = skyboxBlendMaterial;
        skyboxBlendMaterial.SetTexture(fromCubemapTexturePropertyName, fromCubemap);
        skyboxBlendMaterial.SetTexture(toCubemapTexturePropertyName, toCubemap);
        skyboxBlendMaterial.SetFloat(blendNormalizedPropertyName, Mathf.Clamp01(blendNormalized));
        skyboxBlendMaterial.SetFloat(rotationDegreesPropertyName, skyboxRotationDegrees);

        if (skyboxBlendMaterial.HasProperty(tintColorPropertyName))
        {
            skyboxBlendMaterial.SetColor(tintColorPropertyName, skyboxTintColor);
        }

        if (skyboxBlendMaterial.HasProperty(exposurePropertyName))
        {
            skyboxBlendMaterial.SetFloat(exposurePropertyName, Mathf.Max(0f, skyboxExposure));
        }
    }

    private void ResolveSkyboxBlendPair(
        float timeNormalized,
        Cubemap dawnCubemap,
        Cubemap dayCubemap,
        Cubemap duskCubemap,
        Cubemap nightCubemap,
        out Cubemap fromCubemap,
        out Cubemap toCubemap,
        out float blendNormalized
    )
    {
        float quarterLength = 0.25f;

        if (timeNormalized < quarterLength)
        {
            float t = Mathf.InverseLerp(0f, quarterLength, timeNormalized);
            fromCubemap = nightCubemap;
            toCubemap = dawnCubemap;
            blendNormalized = SmoothBlend(t);
            return;
        }

        if (timeNormalized < quarterLength * 2f)
        {
            float t = Mathf.InverseLerp(quarterLength, quarterLength * 2f, timeNormalized);
            fromCubemap = dawnCubemap;
            toCubemap = dayCubemap;
            blendNormalized = SmoothBlend(t);
            return;
        }

        if (timeNormalized < quarterLength * 3f)
        {
            float t = Mathf.InverseLerp(quarterLength * 2f, quarterLength * 3f, timeNormalized);
            fromCubemap = dayCubemap;
            toCubemap = duskCubemap;
            blendNormalized = SmoothBlend(t);
            return;
        }

        {
            float t = Mathf.InverseLerp(quarterLength * 3f, 1f, timeNormalized);
            fromCubemap = duskCubemap;
            toCubemap = nightCubemap;
            blendNormalized = SmoothBlend(t);
        }
    }

    private float SmoothBlend(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private bool TryResolveCubemapFromSkyboxMaterial(Material skyboxMaterial, out Cubemap cubemap)
    {
        cubemap = null;

        if (skyboxMaterial == null)
        {
            return false;
        }

        cubemap = skyboxMaterial.GetTexture("_Tex") as Cubemap;
        return cubemap != null;
    }

    private void ApplyDynamicGIUpdate()
    {
        if (!updateDynamicGI)
        {
            return;
        }

        dynamicGIUpdateElapsedSeconds += Time.deltaTime;

        if (dynamicGIUpdateElapsedSeconds < dynamicGIUpdateIntervalSeconds)
        {
            return;
        }

        dynamicGIUpdateElapsedSeconds = 0f;
        DynamicGI.UpdateEnvironment();
    }

    private AnimationCurve CreateSunIntensityPreset()
    {
        AnimationCurve presetCurve = new AnimationCurve();
        presetCurve.AddKey(new Keyframe(0.00f, 0.00f));
        presetCurve.AddKey(new Keyframe(0.05f, 0.00f));
        presetCurve.AddKey(new Keyframe(0.18f, 0.15f));
        presetCurve.AddKey(new Keyframe(0.25f, 0.75f));
        presetCurve.AddKey(new Keyframe(0.35f, 1.00f));
        presetCurve.AddKey(new Keyframe(0.50f, 0.65f));
        presetCurve.AddKey(new Keyframe(0.60f, 0.35f));
        presetCurve.AddKey(new Keyframe(0.70f, 0.10f));
        presetCurve.AddKey(new Keyframe(0.75f, 0.03f));
        presetCurve.AddKey(new Keyframe(0.80f, 0.00f));
        presetCurve.AddKey(new Keyframe(1.00f, 0.00f));
        return presetCurve;
    }

    private AnimationCurve CreateMoonIntensityPreset()
    {
        AnimationCurve presetCurve = new AnimationCurve();
        presetCurve.AddKey(new Keyframe(0.00f, 0.65f));
        presetCurve.AddKey(new Keyframe(0.05f, 0.80f));
        presetCurve.AddKey(new Keyframe(0.15f, 0.55f));
        presetCurve.AddKey(new Keyframe(0.22f, 0.15f));
        presetCurve.AddKey(new Keyframe(0.25f, 0.05f));
        presetCurve.AddKey(new Keyframe(0.30f, 0.00f));
        presetCurve.AddKey(new Keyframe(0.70f, 0.00f));
        presetCurve.AddKey(new Keyframe(0.75f, 0.05f));
        presetCurve.AddKey(new Keyframe(0.82f, 0.30f));
        presetCurve.AddKey(new Keyframe(0.90f, 0.55f));
        presetCurve.AddKey(new Keyframe(1.00f, 0.65f));
        return presetCurve;
    }

    private AnimationCurve CreateAmbientIntensityPreset()
    {
        AnimationCurve presetCurve = new AnimationCurve();
        presetCurve.AddKey(new Keyframe(0.00f, 0.01f));
        presetCurve.AddKey(new Keyframe(0.05f, 0.02f));
        presetCurve.AddKey(new Keyframe(0.18f, 0.10f));
        presetCurve.AddKey(new Keyframe(0.25f, 0.35f));
        presetCurve.AddKey(new Keyframe(0.35f, 0.60f));
        presetCurve.AddKey(new Keyframe(0.50f, 0.40f));
        presetCurve.AddKey(new Keyframe(0.60f, 0.22f));
        presetCurve.AddKey(new Keyframe(0.70f, 0.08f));
        presetCurve.AddKey(new Keyframe(0.75f, 0.03f));
        presetCurve.AddKey(new Keyframe(0.80f, 0.01f));
        presetCurve.AddKey(new Keyframe(1.00f, 0.01f));
        return presetCurve;
    }

    private Gradient CreateSunColorPreset()
    {
        Gradient presetGradient = new Gradient();
        presetGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 0.55f, 0.30f), 0.20f),
                new GradientColorKey(new Color(1.00f, 0.95f, 0.85f), 0.35f),
                new GradientColorKey(new Color(1.00f, 0.95f, 0.90f), 0.50f),
                new GradientColorKey(new Color(0.90f, 0.45f, 0.25f), 0.70f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return presetGradient;
    }

    private Gradient CreateMoonColorPreset()
    {
        Gradient presetGradient = new Gradient();
        presetGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.55f, 0.65f, 0.90f), 0.00f),
                new GradientColorKey(new Color(0.65f, 0.75f, 1.00f), 0.10f),
                new GradientColorKey(new Color(0.55f, 0.65f, 0.90f), 0.90f),
                new GradientColorKey(new Color(0.55f, 0.65f, 0.90f), 1.00f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return presetGradient;
    }

    private Gradient CreateAmbientColorPreset()
    {
        Gradient presetGradient = new Gradient();
        presetGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.01f, 0.01f, 0.015f), 0.00f),
                new GradientColorKey(new Color(0.03f, 0.03f, 0.04f), 0.08f),
                new GradientColorKey(new Color(0.18f, 0.12f, 0.10f), 0.20f),
                new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 0.35f),
                new GradientColorKey(new Color(0.35f, 0.30f, 0.28f), 0.65f),
                new GradientColorKey(new Color(0.15f, 0.08f, 0.07f), 0.72f),
                new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 0.85f),
                new GradientColorKey(new Color(0.01f, 0.01f, 0.015f), 1.00f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return presetGradient;
    }
}
