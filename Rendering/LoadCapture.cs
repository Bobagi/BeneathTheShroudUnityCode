#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

// Development tool (editor only): screenshots of the Game view every `interval` seconds across a scene change, to
// look at the loading screen (which lives on a DontDestroyOnLoad canvas, like this object). Start it in Play:
//   new GameObject("LoadCapture").AddComponent<LoadCapture>().Run(folder, seconds, interval); SceneFlow.Load("...");
public sealed class LoadCapture : MonoBehaviour
{
    public void Run(string folder, float seconds, float interval)
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(folder);
        StartCoroutine(Routine(folder, seconds, interval));
    }

    private IEnumerator Routine(string folder, float seconds, float interval)
    {
        float start = Time.realtimeSinceStartup;
        int n = 0;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "load_" + (n++).ToString("D3") + "_" + (Time.realtimeSinceStartup - start).ToString("F1") + "s.png"));
            yield return new WaitForSecondsRealtime(interval);
        }

        Destroy(gameObject);
    }
}
#endif
