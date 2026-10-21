using System.Linq;
using UnityEditor;
using UnityEngine;

// Import rules for everything under Audio, and the SoundLibrary filled from the converted files
// (SourceAssets/Tools/convert_sounds.py writes them: WAV, 16-bit, 48 kHz).
//   loops ("_loop" in the name) and music (Audio/Music): Vorbis, streamed from disk, stereo kept (2D ambience)
//   one-shots: decompressed on load as ADPCM (cheap to start, played often), forced to mono (3D sources)
//   voice (Audio/Voice): Vorbis kept compressed in memory, mono
public sealed class AudioImportRules : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(ProjectPaths.Audio + "/") || !assetPath.EndsWith(".wav"))
        {
            return;
        }

        AudioImporter importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        bool loop = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_loop");
        if (assetPath.Contains("/Voice/"))
        {
            // Lines up to a minute long: kept compressed in memory, decoded while playing.
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.forceToMono = true;
        }
        else if (loop || assetPath.Contains("/Music/"))
        {
            // Loops and music (the arrival theme, 66 s of stereo): streamed, stereo kept.
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.forceToMono = false;
            importer.loadInBackground = true;
        }
        else
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            // Menu sounds play in 2D: a stereo file there keeps its width (the start whoosh, 10/10).
            importer.forceToMono = !assetPath.Contains("/Audio/UI/");
        }

        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
    }
}

public static class SoundLibraryMenu
{
    public const string AssetPath = ProjectPaths.Root + "/Settings/Resources/" + SoundLibrary.ResourceName + ".asset";

    [MenuItem("Horror/Som/Atualizar biblioteca de sons")]
    public static void Open()
    {
        Selection.activeObject = Refresh();
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    public static SoundLibrary Refresh()
    {
        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, AssetPath);
        }

        library.splashSoft = Clip("Water/splash_soft");
        library.splashMedium = Clips("Water", "splash_medium_");
        library.splashBig = Clip("Water/splash_big");
        library.splashHeardUnderwater = Clip("Water/splash_heard_underwater");
        library.emergeHeardUnderwater = Clip("Water/emerge_heard_underwater");
        library.drips = Clips("Water", "drip_");
        library.dripsLoop = Clip("Water/drips_soft_loop");
        library.underwaterLoop = Clip("Water/underwater_ambience_loop");
        library.woodCreaks = Clips("Footsteps/creak", "wood_creak_");
        library.waterSteps = Clips("Footsteps/water", "water_step_");
        library.rainRoofInsideLoop = Clip("Ambience/rain_roof_inside_loop");
        library.seaLoop = Clip("Ambience/sea_rocks_loop");
        library.houseCreaks = new[] { Clip("Ambience/house_creak") }.Where(c => c != null).ToArray();
        library.heavyBreaths = Clips("Breath", "breath_heavy_");
        library.exhaustedBreathingLoop = Clip("Breath/breathing_exhausted_loop");
        library.coughs = Clips("Breath", "cough_");
        library.gasMaskBreathingLoop = Clip("Breath/gas_mask_breathing_loop");
        library.thunderClose = Clips("Ambience", "thunder_close_");
        library.thunderFar = Clips("Ambience", "thunder_far_");
        library.powerCut = Clip("Ambience/power_cut");
        library.wallCollapse = Clip("Ambience/wall_collapse");
        library.atticLadder = Clip("Handling/attic_ladder");
        library.metalBoxOpen = Clip("Handling/metal_box_open");
        library.metalBoxClose = Clip("Handling/metal_box_close");
        library.torchClicks = Clips("Handling", "torch_click_");
        library.batteriesInsert = Clip("Handling/batteries_insert");
        library.creatureCrawlLoop = Clip("Creature/creature_crawl_loop");
        library.clawsInWall = Clips("Creature", "claws_wall_");
        library.luminaTone = Clip("Creature/lumina_tone");
        library.luminaHumLoop = Clip("Creature/lumina_hum_loop");
        library.wormsLoop = Clip("Creature/worms_wet_loop");
        library.visorCracks = Clips("Player", "visor_crack_");
        library.outboardMotorLoop = Clip("Ambience/outboard_motor_loop");
        library.rainOutdoorLoop = Clip("Ambience/rain_outdoor_loop");
        library.seaNightLoop = Clip("Ambience/sea_night_loop");
        library.magnumShots = Clips("Weapons", "magnum_shot_").Where(c => c.name != "magnum_shot_tail").ToArray();
        library.magnumShotTail = Clip("Weapons/magnum_shot_tail");
        library.magnumDryFire = Clips("Weapons", "magnum_dryfire_");
        library.magnumCock = Clips("Weapons", "magnum_cock_");
        library.magnumCylinderSpin = Clip("Weapons/magnum_cylinder_spin");
        library.bulletFlesh = Clips("Weapons", "bullet_flesh_");
        library.caseDrops = Clips("Weapons", "case_drop_");
        library.bulletImpacts = Clips("Weapons", "bullet_impact_");
        library.smallCreatureGrunts = Clips("Creature", "small_grunt_");
        library.smallCreatureSqueals = Clips("Creature", "small_squeal_");
        library.smallCreatureDeaths = Clips("Creature", "small_death_");
        // The finale (convert_sounds.py finale).
        library.cylinderClicks = Clips("Weapons", "cylinder_click_");
        library.cylinderOpenHand = Clip("Weapons/cylinder_open_hand");
        library.cylinderSlam = Clip("Weapons/cylinder_slam");
        library.casesIntoHand = Clip("Weapons/cases_into_hand");
        library.brassOnFloor = Clips("Weapons", "brass_floor_");
        library.drownedSearch = Clips("Creature", "drowned_search_");
        library.drownedBreathLoop = Clip("Creature/drowned_breath_loop");
        library.potDrop = Clip("Ambience/pot_drop");
        library.doorBurst = Clip("Doors/door_burst");
        library.gunshotTailIndoor = Clip("Weapons/gunshot_tail_indoor");
        library.machinePowerDown = Clip("Ambience/machine_power_down");
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return library;
    }

    private static AudioClip Clip(string pathWithoutExtension)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/" + pathWithoutExtension + ".wav");
    }

    private static AudioClip[] Clips(string folder, string prefix)
    {
        return AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectPaths.Audio + "/" + folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix))
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();
    }
}
