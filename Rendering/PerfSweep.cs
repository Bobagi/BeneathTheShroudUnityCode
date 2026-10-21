using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Development tool: started by running a player build with "-perfsweep" (optionally "-perfout <file>"). In the
// Flooded_House it skips the intro, stands the player at a few fixed viewpoints and, at each one, measures the frame
// with each costly setting changed one at a time (MSAA, shadows, SSAO, volumetric fog, render scale), so every
// optimisation is decided with GPU numbers from a real player instead of the editor (where the editor itself is the
// bottleneck and the GPU time is not representative). Writes a table and quits.
public sealed class PerfSweep : MonoBehaviour
{
    private const int WarmFrames = 25;
    private const int MeasureFrames = 150;

    private struct Viewpoint
    {
        public string Name;
        public Vector3 Feet;
        public float Yaw;
        public float Pitch;
    }

    private static readonly Viewpoint[] Viewpoints =
    {
        // The Luna mansion (plan coordinates + FloodedHouseBuilder.ManorOffset (-16.32, 0, -18.92), 13/10).
        new Viewpoint { Name = "hall (vitrais, porta fosca)", Feet = new Vector3(-16.32f, 0f, -14.42f), Yaw = 0f },
        new Viewpoint { Name = "sala de jantar (baia)", Feet = new Vector3(-24.12f, 0f, -12.42f), Yaw = 180f },
        new Viewpoint { Name = "cozinha", Feet = new Vector3(-31.72f, 0f, -6.42f), Yaw = 180f },
        new Viewpoint { Name = "galeria (patio, flora)", Feet = new Vector3(-24.92f, 0f, -3.92f), Yaw = 90f },
        new Viewpoint { Name = "biblioteca (seda, ovos)", Feet = new Vector3(-2.82f, 0f, -7.42f), Yaw = 300f },
        new Viewpoint { Name = "despensa alagada", Feet = new Vector3(7.8f, -4.6f, -0.6f), Yaw = 0f },
    };

