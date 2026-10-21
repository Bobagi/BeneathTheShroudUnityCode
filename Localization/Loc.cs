using System.Collections.Generic;
using UnityEngine;

// The game's languages. Every string the menus show lives in one table, Settings/Resources/Localization/ui.txt,
// one line per key and one column per language separated by TABs (key, pt-BR, en, es): it opens in any spreadsheet, so a
// translator never touches code. Loc.T("menu.play") gives the text in the chosen language, falling back to
// Portuguese and then to the key itself, so a missing line shows up on screen instead of hiding.
// The choice is saved in PlayerPrefs; the first run follows the system language.
// Everything the player reads is in the table, the story included (Dayse's script is the pt-BR column: recordings,
// clippings, notes, photos, subtitles, objectives, prompts, the house system). The voices stay Portuguese; only the
// subtitle follows the language. Scene components (ReadableNote, Recorder, StoryPickup, Door...) are built with the
// KEY in their text fields and call Resolve when they show it, so a scene built before the table (plain Portuguese
// in those fields) still shows its text.
public static class Loc
{
    public static readonly string[] Codes = { "pt-BR", "en", "es" };
    public static readonly string[] Names = { "Português (Brasil)", "English", "Español" };

    private const string PrefKey = "Language";
    private static Dictionary<string, string[]> table;

    public static int Language { get; private set; }

    public static string Code => Codes[Language];

    public static event System.Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        table = null;
        Language = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, SystemDefault()), 0, Codes.Length - 1);
    }

    public static void SetLanguage(int index)
    {
        Language = Mathf.Clamp(index, 0, Codes.Length - 1);
        PlayerPrefs.SetInt(PrefKey, Language);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    // The first run, with no choice saved yet, follows the language the player set for the game in Steam
    // (SteamApps.GetCurrentGameLanguage: "brazilian", "english", "latam"...). A choice made in the menu always wins.
    public static void FollowPlatformLanguage(string steamLanguage)
    {
        if (PlayerPrefs.HasKey(PrefKey) || string.IsNullOrEmpty(steamLanguage))
        {
            return;
        }

        int index;
        switch (steamLanguage)
        {
            case "brazilian":
            case "portuguese":
                index = 0;
                break;
            case "spanish":
            case "latam":
                index = 2;
                break;
            case "english":
                index = 1;
                break;
            default:
                return;
        }

        if (index != Language)
        {
            SetLanguage(index);
        }
    }

    public static string T(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        Load();
        if (!table.TryGetValue(key, out string[] row))
        {
            return key;
        }

        string text = Language < row.Length ? row[Language] : null;
        return string.IsNullOrEmpty(text) ? (string.IsNullOrEmpty(row[0]) ? key : row[0]) : text;
    }

    // T(key) with {0}, {1}... filled in.
    public static string F(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    public static bool Has(string key)
    {
        Load();
        return !string.IsNullOrEmpty(key) && table.ContainsKey(key);
    }

    // A serialized text field that may hold a key (scenes built since the table) or plain text (older scenes):
    // the translation when it is a key, the text itself otherwise. No allocation: two dictionary lookups.
    public static string Resolve(string textOrKey)
    {
        return Has(textOrKey) ? T(textOrKey) : textOrKey;
    }

    private static void Load()
    {
        if (table != null)
        {
            return;
        }

        table = new Dictionary<string, string[]>();
        TextAsset asset = Resources.Load<TextAsset>("Localization/ui");
        if (asset == null)
        {
            Debug.LogWarning("[Loc] Settings/Resources/Localization/ui.txt not found");
            return;
        }

        foreach (string raw in asset.text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] cells = line.Split('\t');
            if (cells.Length < 2 || cells[0] == "key")
            {
                continue;
            }

            string[] row = new string[Codes.Length];
            for (int i = 0; i < row.Length && i + 1 < cells.Length; i++)
            {
                // "\n" written in a cell is a line break on screen.
                row[i] = cells[i + 1].Replace("\\n", "\n");
            }

            table[cells[0]] = row;
        }
    }

    private static int SystemDefault()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Portuguese:
                return 0;
            case SystemLanguage.Spanish:
                return 2;
            default:
                return 1;
        }
    }

#if UNITY_EDITOR
    // The table is re-read when it changes in the editor.
    public static void Reload()
    {
        table = null;
        Changed?.Invoke();
    }
#endif
}
