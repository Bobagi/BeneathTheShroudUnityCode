using UnityEngine;

// Player options: loaded from PlayerPrefs, applied before the first scene loads (so the 90 FPS cap
// holds from the very first frame, menu or not) and re-applied whenever a menu changes one (the options are listed in SettingsCatalog).
public static class GameSettings
{
    public static readonly int[] FrameRateCaps = { 30, 60, 90, 120, 144, 165, 240, 0 };
    // Indices into QualitySettings: Fast, Good, Beautiful, Fantastic (named on screen by Loc "quality.0".."quality.3").
    private static readonly int[] QualityLevels = { 1, 3, 4, 5 };

    public const int DefaultFrameRateCap = 90;

    public static float MasterVolume { get; private set; }
    public static float MouseSensitivity { get; private set; }
    public static float Brightness { get; private set; }
    public static int FrameRateCap { get; private set; }
    public static bool VSync { get; private set; }
    public static int QualityIndex { get; private set; }
    public static bool Fullscreen { get; private set; }
    public static int ResolutionIndex { get; private set; }

    public static event System.Action Changed;

    // A ceiling a scene puts over the player's cap while it is open (the main menu: 60, Gustavo 10/10; nothing in it
    // needs more, and an idle menu should not run the GPU flat out). 0 = none. V-sync still wins.
    public static int SceneFrameRateLimit { get; private set; }

    public static void SetSceneFrameRateLimit(int fps)
    {
        SceneFrameRateLimit = Mathf.Max(0, fps);
        ApplyFrameRate();
    }

    private static void ApplyFrameRate()
    {
        int cap = FrameRateCap > 0 ? FrameRateCap : -1;
        if (SceneFrameRateLimit > 0)
        {
            cap = cap > 0 ? Mathf.Min(cap, SceneFrameRateLimit) : SceneFrameRateLimit;
        }

        Application.targetFrameRate = VSync ? -1 : cap;
    }

    // Loaded on first use too, not only at boot: a script reload in the editor's Play mode resets the statics without
    // running Boot again, and the options then read 0 (the menu showed volume 0%, 640 x 480).
    static GameSettings()
    {
        Load();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        Load();
        Apply();
    }

    private static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        MouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 1f);
        Brightness = PlayerPrefs.GetFloat("Brightness", 0f);
        FrameRateCap = PlayerPrefs.GetInt("FrameRateCap", DefaultFrameRateCap);
        VSync = PlayerPrefs.GetInt("VSync", 0) == 1;
        QualityIndex = PlayerPrefs.GetInt("Quality", QualityLevels.Length - 1);
        Fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        ResolutionIndex = PlayerPrefs.GetInt("Resolution", -1);
    }

    public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); Save(); }
    public static void SetMouseSensitivity(float value) { MouseSensitivity = Mathf.Clamp(value, 0.2f, 3f); Save(); }
    public static void SetBrightness(float value) { Brightness = Mathf.Clamp(value, -1f, 1f); Save(); }
    public static void SetFrameRateCap(int value) { FrameRateCap = value; Save(); }
    public static void SetVSync(bool value) { VSync = value; Save(); }
    public static void SetQuality(int index) { QualityIndex = Mathf.Clamp(index, 0, QualityLevels.Length - 1); Save(); }
    public static void SetFullscreen(bool value) { Fullscreen = value; Save(); }
    public static void SetResolution(int index) { ResolutionIndex = index; Save(); }

    public static Resolution[] Resolutions()
    {
        System.Collections.Generic.List<Resolution> unique = new System.Collections.Generic.List<Resolution>();
        foreach (Resolution r in Screen.resolutions)
        {
            if (unique.Count == 0 || unique[unique.Count - 1].width != r.width || unique[unique.Count - 1].height != r.height)
            {
                unique.Add(r);
            }
            else
            {
                unique[unique.Count - 1] = r;
            }
        }

        return unique.ToArray();
    }

    private static void Save()
    {
        PlayerPrefs.SetFloat("MasterVolume", MasterVolume);
        PlayerPrefs.SetFloat("MouseSensitivity", MouseSensitivity);
        PlayerPrefs.SetFloat("Brightness", Brightness);
        PlayerPrefs.SetInt("FrameRateCap", FrameRateCap);
        PlayerPrefs.SetInt("VSync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("Quality", QualityIndex);
        PlayerPrefs.SetInt("Fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("Resolution", ResolutionIndex);
        PlayerPrefs.Save();
        Apply();
    }

    private static void Apply()
    {
        AudioListener.volume = MasterVolume;

        // V-sync paces to the monitor; otherwise the cap (default 90) keeps GPU heat and noise down.
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        ApplyFrameRate();

        int level = QualityLevels[Mathf.Clamp(QualityIndex, 0, QualityLevels.Length - 1)];
        if (QualitySettings.GetQualityLevel() != level)
        {
            QualitySettings.SetQualityLevel(level, true);
            // Switching level resets v-sync to the level's own value: re-apply ours.
            QualitySettings.vSyncCount = VSync ? 1 : 0;
        }

        if (!Application.isEditor)
        {
            Resolution[] resolutions = Resolutions();
            FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (ResolutionIndex >= 0 && ResolutionIndex < resolutions.Length)
            {
                Screen.SetResolution(resolutions[ResolutionIndex].width, resolutions[ResolutionIndex].height, mode);
            }
            else
            {
                Screen.fullScreenMode = mode;
            }
        }

        Changed?.Invoke();
    }
}
