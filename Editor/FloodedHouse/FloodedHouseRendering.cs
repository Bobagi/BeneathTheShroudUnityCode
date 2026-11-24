using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Rendering quality for the flooded house, following the lighting research of 2026-09-29
// (Level Design Book "Hollywood darkness", Frictional's light-vs-dark notes, URP 17 docs):
// Forward+ so the lab's many emergency lights are not culled per object, HDR grading for ACES,
// SSAO to ground furniture in the dark, a dive torch with a real beam pattern (hot spot, reflector
// ring, dim spill: a procedural stand-in for an IES profile, which URP cannot read), wet
// reflections from box-projected probes, and lens dirt for bloom to catch.
public static partial class FloodedHouseBuilder
{
    // Neutral dive light, about 5400 K: warm enough to cut backscatter, cold against the lamps.
    private const float TorchKelvin = 5400f;

    // Every quality level gets the features the look depends on (the pause menu switches levels);
    // levels still differ in shadow resolution, MSAA, render scale and so on.
    private static void ConfigurePipeline()
    {
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            if (!(QualitySettings.GetRenderPipelineAssetAt(level) is UniversalRenderPipelineAsset pipeline))
            {
                continue;
            }

            pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
            pipeline.maxAdditionalLightsCount = 8;
            SerializedObject asset = new SerializedObject(pipeline);
            SetIfPresent(asset, "m_SupportsLightCookies", true);
            // Rendering layers: lets the oil lamps' flames skip their own bodies.
            SetIfPresent(asset, "m_SupportsLightLayers", true);
            if (level == QualitySettings.names.Length - 1)
            {
                SetIfPresent(asset, "m_SoftShadowQuality", 3);
            }

            asset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);