    private readonly FrameTiming[] timings = new FrameTiming[1];
    private int viewpointIndex;
    private readonly StringBuilder report = new StringBuilder();
    private UniversalRenderPipelineAsset asset;
    private List<ScriptableRendererFeature> features;
    private readonly List<KeyValuePair<object, int>> fogSteps = new List<KeyValuePair<object, int>>();
    private readonly List<KeyValuePair<object, int>> fogBlur = new List<KeyValuePair<object, int>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-perfsweep") >= 0)
        {
            new GameObject("PerfSweep").AddComponent<PerfSweep>();
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(4f);
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        ScriptableRenderer renderer = asset.GetRenderer(0);
        PropertyInfo prop = typeof(ScriptableRenderer).GetProperty("rendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        features = prop != null ? prop.GetValue(renderer) as List<ScriptableRendererFeature> : new List<ScriptableRendererFeature>();
        FindFogSteps();

        report.AppendFormat("{0} | {1} | {2}x{3} | {4} | URP {5}\n", SystemInfo.graphicsDeviceName, SystemInfo.processorType,
            Screen.width, Screen.height, SystemInfo.graphicsDeviceType, asset.name);
        foreach (ScriptableRendererFeature f in features)
        {
            report.AppendFormat("  feature {0} ({1}) active {2}\n", f.name, f.GetType().Name, f.isActive);
        }

        SkipIntro();
        yield return new WaitForSecondsRealtime(2f);

        FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
        for (viewpointIndex = 0; viewpointIndex < Viewpoints.Length; viewpointIndex++)
        {
            Viewpoint v = Viewpoints[viewpointIndex];
            if (player != null)
            {
                player.Teleport(v.Feet, v.Yaw);
            }

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            report.AppendFormat("\n== {0} ==\n", v.Name);
            foreach (string config in Configs)
            {
                Apply(config, true);
                yield return Measure(config);
                Apply(config, false);
            }
        }

        string path = Arg("-perfout") ?? System.IO.Path.Combine(Application.persistentDataPath, "perf_sweep.txt");
        System.IO.File.WriteAllText(path, report.ToString());
        Debug.Log("[PerfSweep] written " + path);
        Application.Quit();
    }

    private static readonly string[] Configs =
    {
        "Fantastic (padrao)", "Beautiful", "Good", "Fast",
        "rebatimentos sem culling", "rebatimentos off", "sombras 48", "nevoa off",
    };

    // Applies (on = true) or reverts (on = false) one configuration.
    private int savedMsaa;
    private float savedScale;
    private UpscalingFilterSelection savedFilter;
    private AntialiasingMode savedAa;
    private int savedLevel;
    private GameObject hidden;
    private int savedShadowMaps = -1;
    private bool savedFillShadows;
    private bool savedFillCulling;
    private readonly List<Light> bounces = new List<Light>();

    private void Apply(string config, bool on)
    {
        UniversalAdditionalCameraData cam = Camera.main != null ? Camera.main.GetUniversalAdditionalCameraData() : null;
        if (on)
        {
            savedMsaa = asset.msaaSampleCount;
            savedScale = asset.renderScale;
            savedFilter = asset.upscalingFilter;
            savedAa = cam != null ? cam.antialiasing : AntialiasingMode.None;
            savedLevel = QualitySettings.GetQualityLevel();
        }
        else
        {
            if (QualitySettings.GetQualityLevel() != savedLevel)
            {
                QualitySettings.SetQualityLevel(savedLevel, true);
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                return;
            }

            if (hidden != null)
            {
                hidden.SetActive(true);
                hidden = null;
            }

            foreach (Light l in bounces)
            {
                l.enabled = true;
            }

            bounces.Clear();
            if (savedFillCulling)
            {
                GameTuning.Instance.fillCulling = true;
                savedFillCulling = false;
            }

            if (savedFillShadows)
            {
                GameTuning.Instance.fillShadows = true;
                savedFillShadows = false;
            }

            if (savedShadowMaps >= 0)
            {
                GameTuning.Instance.shadowMaps = savedShadowMaps;
                savedShadowMaps = -1;
            }

            asset.msaaSampleCount = savedMsaa;
            asset.renderScale = savedScale;
            asset.upscalingFilter = savedFilter;
            if (cam != null)
            {
                cam.antialiasing = savedAa;
            }

            SetFeature("ScreenSpaceAmbientOcclusion", true);
            SetFeature("VolumetricFogRendererFeature", true);
            SetFeature("ScreenSpaceGlobalIlluminationURP", true);
            SetSsao(false, false);
            SetFogSteps(-1);
            SetFogBlur(-1);
            return;
        }

        int level = System.Array.IndexOf(QualitySettings.names, config);
        if (level >= 0)
        {
            QualitySettings.SetQualityLevel(level, true);
            // The level brings its own V-Sync (Good and Beautiful: 1); the game re-applies the player's choice
            // (GameSettings), the sweep measures uncapped.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            return;
        }

        // "sombras N": GameTuning shadowMaps (the ShadowBudget hands out N maps; a point light costs 6).
        if (config.StartsWith("sombras "))
        {
            savedShadowMaps = GameTuning.Instance.shadowMaps;
            GameTuning.Instance.shadowMaps = int.Parse(config.Substring(8));
            return;
        }

        // The lamps' fill lights (point lights named *Bounce): off, or kept out of the shadow budget.
        if (config.StartsWith("rebatimentos"))
        {
            if (config == "rebatimentos sem culling")
            {
                GameTuning.Instance.fillCulling = false;
                savedFillCulling = true;
            }
            else if (config == "rebatimentos off")
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                {
                    if (l.name.EndsWith("Bounce") && l.enabled)
                    {
                        l.enabled = false;
                        bounces.Add(l);
                    }
                }
            }
            else
            {
                GameTuning.Instance.fillShadows = false;
                savedFillShadows = true;
            }

            if (config.EndsWith("sombras 48"))
            {
                savedShadowMaps = GameTuning.Instance.shadowMaps;
                GameTuning.Instance.shadowMaps = 48;
            }

            return;
        }

        // "sem:<Name>": the scene object of that name switched off (to find what a room costs).
        if (config.StartsWith("sem:"))
        {
            hidden = GameObject.Find(config.Substring(4));
            if (hidden != null)
            {
                hidden.SetActive(false);
            }

            return;
        }

        switch (config)
        {
            case "msaa 4x": asset.msaaSampleCount = 4; break;
            case "msaa 2x": asset.msaaSampleCount = 2; break;
            case "msaa off": asset.msaaSampleCount = 1; break;
            case "msaa off + smaa": asset.msaaSampleCount = 1; Smaa(cam); break;
            case "ssao off": SetFeature("ScreenSpaceAmbientOcclusion", false); break;
            case "ssao meia res": SetSsao(true, false); break;
            case "ssao meia res + baixa": SetSsao(true, true); break;
            case "nevoa off": SetFeature("VolumetricFogRendererFeature", false); break;
            case "nevoa 32 passos": SetFogSteps(32); break;
            case "nevoa 24 passos": SetFogSteps(24); break;
            case "nevoa blur 1": SetFogBlur(1); break;
            case "ssgi off": SetFeature("ScreenSpaceGlobalIlluminationURP", false); break;
            case "escala 0,87 FSR": asset.renderScale = 0.87f; asset.upscalingFilter = UpscalingFilterSelection.FSR; break;
            case "escala 0,77 FSR": asset.renderScale = 0.77f; asset.upscalingFilter = UpscalingFilterSelection.FSR; break;
            case "alto: msaa2 ssao meia nevoa 32":
                asset.msaaSampleCount = 2;
                SetSsao(true, false);
                SetFogSteps(32);
                break;
            case "medio: msaa off+smaa ssao meia+baixa nevoa 24":
                asset.msaaSampleCount = 1;
                Smaa(cam);
                SetSsao(true, true);
                SetFogSteps(24);
                break;
        }
    }

    private static void Smaa(UniversalAdditionalCameraData cam)
    {
        if (cam != null)
        {
            cam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.antialiasingQuality = AntialiasingQuality.High;
        }
    }

