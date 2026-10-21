using UnityEditor;
using UnityEngine;

// Turns the Steam connection on in the editor before the game has its own App ID, using Valve's public test app
// 480 (Spacewar). While on, entering Play connects to the Steam client and your profile shows "Spacewar" as played.
// In Play, "Conferir recursos da Steam" runs SteamSelfTest (achievements, stats, cloud, presence, timeline, overlay);
// "Apagar conquistas locais" empties achievements.json (and the cloud copy) to test unlocks again.
public static class SteamMenu
{
    private const string TestMenu = "Horror/Steam/Testar com o App ID 480 (Spacewar)";
    private const int SpacewarAppId = 480;

    [MenuItem(TestMenu)]
    private static void ToggleTest()
    {
        bool on = EditorPrefs.GetInt(SteamPlatform.EditorTestAppIdKey, 0) > 0;
        EditorPrefs.SetInt(SteamPlatform.EditorTestAppIdKey, on ? 0 : SpacewarAppId);
        Debug.Log(on ? "[Steam] Teste no editor desligado." : "[Steam] Teste no editor ligado (App ID 480): entre em Play.");
    }

    [MenuItem(TestMenu, true)]
    private static bool ToggleTestCheck()
    {
        Menu.SetChecked(TestMenu, EditorPrefs.GetInt(SteamPlatform.EditorTestAppIdKey, 0) > 0);
        return true;
    }

    [MenuItem("Horror/Steam/Conferir recursos da Steam (em Play)")]
    private static void SelfTest()
    {
        SteamSelfTest.Run();
    }

    [MenuItem("Horror/Steam/Conferir recursos da Steam (em Play)", true)]
    private static bool SelfTestCheck()
    {
        return Application.isPlaying;
    }

    [MenuItem("Horror/Steam/Apagar conquistas locais")]
    private static void ResetLocal()
    {
        Achievements.ResetAll(false);
        Debug.Log("[Conquista] Registro local apagado: " + Achievements.FilePath);
    }
}
