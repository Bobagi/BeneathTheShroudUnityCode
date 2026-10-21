using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The demo's save: one checkpoint file, written by the house's director every time the story moves on (and
// when an item is picked up), read by "Continuar"/"Carregar" in the main menu. JSON in persistentDataPath
// (on Windows: %USERPROFILE%/AppData/LocalLow/<company>/Projeto Ilha/save.json), so it survives updates.
// Loading: the menu sets PendingLoad and loads the house; the director takes it in Start and rebuilds that
// moment (items, diary, doors, power, the wall, the suit, the stage) instead of playing the opening.
// Steam Cloud (SteamCloud): every write is mirrored to the cloud, and a newer cloud copy (another PC) replaces the
// file on disk before it is read. The run's deaths travel with it (the "no deaths" achievement survives a Continue).
public static class SaveGame
{
    public const int Version = 1;

    [System.Serializable]
    public sealed class Data
    {
        public int version = Version;
        public string scene;
        public string stage;
        public string savedAt;
        public float playSeconds;
        public int deaths;

        public Vector3 position;
        public float yaw;

        public List<string> items = new List<string>();
        public List<string> archive = new List<string>();
        public List<string> doorsOpen = new List<string>();
        public List<string> pickupsTaken = new List<string>();
        public List<string> recordersHeard = new List<string>();

        public bool hasTorch;
        public bool hasBatteries;
        public bool hasGear;
        public bool torchOn;
        public bool hasMask;
        public int spareFilters;
        public float filter = 1f;

        public bool powerOn = true;
        public bool wallBroken;
        public bool hatchDown;
        public bool blockerUnlocked;
        public bool blockerPushed;
        public bool blackoutDone;
        public bool dehumidifierSeen;
        public bool mapHintShown;
        public int hours = 5;

        // Combat (17/10): the revolver's chambers and reserve, what is in the hand, enemies already dead by name,
        // whether the stair grab of the finale already happened.
        public int roundsLoaded = -1;
        public int roundsSpent;
        public int roundsSpare;
        public string hand = "";
        public List<string> enemiesDead = new List<string>();
        public bool grabbed;
    }

    private static Data cached;
    private static bool cacheRead;

    public static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    // Set by the menu right before loading the scene; the director takes it once.
    public static Data PendingLoad { get; set; }

    public const string CloudName = "save.json";

    // Seconds played in this run, kept across checkpoints (the director adds to it).
    public static float PlaySeconds { get; set; }

    // Times the player died in this run (new game = 0, Continue = what the save had).
    public static int Deaths { get; set; }

    public static bool Exists => Read() != null;

    public static event System.Action Saved;

    public static Data Read()
    {
        if (cacheRead)
        {
            return cached;
        }

        cacheRead = true;
        cached = null;
        SteamCloud.PullIfNewer(CloudName, FilePath);
        try
        {
            if (File.Exists(FilePath))
            {
                Data data = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                // A save from another version of the demo is not trusted: the house may have changed under it.
                cached = data != null && data.version == Version && !string.IsNullOrEmpty(data.stage) ? data : null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] could not read " + FilePath + ": " + e.Message);
        }

        return cached;
    }

    public static void Write(Data data)
    {
        data.version = Version;
        data.savedAt = System.DateTime.Now.ToString("o");
        data.playSeconds = PlaySeconds;
        data.deaths = Deaths;
        try
        {
            // Written next to the old one and swapped in, so a crash mid-write never leaves a broken save.
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            File.Move(temp, FilePath);
            SteamCloud.Push(CloudName, FilePath);
            cached = data;
            cacheRead = true;
            Saved?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] could not write " + FilePath + ": " + e.Message);
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            SteamCloud.Delete(CloudName);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] could not delete " + FilePath + ": " + e.Message);
        }

        cached = null;
        cacheRead = true;
    }

    // The scene object's place in the hierarchy ("FloodedHouse_Generated/Doors/DehumidifierDoor"): how a save
    // names a door or a pickup. Generated scenes rebuild the same names, so it holds across a rebuild.
    public static string PathOf(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            path = p.name + "/" + path;
        }

        return path;
    }

    // "1 h 05 min" / "12 min".
    public static string FormatPlayTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        return minutes >= 60 ? (minutes / 60) + " h " + (minutes % 60).ToString("00") + " min" : Mathf.Max(1, minutes) + " min";
    }
}
