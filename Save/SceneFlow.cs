using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Which scene comes after which: the main menu opens the game; "Jogar" plays the opening cutscene (Arrival,
// skippable) and goes on to the house when it ends; "Continuar" goes straight to the house and rebuilds the last
// checkpoint; the pause menu and the end card go back to the menu. Every change fades to black, shows the loading
// screen (LoadingScreen: a still of the house and the cassette whose reels turn), loads in the background, warms up
// the graphics pipeline states (GraphicsStateWarmup) and fades back in. Scene names here only.
public static class SceneFlow
{
    public const string MainMenuScene = "MainMenu";
    public const string OpeningScene = "Arrival";
    public const string HouseScene = "Flooded_House";

    private static FlowRunner runner;
    private static bool openingLeadsToHouse;

    public static bool Busy => runner != null && runner.Busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        openingLeadsToHouse = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void NewGame()
    {
        SaveGame.PendingLoad = null;
        SaveGame.PlaySeconds = 0f;
        SaveGame.Deaths = 0;
        if (Application.CanStreamedLevelBeLoaded(OpeningScene))
        {
            openingLeadsToHouse = true;
            Load(OpeningScene);
        }
        else
        {
            Load(HouseScene);
        }
    }

    public static void Continue()
    {
        SaveGame.Data data = SaveGame.Read();
        if (data == null)
        {
            NewGame();
            return;
        }

        SaveGame.PendingLoad = data;
        SaveGame.PlaySeconds = data.playSeconds;
        SaveGame.Deaths = data.deaths;
        Load(string.IsNullOrEmpty(data.scene) ? HouseScene : data.scene);
    }

    public static void ToMainMenu()
    {
        SaveGame.PendingLoad = null;
        Load(MainMenuScene);
    }

    public static void Load(string scene)
    {
        if (runner == null)
        {
            GameObject go = new GameObject("[SceneFlow]");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<FlowRunner>();
        }

        runner.Begin(scene);
    }

    // The opening was started by "Jogar": when it ends (or is skipped) the game goes on to the house.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SteamPresence.ForScene(scene.name);
        if (scene.name != OpeningScene || !openingLeadsToHouse)
        {
            return;
        }

        openingLeadsToHouse = false;
        CutsceneDirector cutscene = Object.FindAnyObjectByType<CutsceneDirector>();
        if (cutscene == null)
        {
            Load(HouseScene);
            return;
        }

        cutscene.Finished += () => Load(HouseScene);
    }

    private sealed class FlowRunner : MonoBehaviour
    {
        private const float FadeSeconds = 0.9f;

        private LoadingScreen screen;
        private bool busy;

        public bool Busy => busy;

        public void Begin(string scene)
        {
            if (!busy)
            {
                StartCoroutine(Run(scene));
            }
        }

        private IEnumerator Run(string scene)
        {
            busy = true;
            if (screen == null)
            {
                screen = LoadingScreen.Create(gameObject);
            }

            screen.BlocksRaycasts = true;
            screen.SetLabel(Loc.T("flow.loading"));
            screen.Progress = 0f;
            float startVolume = AudioListener.volume;
            for (float t = screen.Alpha; t < 1f; t += Time.unscaledDeltaTime / FadeSeconds)
            {
                screen.Alpha = t;
                AudioListener.volume = startVolume * (1f - t);
                yield return null;
            }

            screen.Alpha = 1f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SteamPresence.Loading();
            // The scene's assets (0 to 0.7 of the bar), then the pipeline states compiled before the first frame is
            // shown (GraphicsStateWarmup, 0.7 to 1): what used to stutter in the first minutes of play.
            bool warm = GraphicsStateWarmup.Prepare();
            float loadShare = warm ? 0.7f : 1f;
            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            load.allowSceneActivation = false;
            while (load.progress < 0.9f)
            {
                screen.Progress = Mathf.Clamp01(load.progress / 0.9f) * loadShare * 0.9f;
                yield return null;
            }

            load.allowSceneActivation = true;
            while (!load.isDone)
            {
                yield return null;
            }

            screen.Progress = loadShare;
            // A frame for the new scene's Start (the director rebuilds a checkpoint there) before showing it.
            yield return null;
            yield return null;
            if (warm)
            {
                float t0 = Time.realtimeSinceStartup;
                while (!GraphicsStateWarmup.Done && Time.realtimeSinceStartup - t0 < 40f)
                {
                    GraphicsStateWarmup.Step();
                    screen.Progress = loadShare + (1f - loadShare) * GraphicsStateWarmup.Progress;
                    yield return null;
                }

                Debug.Log("[PSO] warm-up " + GraphicsStateWarmup.Total + " states in " + (Time.realtimeSinceStartup - t0).ToString("F1") + " s");
            }

            screen.Progress = 1f;
            yield return null;
            AudioListener.volume = GameSettings.MasterVolume;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / FadeSeconds)
            {
                screen.Alpha = t;
                yield return null;
            }

            screen.Alpha = 0f;
            screen.BlocksRaycasts = false;
            busy = false;
        }
    }
}
