using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

// Switches the house's surface materials (Art/Materials/FloodedHouse/<Set>.mat, the sets of LunaManor.SurfaceTiles
// and the older FloodedHouseBuilder ones) between URP Lit and Horror/Lit Hex: the same Lit with hex tiling
// (Mikkelsen 2022, Art/Shaders/HexLit) that hides the repeat of a 2 m texture on the big walls and floors. The
// properties keep their names in both shaders, so they survive the swap; the keywords that depend on textures
// (_NORMALMAP, _METALLICSPECGLOSSMAP, _OCCLUSIONMAP, _EMISSION, ...) are re-derived as the Lit inspector does.
// Menus: Horror > Shaders. Code: HexTileMaterials.Convert(on, paths...).
public static class HexTileMaterials
{
    public const string HexShaderName = "Horror/Lit Hex";
    public const string LitShaderName = "Universal Render Pipeline/Lit";

    // Surfaces without a regular pattern (plaster, stucco, concrete, peeling paint, grass, gravel): random offsets and
    // rotations hide the repeat and nothing has to line up. These are the big walls (rotunda and galleries: WornPlaster;
    // facade: CreamFacade; service rooms: RoughPlaster) where the repeat shows.
    public static readonly string[] StochasticSurfaces =
    {
        "WornPlaster", "CleanPlaster", "PaintedPlaster", "CeilingPlaster", "InteriorCeiling", "RoughPlaster",
        "MossyStucco", "WhiteStucco", "CreamFacade", "OchrePlinth",
        "ConcreteDamaged", "RoughConcrete", "WornConcreteFloor",
        "PeelingWall", "PeelingTimber",
        "GardenGrass", "Pebbles",
        // 13/11: the sets that replaced CreamFacade, WornPlaster, RoughPlaster and GardenGrass (and the courtyard copy).
        "CreamStucco", "CreamStucco_18", "InteriorPlaster", "GardenGround",
    };

    // Surfaces with a grid or motif (tiles, boards, planks, parquet, period wallpapers): every hex border shifts the
    // pattern, so these only get hex tiling when asked for all surfaces, and with rotation 0 (the hash offset still
    // breaks the repeat; the grout lines stay vertical and horizontal).
    public static readonly string[] PatternedSurfaces =
    {
        "Parquet", "WornWoodFloor", "Floorboards", "HallPlanks", "DarkPlanks", "WallBoards", "DarkPanels",
        "WallTiles", "KitchenFloor", "LabTile", "Azulejo", "MarbleTiles", "TerracottaFloor", "MosaicFloor", "BathMosaic", "BathWallTiles",
        "RoofTiles", "SlateRoof", "SlateRoof2", "KitchenTiles", "WhitewashedBrick",
        "OldWallpaper", "WallpaperMold", "WallpaperGold", "WallpaperRed", "WallpaperGreen",
    };

    public static string MaterialPath(string set) => $"{ProjectPaths.FloodedHouseMaterials}/{set}.mat";

    [MenuItem("Horror/Shaders/Hex tiling nas superfícies da casa (sem padrão regular)")]
    public static void HexOnStochastic() => Report(Convert(true, Paths(StochasticSurfaces)), true);

    [MenuItem("Horror/Shaders/Hex tiling em todas as superfícies da casa")]
    public static void HexOnAll() => Report(Convert(true, Paths(StochasticSurfaces, PatternedSurfaces)), true);

    [MenuItem("Horror/Shaders/Voltar ao Lit padrão em todas as superfícies da casa")]
    public static void HexOffAll() => Report(Convert(false, Paths(StochasticSurfaces, PatternedSurfaces)), false);

    // Each material asset goes to Horror/Lit Hex with hex tiling on (on = true) or back to URP Lit (on = false), keeping
    // every property and keyword, and is saved. Returns how many materials were converted.
    public static int Convert(bool on, params string[] materialPaths)
    {
        List<Material> materials = new List<Material>();
        foreach (string path in materialPaths)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Debug.LogWarning($"[HexTile] Material not found, skipped: {path}");
                continue;
            }

            materials.Add(material);
        }

        int converted = Convert(on, materials.ToArray());
        AssetDatabase.SaveAssets();
        return converted;
    }

    // The same on loaded materials, not saved here. A material coming from Lit gets the defaults (scale 1, contrast
    // 0.5, rotation 0.3, or 0 for a patterned surface, recognised by the material name); one already on the hex shader
    // keeps whatever was tuned in the inspector.
    public static int Convert(bool on, params Material[] materials)
    {
        Shader target = Shader.Find(on ? HexShaderName : LitShaderName);
        if (target == null)
        {
            Debug.LogError($"[HexTile] Shader not found: {(on ? HexShaderName : LitShaderName)}");
            return 0;
        }

        int converted = 0;
        foreach (Material material in materials)
        {
            if (material == null)
            {
                continue;
            }

            bool swapped = material.shader != target;
            if (swapped)
            {
                material.shader = target;
            }

            if (on)
            {
                material.SetFloat("_HexTile", 1f);
                if (swapped)
                {
                    bool patterned = System.Array.IndexOf(PatternedSurfaces, material.name) >= 0;
                    material.SetFloat("_HexScale", 1f);
                    material.SetFloat("_HexContrast", 0.5f);
                    material.SetFloat("_HexRotation", patterned ? 0f : 0.3f);
                }
            }
            else if (material.HasProperty("_HexTile"))
            {
                material.SetFloat("_HexTile", 0f);
            }

            // Keywords from the textures and values, as the Lit inspector does after every change (surface type, blend,
            // render queue, _EMISSION, _NORMALMAP, _METALLICSPECGLOSSMAP, _OCCLUSIONMAP, _PARALLAXMAP, details, hex).
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, HexLitShaderGUI.SetHexLitKeywords);
            if (!on)
            {
                material.DisableKeyword("_HEXTILE_ON");
            }

            EditorUtility.SetDirty(material);
            converted++;
        }

        return converted;
    }

    private static string[] Paths(params string[][] sets)
    {
        List<string> paths = new List<string>();
        foreach (string[] group in sets)
        {
            foreach (string set in group)
            {
                paths.Add(MaterialPath(set));
            }
        }

        return paths.ToArray();
    }

    // What Rebuild Everything applies after creating the house materials (Material() resets every shader to Lit).
    public static int ApplyHouseDefault() => Convert(true, Paths(StochasticSurfaces));

    private static void Report(int converted, bool on)
    {
        Debug.Log($"[HexTile] {converted} material(s) now on {(on ? HexShaderName + " with hex tiling" : LitShaderName)}.");
    }
}
