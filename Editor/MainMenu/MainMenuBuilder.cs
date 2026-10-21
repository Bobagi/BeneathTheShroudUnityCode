using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Horror > Menu principal > Gerar cena do menu: builds Scenes/MainMenu.unity, the first scene of the game (Gustavo
// 09/10, after a reference picture he liked but asked to be only an inspiration): the island at night in the storm,
// seen from the water. The mansion on its hill to the right, dark (abandoned), the island's forest, the rough sea, a
// bolt now and then behind the house; close on the left an old mooring post with an oil lantern swinging from it on a
// rope, the one warm light against the cold night. Something enormous calls from under the water now and then.
// (The first version was a half-submerged "split shot" with the colossal octopus under the waterline; Gustavo took
// it out on 10/10.)
//   World: the opening cutscene's (ArrivalCutsceneBuilder.BuildWorld), with a rougher sea and a quicker storm.
//   Camera: 0.9 m over the water (MenuCameraDrift: swell, roll, mouse lean); rain drops on the lens (LensRain).
//   UI: Prefabs/UI/MainMenu.prefab (MainMenuBuilder.UI.cs), made once and editable; this only places it.
// Layout (metres, the island's): the house at the origin on its 12 m plateau; the camera 150 m south of it, out at
// sea past the island's shelf (the terrain's sea floor is a flat 4 m deep and ends at z -110: from here the water
// under the camera is deep, empty and dark, room for the creature), looking north-north-west with a long lens so the
// house stands in the right third. The moon is moved for the menu to stand low just right of the house (MoonInFrame):
// the house and the hill are cut out against it, its path glitters on the water toward the camera.
public static partial class MainMenuBuilder
{
    public const string ScenePath = ProjectPaths.Scenes + "/MainMenu.unity";
    private const string MaterialFolder = ProjectPaths.Art + "/Materials/MainMenu";

    private static readonly Vector3 CameraPosition = new Vector3(25f, 0.9f, -148f);
    // The middle of the mansion of the floor plan (LunaManor: 43 m across, running 38 m north from the front door).
    private static readonly Vector3 HouseAim = new Vector3(2f, 16f, 9f);
    // How far right of the centre the house stands (degrees) and how far up the camera looks. 19 with the old 26 m
    // house; the plan's is ~15 degrees wide from out here and its east end left the frame (11/10).
    private const float HouseRightOfCentre = 15f;
    private const float LookUp = 1.5f;
    private const float FieldOfView = 32f;
    // The moon: this many degrees right of the house (Art/Textures/Sky/MenuNightSky.png, the pack's sky turned up 6
    // degrees by SourceAssets/Tools/sky_tilt.py: the moon at 15 degrees, just over the island's hills behind the
    // house; at the pack's 9 they hid it), and the height its light comes from.
    private const float MoonRightOfHouse = 4f;
    private const float MoonLightHeight = 14f;

    private static float HouseBearing
    {
        get
        {
            Vector3 d = HouseAim - CameraPosition;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
    }

    private static Vector3 CameraEuler => new Vector3(-LookUp, HouseBearing - HouseRightOfCentre, 0f);

    private static Transform root;

    [MenuItem("Horror/Menu principal/Gerar cena do menu")]
    public static void Build()
    {
        EnsureFolder(ProjectPaths.Art + "/Materials", "MainMenu");

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Transform world = ArrivalCutsceneBuilder.BuildWorld("MainMenu_World", out Renderer sea);
        TuneWorld(world, sea);
        root = new GameObject("MainMenu_Generated").transform;

        Camera above = MenuCamera();
        Foreground(above.transform);
        CreatureVoice();
        Bolt();
        AudioSource uiSource = Sound();

        // The first lens drops (flat UI sprites): kept, switched off, behind the shader's.
        LensDrops();

        // The UI: the menu prefab.
        GameObject prefab = EnsureMenuPrefab();
        GameObject menu = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        menu.transform.SetParent(root, false);
        MenuSounds sounds = menu.GetComponentInChildren<MenuSounds>(true);
        if (sounds != null)
        {
            ArrivalCutsceneBuilder.SetField(sounds, "source", uiSource);
        }

        GameObject events = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        events.transform.SetParent(root, false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuild();
        Debug.Log("[MainMenu] scene built: " + ScenePath);
    }

    // The mansion of the floor plan (GDD, 11/10) in the menu without rebuilding the scene: only the world's island,
    // house and plants are redone; the menu's UI instance and anything adjusted by hand in it stay as they are.
    [MenuItem("Horror/Menu principal/Atualizar casa e ilha (mantém o resto da cena)")]
    public static void UpdateHouse()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject world = GameObject.Find("MainMenu_World");
        if (world == null)
        {
            Debug.LogError("[MainMenu] MainMenu_World not found: build the menu scene first.");
            return;
        }

        ArrivalCutsceneBuilder.RebuildHouseAndIsland(world.transform);
        GameObject rig = GameObject.Find("CameraRig");
        if (rig != null)
        {
            rig.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[MainMenu] house and island updated: " + ScenePath);
    }

    // ---- world ---------------------------------------------------------------------------------------------------

    // A rougher sea than the opening's (the menu sits out at sea, where the swell is), a storm that strikes more
    // often (the menu is seen for a minute, not ten).
    private static void TuneWorld(Transform world, Renderer sea)
    {
        Material source = sea.sharedMaterial;
        string path = MaterialFolder + "/MenuSea.mat";
        Material rough = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (rough == null)
        {
            rough = new Material(source);
            AssetDatabase.CreateAsset(rough, path);
        }
        else
        {
            rough.CopyPropertiesFromMaterial(source);
        }

        rough.SetFloat("_ShoreChop", 0.6f);
        // Not plastic (Gustavo 10/10): fine ripples near the camera break the moon into glitter that follows the
        // waves, the rain rings the surface, and the glint is tighter (it was 0.26: wide smooth blobs). Values from
        // an A/B of three settings in Play (StormSea.shader, _DetailStrength / _RainRipples).
        rough.SetFloat("_DetailStrength", 0.32f);
        rough.SetFloat("_DetailTiling", 0.4f);
        rough.SetFloat("_RainRipples", 0.6f);
        rough.SetFloat("_RainDensity", 2.5f);
        EditorUtility.SetDirty(rough);
        foreach (Renderer r in world.GetComponentsInChildren<Renderer>(true))
        {
            if (r.sharedMaterial == source)
            {
                r.sharedMaterial = rough;
            }
        }

        // Some of the open sea's swell reaches the camera (a sixth: more and it climbs over the lens), the chop is
        // rough; calm at the rocks.
        SeaWaves waves = sea.GetComponent<SeaWaves>();
        if (waves != null)
        {
            waves.OverrideIsland(new Vector2(0f, 215f), 330f, 560f);
        }

        MoonInFrame(world, rough);
        MenuGrade(world);

        // The rain a little under the opening's (0.14): it covered the calls from the water.
        foreach (AudioSource s in world.GetComponentsInChildren<AudioSource>(true))
        {
            if (s.clip != null && s.clip.name == "rain_outdoor_loop")
            {
                s.volume = 0.115f;
            }
        }

        LightningStorm storm = world.GetComponentInChildren<LightningStorm>(true);
        if (storm != null)
        {
            ArrivalCutsceneBuilder.SetVector2(storm, "intervalSeconds", new Vector2(9f, 17f));
            // Half the opening's flash: at 4.5 the peak lit the island like an overcast day and washed out the menu.
            ArrivalCutsceneBuilder.SetFloat(storm, "flashIntensity", 2.2f);
        }
    }

    // The opening's sky has its moon lifted to 24 degrees and turned behind the island for the boat's shots. The
    // menu's has it at 15 (MenuNightSky.png) and turns it so the moon stands just right of the house, over the
    // hills; the moon's light, the sea's reflection of the sky and its glitter follow. The house stands against the
    // island's darker, higher hills, so a patch of moonlight through the cloud (a narrow cold spot from the sea's
    // side) picks its front out.
    private static void MoonInFrame(Transform world, Material sea)
    {
        float bearing = HouseBearing + MoonRightOfHouse;
        Material source = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.AllSkyFree + "/Night MoonBurst/AllSky_Night_MoonBurst Equirect.mat");
        string path = MaterialFolder + "/MenuNightSky.mat";
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sky == null)
        {
            sky = new Material(source);
            AssetDatabase.CreateAsset(sky, path);
        }
        else
        {
            sky.shader = source.shader;
            sky.CopyPropertiesFromMaterial(source);
        }

        string lifted = ProjectPaths.Textures + "/Sky/MenuNightSky.png";
        if (AssetImporter.GetAtPath(lifted) is TextureImporter importer &&
            AssetImporter.GetAtPath(ProjectPaths.Textures + "/Sky/ArrivalNightSky.png") is TextureImporter like &&
            importer.maxTextureSize != like.maxTextureSize)
        {
            TextureImporterSettings settings = new TextureImporterSettings();
            like.ReadTextureSettings(settings);
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = like.maxTextureSize;
            importer.textureCompression = like.textureCompression;
            importer.SaveAndReimport();
        }

        Texture skyTexture = AssetDatabase.LoadAssetAtPath<Texture>(lifted);
        if (skyTexture != null)
        {
            sky.SetTexture("_Tex", skyTexture);
        }

        sky.SetFloat("_Rotation", Mathf.Repeat(360f - bearing, 360f));
        sky.SetFloat("_Exposure", 0.85f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        sea.SetTexture("_SkyCube", sky.GetTexture("_Tex"));
        sea.SetFloat("_SkyRotation", sky.GetFloat("_Rotation"));
        sea.SetFloat("_SkyExposure", sky.GetFloat("_Exposure"));
        sea.SetColor("_SkyTint", sky.GetColor("_Tint"));
        // The glitter path under the moon, a little stronger than in the opening (it leads the eye to the house).
        sea.SetFloat("_GlintStrength", 0.4f);
        sea.SetFloat("_GlintRoughness", 0.16f);
        EditorUtility.SetDirty(sea);

        Transform moon = world.Find("Lighting/Moon");
        if (moon != null)
        {
            moon.rotation = Quaternion.Euler(MoonLightHeight, bearing - 180f, 0f);
        }

        DynamicGI.UpdateEnvironment();

        Vector3 house = new Vector3(0f, 16f, -2f);
        Vector3 fromSea = Vector3.ProjectOnPlane(CameraPosition - house, Vector3.up).normalized;
        GameObject patch = new GameObject("HouseMoonlight");
        patch.transform.SetParent(world, false);
        patch.transform.position = house + fromSea * 38f + Vector3.up * 26f;
        patch.transform.rotation = Quaternion.LookRotation(house - patch.transform.position);
        Light spot = patch.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.62f, 0.72f, 0.92f);
        spot.intensity = 380f;
        spot.range = 90f;
        spot.spotAngle = 34f;
        spot.innerSpotAngle = 12f;
        spot.shadows = LightShadows.Soft;
    }

    // Over the opening's night grade, for the menu only: the sea mist much thinner (at 150 m it hid the house in a
    // milky wash, lit purple by the lantern), a touch more exposure and contrast so the house cuts out against the
    // moon.
    private static void MenuGrade(Transform world)
    {
        string path = ProjectPaths.RenderingSettings + "/MenuNight_Volume.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        VolumetricFogVolumeComponent mist = Override<VolumetricFogVolumeComponent>(profile);
        mist.density.Override(0.035f);
        mist.scattering.Override(0.1f);
        ColorAdjustments colour = Override<ColorAdjustments>(profile);
        colour.postExposure.Override(0.55f);
        colour.contrast.Override(22f);
        Bloom bloom = Override<Bloom>(profile);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.55f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject go = new GameObject("MenuGrade");
        go.transform.SetParent(world, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    // ---- camera --------------------------------------------------------------------------------------------------

    private static Camera MenuCamera()
    {
        GameObject rig = new GameObject("CameraRig");
        rig.transform.SetParent(root, false);
        rig.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));

        GameObject go = new GameObject("MenuCamera");
        go.tag = "MainCamera";
        go.transform.SetParent(rig.transform, false);
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = FieldOfView;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 16000f;
        UniversalAdditionalCameraData data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        go.AddComponent<AudioListener>();
        go.AddComponent<MenuCameraDrift>();
        // Rain on the lens: drops that are little lenses, a few resting and drying, now and then one running down
        // (LensRain.shader, a full screen pass on the renderer).
        LensRainSetup.Ensure();
        go.AddComponent<LensRain>();
        return cam;
    }

    // ---- foreground: the posts, the rope and the lantern ---------------------------------------------------------

    // What is left of an old jetty close on the left: a post standing out of the water with rope wound round it, a
    // second one further off and a rotten beam between them; from an iron arm on the near post (above the frame) the
    // lantern swings on a rope: the warm light of the picture.
    private static void Foreground(Transform view)
    {
        Transform group = new GameObject("Foreground").transform;
        group.SetParent(root, false);
        Vector3 Local(float x, float y, float z) => view.TransformPoint(new Vector3(x, y, z));
        Quaternion facing = Quaternion.Euler(0f, view.eulerAngles.y, 0f);

        Material bark = PilingMaterial("MenuPiling", AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.EnvironmentMaterials + "/DeadBark.mat"), new Vector2(1f, 3f), Color.white);
        Material weed = PilingMaterial("MenuPilingWeed", bark, new Vector2(1f, 3f), new Color(0.16f, 0.2f, 0.14f));
        Material planks = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/DarkPlanks.mat");
        Material iron = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/DarkMetal.mat");
        Material rope = RopeMaterial();

        Transform post = Post(group, "Post", Local(-1.35f, 0f, 3.4f), 0.3f, facing * Quaternion.Euler(3f, 0f, -4f), bark, weed, rope, true);
        Transform far = Post(group, "PostFar", Local(-3.4f, 0f, 8.5f), 0.26f, facing * Quaternion.Euler(-2f, 0f, 6f), bark, weed, rope, false);
        Vector3 a = post.position + Vector3.up * 0.55f;
        Vector3 b = far.position + Vector3.up * 0.2f;
        Transform beam = ArrivalCutsceneBuilder.Box("Beam", group, (a + b) * 0.5f, new Vector3(0.14f, 0.2f, Vector3.Distance(a, b) + 0.3f), planks, false);
        beam.rotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(0f, 0f, 8f);

        // The lantern: centred a quarter in from the left, just above the middle; the arm and the knot above the frame.
        Vector3 lanternCentre = Local(-0.62f, -0.02f, 2.4f);
        const float ropeLength = 0.95f;
        const float lanternHalf = 0.15f;
        Vector3 hook = lanternCentre + Vector3.up * (ropeLength + lanternHalf);
        Vector3 armFrom = post.position + Vector3.up * (hook.y - post.position.y);
        Vector3 armAxis = (hook - armFrom).normalized;
        Transform arm = ArrivalCutsceneBuilder.Box("Arm", group, Vector3.Lerp(armFrom, hook + armAxis * 0.06f, 0.5f), Vector3.one, iron, false);
        arm.rotation = Quaternion.LookRotation(armAxis);
        arm.localScale = new Vector3(0.04f, 0.04f, Vector3.Distance(armFrom, hook) + 0.1f);
        // The knot: the rope wound four times round the arm's end, under which it hangs.
        Tube("RopeKnot", group, Helix(hook, armAxis, 0.02f + RopeRadius, 4f, 0.05f, 48), RopeRadius, rope);

        GameObject pivot = new GameObject("LanternPivot");
        pivot.transform.SetParent(group, false);
        pivot.transform.position = hook + Vector3.down * 0.02f;
        pivot.transform.rotation = facing;
        Vector3 handle = Lantern(pivot.transform, hook + Vector3.down * ropeLength);
        // The hanging rope, a hair of bow in it, from under the knot to the lantern's handle (it swings with the pivot).
        List<Vector3> hanging = new List<Vector3>();
        Vector3 top = hook + Vector3.down * 0.03f;
        Vector3 side = Vector3.Cross(Vector3.up, armAxis).normalized;
        for (int i = 0; i <= 16; i++)
        {
            float k = i / 16f;
            hanging.Add(Vector3.Lerp(top, handle, k) + side * Mathf.Sin(k * Mathf.PI) * 0.012f);
        }

        Tube("Rope", pivot.transform, hanging, RopeRadius, rope);

        pivot.AddComponent<LanternSway>();
    }

    // A post from 9 m under the water to 2.6 m over it, its lower part dark with weed; on the near one a rope wound
    // round it at the waterline, its loose end trailing down into the water.
    private static Transform Post(Transform group, string name, Vector3 foot, float thickness, Quaternion lean, Material bark, Material weed, Material rope, bool roped)
    {
        foot.y = 0f;
        Transform post = Cylinder(name, group, foot + lean * Vector3.up * 1.3f, thickness, 2.6f, bark);
        post.rotation = lean;
        Transform under = Cylinder(name + "Underwater", group, foot + lean * Vector3.down * 4.45f, thickness * 1.05f, 9f, weed);
        under.rotation = lean;
        if (roped)
        {
            Vector3 axis = lean * Vector3.up;
            Vector3 start = foot + axis * 0.38f;
            // Six turns side by side (each a rope's width apart: 0.03 m packed them into one flat band).
            List<Vector3> coil = Helix(start + axis * 0.065f, axis, thickness * 0.5f + RopeRadius * 1.3f, 6f, 6f * RopeRadius * 2.6f * 1.1f, 160);
            Vector3 last = coil[coil.Count - 1];
            Vector3 outward = Vector3.ProjectOnPlane(last - (foot + axis * Vector3.Dot(last - foot, axis)), axis).normalized;
            for (int i = 1; i <= 10; i++)
            {
                float k = i / 10f;
                coil.Add(last + outward * (0.05f * Mathf.Sin(k * Mathf.PI * 0.5f)) + Vector3.down * (0.75f * k * k + 0.05f * k));
            }

            Tube("RopeCoil", group, coil, RopeRadius * 1.3f, rope);
        }

        return post;
    }

    // ---- the lantern ---------------------------------------------------------------------------------------------

    // How an oil lantern lights (Gustavo 10/10: "a iluminação dela não faz nenhum sentido"; the same mistakes as the
    // house's old lamps): the light comes from the flame, a small living flame on the wick inside the glass; the cap
    // and the burner of the lantern stop it going straight up and down (they cast the shadows). The lantern itself must
    // not take its own light from a few centimetres away: that burned every edge of the frame orange (the "outline").
    // So the lantern's renderers are on their own rendering layer ("Lantern"), left out of the flame's light (they
    // still cast its shadows: custom shadow layers), and a weak fill on that layer only, a little toward the camera,
    // gives the brass the warm sheen the flame would. The glass is clear with an even warm glow, no halo sprite.
    // Returns the top of the handle, where the rope is tied.
    private static Vector3 Lantern(Transform pivot, Vector3 handle)
    {
        EnsureRenderingLayer(LanternLayerIndex, "Lantern");
        uint lanternBit = 1u << LanternLayerIndex;

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.Props + "/Lantern_01/Lantern_01_1k.fbx");
        GameObject lantern = (GameObject)PrefabUtility.InstantiatePrefab(model);
        lantern.name = "Lantern";
        lantern.transform.SetParent(pivot, false);
        lantern.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
        Material glass = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.Props + "/Lantern_01/Lantern_01_glass.mat");
        Material clearGlass = LanternGlass(glass);
        foreach (Renderer r in lantern.GetComponentsInChildren<Renderer>(true))
        {
            r.renderingLayerMask = lanternBit;
            bool isGlass = r.sharedMaterial == glass;
            // The glass lets the light through; the frame (cap, burner, bars) casts the shadows.
            r.shadowCastingMode = isGlass ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (isGlass)
            {
                r.sharedMaterial = clearGlass;
            }
        }

        // Hung by the top of its handle at the end of the rope.
        Bounds bounds = WorldBounds(lantern);
        lantern.transform.position += handle - new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        bounds = WorldBounds(lantern);
        float height = bounds.size.y;
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y - 0.004f, bounds.center.z);

        // The wick: Lantern_01 is 29.4 cm tall, its glass from 6.1 to 13 cm; the flame stands from about 7 to 10 cm.
        Vector3 flameAt = new Vector3(bounds.center.x, bounds.min.y + height * (0.085f / 0.294f), bounds.center.z);
        Flame(lantern.transform, flameAt);

        GameObject lightObject = new GameObject("FlameLight");
        lightObject.transform.SetParent(lantern.transform, true);
        lightObject.transform.position = flameAt + Vector3.up * 0.01f;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.3f);
        light.intensity = 2.2f;
        light.range = 7f;
        light.shadows = LightShadows.Soft;
        light.shadowNearPlane = 0.1f;
        light.renderingLayerMask = unchecked((int)~lanternBit);
        // The URP of Unity 6 reads the light's layers from m_RenderingLayersMask (the old m_RenderingLayers is ignored),
        // and only named layers count: "all but the lantern" written as ~bit lit nothing (measured 10/10).
        int others = (1 << 0) | (1 << 1);
        UniversalAdditionalLightData lightData = lightObject.GetComponent<UniversalAdditionalLightData>() ?? lightObject.AddComponent<UniversalAdditionalLightData>();
        SetLightLayers(lightData, others, others);
        FlickerLight flicker = lightObject.AddComponent<FlickerLight>();
        SetSerialized(flicker, "amount", 0.12f);
        SetSerialized(flicker, "speed", 9f);
        VolumetricAdditionalLight volumetric = lightObject.AddComponent<VolumetricAdditionalLight>();
        SetSerialized(volumetric, "scattering", 0.12f);
        SetSerialized(volumetric, "anisotropy", 0.3f);
        SetSerialized(volumetric, "radius", 0.06f);

        // The flame's light on the lantern itself, soft and from a little in front: only the "Lantern" layer.
        GameObject fillObject = new GameObject("LanternSelfLight");
        fillObject.transform.SetParent(lantern.transform, true);
        Vector3 towardCamera = Vector3.ProjectOnPlane(CameraPosition - flameAt, Vector3.up).normalized;
        fillObject.transform.position = flameAt + towardCamera * 0.14f + Vector3.up * 0.02f;
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.color = new Color(1f, 0.62f, 0.32f);
        fill.intensity = 0.3f;
        fill.range = 0.45f;
        fill.shadows = LightShadows.None;
        fill.renderingLayerMask = unchecked((int)lanternBit);
        UniversalAdditionalLightData fillData = fillObject.GetComponent<UniversalAdditionalLightData>() ?? fillObject.AddComponent<UniversalAdditionalLightData>();
        SetLightLayers(fillData, (int)lanternBit, (int)lanternBit);
        FlickerLight fillFlicker = fillObject.AddComponent<FlickerLight>();
        SetSerialized(fillFlicker, "amount", 0.12f);
        SetSerialized(fillFlicker, "speed", 9f);
        return top;
    }

    // A small flame on the wick: a short-lived stream of additive flame sprites (Kenney flame_01) rising a few
    // millimetres and fading from pale yellow to orange, in the lantern's space so it swings with it, and a bright core.
    private static void Flame(Transform lantern, Vector3 at)
    {
        string path = MaterialFolder + "/LanternFlame.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null || m.shader.name != "Universal Render Pipeline/Particles/Unlit")
        {
            if (m != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/flame_01.png"));
        m.SetColor("_BaseColor", new Color(1f, 0.7f, 0.35f) * 2.2f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent + 10;
        EditorUtility.SetDirty(m);

        GameObject go = new GameObject("Flame");
        go.transform.SetParent(lantern, true);
        go.transform.position = at;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.026f, 0.034f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 55f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.0015f;
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.004f, 0.004f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.004f, 0.004f);
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
        ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
        colour.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.7f, 0.3f), 0.45f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colour.color = g;
        ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = m;
        pr.shadowCastingMode = ShadowCastingMode.Off;
        pr.receiveShadows = false;

        // The bright blue-white base of a wick flame, tiny.
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "FlameCore";
        Object.DestroyImmediate(core.GetComponent<Collider>());
        core.transform.SetParent(go.transform, false);
        core.transform.position = at + Vector3.up * 0.004f;
        core.transform.localScale = Vector3.one;
        Vector3 s = core.transform.lossyScale;
        core.transform.localScale = new Vector3(0.007f / s.x, 0.016f / s.y, 0.007f / s.z);
        Renderer coreRenderer = core.GetComponent<Renderer>();
        coreRenderer.sharedMaterial = ArrivalCutsceneBuilder.Mat("LanternFlameCore", Color.black, 0f);
        Material coreMaterial = coreRenderer.sharedMaterial;
        coreMaterial.shader = Shader.Find("Universal Render Pipeline/Unlit");
        coreMaterial.SetColor("_BaseColor", new Color(1f, 0.85f, 0.55f) * 4f);
        EditorUtility.SetDirty(coreMaterial);
        coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // Clear old glass with an even warm glow from the flame behind it. Transparent without "preserve specular" (the
    // URP's default premultiplied blend kept the full reflection on a nearly clear pane: Flooded House 29/09).
    private static Material LanternGlass(Material source)
    {
        string path = MaterialFolder + "/MenuLanternGlass.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_BlendModePreserveSpecular", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(1f, 0.86f, 0.68f, 0.22f));
        m.SetFloat("_Smoothness", 0.88f);
        m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        // The emission of a transparent surface is scaled by its alpha (0.22): 2.4 reads as a lit globe.
        m.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.28f) * 2.4f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- rope ----------------------------------------------------------------------------------------------------

    private const float RopeRadius = 0.0075f;
    private const int LanternLayerIndex = 2;

    // Twisted natural-fibre rope (ambientCG Rope001, CC0: Art/Textures/Rope), dark and wet with rain.
    private static Material RopeMaterial()
    {
        string normalPath = ProjectPaths.Textures + "/Rope/Rope_NormalGL.jpg";
        if (AssetImporter.GetAtPath(normalPath) is TextureImporter normalImporter && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        string path = MaterialFolder + "/Rope.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Textures + "/Rope/Rope_Color.jpg"));
        m.SetColor("_BaseColor", new Color(0.9f, 0.82f, 0.7f));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", 0.32f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Points of a helix round `axis` through `centre`: `turns` turns over `length` metres, centred on `centre`.
    private static List<Vector3> Helix(Vector3 centre, Vector3 axis, float radius, float turns, float length, int steps)
    {
        axis = axis.normalized;
        Vector3 u = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 v = Vector3.Cross(axis, u);
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i <= steps; i++)
        {
            float k = i / (float)steps;
            float angle = k * turns * Mathf.PI * 2f;
            points.Add(centre + axis * ((k - 0.5f) * length) + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius);
        }

        return points;
    }

    // A round tube along world points (8 sides, frames carried along the curve so it never twists), under `parent`.
    // UVs: once round the rope, and down it at the texture's own proportion (the 2:1 rope picture spans the
    // circumference across and half of it along), so the strands keep their shape at any thickness.
    private static GameObject Tube(string name, Transform parent, IList<Vector3> points, float radius, Material material)
    {
        const int sides = 8;
        int n = points.Count;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        float circumference = Mathf.PI * 2f * radius;
        float along = 0f;
        Vector3 tangent = (points[1] - points[0]).normalized;
        Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up).normalized;
        for (int i = 0; i < n; i++)
        {
            Vector3 next = (points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
            normal = Vector3.ProjectOnPlane(normal, next).normalized;
            Vector3 binormal = Vector3.Cross(next, normal);
            if (i > 0)
            {
                along += Vector3.Distance(points[i], points[i - 1]);
            }

            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                vertices.Add(parent.InverseTransformPoint(points[i] + dir * radius));
                normals.Add(parent.InverseTransformDirection(dir));
                uvs.Add(new Vector2(s / (float)sides, along / (circumference * 0.5f)));
            }
        }

        for (int i = 0; i < n - 1; i++)
        {
            for (int s = 0; s < sides; s++)
            {
                int a0 = i * (sides + 1) + s;
                int b0 = a0 + sides + 1;
                triangles.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1 });
            }
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return go;
    }

    // A rendering layer name in Tags and Layers (without a name the URP masks the bit off and nothing lights it).
    private static void EnsureRenderingLayer(int index, string layerName)
    {
        SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tags.FindProperty("m_RenderingLayers");
        if (layers == null)
        {
            return;
        }

        if (layers.arraySize <= index)
        {
            layers.arraySize = index + 1;
        }

        SerializedProperty slot = layers.GetArrayElementAtIndex(index);
        if (slot.stringValue != layerName)
        {
            slot.stringValue = layerName;
            tags.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }


    private static Material PilingMaterial(string name, Material source, Vector2 tiling, Color tint)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(source);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.CopyPropertiesFromMaterial(source);
        }

        m.SetTextureScale("_BaseMap", tiling);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", name.Contains("Weed") ? 0.55f : 0.25f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- the voices from under the water --------------------------------------------------------------------------

    // Two voices (Gustavo 10/10): the growls, short and muffled, now and then; and far below, rarely, the roar of
    // something colossal, heard from deep under and far away (Creature/abyss_far_roar.wav, made by
    // SourceAssets/Tools/convert_sounds.py menu).
    private static void CreatureVoice()
    {
        Voice("CreatureVoice", 0.75f, new Vector2(18f, 34f), new Vector2(6f, 11f), new Vector2(0.55f, 0.75f), 1400f,
            ProjectPaths.Audio + "/Creature/creature_roar_2.wav",
            ProjectPaths.Audio + "/Creature/foghorn_kinda_gross.mp3",
            ProjectPaths.Audio + "/Creature/monster_03.ogg");
        Voice("DeepCreature", 0.8f, new Vector2(45f, 90f), new Vector2(20f, 35f), new Vector2(0.92f, 1.04f), 0f,
            ProjectPaths.Audio + "/Creature/abyss_far_roar.wav");
    }

    private static void Voice(string name, float volume, Vector2 interval, Vector2 first, Vector2 pitch, float lowpass, params string[] clips)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        if (lowpass > 0f)
        {
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = lowpass;
        }

        MenuAmbience voice = go.AddComponent<MenuAmbience>();
        ArrivalCutsceneBuilder.SetField(voice, "source", source);
        ArrivalCutsceneBuilder.SetField(voice, "calls", System.Array.ConvertAll(clips, c => (Object)AssetDatabase.LoadAssetAtPath<AudioClip>(c)));
        SerializedObject so = new SerializedObject(voice);
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("intervalSeconds").vector2Value = interval;
        so.FindProperty("firstDelay").vector2Value = first;
        so.FindProperty("pitchRange").vector2Value = pitch;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        component.active = true;
        return component;
    }

    // ---- sky -----------------------------------------------------------------------------------------------------

    // The bolt behind the house, right of the title: far out, so it reads as part of the storm, not a prop.
    private static void Bolt()
    {
        string path = MaterialFolder + "/LightningBolt.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        // HDR in the material (line renderer vertex colours are clamped to 1): the bloom makes the glow.
        m.SetColor("_BaseColor", new Color(0.8f, 0.88f, 1f) * 6f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);

        GameObject go = new GameObject("LightningBolt");
        go.transform.SetParent(root, false);
        // 22 degrees right of the view's centre (a tenth of the screen in from the right edge), 330 m out: on the hills
        // behind the house, which stands 19 degrees right. Further out the night fog swallowed it (at 900 m the fog
        // left 2% of it).
        Vector3 along = Quaternion.Euler(0f, CameraEuler.y + 22f, 0f) * Vector3.forward;
        Vector3 ground = CameraPosition + along * 330f;
        ground.y = 0f;
        go.transform.position = ground;
        go.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
        LightningBolt bolt = go.AddComponent<LightningBolt>();
        ArrivalCutsceneBuilder.SetField(bolt, "material", m);
        ArrivalCutsceneBuilder.SetFloat(bolt, "width", 3.2f);
        ArrivalCutsceneBuilder.SetFloat(bolt, "intensity", 1f);
        ArrivalCutsceneBuilder.SetFloat(bolt, "height", 230f);
        ArrivalCutsceneBuilder.SetFloat(bolt, "spread", 50f);
    }

    // ---- sound ---------------------------------------------------------------------------------------------------

    // The music (the opening's theme, low) and the menu's own 2D
    // source for its clicks. Rain, sea and thunder come with the world's storm.
    private static AudioSource Sound()
    {
        GameObject go = new GameObject("MenuAudio");
        go.transform.SetParent(root, false);
        AudioSource music = go.AddComponent<AudioSource>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Music/arrival_theme.wav");
        music.loop = true;
        music.playOnAwake = true;
        music.spatialBlend = 0f;
        music.volume = 0.38f;

        GameObject ui = new GameObject("UiSounds");
        ui.transform.SetParent(go.transform, false);
        AudioSource source = ui.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        return source;
    }

    // ---- drops on the lens ----------------------------------------------------------------------------------------

    // A canvas under the menu's with LensDroplets: a few rain drops now and then on the camera's glass.
    private static void LensDrops()
    {
        GameObject go = new GameObject("LensDrops", typeof(RectTransform));
        go.transform.SetParent(root, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        LensDroplets drops = go.AddComponent<LensDroplets>();
        drops.enabled = false;
        ArrivalCutsceneBuilder.SetField(drops, "drop", LensSprite("LensDrop"));
        ArrivalCutsceneBuilder.SetField(drops, "trail", LensSprite("LensTrail"));
    }

    // Art/Textures/Lens (SourceAssets/Tools/lens_drop_textures.py) as single sprites.
    private static Sprite LensSprite(string name)
    {
        string path = ProjectPaths.Textures + "/Lens/" + name + ".png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------

    private static Transform Cylinder(string name, Transform parent, Vector3 centre, float diameter, float height, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            b.Encapsulate(r.bounds);
        }

        return b;
    }

    private static void SetSerialized(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null)
        {
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Light layers and shadow layers of a light (custom shadow layers on), as the URP of Unity 6 stores them.
    private static void SetLightLayers(UniversalAdditionalLightData data, int lightBits, int shadowBits)
    {
        SerializedObject so = new SerializedObject(data);
        so.FindProperty("m_RenderingLayersMask.m_Bits").intValue = lightBits;
        so.FindProperty("m_ShadowRenderingLayersMask.m_Bits").intValue = shadowBits;
        so.FindProperty("m_CustomShadowLayers").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The menu first in the build, then the opening, then the house (the others stay as they were, after them).
    private static void AddToBuild()
    {
        string[] first = { ScenePath, ArrivalCutsceneBuilder.ScenePath, ProjectPaths.Scenes + "/Flooded_House.unity" };
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in first)
        {
            if (System.IO.File.Exists(path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (System.Array.IndexOf(first, s.path) < 0)
            {
                scenes.Add(s);
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