            StripNullRenderers(pipeline);
            SerializedProperty list = asset.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData renderer)
                {
                    renderer.renderingMode = RenderingMode.ForwardPlus;
                    EnsureSsao(renderer);
                    EnsureVolumetricFog(renderer);
                    EnsureMaskVisor(renderer);
                    EnsureSsgi(renderer);
                    EnsureAutoExposure(renderer);
                    EditorUtility.SetDirty(renderer);
                    // Bullet holes and blood (CombatEffectsSetup).
                    CombatEffectsSetup.EnsureDecalFeature();
                }
            }
        }

        AssetDatabase.SaveAssets();
    }

    // An empty slot in the pipeline asset's renderer list disables the GPU Resident Drawer for the whole level
    // ("GPUResidentDrawer Disabled. One or more Scriptable Renderer in the Render Pipeline Asset is null"). The
    // Fantastic asset got one twice (11/10 and again by 16/10, most likely the "+" of the Inspector list), so every
    // pass over the pipeline assets drops the empty slots first. The default renderer index is kept in range.
    public static void StripNullRenderers(UniversalRenderPipelineAsset pipeline)
    {
        SerializedObject so = new SerializedObject(pipeline);
        SerializedProperty list = so.FindProperty("m_RendererDataList");
        if (list == null)
        {
            return;
        }

        int removed = 0;
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue != null)
            {
                continue;
            }

            // Deleting an object reference element clears it first; the second call removes the slot.
            list.DeleteArrayElementAtIndex(i);
            if (i < list.arraySize && list.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                list.DeleteArrayElementAtIndex(i);
            }

            removed++;
        }

        if (removed == 0)
        {
            return;
        }

        SerializedProperty defaultIndex = so.FindProperty("m_DefaultRendererIndex");
        if (defaultIndex != null && defaultIndex.intValue >= list.arraySize)
        {
            defaultIndex.intValue = 0;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        Debug.Log("[FloodedHouse] " + pipeline.name + ": removed " + removed + " empty renderer slot(s) (they disable the GPU Resident Drawer).");
    }

    private static void SetIfPresent(SerializedObject so, string field, object value)
    {
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning("[FloodedHouse] Pipeline has no field " + field);
            return;
        }

        if (value is bool b)
        {
            property.boolValue = b;
        }
        else if (value is int i)
        {
            property.intValue = i;
        }
        else if (value is float f)
        {
            property.floatValue = f;
        }
    }

    // Volumetric fog (com.cqf.urpvolumetricfog, MIT, github.com/CristianQiu/Unity-URP-Volumetric-Light):
    // ray-marched fog lit by the main and additional lights, so beams read in the air and under water.
    // The feature is shared by the renderer; it does nothing where no volume enables the fog component.
    private static void EnsureVolumetricFog(UniversalRendererData renderer)
    {
        VolumetricFogRendererFeature fog = (VolumetricFogRendererFeature)renderer.rendererFeatures.Find(f => f is VolumetricFogRendererFeature);
        if (fog == null)
        {
            fog = ScriptableObject.CreateInstance<VolumetricFogRendererFeature>();
            fog.name = "VolumetricFog";
            AssetDatabase.AddObjectToAsset(fog, renderer);
            renderer.rendererFeatures.Add(fog);
            SerializedObject data = new SerializedObject(renderer);
            SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(fog, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // Created from code the feature never runs its Reset(), so its shaders must be set by hand.
        SerializedObject settings = new SerializedObject(fog);
        settings.FindProperty("downsampleDepthShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.cqf.urpvolumetricfog/Shaders/DownsampleDepth.shader");
        settings.FindProperty("volumetricFogShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.cqf.urpvolumetricfog/Shaders/VolumetricFog.shader");
        settings.ApplyModifiedPropertiesWithoutUndo();
        fog.SetActive(true);
    }

    // The gas mask seen from inside (MaskVisor.cs + Art/Shaders/MaskVisor.shader), after post-processing so the
    // rubber rim is not tone-mapped or bloomed. It draws nothing unless the player's camera sets _MaskVisor.
    private static void EnsureMaskVisor(UniversalRendererData renderer)
    {
        string path = ProjectPaths.Shaders + "/MaskVisor.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Horror/MaskVisor"));
            AssetDatabase.CreateAsset(material, path);
        }

        // Textures from SourceAssets/Tools/visor_textures.py: the frame in colour, the rest are data.
        string folder = ProjectPaths.Textures + "/Visor/";
        material.SetTexture("_FrameTex", VisorTexture(folder + "VisorFrame.png", true, TextureWrapMode.Clamp));
        material.SetTexture("_CrackTex", VisorTexture(folder + "VisorCrack.png", false, TextureWrapMode.Clamp));
        material.SetTexture("_DirtTex", VisorTexture(folder + "VisorDirt.png", false, TextureWrapMode.Repeat));
        EditorUtility.SetDirty(material);

        FullScreenPassRendererFeature visor = (FullScreenPassRendererFeature)renderer.rendererFeatures.Find(f => f is FullScreenPassRendererFeature && f.name == "MaskVisor");
        if (visor == null)
        {
            visor = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            visor.name = "MaskVisor";
            AssetDatabase.AddObjectToAsset(visor, renderer);
            renderer.rendererFeatures.Add(visor);
            SerializedObject data = new SerializedObject(renderer);
            SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(visor, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        visor.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        visor.fetchColorBuffer = true;
        visor.requirements = ScriptableRenderPassInput.Color;
        visor.passMaterial = material;
        visor.passIndex = 0;
        visor.SetActive(true);
        EditorUtility.SetDirty(visor);
    }

    private static Texture2D VisorTexture(string path, bool colour, TextureWrapMode wrap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.sRGBTexture != colour || importer.wrapMode != wrap || importer.maxTextureSize != 2048
            || importer.textureCompression != TextureImporterCompression.CompressedHQ))
        {
            importer.sRGBTexture = colour;
            importer.wrapMode = wrap;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // House: a faint haze of dust and damp, enough for lamp glow and the lightning through the window
    // to become shafts. Under water: dense, teal, strongly forward-scattering, so the torch is a beam.
    private static void ConfigureVolumetricFog(VolumeProfile profile, bool underwater)
    {
        VolumetricFogVolumeComponent fog = BattlefieldGet<VolumetricFogVolumeComponent>(profile);
        fog.enabled.Override(true);
        fog.enableMainLightContribution.Override(!underwater);
        fog.enableAdditionalLightsContribution.Override(true);
        fog.baseHeight.Override(-11f);
        fog.maximumHeight.Override(3.4f);
        fog.distance.Override(underwater ? 14f : 40f);
        // Density, scattering and attenuation live in GameTuning (TunedVolume re-applies them live).
        // A/B tests of 2026-09-29: in the house 0.22 was invisible in play (fog only shows where a light
        // marked Volumetric reaches it); under water 0.28 with the torch turned into a milky wall.
        GameTuning tuning = GameTuning.Instance;
        fog.density.Override(underwater ? tuning.underwaterFogDensity : tuning.houseFogDensity);
        fog.attenuationDistance.Override(underwater ? tuning.underwaterFogAttenuation : tuning.houseFogAttenuation);
        fog.anisotropy.Override(underwater ? 0.65f : 0.45f);
        fog.scattering.Override(underwater ? tuning.underwaterFogScattering : tuning.houseFogScattering);
        fog.tint.Override(underwater ? new Color(0.55f, 0.9f, 0.85f) : new Color(0.85f, 0.9f, 1f));
        fog.maxSteps.Override(underwater ? 64 : 48);
        fog.blurIterations.Override(2);
        EditorUtility.SetDirty(profile);
    }

    // Marks a light as one that should light the fog (costly: only lights that make a visible beam).
    private static void Volumetric(Light light, float scattering, float anisotropy, float radius = 0.2f)
    {
        VolumetricAdditionalLight beam = light.GetComponent<VolumetricAdditionalLight>() ?? light.gameObject.AddComponent<VolumetricAdditionalLight>();
        SetField(beam, "scattering", scattering);
        SetField(beam, "anisotropy", anisotropy);
        SetField(beam, "radius", radius);
    }

    // A renderer feature of type T on the renderer, added (and registered in its feature map) if missing.
    private static T EnsureFeature<T>(UniversalRendererData renderer, string name) where T : ScriptableRendererFeature
    {
        T feature = (T)renderer.rendererFeatures.Find(f => f is T);
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<T>();
            feature.name = name;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            SerializedObject data = new SerializedObject(renderer);
            SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        return feature;
    }

    // Screen-space bounce light (04/10; github.com/jiaozi158/UnitySSGIURP, MIT, embedded in Packages/ and patched for
    // Unity 6.5, which removed the compatibility-mode overrides). The torch and the lamps light the walls next to
    // what they hit, which the URP cannot do by itself (the lamps had a second, shadowless "bounce" light, which lit
    // through walls). It runs only where a volume enables it (the house's), so the cutscene and the trenches pay nothing.
    private static void EnsureSsgi(UniversalRendererData renderer)
    {
        ScreenSpaceGlobalIlluminationURP ssgi = EnsureFeature<ScreenSpaceGlobalIlluminationURP>(renderer, "SSGI");
        SerializedObject settings = new SerializedObject(ssgi);
        settings.FindProperty("m_Shader").objectReferenceValue = Shader.Find("Hidden/Lighting/ScreenSpaceGlobalIllumination");
        settings.FindProperty("m_ReflectionProbes").boolValue = false;
        settings.FindProperty("m_OverrideAmbientLighting").boolValue = true;
        settings.FindProperty("m_HighQualityUpscaling").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        ssgi.Create();
        ssgi.SetActive(true);
        EditorUtility.SetDirty(ssgi);
    }

    // Eye adaptation (Rendering/AutoExposureFeature.cs): only for cameras with an AutoExposure component.
    private static void EnsureAutoExposure(UniversalRendererData renderer)
    {
        AutoExposureFeature eye = EnsureFeature<AutoExposureFeature>(renderer, "AutoExposure");
        SerializedObject settings = new SerializedObject(eye);
        settings.FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/Horror/AutoExposure");
        settings.ApplyModifiedPropertiesWithoutUndo();
        eye.Create();
        eye.SetActive(true);
        EditorUtility.SetDirty(eye);
    }

    // The house's bounce light: half-resolution SSGI, two rays, denoised; it lights what the lamps and the torch
    // reach, it does not invent light (the ambient it replaces is near black at night).
    private static void ConfigureSsgi(VolumeProfile profile)
    {
        ScreenSpaceGlobalIlluminationVolume ssgi = BattlefieldGet<ScreenSpaceGlobalIlluminationVolume>(profile);
        GameTuning tuning = GameTuning.Instance;
        ssgi.enable.Override(tuning.ssgi);
        ssgi.indirectDiffuseLightingMultiplier.Override(tuning.ssgiIntensity);
        ssgi.quality.Override(ScreenSpaceGlobalIlluminationVolume.QualityMode.Medium);
        ssgi.fullResolutionSS.Override(false);
        ssgi.resolutionScaleSS.Override(0.5f);
        ssgi.sampleCount.Override(2);
        ssgi.maxRaySteps.Override(32);
        ssgi.depthBufferThickness.Override(0.12f);
        ssgi.denoiseSS.Override(true);
        ssgi.rayMiss.Override(ScreenSpaceGlobalIlluminationVolume.RayMarchingFallbackHierarchy.Sky);
        EditorUtility.SetDirty(profile);
    }

    // SSAO is shared by the renderer, so the trenches get it too (it only darkens contact creases).
    private static void EnsureSsao(UniversalRendererData renderer)
    {
        System.Type ssaoType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
        ScriptableRendererFeature ssao = renderer.rendererFeatures.Find(f => f != null && f.GetType() == ssaoType);
        if (ssao == null)
        {
            ssao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            renderer.rendererFeatures.Add(ssao);
            SerializedObject data = new SerializedObject(renderer);
            SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject settings = new SerializedObject(ssao);
        SetIfPresent(settings, "m_Settings.Intensity", 1.2f);
        SetIfPresent(settings, "m_Settings.Radius", 0.3f);
        SetIfPresent(settings, "m_Settings.DirectLightingStrength", 0.35f);
        SetIfPresent(settings, "m_Settings.Falloff", 60f);
        // Half resolution (measured 10/10 in a player build: 0.2 to 0.4 ms less, no visible change under the grain).
        SetIfPresent(settings, "m_Settings.Downsample", true);
        SetIfPresent(settings, "m_Settings.AfterOpaque", false);
        settings.ApplyModifiedPropertiesWithoutUndo();
        ssao.SetActive(true);
    }

    private static void ConfigureTorch(Light torch)
    {
        torch.color = Mathf.CorrelatedColorTemperatureToRGB(TorchKelvin);
        torch.spotAngle = GameTuning.Instance.torchAngle;
        torch.innerSpotAngle = GameTuning.Instance.TorchInnerAngle;
        torch.intensity = GameTuning.Instance.torchIntensity;
        torch.range = GameTuning.Instance.torchRange;
        torch.cookie = TorchCookie();
        torch.shadows = LightShadows.Soft;
        torch.shadowNearPlane = 0.2f;
        UniversalAdditionalLightData data = torch.GetComponent<UniversalAdditionalLightData>() ?? torch.gameObject.AddComponent<UniversalAdditionalLightData>();
        data.softShadowQuality = SoftShadowQuality.High;
        // The torch lights everything, oil-lamp bodies included (they are on their own layer).
        data.renderingLayers = uint.MaxValue;
        torch.renderingLayerMask = -1;
        // The torch beam is the star of the fog: full scattering, strongly forward.
        // Small origin fade: the fog should start right at the lens (TorchBeam fills the first metres).
        Volumetric(torch, 0.65f, 0.6f, 0.05f);
    }

    // Beam pattern of a real dive light: bright core, a faint reflector ring around it, a dim spill
    // that dies at the edge, and slight irregularity so it does not look like a perfect disc.
    private static Texture2D TorchCookie()
    {
        // Always regenerated: it is cheap, and tuning the pattern here must reach the asset.
        string path = ProjectPaths.Textures + "/Lights/DiveTorch_Cookie.png";
        {
            BattlefieldTerrainBuilder.EnsureFolder(ProjectPaths.Textures + "/Lights");
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float angle = Mathf.Atan2(v, u);
                    // 0.6 and wider (04/10): at 1 the core plus the spill passed 1 and was clipped flat, a white
                    // disc with a hard edge on any near wall (it read as a ball of light in front of the torch).
                    float core = 0.6f * Mathf.Exp(-r * r / 0.06f);
                    float ring = 0.32f * Mathf.Exp(-Mathf.Pow((r - 0.36f) / 0.05f, 2f));
                    float spill = 0.4f * Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
                    float ripple = 1f + 0.06f * Mathf.Sin(r * 60f) * Mathf.Clamp01(r * 2f);
                    float wobble = 1f + 0.08f * Mathf.PerlinNoise(Mathf.Cos(angle) * 3f + 5f, Mathf.Sin(angle) * 3f + 5f);
                    float edge = Mathf.Clamp01((0.98f - r) * 20f);
                    float value = Mathf.Clamp01((core + ring + spill) * ripple * wobble) * edge;
                    texture.SetPixel(x, y, new Color(value, value, value, value));
                }
            }

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Cookie;
            importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Smudges and specks on the "lens" (the mask glass): bloom lights them up around bright sources.
    private static Texture2D LensDirt()
    {
        string path = ProjectPaths.Textures + "/Lights/LensDirt_v2.png";
        if (!System.IO.File.Exists(path))
        {
            BattlefieldTerrainBuilder.EnsureFolder(ProjectPaths.Textures + "/Lights");
            const int width = 512, height = 288;
            Color[] pixels = new Color[width * height];
            System.Random random = new System.Random(77);
            for (int s = 0; s < 70; s++)
            {
                float cx = (float)random.NextDouble() * width, cy = (float)random.NextDouble() * height;
                // Few, faint large smears and many small specks: big bright discs read as a UI bug.
                float radius = 3f + (float)random.NextDouble() * (s < 8 ? 28f : 10f);
                float strength = 0.08f + (float)random.NextDouble() * 0.22f;
                for (int y = Mathf.Max(0, (int)(cy - radius)); y < Mathf.Min(height, (int)(cy + radius)); y++)
                {
                    for (int x = Mathf.Max(0, (int)(cx - radius)); x < Mathf.Min(width, (int)(cx + radius)); x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                        float a = strength * Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                        pixels[y * width + x] += new Color(a, a, a, 0f);
                    }
                }
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = 1f;
            }

            texture.SetPixels(pixels);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // One box-projected probe per room, captured once at start: wet floors and water surfaces reflect
    // their own room instead of the sky.
    private static void BuildReflectionProbes()
    {
        Transform probes = Group("ReflectionProbes");
        // The mansion: one per main room, the courtyard's gallery and the upper galleries.
        foreach ((string name, Rect room, float y0, float y1) in new[]
                 {
                     ("Hall", LunaManor.Hall, 0f, GC), ("Jantar", LunaManor.Jantar, 0f, GC), ("Estar", LunaManor.Estar, 0f, GC), ("Cozinha", LunaManor.Cozinha, 0f, GC),
                     ("Despensa", LunaManor.Despensa, 0f, GC), ("Lavanderia", LunaManor.Lavanderia, 0f, GC), ("Biblioteca", LunaManor.Biblioteca, 0f, GC),
                     ("Patio", LunaManor.Courtyard, LunaManor.GardenY, GC), ("GaleriaOeste", LunaManor.GalleryWest, UF, UC), ("GaleriaNorte", LunaManor.GalleryNorth, UF, UC),
                     ("GaleriaLeste", LunaManor.GalleryEast, UF, UC), ("Gerador2", LunaManor.Gerador2, UF, UC),
                 })
        {
            Probe(probes, name, M(room.xMin, y0, room.yMin), M(room.xMax, y1, room.yMax));
        }

        Probe(probes, "Basement", new Vector3(-6f, -4.5f, -5f), new Vector3(2f, -0.25f, 1f));
        Probe(probes, "Bedroom", new Vector3(2.3f, -6f, -4f), new Vector3(8.3f, -3.2f, 1f));
        Probe(probes, "Corridor", new Vector3(-2f, -10f, -18f), new Vector3(0f, -7.5f, -2f));
        Probe(probes, "AirPocket", new Vector3(0.25f, -10f, -12f), new Vector3(5.25f, -6.5f, -7f));
        Probe(probes, "TankRoom", new Vector3(-4f, -10f, -24f), new Vector3(2f, -7f, -18f));
    }

    private static void Probe(Transform parent, string name, Vector3 min, Vector3 max)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = (min + max) * 0.5f;
        ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.size = max - min;
        probe.boxProjection = true;
        probe.resolution = 128;
        probe.blendDistance = 0.5f;
        probe.intensity = 0.8f;
    }
}
