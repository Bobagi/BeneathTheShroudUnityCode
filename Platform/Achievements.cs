using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;

// The game's achievements (GDD "Conquistas da Steam", 21/10). Every unlock is first written to a local record,
// achievements.json next to the save (and mirrored to Steam Cloud by SteamCloud), and only then sent to Steam.
// Valve's guidance for demos is to leave achievements off in the demo app and grant them when the full game loads
// (partner.steamgames.com/doc/store/application/demos): so while the running App ID is the demo's
// (StoreLinks.IsDemoApp) nothing goes to Steam, and the full game's first connection (SteamPlatform calls
// PushRecordToSteam) unlocks on the player's account everything the record holds, the demo's included.
// Ids are the API names to create in Steamworks (App Admin > Stats & Achievements); names and descriptions in the
// three languages are the ach.<ID>.name / ach.<ID>.desc lines of ui.txt, and Docs/Steam/STEAMWORKS.md lists them all
// with how each one is earned. A new achievement = a constant here, the two lines in ui.txt, the call where it is earned.
public static class Achievements
{
    public const string FirstTape = "ACH_FIRST_TAPE";
    public const string Light = "ACH_LIGHT";
    public const string Chase = "ACH_CHASE";
    public const string Generators = "ACH_GENERATORS";
    public const string Case = "ACH_CASE";
    public const string Box = "ACH_BOX";
    public const string Demo = "ACH_DEMO";
    public const string NoDeath = "ACH_NO_DEATH";
    public const string Kill = "ACH_KILL";
    public const string Headshot = "ACH_HEADSHOT";
    public const string Stun = "ACH_STUN";
    public const string Archive = "ACH_ARCHIVE";
    public const string Tapes = "ACH_TAPES";
    public const string OwnShot = "ACH_OWN_SHOT";

    // Stats (int) to create in Steamworks next to the achievements.
    public const string StatKills = "STAT_KILLS";
    public const string StatDeaths = "STAT_DEATHS";

    public static readonly string[] All =
    {
        FirstTape, Light, Chase, Generators, Case, Box, Demo, NoDeath, Kill, Headshot, Stun, Archive, Tapes, OwnShot,
    };

    public const string FileName = "achievements.json";

    [System.Serializable]
    private sealed class Record
    {
        public List<string> unlocked = new List<string>();
        public List<string> statNames = new List<string>();
        public List<int> statValues = new List<int>();
    }

    private static Record record;
    private static readonly HashSet<string> MissingOnSteam = new HashSet<string>();

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // Unlocked on this machine (or pulled from the cloud), whatever Steam knows.
    public static event System.Action<string> Unlocked;

    // Whether unlocks go to Steam now: connected, and not the demo app.
    public static bool ReportsToSteam => SteamPlatform.IsReady && !StoreLinks.IsDemoApp(SteamPlatform.AppId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        record = null;
        MissingOnSteam.Clear();
        CreatureVitals.AnyKilled -= OnCreatureKilled;
        CreatureVitals.AnyKilled += OnCreatureKilled;
        TorchStun.AnyStunned -= OnStunned;
        TorchStun.AnyStunned += OnStunned;
    }

    public static bool IsUnlocked(string id)
    {
        return Load().unlocked.Contains(id);
    }

    public static IReadOnlyList<string> UnlockedIds => Load().unlocked;

    public static void Unlock(string id)
    {
        Record r = Load();
        if (string.IsNullOrEmpty(id) || r.unlocked.Contains(id))
        {
            return;
        }

        r.unlocked.Add(id);
        Save();
        Debug.Log("[Conquista] " + id + (ReportsToSteam ? " (Steam)" : " (registro local)"));
        if (ReportsToSteam)
        {
            if (SetOnSteam(id))
            {
                SteamUserStats.StoreStats();
            }
        }
        else
        {
            // Steam marks its own unlocks on the recording's timeline; the demo's local ones get a marker here.
            SteamPresence.Event(Loc.T("ach." + id + ".name"), Loc.T("ach." + id + ".desc"), "steam_achievement", 200, false);
        }

        Unlocked?.Invoke(id);
    }

    public static int Stat(string name)
    {
        Record r = Load();
        int i = r.statNames.IndexOf(name);
        return i >= 0 ? r.statValues[i] : 0;
    }

    public static void AddStat(string name, int amount = 1)
    {
        Record r = Load();
        int i = r.statNames.IndexOf(name);
        if (i < 0)
        {
            r.statNames.Add(name);
            r.statValues.Add(0);
            i = r.statNames.Count - 1;
        }

        r.statValues[i] += amount;
        Save();
        if (ReportsToSteam && SteamUserStats.SetStat(name, r.statValues[i]))
        {
            SteamUserStats.StoreStats();
        }
    }

