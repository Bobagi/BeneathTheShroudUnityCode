using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Marketing and review tool (16/10, Gustavo: "imagens e vídeos curtos de gameplay para a divulgação"): records the
// main camera frame by frame at a fixed rate (Time.captureFramerate, so a heavy frame never drops), JPEG per frame,
// and SourceAssets/Tools/marketing_assemble.py turns the folder into an mp4. Started in Play from code (MCP):
//   new GameObject().AddComponent<FrameRecorder>().Record(folder, seconds, 1920, 1080);
// A tour moves the first-person player through waypoints while recording (position in scene metres, yaw and pitch
// in degrees, seconds to reach it from the previous one; eased):
//   recorder.Tour(folder, new[] { new FrameRecorder.Waypoint(...), ... }, 1920, 1080);
// Still(path, width, height) saves one picture through the full pipeline (post, fog, HUD not included: camera only).
public sealed class FrameRecorder : MonoBehaviour
{
    public struct Waypoint
    {
        public Vector3 position;
        public float yaw, pitch, seconds;

        public Waypoint(Vector3 position, float yaw, float pitch, float seconds)
        {
            this.position = position;
            this.yaw = yaw;
            this.pitch = pitch;
            this.seconds = seconds;
        }
    }

    public bool Done { get; private set; }
    public string Log { get; private set; } = "";

    private const int Fps = 30;
    private RenderTexture rt;
    private Texture2D tex;
    private string folder;
    private int frame;
    private bool recording;
    private int width, height;

    public void Record(string outputFolder, float seconds, int w = 1920, int h = 1080)
    {
        Setup(outputFolder, w, h);
        StartCoroutine(RecordFor(seconds));
    }

    public void Tour(string outputFolder, Waypoint[] points, int w = 1920, int h = 1080)
    {
        Setup(outputFolder, w, h);
        StartCoroutine(TourRoutine(points));
    }

    public static string Still(string path, int w = 3840, int h = 2160)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return "no camera";
        }

        RenderTexture r = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = r;
        cam.Render();
        cam.targetTexture = previous;
        RenderTexture.active = r;
        Texture2D t = new Texture2D(w, h, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        t.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(r);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, t.EncodeToPNG());
        Destroy(t);
        return path;
    }

    private void Setup(string outputFolder, int w, int h)
    {
        folder = outputFolder;
        width = w;
        height = h;
        Directory.CreateDirectory(folder);
        Application.runInBackground = true;
        Time.captureFramerate = Fps;
        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        tex = new Texture2D(width, height, TextureFormat.RGB24, false);
    }

    private IEnumerator RecordFor(float seconds)
    {
        recording = true;
        int frames = Mathf.CeilToInt(seconds * Fps);
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForEndOfFrame();
            Capture();
        }

        Finish();
    }

    private IEnumerator TourRoutine(Waypoint[] points)
    {
        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
        if (controller == null || points.Length == 0)
        {
            Log = "no controller or no points";
            Finish();
            yield break;
        }

        controller.Teleport(points[0].position, points[0].yaw);
        yield return null;
        yield return null;
        recording = true;
        for (int i = 1; i < points.Length; i++)
        {
            Waypoint a = points[i - 1], b = points[i];
            int frames = Mathf.Max(1, Mathf.RoundToInt(b.seconds * Fps));
            for (int f = 1; f <= frames; f++)
            {
                float t = Mathf.SmoothStep(0f, 1f, f / (float)frames);
                Vector3 p = Vector3.Lerp(a.position, b.position, t);
                float yaw = Mathf.LerpAngle(a.yaw, b.yaw, t);
                float pitch = Mathf.Lerp(a.pitch, b.pitch, t);
                controller.Teleport(p, yaw);
                controller.LookAt(p + Vector3.up * 1.6f + Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * 10f);
                yield return new WaitForEndOfFrame();
                Capture();
            }
        }

        Finish();
    }

    private void Capture()
    {
        Camera cam = Camera.main;
        if (cam == null || !recording)
        {
            return;
        }

        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = previous;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(folder, "f_" + frame.ToString("D5") + ".jpg"), tex.EncodeToJPG(92));
        frame++;
    }

    private void Finish()
    {
        recording = false;
        Time.captureFramerate = 0;
        Log += " frames " + frame;
        Done = true;
        if (rt != null)
        {
            rt.Release();
        }
    }
}