    // SSAO settings live in an internal class on the feature; changed by reflection (read every frame by the pass).
    private object ssaoSettings;
    private bool ssaoDownsample;
    private object ssaoSamples;

    private void SetSsao(bool on, bool lowSamples)
    {
        if (ssaoSettings == null)
        {
            foreach (ScriptableRendererFeature f in features)
            {
                if (f.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    ssaoSettings = f.GetType().GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f);
                    ssaoDownsample = (bool)Field("Downsample").GetValue(ssaoSettings);
                    ssaoSamples = Field("Samples").GetValue(ssaoSettings);
                }
            }

            if (ssaoSettings == null)
            {
                return;
            }
        }

        Field("Downsample").SetValue(ssaoSettings, on || ssaoDownsample);
        FieldInfo samples = Field("Samples");
        samples.SetValue(ssaoSettings, lowSamples ? System.Enum.Parse(samples.FieldType, "Low") : ssaoSamples);
    }

    private FieldInfo Field(string name)
    {
        return ssaoSettings.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }

    private void SetFeature(string typeName, bool active)
    {
        foreach (ScriptableRendererFeature f in features)
        {
            if (f.GetType().Name == typeName && f.isActive != active)
            {
                f.SetActive(active);
            }
        }
    }

    private void FindFogSteps()
    {
        foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            VolumeProfile profile = volume.sharedProfile;
            if (profile == null)
            {
                continue;
            }

            foreach (VolumeComponent c in profile.components)
            {
                if (c.GetType().Name != "VolumetricFogVolumeComponent")
                {
                    continue;
                }

                FieldInfo field = c.GetType().GetField("maxSteps");
                if (field != null && field.GetValue(c) is ClampedIntParameter p)
                {
                    fogSteps.Add(new KeyValuePair<object, int>(p, p.overrideState ? p.value : -1));
                    if (c.GetType().GetField("blurIterations").GetValue(c) is ClampedIntParameter b)
                    {
                        fogBlur.Add(new KeyValuePair<object, int>(b, b.overrideState ? b.value : -1));
                    }

                    report.AppendFormat("  fog steps in {0}: {1}\n", volume.name, p.overrideState ? p.value.ToString() : "default " + p.value);
                }
            }
        }
    }

    private void SetFogSteps(int steps)
    {
        SetParameters(fogSteps, steps);
    }

    private void SetFogBlur(int iterations)
    {
        SetParameters(fogBlur, iterations);
    }

    // value < 0 restores the saved values.
    private static void SetParameters(List<KeyValuePair<object, int>> list, int steps)
    {
        foreach (KeyValuePair<object, int> pair in list)
        {
            ClampedIntParameter p = (ClampedIntParameter)pair.Key;
            if (steps >= 0)
            {
                p.overrideState = true;
                p.value = steps;
            }
            else if (pair.Value >= 0)
            {
                p.value = pair.Value;
            }
            else
            {
                p.overrideState = false;
            }
        }
    }

    private IEnumerator Measure(string label)
    {
        // The ShadowBudget samples four times a second: let it hand out the maps of the new setting first.
        yield return new WaitForSecondsRealtime(0.3f);
        for (int i = 0; i < WarmFrames; i++)
        {
            FrameTimingManager.CaptureFrameTimings();
            yield return null;
        }

        double cpu = 0, main = 0, render = 0, gpu = 0;
        var gpus = new List<double>(MeasureFrames);
        int count = 0;
        double wall = 0;
        while (count < MeasureFrames)
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                FrameTiming t = timings[0];
                cpu += t.cpuFrameTime;
                main += t.cpuMainThreadFrameTime;
                render += t.cpuRenderThreadFrameTime;
                gpu += t.gpuFrameTime;
                gpus.Add(t.gpuFrameTime);
                wall += Time.unscaledDeltaTime * 1000.0;
                count++;
            }

            yield return null;
        }

        gpus.Sort();
        report.AppendFormat("  {0,-46} quadro {6,5:0.00} ms  gpu {1,5:0.00} ms (p95 {2,5:0.00})  cpu {3,5:0.00} (main {4,4:0.00}, render {5,4:0.00})\n",
            label, gpu / count, gpus[(int)(gpus.Count * 0.95f)], cpu / count, main / count, render / count, wall / count);

        // One screenshot per configuration and viewpoint, to judge what each saving costs in the image.
        string shots = Arg("-perfshots");
        if (shots != null)
        {
            System.IO.Directory.CreateDirectory(shots);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shots, viewpointIndex + "_" + System.Array.IndexOf(Configs, label).ToString("00") + ".png"));
            yield return null;
            yield return null;
        }
    }

    private void SkipIntro()
    {
        FloodedHouseDirector director = FindAnyObjectByType<FloodedHouseDirector>();
        FieldInfo held = typeof(FloodedHouseDirector).GetField("skipHeld", BindingFlags.Instance | BindingFlags.NonPublic);
        if (director != null && held != null)
        {
            held.SetValue(director, 10f);
        }
    }

    private static string Arg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
