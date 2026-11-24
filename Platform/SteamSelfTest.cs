using System.Text;
using Steamworks;
using UnityEngine;

// Checks, in Play with Steam connected, every Steamworks feature the game uses, against the live client:
// achievements and stats (with Spacewar, App 480, on its own ACH_WIN_ONE_GAME and NumGames, set and put back),
// Steam Cloud (writes, reads back and deletes a small file), rich presence (sets and reads back our own),
// the timeline (adds a marker) and the overlay. Nothing of the game's own record is touched.
// "Horror > Steam > Conferir recursos da Steam (em Play)" runs it; the report goes to the console.
public static class SteamSelfTest
{
    private const uint Spacewar = 480;

    public static string Run()
    {
        StringBuilder r = new StringBuilder();
        if (!SteamPlatform.IsReady)
        {
            return "[Steam] Desligada: ligue o teste com o App ID 480 e entre em Play (o cliente Steam precisa estar aberto).";
        }

        r.AppendLine("[Steam] App " + SteamPlatform.AppId + ", jogador " + SteamPlatform.PlayerName + ", idioma da Steam " + SteamApps.GetCurrentGameLanguage()
            + ", conquistas para a Steam: " + (Achievements.ReportsToSteam ? "sim" : "não (app da demo)"));

        // Achievements and stats.
        string ach = SteamPlatform.AppId == Spacewar ? "ACH_WIN_ONE_GAME" : Achievements.Demo;
        bool known = SteamUserStats.GetAchievement(ach, out bool before);
        bool set = known && SteamUserStats.SetAchievement(ach);
        SteamUserStats.GetAchievement(ach, out bool after);
        if (known && !before)
        {
            SteamUserStats.ClearAchievement(ach);
        }

        SteamUserStats.StoreStats();
        r.AppendLine("Conquista " + ach + ": existe " + known + ", desbloqueou " + (set && after) + (known && !before ? " (desfeita)" : " (já estava)"));
        bool ours = SteamUserStats.GetAchievement(Achievements.FirstTape, out bool _);
        r.AppendLine("Conquista " + Achievements.FirstTape + " existe neste app: " + ours + (SteamPlatform.AppId == Spacewar ? " (esperado false no Spacewar)" : string.Empty));

        string stat = SteamPlatform.AppId == Spacewar ? "NumGames" : Achievements.StatKills;
        if (SteamUserStats.GetStat(stat, out int value))
        {
            bool wrote = SteamUserStats.SetStat(stat, value + 1);
            SteamUserStats.GetStat(stat, out int read);
            SteamUserStats.SetStat(stat, value);
            SteamUserStats.StoreStats();
            r.AppendLine("Estatística " + stat + ": " + value + " -> " + read + " (gravou " + wrote + ", voltou a " + value + ")");
        }
        else
        {
            r.AppendLine("Estatística " + stat + ": não existe neste app");
        }

        // Cloud.
        bool account = SteamRemoteStorage.IsCloudEnabledForAccount();
        bool app = SteamRemoteStorage.IsCloudEnabledForApp();
        SteamRemoteStorage.GetQuota(out ulong total, out ulong free);
        string line = "Steam Cloud: conta " + account + ", app " + app + ", cota " + total / 1024 + " KB (livres " + free / 1024 + " KB)";
        if (account && app)
        {
            byte[] data = Encoding.UTF8.GetBytes("beneath the shroud " + System.DateTime.UtcNow.ToString("o"));
            bool wrote = SteamRemoteStorage.FileWrite("selftest.txt", data, data.Length);
            byte[] back = new byte[data.Length];
            int read = wrote ? SteamRemoteStorage.FileRead("selftest.txt", back, back.Length) : 0;
            bool same = read == data.Length && Encoding.UTF8.GetString(back) == Encoding.UTF8.GetString(data);
            bool deleted = wrote && SteamRemoteStorage.FileDelete("selftest.txt");
            line += "; gravou " + wrote + ", leu igual " + same + ", apagou " + deleted;
            line += "; save.json na nuvem " + SteamRemoteStorage.FileExists(SaveGame.CloudName) + ", achievements.json " + SteamRemoteStorage.FileExists(Achievements.FileName);
        }

        r.AppendLine(line);

        // Rich presence.
        string previous = SteamPresence.Current;
        bool presence = SteamFriends.SetRichPresence("steam_display", SteamPresence.Basement);
        string readBack = SteamFriends.GetFriendRichPresence(SteamUser.GetSteamID(), "steam_display");
        r.AppendLine("Presença: gravou " + presence + ", lida de volta \"" + readBack + "\"");
        if (!string.IsNullOrEmpty(previous))
        {
            SteamPresence.Status(previous);
        }

        // Timeline and overlay.
        TimelineEventHandle_t marker = SteamTimeline.AddInstantaneousTimelineEvent("Teste", "SteamSelfTest", "steam_marker", 0, 0f,
            ETimelineEventClipPriority.k_ETimelineEventClipPriority_None);
        r.AppendLine("Linha do tempo: marcador " + marker.m_TimelineEventHandle + "; overlay ligado " + SteamUtils.IsOverlayEnabled());
        if (marker.m_TimelineEventHandle != 0)
        {
            SteamTimeline.RemoveTimelineEvent(marker);
        }

        string report = r.ToString();
        Debug.Log(report);
        return report;
    }
}
