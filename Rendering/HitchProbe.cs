#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditorInternal;
using UnityEditor.Profiling;
using UnityEngine;

// Development tool (editor only): finds what the frame hitches are made of. Walks the player smoothly through a
// list of waypoints with the Profiler recording, then reads every recorded frame back from the Profiler and, for
// each frame longer than `thresholdMs`, lists the heaviest samples of the main thread (name, time, depth), plus a
// histogram of which markers head the hitches most often. Writes the report to a text file. Start it from code in
// Play:
//   new GameObject("HitchProbe").AddComponent<HitchProbe>().Run("D:/.../hitches.txt", waypoints, 40f);
public sealed class HitchProbe : MonoBehaviour
{
    public bool Done { get; private set; }

    private string output;
    private FrameRecorder.Waypoint[] points;
    private float threshold;

    public void Run(string outputPath, FrameRecorder.Waypoint[] waypoints, float thresholdMs = 40f)
    {
        output = outputPath;
        points = waypoints;
        threshold = thresholdMs;
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        Application.runInBackground = true;
        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
        if (controller == null || points.Length == 0)
        {
            File.WriteAllText(output, "no controller or no points");
            Done = true;
            yield break;
        }

        controller.Teleport(points[0].position, points[0].yaw);
        yield return null;
        yield return null;
        ProfilerDriver.ClearAllFrames();
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.deepProfiling = false;
        ProfilerDriver.enabled = true;
        UnityEngine.Profiling.Profiler.enabled = true;
        yield return null;
        int firstFrame = Time.frameCount;
        float started = Time.realtimeSinceStartup;
        List<string> segments = new List<string>();
        for (int i = 1; i < points.Length; i++)
        {
            FrameRecorder.Waypoint a = points[i - 1], b = points[i];
            float t0 = Time.realtimeSinceStartup;
            int segFrame = Time.frameCount;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.1f, b.seconds))
            {
                float s = Mathf.SmoothStep(0f, 1f, t);
                Vector3 p = Vector3.Lerp(a.position, b.position, s);
                float yaw = Mathf.LerpAngle(a.yaw, b.yaw, s);
                float pitch = Mathf.Lerp(a.pitch, b.pitch, s);
                controller.Teleport(p, yaw);
                controller.LookAt(p + Vector3.up * 1.6f + Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * 10f);
                yield return null;
            }

            segments.Add($"segment {i}: frames {segFrame - firstFrame}..{Time.frameCount - firstFrame} ({Time.realtimeSinceStartup - t0:F1} s) to {b.position}");
        }

        yield return null;
        ProfilerDriver.enabled = false;
        UnityEngine.Profiling.Profiler.enabled = false;
        Report(segments, Time.realtimeSinceStartup - started);
        Done = true;
    }

    private void Report(List<string> segments, float seconds)
    {
        StringBuilder sb = new StringBuilder();
        int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
        sb.AppendLine($"profiler frames {first}..{last}, walk {seconds:F1} s, threshold {threshold} ms");
        foreach (string s in segments)
        {
            sb.AppendLine(s);
        }

        Dictionary<string, int> heads = new Dictionary<string, int>();
        Dictionary<string, double> headMs = new Dictionary<string, double>();
        List<float> times = new List<float>();
        int hitches = 0;
        StringBuilder detail = new StringBuilder();
        for (int f = first; f <= last; f++)
        {
            using (RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(f, 0))
            {
                if (view == null || !view.valid)
                {
                    continue;
                }

                float ms = (float)view.frameTimeMs;
                times.Add(ms);
                if (ms < threshold)
                {
                    continue;
                }

                hitches++;
                detail.AppendLine($"--- frame {f} ({f - first} from start): {ms:F1} ms, gpu {view.frameGpuTimeMs:F1}");
                int count = view.sampleCount;
                int[] depthOf = new int[count];
                List<(string name, double ms, int depth)> heavy = new List<(string, double, int)>();
                for (int i = 0; i < count; i++)
                {
                    int children = view.GetSampleChildrenCount(i);
                    int d = depthOf[i];
                    double t = view.GetSampleTimeMs(i);
                    if (i > 0 && t >= 1.5)
                    {
                        heavy.Add((view.GetSampleName(i), t, d));
                    }

                    // Children follow their parent contiguously in depth-first order.
                    int cursor = i + 1;
                    for (int c = 0; c < children && cursor < count; c++)
                    {
                        depthOf[cursor] = d + 1;
                        cursor += 1 + Subtree(view, cursor);
                    }
                }

                heavy.Sort((x, y) => y.ms.CompareTo(x.ms));
                string head = null;
                int shown = 0;
                foreach ((string name, double t, int d) in heavy)
                {
                    if (shown++ >= 26)
                    {
                        break;
                    }

                    detail.AppendLine($"  {new string(' ', d * 2)}{name}: {t:F1} ms");
                    if (head == null && d >= 2 && !IsLoopMarker(name))
                    {
                        head = name;
                    }
                }

                head = head ?? (heavy.Count > 0 ? heavy[0].name : "?");
                heads[head] = heads.TryGetValue(head, out int n) ? n + 1 : 1;
                headMs[head] = (headMs.TryGetValue(head, out double m) ? m : 0.0) + ms;
            }
        }

        times.Sort();
        float median = times.Count > 0 ? times[times.Count / 2] : 0f;
        float p99 = times.Count > 0 ? times[Mathf.Min(times.Count - 1, Mathf.FloorToInt(times.Count * 0.99f))] : 0f;
        float max = times.Count > 0 ? times[times.Count - 1] : 0f;
        sb.AppendLine($"frames {times.Count}, median {median:F1} ms, p99 {p99:F1} ms, max {max:F1} ms, hitches above threshold {hitches}");
        sb.AppendLine("--- hitch heads (marker: count, total ms)");
        List<string> keys = new List<string>(heads.Keys);
        keys.Sort((a, b) => headMs[b].CompareTo(headMs[a]));
        foreach (string k in keys)
        {
            sb.AppendLine($"  {k}: {heads[k]} x, {headMs[k]:F0} ms");
        }

        sb.Append(detail);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, sb.ToString());
    }

    private static bool IsLoopMarker(string name)
    {
        return name.StartsWith("PlayerLoop") || name.StartsWith("Update.") || name.StartsWith("EarlyUpdate") || name.StartsWith("PreLateUpdate")
            || name.StartsWith("PostLateUpdate") || name.StartsWith("FixedUpdate") || name.StartsWith("Initialization") || name.StartsWith("PreUpdate")
            || name.StartsWith("Profiler.");
    }

    // Number of samples under sample `index` (its whole subtree), so siblings can be found in the depth-first layout.
    private static int Subtree(RawFrameDataView view, int index)
    {
        int children = view.GetSampleChildrenCount(index);
        int total = 0;
        int cursor = index + 1;
        for (int c = 0; c < children; c++)
        {
            int sub = Subtree(view, cursor);
            total += 1 + sub;
            cursor += 1 + sub;
        }

        return total;
    }
}
#endif
