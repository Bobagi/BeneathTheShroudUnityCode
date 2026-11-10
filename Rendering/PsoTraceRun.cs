using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// The player run with "-psotrace": GraphicsStateWarmup records every pipeline state the game creates; this drives
// the game through the demo by itself (SceneFlow straight to the house, the scripted DemoPlaythrough on its fast
// path, which draws the torch, the revolver, the spores, the finale) and quits, so the trace covers what a player
// meets, without anyone at the keyboard. The collection lands in persistentDataPath/pso_<api>.graphicsstate
// (GraphicsStateWarmup.EndTrace on quit); copy it into Art/UI/Resources/Loading and rebuild.
//   "Projeto Ilha.exe" -psotrace [-psotrace-out <file>]
public sealed class PsoTraceRun : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-psotrace") < 0)
        {
            return;
        }

        GameObject go = new GameObject("[PsoTraceRun]");
        DontDestroyOnLoad(go);
        go.AddComponent<PsoTraceRun>();
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        // The menu for a few frames (its own shaders), then the house.
        yield return new WaitForSecondsRealtime(3f);
        if (SceneManager.GetActiveScene().name != SceneFlow.HouseScene)
        {
            SceneFlow.Load(SceneFlow.HouseScene);
            while (SceneFlow.Busy || SceneManager.GetActiveScene().name != SceneFlow.HouseScene)
            {
                yield return null;
            }
        }

        yield return new WaitForSecondsRealtime(4f);
        string folder = System.IO.Path.Combine(Application.persistentDataPath, "psotrace");
        DemoPlaythrough run = new GameObject("Playthrough").AddComponent<DemoPlaythrough>();
        run.Run(folder, true);
        float started = Time.realtimeSinceStartup;
        while (run != null && !run.Done && Time.realtimeSinceStartup - started < 400f)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(3f);
        Debug.Log("[PSO] trace run finished, quitting (" + GraphicsStateWarmup.TracedCount + " states so far)");
        Application.Quit();
    }
}
