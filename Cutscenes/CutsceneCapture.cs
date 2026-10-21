using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;

// Development tool (04/10): plays a cutscene from the start in real time and saves the main camera's picture at the
// given seconds (PNG, through the full pipeline: post, fog, bloom), with the mean frame time of the second before
// each, and the brightest pixel and the share of blown-out pixels, so a change to the light is judged on the same
// frames, measured, and not by eye on a moving editor. CutsceneCapture.Run(...) from the editor in Play mode.
public sealed class CutsceneCapture : MonoBehaviour
{
    public static string LastReport { get; private set; } = "";
    public static bool Busy { get; private set; }

    private PlayableDirector director;
    private float[] times;
    private string folder;
    private int width;
    private int height;

    public static void Run(float[] seconds, string outputFolder, int width = 1600, int height = 900)
    {
        PlayableDirector found = FindAnyObjectByType<PlayableDirector>();
        if (found == null || Busy)
        {
            LastReport = found == null ? "no director" : "busy";
            return;
        }

        Application.runInBackground = true;
        CutsceneCapture capture = new GameObject("CutsceneCapture").AddComponent<CutsceneCapture>();
        capture.director = found;
        capture.times = seconds;
        capture.folder = outputFolder;
        capture.width = width;
        capture.height = height;
        Busy = true;
        LastReport = "running";
        capture.StartCoroutine(capture.Capture());
    }

    // One picture of the main camera now (any scene, Play mode), with the same numbers as the cutscene captures.
    public static string Shot(string path, int width = 1600, int height = 900)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return "no camera";
        }

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = previous;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = tex.GetPixels32();
        int max = 0, blown = 0;
        double mean = 0;
        foreach (Color32 c in px)
        {
            int m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            max = Mathf.Max(max, m);
            blown += m >= 250 ? 1 : 0;
            mean += 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}: mean {1:0.0}, max {2}, blown {3:0.000}%",
            Path.GetFileName(path), mean / px.Length, max, blown * 100.0 / px.Length);
    }

    private IEnumerator Capture()
    {
        Directory.CreateDirectory(folder);
        StringBuilder report = new StringBuilder();
        director.time = 0;
        director.Play();
        foreach (float at in times)
        {
            float frames = 0f, seconds = 0f;
            while (director.time < at && director.state == PlayState.Playing)
            {
                if (director.time > at - 1.0)
                {
                    frames++;
                    seconds += Time.unscaledDeltaTime;
                }

                yield return null;
            }

            yield return new WaitForEndOfFrame();
            Camera cam = Camera.main;
            if (cam == null)
            {
                continue;
            }

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            Color32[] px = tex.GetPixels32();
            int max = 0, blown = 0;
            double mean = 0;
            foreach (Color32 c in px)
            {
                int m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                max = Mathf.Max(max, m);
                blown += m >= 250 ? 1 : 0;
                mean += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b);
            }

            string name = "t" + at.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + ".png";
            File.WriteAllBytes(Path.Combine(folder, name), tex.EncodeToPNG());
            Destroy(tex);
            report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0}: frame {1:0.0} ms, mean {2:0.0}, max {3}, blown {4:0.000}%\n",
                name, frames > 0 ? seconds / frames * 1000f : 0f, mean / px.Length, max, blown * 100.0 / px.Length);
        }

        LastReport = report.ToString();
        File.WriteAllText(Path.Combine(folder, "report.txt"), LastReport);
        Busy = false;
        Destroy(gameObject);
    }
}
