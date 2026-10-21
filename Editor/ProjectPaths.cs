// Single source of truth for the asset folders the editor builders read from and write to.
// If a folder moves in the Project window, change it here only. Layout is documented in
// Assets/_Project/README.md.
public static class ProjectPaths
{
    public const string Root = "Assets/_Project";
    public const string Scenes = Root + "/Scenes";

    // Art
    public const string Art = Root + "/Art";
    public const string Models = Art + "/Models";
    public const string CreatureModels = Models + "/Creature";
    public const string TankModels = Models + "/Tank";
    // Optimised models (game-3d-assets skill): one folder per model with <Name>.fbx and its texture set.
    public const string PropModels = Models + "/Props";
    public const string CharacterModels = Models + "/Characters";
    public const string PlayerAnimations = Art + "/Animations/Player";
    // First person arms (FirstPersonArms rig): fixed clips for the hands, the torch, the revolver, actions and the
    // takes recorded from video (Editor/FirstPerson). Guide: Docs/Animacao/PRIMEIRA_PESSOA.md.
    public const string FirstPersonAnimations = Art + "/Animations/FirstPerson";
    public const string FirstPersonTakes = FirstPersonAnimations + "/Tomadas";
    // Whole-body clips recorded from video (D:\Projetos\hand-mocap body_fit.py, HandMocap/RecordedBody).
    public const string BodyAnimations = Art + "/Animations/Body";
    public const string Props = Art + "/Props";
    public const string Textures = Art + "/Textures";
    public const string EnvironmentMaterials = Art + "/Materials/Environment";
    public const string SkyMaterials = Art + "/Materials/Sky";
    public const string FloodedHouseMaterials = Art + "/Materials/FloodedHouse";
    public const string Shaders = Art + "/Shaders";
    public const string TerrainData = Art + "/Terrain/Data";
    public const string TerrainLayers = Art + "/Terrain/Layers";
    public const string Vfx = Art + "/VFX";
    public const string Items = Art + "/Items";

    // Audio, prefabs, UI
    public const string Audio = Root + "/Audio";
    public const string ItemPrefabs = Root + "/Prefabs/Items";
    public const string PlayerPrefabs = Root + "/Prefabs/Player";
    public const string Fonts = Root + "/UI/Fonts";
    public const string Icons = Root + "/UI/Icons";

    // Settings
    public const string RenderingSettings = Root + "/Settings/Rendering";

    // Third party packs
    public const string AllSkyFree = "Assets/ThirdParty/AllSkyFree";
}
