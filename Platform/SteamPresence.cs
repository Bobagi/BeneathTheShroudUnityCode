using Steamworks;
using UnityEngine;

// What Steam shows about the game while it runs, all optional and silent without Steam:
//   Rich presence: the line under the player's name in the friends list ("Beneath the Shroud - No porão alagado").
//   Status(token) sets "steam_display" to a token of Docs/Steam/rich_presence.vdf (#Status_Menu, #Status_Dark...),
//   which must be uploaded in Steamworks (App Admin > Rich Presence) with the three languages; until then Steam
//   just shows the game's name.
//   Timeline (Steam game recording, SDK 1.61+): Mode() colours the bar (playing, menus, loading) and Event() drops a
//   marker the player can jump to and clip (a death, the chase, the end). Icons are Steam's defaults (steam_death...).
public static class SteamPresence
{
    public const string Menu = "#Status_Menu";
    public const string Opening = "#Status_Opening";
    public const string Explore = "#Status_Explore";
    public const string Dark = "#Status_Dark";
    public const string Chase = "#Status_Chase";
    public const string Spores = "#Status_Spores";
    public const string Basement = "#Status_Basement";
    public const string Escape = "#Status_Escape";
    public const string Done = "#Status_Done";

    private static string current;

    public static string Current => current;

    public static void Status(string token)
    {
        current = token;
        if (SteamPlatform.IsReady)
        {
            SteamFriends.SetRichPresence("steam_display", token);
        }
    }

    // Sent again on connecting (the status may have been set before Steam was up).
    public static void Reapply()
    {
        if (!string.IsNullOrEmpty(current))
        {
            Status(current);
        }
    }

    public static void Mode(ETimelineGameMode mode)
    {
        if (SteamPlatform.IsReady)
        {
            SteamTimeline.SetTimelineGameMode(mode);
        }
    }

    // A marker on the recording's timeline. priority: 0 to 1000 (which icon wins when two overlap);
    // clip: whether Steam offers it as a clip (Featured) or only marks it.
    public static void Event(string title, string description, string icon, uint priority, bool clip)
    {
        if (!SteamPlatform.IsReady)
        {
            return;
        }

        SteamTimeline.AddInstantaneousTimelineEvent(title, description, icon, priority, 0f,
            clip ? ETimelineEventClipPriority.k_ETimelineEventClipPriority_Featured : ETimelineEventClipPriority.k_ETimelineEventClipPriority_Standard);
    }

    // The scene decides the base status: the house's director refines it with the story stage.
    public static void ForScene(string scene)
    {
        if (scene == SceneFlow.MainMenuScene)
        {
            Status(Menu);
            Mode(ETimelineGameMode.k_ETimelineGameMode_Menus);
        }
        else if (scene == SceneFlow.OpeningScene)
        {
            Status(Opening);
            Mode(ETimelineGameMode.k_ETimelineGameMode_Playing);
        }
        else
        {
            Mode(ETimelineGameMode.k_ETimelineGameMode_Playing);
        }
    }

    public static void Loading()
    {
        Mode(ETimelineGameMode.k_ETimelineGameMode_LoadingScreen);
    }
}