    // On connecting to the full game: everything the record holds goes to the account (the demo's unlocks, ones
    // earned offline). Steam ignores achievements already unlocked; stats only ever go up.
    public static void PushRecordToSteam()
    {
        if (!ReportsToSteam)
        {
            return;
        }

        Record r = Load();
        bool changed = false;
        foreach (string id in r.unlocked)
        {
            if (SteamUserStats.GetAchievement(id, out bool achieved) && !achieved)
            {
                changed |= SetOnSteam(id);
            }
        }

        for (int i = 0; i < r.statNames.Count; i++)
        {
            if (SteamUserStats.GetStat(r.statNames[i], out int onSteam) && onSteam < r.statValues[i])
            {
                changed |= SteamUserStats.SetStat(r.statNames[i], r.statValues[i]);
            }
        }

        if (changed)
        {
            SteamUserStats.StoreStats();
        }
    }

    // Read again from disk (and merged with Steam Cloud, when connected): SteamPlatform calls it on connecting.
    public static void Reload()
    {
        record = null;
    }

    // Erases the local record (and, with Steam connected, the account's stats and achievements for this app).
    // For tests only: the menu "Horror > Steam" uses it.
    public static void ResetAll(bool steamToo)
    {
        record = new Record();
        Save();
        if (steamToo && SteamPlatform.IsReady)
        {
            SteamUserStats.ResetAllStats(true);
            SteamUserStats.StoreStats();
        }
    }

    private static bool SetOnSteam(string id)
    {
        if (SteamUserStats.SetAchievement(id))
        {
            return true;
        }

        // Not created in Steamworks yet (or a test app like Spacewar): said once, then quiet.
        if (MissingOnSteam.Add(id))
        {
            Debug.LogWarning("[Conquista] " + id + " não existe no App " + SteamPlatform.AppId + " da Steam (criar em Stats & Achievements).");
        }

        return false;
    }

    private static void OnCreatureKilled(CreatureVitals creature, CreatureHitbox hitbox)
    {
        AddStat(StatKills);
        Unlock(Kill);
        if (hitbox != null && hitbox.region == BodyRegion.Head)
        {
            Unlock(Headshot);
        }
    }

    private static void OnStunned()
    {
        Unlock(Stun);
    }

    private static Record Load()
    {
        if (record != null)
        {
            return record;
        }

        record = new Record();
        try
        {
            if (File.Exists(FilePath))
            {
                record = JsonUtility.FromJson<Record>(File.ReadAllText(FilePath)) ?? new Record();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Conquista] não leu " + FilePath + ": " + e.Message);
        }

        Trim(record);
        // The copy in Steam Cloud (another PC, a reinstall) joins in: an achievement unlocked on either side
        // stays unlocked and a stat keeps the larger count. Merged, not "newest wins", so nothing is ever lost.
        string cloud = SteamCloud.ReadText(FileName);
        if (!string.IsNullOrEmpty(cloud) && Merge(record, cloud))
        {
            Save();
        }

        return record;
    }

    private static bool Merge(Record into, string json)
    {
        Record other;
        try
        {
            other = JsonUtility.FromJson<Record>(json);
        }
        catch (System.Exception)
        {
            return false;
        }

        if (other == null)
        {
            return false;
        }

        Trim(other);
        bool changed = false;
        foreach (string id in other.unlocked)
        {
            if (!into.unlocked.Contains(id))
            {
                into.unlocked.Add(id);
                changed = true;
            }
        }

        for (int i = 0; i < other.statNames.Count; i++)
        {
            int at = into.statNames.IndexOf(other.statNames[i]);
            if (at < 0)
            {
                into.statNames.Add(other.statNames[i]);
                into.statValues.Add(other.statValues[i]);
                changed = true;
            }
            else if (into.statValues[at] < other.statValues[i])
            {
                into.statValues[at] = other.statValues[i];
                changed = true;
            }
        }

        return changed;
    }

    // A hand-edited or old file with the two stat lists out of step: keep the pairs that line up.
    private static void Trim(Record r)
    {
        int pairs = Mathf.Min(r.statNames.Count, r.statValues.Count);
        r.statNames.RemoveRange(pairs, r.statNames.Count - pairs);
        r.statValues.RemoveRange(pairs, r.statValues.Count - pairs);
    }

    private static void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(record, true);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            File.Move(temp, FilePath);
            SteamCloud.Push(FileName, FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Conquista] não gravou " + FilePath + ": " + e.Message);
        }
    }
}
