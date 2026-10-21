using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Which scene comes after which: the main menu opens the game; "Jogar" plays the opening cutscene (Arrival,
// skippable) and goes on to the house when it ends; "Continuar" goes straight to the house and rebuilds the last
// checkpoint; the pause menu and the end card go back to the menu. Every change fades to black, loads in the
// background with a small "Carregando" in the corner, and fades back in. Scene names here only.
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

        private CanvasGroup group;
        private TextMeshProUGUI label;
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
            Build();
            group.blocksRaycasts = true;
            label.text = Loc.T("flow.loading");
            float startVolume = AudioListener.volume;
            for (float t = group.alpha; t < 1f; t += Time.unscaledDeltaTime / FadeSeconds)
            {
                group.alpha = t;
                AudioListener.volume = startVolume * (1f - t);
                yield return null;
            }

            group.alpha = 1f;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SteamPresence.Loading();
            AsyncOperation load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone)
            {
                // The label breathes while it loads, so a long load never looks frozen.
                label.alpha = 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
                yield return null;
            }

            // A frame for the new scene's Start (the director rebuilds a checkpoint there) before showing it.
            yield return null;
            yield return null;
            AudioListener.volume = GameSettings.MasterVolume;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / FadeSeconds)
            {
                group.alpha = t;
                yield return null;
            }

            group.alpha = 0f;
            group.blocksRaycasts = false;
            busy = false;
        }

        private void Build()
        {
            if (group != null)
            {
                return;
            }

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            Image black = HudFactory.Image("Black", transform, HudSprites.Square, Color.black);
            HudFactory.Stretch(black.rectTransform);
            black.raycastTarget = true;
            label = HudFactory.Label("Loading", transform, 26f, TextAlignmentOptions.BottomRight, HudFactory.MutedInk);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(1f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(-64f, 48f);
            label.rectTransform.sizeDelta = new Vector2(600f, 60f);
            label.characterSpacing = 12f;
        }
    }
}
