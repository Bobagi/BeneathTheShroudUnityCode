using System.Collections.Generic;
using UnityEngine;

// Every player option, once: its label (a Loc key), how its value reads on screen and how the left/right arrows
// change it. The main menu (its rows are SettingRow components, editable in the prefab) and the pause menu both
// read this list, so an option added here shows up in both. GameSettings stores and applies the values.
public static class SettingsCatalog
{
    public enum Page { Controls, Sound, Graphics, Language }

    public sealed class Setting
    {
        public string id;
        public string labelKey;
        public Page page;
        public System.Func<string> value;
        public System.Action<int> step;
    }

    // Keys and what they do (Loc keys for the action), shown on the controls page.
    public static readonly (string keys, string actionKey)[] Bindings =
    {
        ("W A S D", "bind.move"),
        ("Mouse", "bind.look"),
        ("Shift", "bind.sprint"),
        ("E", "bind.use"),
        ("F", "bind.torch"),
        ("Tab", "bind.diary"),
        ("M", "bind.map"),
        ("G", "bind.filter"),
        ("Espaço / Space", "bind.up"),
        ("Ctrl / C", "bind.crouch"),
        ("Esc", "bind.pause"),
    };

    public static readonly Setting[] All =
    {
        new Setting
        {
            id = "sensitivity", labelKey = "opt.sensitivity", page = Page.Controls,
            value = () => GameSettings.MouseSensitivity.ToString("0.0") + "x",
            step = dir => GameSettings.SetMouseSensitivity(Mathf.Round((GameSettings.MouseSensitivity + dir * 0.1f) * 10f) / 10f),
        },
        new Setting
        {
            id = "volume", labelKey = "opt.volume", page = Page.Sound,
            value = () => Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%",
            step = dir => GameSettings.SetMasterVolume(Mathf.Round((GameSettings.MasterVolume + dir * 0.1f) * 10f) / 10f),
        },
        new Setting
        {
            id = "quality", labelKey = "opt.quality", page = Page.Graphics,
            value = () => Loc.T("quality." + GameSettings.QualityIndex),
            step = dir => GameSettings.SetQuality(GameSettings.QualityIndex + dir),
        },
        new Setting
        {
            id = "fps", labelKey = "opt.fps", page = Page.Graphics,
            value = () => GameSettings.VSync ? "V-Sync" : (GameSettings.FrameRateCap <= 0 ? Loc.T("opt.unlimited") : GameSettings.FrameRateCap + " FPS"),
            step = dir => GameSettings.SetFrameRateCap(Step(GameSettings.FrameRateCaps, GameSettings.FrameRateCap, dir)),
        },
        new Setting
        {
            id = "vsync", labelKey = "opt.vsync", page = Page.Graphics,
            value = () => Loc.T(GameSettings.VSync ? "opt.on" : "opt.off"),
            step = _ => GameSettings.SetVSync(!GameSettings.VSync),
        },
        new Setting
        {
            id = "brightness", labelKey = "opt.brightness", page = Page.Graphics,
            value = () => (GameSettings.Brightness >= 0f ? "+" : "") + GameSettings.Brightness.ToString("0.0"),
            step = dir => GameSettings.SetBrightness(Mathf.Round((GameSettings.Brightness + dir * 0.1f) * 10f) / 10f),
        },
        new Setting
        {
            id = "screen", labelKey = "opt.screen", page = Page.Graphics,
            value = () => Loc.T(GameSettings.Fullscreen ? "opt.fullscreen" : "opt.windowed"),
            step = _ => GameSettings.SetFullscreen(!GameSettings.Fullscreen),
        },
        new Setting
        {
            id = "resolution", labelKey = "opt.resolution", page = Page.Graphics,
            value = ResolutionLabel,
            step = dir =>
            {
                Resolution[] all = GameSettings.Resolutions();
                if (all.Length == 0)
                {
                    return;
                }

                int current = GameSettings.ResolutionIndex < 0 || GameSettings.ResolutionIndex >= all.Length ? all.Length - 1 : GameSettings.ResolutionIndex;
                GameSettings.SetResolution(Mathf.Clamp(current + dir, 0, all.Length - 1));
            },
        },
        new Setting
        {
            id = "language", labelKey = "opt.language", page = Page.Language,
            value = () => Loc.Names[Loc.Language],
            step = dir => Loc.SetLanguage((Loc.Language + dir + Loc.Codes.Length) % Loc.Codes.Length),
        },
    };

    public static Setting Find(string id)
    {
        foreach (Setting setting in All)
        {
            if (setting.id == id)
            {
                return setting;
            }
        }

        return null;
    }

    public static IEnumerable<Setting> On(Page page)
    {
        foreach (Setting setting in All)
        {
            if (setting.page == page)
            {
                yield return setting;
            }
        }
    }

    private static string ResolutionLabel()
    {
        Resolution[] all = GameSettings.Resolutions();
        if (all.Length == 0)
        {
            return Screen.width + " x " + Screen.height;
        }

        Resolution r = all[GameSettings.ResolutionIndex < 0 || GameSettings.ResolutionIndex >= all.Length ? all.Length - 1 : GameSettings.ResolutionIndex];
        return r.width + " x " + r.height;
    }

    private static int Step(int[] options, int current, int dir)
    {
        int index = System.Array.IndexOf(options, current);
        index = index < 0 ? 0 : Mathf.Clamp(index + dir, 0, options.Length - 1);
        return options[index];
    }
}
