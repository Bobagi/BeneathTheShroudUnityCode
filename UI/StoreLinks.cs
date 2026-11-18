using Steamworks;
using UnityEngine;

// Where the game lives on the store. The Steam page does not exist yet (the Valve review is pending, 09/10): while
// SteamAppId is 0 every wishlist button stays hidden, and filling the id here is all it takes for them to appear
// (end card of the demo, main menu). Inside Steam the store page opens in the overlay (the wishlist button is
// there); without the client the plain https link opens in the browser.
// The demo is its own app on Steam, with its own App ID (created from the game's page in Steamworks: "Create a
// demo"), so there are two ids: SteamAppId is the full game (the store page, what wishlists point to) and
// SteamDemoAppId the demo build (what this project ships for the festival; ShipsDemo picks which one the build
// connects as). Achievements stay local while the running app is the demo (Achievements, Valve's guidance).
public static class StoreLinks
{
    public const int SteamAppId = 0;
    public const int SteamDemoAppId = 0;

    // This build is the festival demo: it connects to Steam as SteamDemoAppId.
    public const bool ShipsDemo = true;

    public static bool HasSteamPage => SteamAppId > 0;

    public static string SteamPageUrl => "https://store.steampowered.com/app/" + SteamAppId + "/";

    // The App ID this build runs as (0 = Steam off).
    public static int RunningAppId => ShipsDemo ? SteamDemoAppId : SteamAppId;

    public static bool IsDemoApp(uint appId)
    {
        return SteamDemoAppId > 0 && appId == (uint)SteamDemoAppId;
    }

    public static void OpenWishlist()
    {
        if (!HasSteamPage)
        {
            return;
        }

        if (SteamPlatform.IsReady && SteamUtils.IsOverlayEnabled())
        {
            SteamFriends.ActivateGameOverlayToStore(new AppId_t((uint)SteamAppId), EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
            return;
        }

        Application.OpenURL(SteamPageUrl);
    }
}
