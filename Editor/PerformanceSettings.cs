using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Project-wide performance setup. Project goal (Gustavo, 2026-09-29): the best performance
// possible, using the machine to the fullest (multithreading and anything else that helps).
// Re-runnable from Horror > Performance > Aplicar configurações de desempenho.
//  - Graphics Jobs (Split): render command building spread across worker threads (DX12/Vulkan).
//  - Direct3D 12 first: needed for split graphics jobs; D3D11 stays as fallback.
//  - IL2CPP, "Optimize for speed" code generation and the Master C++ configuration for releases.
//  - Incremental GC: garbage collection spread over frames instead of one hitch.
//  - GPU Resident Drawer (Unity 6, needs Forward+) with GPU occlusion culling: draw submission on
//    the GPU through the BatchRendererGroup API. It skips statically batched objects, so static
//    batching is turned off for the standalone player.
//  - GPU skinning and stripping of unused mesh channels.
public static class PerformanceSettings
{
    [MenuItem("Horror/Performance/Aplicar configurações de desempenho")]
    public static void Apply()
    {
        NamedBuildTarget standalone = NamedBuildTarget.Standalone;

        PlayerSettings.graphicsJobs = true;
        PlayerSettings.graphicsJobMode = GraphicsJobMode.Split;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });

        string engines = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.StandaloneWindows64, BuildOptions.None);
        bool il2cppInstalled = System.IO.Directory.Exists(System.IO.Path.Combine(engines, "Variations", "il2cpp"))
            || System.IO.Directory.Exists(System.IO.Path.Combine(engines, "Variations", "win64_player_nondevelopment_il2cpp"));
        if (il2cppInstalled)
        {
            PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCodeGeneration(standalone, Il2CppCodeGeneration.OptimizeSpeed);
            PlayerSettings.SetIl2CppCompilerConfiguration(standalone, Il2CppCompilerConfiguration.Master);
        }
        else
        {
            Debug.LogWarning("[Performance] IL2CPP module not installed (Unity Hub > Installs > Add modules > Windows Build Support (IL2CPP)). Scripting backend left as is.");
        }

        PlayerSettings.gcIncremental = true;
        // Frame timing stats: CPU and GPU time per frame for PerfProbe and dynamic resolution (negligible cost).
        PlayerSettings.enableFrameTimingStats = true;
        PlayerSettings.stripUnusedMeshComponents = true;
        PlayerSettings.meshDeformation = MeshDeformation.GPUBatched;
        PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneWindows64, false);

        // "BatchRendererGroup Variants: Keep All" (Project Settings > Graphics), required by the GPU
        // Resident Drawer in builds. Read-only through the API in this version, so set the asset field.
        SerializedObject graphics = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
        SerializedProperty brg = graphics.FindProperty("m_BrgStripping");
        if (brg != null)
        {
            brg.intValue = (int)UnityEditor.Rendering.BatchRendererGroupStrippingMode.KeepAll;
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[Performance] Set Project Settings > Graphics > BatchRendererGroup Variants to Keep All by hand.");
        }
        ApplySkinWeights();
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            if (QualitySettings.GetRenderPipelineAssetAt(level) is UniversalRenderPipelineAsset pipeline)
            {
                // An empty renderer slot silently disables the drawer (seen twice on the Fantastic asset).
                FloodedHouseBuilder.StripNullRenderers(pipeline);
                pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing;
                pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = true;
                ApplyQualityTier(QualitySettings.names[level], pipeline);
                EditorUtility.SetDirty(pipeline);
            }
        }

        // The house's reflection probes are realtime but render once on load (OnAwake): Unity's lower presets turned
        // realtime probes off, so they never rendered and every wall reflected the blue sky (10/10). One frame on load.
        int current = QualitySettings.GetQualityLevel();
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            QualitySettings.SetQualityLevel(level, false);
            QualitySettings.realtimeReflectionProbes = true;
        }

        QualitySettings.SetQualityLevel(current, false);

        AssetDatabase.SaveAssets();
        Debug.Log("[Performance] Applied. IL2CPP: " + il2cppInstalled + ". Restart the editor for the graphics API change to reach the editor itself.");
    }

    // Anti-aliasing and render scale per quality level, from the player sweep of 10/10 (PerfSweep, RTX 3060 Ti at
    // 1080p, GPU bound): MSAA 8x cost 0.8 to 1.3 ms more than none and looked the same under the film grain, so the top
    // level keeps 2x; the levels below drop MSAA for SMAA (RenderQuality sets it on the cameras) and the two lowest
    // render below native resolution with FSR. RenderQuality also caps the volumetric fog steps and the SSAO samples.
    private static void ApplyQualityTier(string level, UniversalRenderPipelineAsset pipeline)
    {
        switch (level)
        {
            case "Fantastic":
                pipeline.msaaSampleCount = 2;
                pipeline.renderScale = 1f;
                break;
            case "Beautiful":
                pipeline.msaaSampleCount = 1;
                pipeline.renderScale = 1f;
                break;
            case "Good":
                pipeline.msaaSampleCount = 1;
                pipeline.renderScale = 0.87f;
                pipeline.upscalingFilter = UpscalingFilterSelection.FSR;
                break;
            case "Fast":
                pipeline.msaaSampleCount = 1;
                pipeline.renderScale = 0.77f;
                pipeline.upscalingFilter = UpscalingFilterSelection.FSR;
                // Unity's Fast preset had no shadows and per-vertex lights: in the house every lamp lit through the
                // walls (seen in the player capture of 10/10). Hard shadows on a small atlas cost little next to that.
                SerializedObject data = new SerializedObject(pipeline);
                // Its main light was off too, so the moon became an unshadowed additional light that lit every wall.
                data.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                data.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                SerializedProperty anyShadows = data.FindProperty("m_AnyShadowsSupported");
                if (anyShadows != null)
                {
                    anyShadows.boolValue = true;
                }

                data.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                data.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
                data.FindProperty("m_SoftShadowsSupported").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                break;
        }
    }

    // Every quality level skins with no limit on bone influences: the characters import 8 (CharacterSkinImport), and
    // at Unity's default of 4 the MetaHuman palm folded (04/10). Only meshes that carry more than 4 pay for it.
    public static void ApplySkinWeights()
    {
        int current = QualitySettings.GetQualityLevel();
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            QualitySettings.SetQualityLevel(level, false);
            QualitySettings.skinWeights = SkinWeights.Unlimited;
        }
        QualitySettings.SetQualityLevel(current, false);
    }
}
