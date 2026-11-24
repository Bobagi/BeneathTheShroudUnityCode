using System.Text;
using UnityEngine;

// Development tool: measures the frame over a number of frames (CPU main thread, render thread, GPU, from
// FrameTimingManager, plus triangles and draw calls in the editor) and writes the averages to Temp/perf.txt, so a
// setting can be compared before and after with numbers instead of by eye. Start it from code:
//   new GameObject("PerfProbe").AddComponent<PerfProbe>().Measure("label", 180);
// Needs Project Settings > Player > Frame Timing Stats (set by PerformanceSettings).
public sealed class PerfProbe : MonoBehaviour
{
    private readonly FrameTiming[] timings = new FrameTiming[1];
    private string label;
    private int frames;
    private int warmup;
    private int count;
    private double cpu;
    private double main;
    private double render;
    private double gpu;
    private double maxGpu;
    private long triangles;
    private long draws;

    public static string LastResult { get; private set; }

    public void Measure(string name, int frameCount)
    {
        label = name;
        frames = frameCount;
        warmup = 30;
        count = 0;
        cpu = main = render = gpu = maxGpu = 0;
        triangles = draws = 0;
        LastResult = null;
    }

    private void Update()
    {
        if (frames <= 0)
        {
            return;
        }

        FrameTimingManager.CaptureFrameTimings();
        if (warmup > 0)
        {
            warmup--;
            return;
        }

        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            FrameTiming t = timings[0];
            cpu += t.cpuFrameTime;
            main += t.cpuMainThreadFrameTime;
            render += t.cpuRenderThreadFrameTime;
            gpu += t.gpuFrameTime;
            maxGpu = System.Math.Max(maxGpu, t.gpuFrameTime);
#if UNITY_EDITOR
            triangles += UnityEditor.UnityStats.triangles;
            draws += UnityEditor.UnityStats.drawCalls;
#endif
            count++;
        }

        if (count >= frames)
        {
            frames = 0;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}: cpu {1:0.00} ms (main {2:0.00}, render {3:0.00}), gpu {4:0.00} ms (max {5:0.00}), tris {6:0.00} M, draws {7}",
                label, cpu / count, main / count, render / count, gpu / count, maxGpu, triangles / (double)count / 1e6, draws / count);
            LastResult = sb.ToString();
            System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Temp/perf.txt"), LastResult + "\n");
            Debug.Log("[Perf] " + LastResult);
        }
    }
}
