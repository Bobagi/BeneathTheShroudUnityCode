using System.IO;
using Steamworks;
using UnityEngine;

// The Steam client connection (Steamworks.NET, pinned to Steamworks SDK 1.65 in Packages/manifest.json; the SDK
// itself, with the ContentBuilder used to upload builds, is kept in SourceAssets/Downloads/Steamworks/sdk_165).
// Starts before the first scene and lives for the whole run. The game never depends on it: with no App ID yet
// (StoreLinks.SteamAppId 0, the Valve review is pending), without the Steam client or if Init fails, everything else
// runs and IsReady stays false, so features (achievements, cloud saves, overlay) must check IsReady first.
// On connecting: the local achievement record goes to the account (Achievements.PushRecordToSteam, skipped in the
// demo app), the rich presence set before Steam was up is sent, the first run takes Steam's language for the game,
// and opening the overlay (Shift+Tab) pauses the game like Esc (Valve asks games to pause there).
// The Steamworks features in use and what the backend needs are listed in Docs/Steam/STEAMWORKS.md.
// In the editor, "Horror > Steam > Testar com o App ID 480 (Spacewar)" turns it on with Valve's public test app.
public sealed class SteamPlatform : MonoBehaviour
{
    public const string EditorTestAppIdKey = "Horror.Steam.EditorTestAppId";

    private static SteamPlatform instance;

    private Callback<GameOverlayActivated_t> overlayChanged;

    public static bool IsReady { get; private set; }

    public static uint AppId { get; private set; }

    public static string PlayerName => IsReady ? SteamFriends.GetPersonaName() : string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        IsReady = false;
        AppId = ConfiguredAppId();
        if (AppId == 0)
        {
            Debug.Log("[Steam] Desligada: ainda sem App ID (StoreLinks.SteamDemoAppId / SteamAppId).");
            return;
        }

#if !UNITY_EDITOR
        // Started outside Steam: Steam relaunches the game through the client and this copy closes.
        if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
        {
            Application.Quit();
            return;
        }
#else
        // Outside Steam the API reads the App ID from steam_appid.txt next to the process (the project folder).
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt"), AppId.ToString());
#endif

        if (!Connect())
        {
            return;
        }

        GameObject go = new GameObject("[SteamPlatform]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SteamPlatform>();
        Debug.Log("[Steam] Pronta, App ID " + AppId + ", jogador " + SteamFriends.GetPersonaName() + ".");
        instance.Connected();
    }

    private void Connected()
    {
        overlayChanged = Callback<GameOverlayActivated_t>.Create(OnOverlay);
        Loc.FollowPlatformLanguage(SteamApps.GetCurrentGameLanguage());
        Achievements.Reload();
        Achievements.PushRecordToSteam();
        SteamPresence.Reapply();
    }

    private static void OnOverlay(GameOverlayActivated_t data)
    {
        if (data.m_bActive == 0)
        {
            return;
        }

        PauseMenu pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null)
        {
            pause.PauseFromOverlay();
        }
    }

    private static bool Connect()
    {
        if (!Packsize.Test() || !DllCheck.Test())
        {
            Debug.LogWarning("[Steam] steam_api64.dll não corresponde ao Steamworks.NET.");
            return false;
        }

        ESteamAPIInitResult result = SteamAPI.InitEx(out string message);
        if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
        {
            Debug.LogWarning("[Steam] Não iniciou (" + result + "): " + message);
            return false;
        }

        IsReady = true;
        return true;
    }

    private static uint ConfiguredAppId()
    {
#if UNITY_EDITOR
        int test = UnityEditor.EditorPrefs.GetInt(EditorTestAppIdKey, 0);
        if (test > 0)
        {
            return (uint)test;
        }
#endif
        return StoreLinks.RunningAppId > 0 ? (uint)StoreLinks.RunningAppId : 0u;
    }

    // A script reload during Play (recompile and continue) keeps this object but wipes the statics and the
    // Steamworks.NET callback dispatcher: connect again so IsReady and callbacks come back.
    private void OnEnable()
    {
        if (instance != null)
        {
            return;
        }

        instance = this;
        AppId = ConfiguredAppId();
        if (AppId > 0 && !IsReady && Connect())
        {
            Connected();
        }
    }

    private void Update()
    {
        if (IsReady)
        {
            SteamAPI.RunCallbacks();
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;
        overlayChanged?.Dispose();
        overlayChanged = null;
        if (IsReady)
        {
            IsReady = false;
            SteamAPI.Shutdown();
        }
    }
}
