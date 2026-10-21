using UnityEngine;

// The scene-side face of GameTuning: a DontDestroyOnLoad object, created before the first scene,
// that survives scene changes. Selecting it in Play Mode shows the tuning values in the Inspector
// (GameTuningHostEditor draws the asset inline), and edits go to the asset, so they are kept.
public sealed class GameTuningHost : MonoBehaviour
{
    [SerializeField]
    private GameTuning tuning;

    public GameTuning Tuning => tuning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        GameObject host = new GameObject("[GameTuning]");
        DontDestroyOnLoad(host);
        host.AddComponent<GameTuningHost>().tuning = GameTuning.Instance;
    }
}
