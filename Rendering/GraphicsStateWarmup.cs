using System;
using System.IO;
using System.Reflection;
using UnityEngine;

// Pipeline state objects (PSOs) compiled during the loading screen instead of mid-game. On D3D12 (this game's first
// choice) the driver builds a PSO the first time each shader variant meets a new vertex layout, blend or depth state,
// and that build lands on the main thread as a hitch of tens of milliseconds, once per new combination: the
// frequent stutters while walking through the house (Gustavo, 22/10). Unity 6 records those combinations into a
// GraphicsStateCollection: traced once during a playthrough (BeginTrace/EndTrace, saved as a .graphicsstate asset in
// Art/UI/Resources/Loading, one per graphics API), then at every load WarmUpProgressively() rebuilds them on a worker
// while the loading screen turns (SceneFlow), so the game meets them already compiled.
//   Trace: run the player with "-psotrace" (or GraphicsStateWarmup.BeginTrace(path) from code), play through the
//   house, quit: the file is written to persistentDataPath/pso_<api>.graphicsstate. Copy it into Resources/Loading.
// The class is reached by reflection: its namespace moved between Unity 6 releases (Experimental.Rendering, then
// Rendering) and the compile must not depend on which one this editor has; without it, nothing is warmed up.
public static class GraphicsStateWarmup
{
    private const string ResourceFolder = "Loading/";
    private const int StatesPerStep = 24;

    private static readonly Type CollectionType = FindType();
    private static UnityEngine.Object collection;
    private static UnityEngine.Object traceCollection;
    private static bool loaded;
    private static bool traced;
    private static bool tracing;
    private static string tracePath;

    public static bool Supported => CollectionType != null;
    public static bool IsTracing => tracing;
    public static bool HasTraced => traced;

    private static Type FindType()
    {
        foreach (string name in new[] { "UnityEngine.Rendering.GraphicsStateCollection", "UnityEngine.Experimental.Rendering.GraphicsStateCollection" })
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType(name, false);
                if (t != null)
                {
                    return t;
                }
            }
        }

        return null;
    }

    private static object Get(UnityEngine.Object target, string property)
    {
        PropertyInfo p = CollectionType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
        return p != null ? p.GetValue(target) : null;
    }

    private static object Call(UnityEngine.Object target, string method, params object[] args)
    {
        Type[] types = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            types[i] = args[i].GetType();
        }

        MethodInfo m = CollectionType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, types, null);
        return m != null ? m.Invoke(target, args) : null;
    }

    private static int Int(UnityEngine.Object target, string property)
    {
        object v = Get(target, property);
        return v is int i ? i : 0;
    }

    public static int Total => collection != null ? Int(collection, "totalGraphicsStateCount") : 0;
    public static int Completed => collection != null ? Int(collection, "completedWarmupCount") : 0;
    public static bool Done => collection == null || (Get(collection, "isWarmedUp") is bool b && b);

    // What was warmed up so far, 0 to 1 (1 when there is nothing to warm up).
    public static float Progress
    {
        get
        {
            int total = Total;
            return total == 0 ? 1f : Mathf.Clamp01(Completed / (float)total);
        }
    }

    public static string FileName => "pso_" + SystemInfo.graphicsDeviceType.ToString().ToLowerInvariant() + ".graphicsstate";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        collection = null;
        loaded = false;
        if (!Application.isEditor && Array.IndexOf(Environment.GetCommandLineArgs(), "-psotrace") >= 0)
        {
            BeginTrace(Path.Combine(Application.persistentDataPath, FileName));
            Application.quitting += () => EndTrace();
        }
    }

    // Loads the collection for this graphics API once; false when there is none (nothing to warm up).
    public static bool Prepare()
    {
        if (loaded)
        {
            return collection != null;
        }

        loaded = true;
        if (CollectionType == null)
        {
            Debug.Log("[PSO] GraphicsStateCollection is not available in this Unity: nothing to warm up");
            return false;
        }

        string name = ResourceFolder + Path.GetFileNameWithoutExtension(FileName);
        collection = Resources.Load(name, CollectionType);
        if (collection == null)
        {
            Debug.Log("[PSO] no " + name + " in Resources: nothing to warm up");
            return false;
        }

        object device = Get(collection, "graphicsDeviceType");
        if (device != null && !device.Equals(SystemInfo.graphicsDeviceType))
        {
            Debug.LogWarning("[PSO] " + name + " was traced for " + device + ", this is " + SystemInfo.graphicsDeviceType);
            collection = null;
            return false;
        }

        Debug.Log("[PSO] " + Total + " states to warm up (" + Get(collection, "qualityLevelName") + ")");
        return true;
    }

    // One slice of the warm-up; call every frame of the loading screen until Done.
    public static void Step()
    {
        if (collection != null && !Done)
        {
            Call(collection, "WarmUpProgressively", StatesPerStep);
        }
    }

    public static void BeginTrace(string path)
    {
        if (tracing || CollectionType == null)
        {
            return;
        }

        tracePath = path;
        traceCollection = (UnityEngine.Object)Activator.CreateInstance(CollectionType);
        Call(traceCollection, "BeginTrace");
        tracing = true;
        Debug.Log("[PSO] tracing to " + path);
    }

    public static string EndTrace()
    {
        if (!tracing || traceCollection == null)
        {
            return null;
        }

        tracing = false;
        traced = true;
        Call(traceCollection, "EndTrace");
        Directory.CreateDirectory(Path.GetDirectoryName(tracePath));
        bool ok = Call(traceCollection, "SaveToFile", tracePath) is bool b && b;
        Debug.Log("[PSO] trace " + (ok ? "saved" : "FAILED") + ": " + Int(traceCollection, "totalGraphicsStateCount") + " states, " + tracePath);
        return ok ? tracePath : null;
    }

    public static int TracedCount => traceCollection != null ? Int(traceCollection, "totalGraphicsStateCount") : 0;
}
