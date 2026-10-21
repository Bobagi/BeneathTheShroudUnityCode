using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Light, weather, furniture, the dive kit, the lab and its tanks, the creature, the player rig and the
// HUD. Dayse's demo story is placed by FloodedHouseMansionStory. Values here are the ones to tune while play-testing.
public static partial class FloodedHouseBuilder
{
    private static Light moon, lightningSun, lightningSpot;
    private static ReadableNote labJournal;
    private static PassageBlocker bedroomBlocker;
    private static Volume houseVolume, underwaterVolume;
    private static Material yellowPaint, particle;

    // ---------------------------------------------------------------- light and mood

    private static void BuildLighting()
    {
        Transform lights = Group("Lighting");

        Material source = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.AllSkyFree + "/Overcast Low/AllSky_Overcast4_Low.mat");
        Material sky = Material("StormSky", source.shader);
        sky.CopyPropertiesFromMaterial(source);
        sky.SetColor("_Tint", new Color(0.17f, 0.2f, 0.23f));
        sky.SetFloat("_Exposure", 0.045f);
        sky.SetFloat("_Rotation", 40f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        // Like the trenches: contrast, not darkness. Almost no fill; the lamps carve the rooms.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.12f, 0.14f, 0.17f);
        RenderSettings.ambientEquatorColor = new Color(0.06f, 0.07f, 0.075f);
        RenderSettings.ambientGroundColor = new Color(0.02f, 0.022f, 0.022f);
        RenderSettings.reflectionIntensity = 0.25f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.04f, 0.048f, 0.055f);
        RenderSettings.fogDensity = 0.028f;

        moon = DirectionalLight("Moon", lights, Quaternion.Euler(38f, 165f, 0f), new Color(0.5f, 0.6f, 0.8f), 0.2f);
        RenderSettings.sun = moon;
        lightningSun = DirectionalLight("Lightning", lights, Quaternion.Euler(42f, 195f, 0f), new Color(0.78f, 0.85f, 1f), 0f);
        // Through the dining room's bay window, from outside: the room where the first recording plays.
        lightningSpot = PointOrSpot("LightningThroughWindow", lights, M(-7.8f, 4.5f, -6f), LightType.Spot, new Color(0.78f, 0.85f, 1f), 0f, 22f);
        lightningSpot.transform.rotation = Quaternion.LookRotation(M(-7.8f, 0.8f, 4.5f) - lightningSpot.transform.position);
        lightningSpot.spotAngle = 95f;
        // Lightning through the big window: the one moment the whole room's haze lights up.
        Volumetric(lightningSpot, 1f, 0.5f);

        // Window fills also light the haze: the cold, damp air by the glass. At the height of the glass, just
        // inside it: 30 cm under the ceiling (as they were) they drew a bright blue disc on it (01/10).
        // The two bay windows of the front rooms.
        Volumetric(PointOrSpot("WindowFill", lights, M(-7.8f, 1.5f, 1.7f), LightType.Point, new Color(0.45f, 0.55f, 0.7f), 0.55f, 9f), 1f, 0.2f);
        Volumetric(PointOrSpot("EastWindowFill", lights, M(7.8f, 1.5f, 1.7f), LightType.Point, new Color(0.45f, 0.55f, 0.7f), 0.45f, 8f), 1f, 0.2f);
        // The courtyard under the open sky: a cold wash over the garden.
        Volumetric(PointOrSpot("CourtyardFill", lights, M(0.2f, 4.5f, 15.3f), LightType.Point, new Color(0.45f, 0.55f, 0.7f), 0.8f, 14f), 1f, 0.2f);
        // The sunken bedroom is the scene's money shot: a cold wash seeping from the window keeps the
        // floating chairs readable before the torch even finds them.
        PointOrSpot("BedroomWindowGlow", lights, new Vector3(5.5f, -4.2f, 0.6f), LightType.Point, new Color(0.3f, 0.55f, 0.6f), 0.8f, 6f);
        PointOrSpot("BedroomCeilingWash", lights, new Vector3(5.2f, -3.5f, -1.6f), LightType.Point, new Color(0.35f, 0.55f, 0.6f), 0.8f, 7f);

        // A red pulse at the bottom of the basement water: the way down to the lab.
        Light shaftGlow = PointOrSpot("ShaftGlow", lights, new Vector3(-1f, -5.2f, -3f), LightType.Point, new Color(1f, 0.15f, 0.08f), 1.4f, 5.5f);
        shaftGlow.gameObject.AddComponent<PulsingLight>();

        foreach (Vector3 at in new[] { new Vector3(-1.7f, -7.9f, -3.3f), new Vector3(-1.7f, -7.9f, -10.5f), new Vector3(-1.7f, -7.9f, -16.5f),
                     new Vector3(-3.7f, -7.7f, -19f), new Vector3(1.7f, -7.7f, -23.5f) })
        {
            Light emergency = PointOrSpot("EmergencyLight", lights, at, LightType.Point, new Color(1f, 0.13f, 0.08f), 2.4f, 6.5f);
            emergency.gameObject.AddComponent<PulsingLight>();
            Volumetric(emergency, 0.8f, 0.3f);
        }

        BuildVolumes();
    }

    private static Light DirectionalLight(string name, Transform parent, Quaternion rotation, Color color, float intensity)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.rotation = rotation;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.Soft;
        return light;
    }

    private static Light PointOrSpot(string name, Transform parent, Vector3 position, LightType type, Color color, float intensity, float range, bool shadows = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return light;
    }

    private static void BuildVolumes()
    {
        VolumeProfile house = Profile("FloodedHouse_Volume");
        BattlefieldGet<Tonemapping>(house).mode.Override(TonemappingMode.ACES);
        ColorAdjustments grade = BattlefieldGet<ColorAdjustments>(house);
        // "Hollywood darkness": dark-feeling but navigable. Split toning and the cool white balance
        // pull the image down, so exposure sits higher than the raw values would suggest.
        grade.postExposure.Override(0.65f);
        grade.contrast.Override(24f);
        // 11/10: -22 and the cold balance left every room one teal tone and buried the aged textures; less of both.
        grade.saturation.Override(-14f);
        ShadowsMidtonesHighlights split = BattlefieldGet<ShadowsMidtonesHighlights>(house);
        split.shadows.Override(new Vector4(0.86f, 0.98f, 1.1f, -0.06f));
        split.highlights.Override(new Vector4(1.1f, 1.02f, 0.9f, 0.04f));
        Vignette vignette = BattlefieldGet<Vignette>(house);
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.45f);
        FilmGrain grain = BattlefieldGet<FilmGrain>(house);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.28f);
        grain.response.Override(0.8f);
        // Bloom only as a faint halo on what really is a light source (a bulb, the torch hitting something
        // close, lightning). 01/10, Gustavo: "uma bola de luz", "tentativa de cegar o jogador". URP docs and
        // practice: scattering bloom is subtle and meant for low intensities (0.2 to 0.5, threshold 0.9 to 1);
        // in a dark scene everything lit sits near the threshold, so it goes a little higher and the lens
        // dirt, which multiplied every highlight into a smear, drops to a trace.
        Bloom bloom = BattlefieldGet<Bloom>(house);
        bloom.intensity.Override(0.16f);
        bloom.threshold.Override(1.2f);
        bloom.scatter.Override(0.5f);
        bloom.highQualityFiltering.Override(true);
        bloom.dirtTexture.Override(LensDirt());
        bloom.dirtIntensity.Override(0.2f);
        BattlefieldGet<ChromaticAberration>(house).intensity.Override(0.07f);
        // Third hue: teal shadows against the warm practicals, and a cool white balance overall.
        SplitToning toning = BattlefieldGet<SplitToning>(house);
        toning.shadows.Override(new Color(0.38f, 0.45f, 0.47f));
        toning.highlights.Override(new Color(0.62f, 0.5f, 0.38f));
        toning.balance.Override(-20f);
        BattlefieldGet<WhiteBalance>(house).temperature.Override(-4f);
        EditorUtility.SetDirty(house);

        // Under water: red is absorbed first (channel mixer), cold green-blue balance, murky teal,
        // near-sighted blur standing in for turbidity, the edges crushed and slightly warped.
        VolumeProfile under = Profile("FloodedHouse_Underwater");
        ColorAdjustments murk = BattlefieldGet<ColorAdjustments>(under);
        murk.colorFilter.Override(new Color(0.62f, 0.88f, 0.82f));
        murk.saturation.Override(-35f);
        murk.postExposure.Override(0.55f);
        murk.contrast.Override(12f);
        ChannelMixer absorb = BattlefieldGet<ChannelMixer>(under);
        absorb.redOutRedIn.Override(82f);
        absorb.redOutGreenIn.Override(10f);
        WhiteBalance cold = BattlefieldGet<WhiteBalance>(under);
        cold.temperature.Override(-30f);
        cold.tint.Override(-18f);
        DepthOfField turbid = BattlefieldGet<DepthOfField>(under);
        turbid.mode.Override(DepthOfFieldMode.Gaussian);
        // Sharp distance and blur end come from GameTuning (TunedVolume keeps them in sync).
        turbid.gaussianStart.Override(GameTuning.Instance.sharpDistance);
        turbid.gaussianEnd.Override(GameTuning.Instance.blurEndDistance);
        turbid.gaussianMaxRadius.Override(1.2f);
        turbid.highQualitySampling.Override(true);
        Vignette deep = BattlefieldGet<Vignette>(under);
        deep.intensity.Override(0.5f);
        deep.smoothness.Override(0.5f);
        deep.color.Override(new Color(0f, 0.04f, 0.04f));
        LensDistortion warp = BattlefieldGet<LensDistortion>(under);
        warp.intensity.Override(-0.2f);
        warp.scale.Override(1.03f);
        BattlefieldGet<ChromaticAberration>(under).intensity.Override(0.35f);
        // Heavier, coarser grain than on land: the murk of suspended filth in front of the mask.
        FilmGrain murkGrain = BattlefieldGet<FilmGrain>(under);
        murkGrain.type.Override(FilmGrainLookup.Medium5);
        murkGrain.intensity.Override(0.45f);
        murkGrain.response.Override(0.6f);
        EditorUtility.SetDirty(under);

        ConfigureVolumetricFog(house, false);
        ConfigureVolumetricFog(under, true);
        ConfigureSsgi(house);

        Transform post = Group("PostProcessing");
        houseVolume = GlobalVolume("HouseVolume", post, house, 0f, 1f);
        underwaterVolume = GlobalVolume("UnderwaterVolume", post, under, 10f, 0f);
        houseVolume.gameObject.AddComponent<TunedVolume>().SetKind(TunedVolume.Kind.House);
        underwaterVolume.gameObject.AddComponent<TunedVolume>().SetKind(TunedVolume.Kind.Underwater);
    }

    private static VolumeProfile Profile(string name)
    {
        string path = $"{ProjectPaths.RenderingSettings}/{name}.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        return profile;
    }

    private static T BattlefieldGet<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        component.active = true;
        return component;
    }

    private static Volume GlobalVolume(string name, Transform parent, VolumeProfile profile, float priority, float weight)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        volume.priority = priority;
        volume.weight = weight;
        return volume;
    }

    // ---------------------------------------------------------------- weather

    private static void BuildRain(Transform parent)
    {
        particle = Material("Particle", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        particle.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/light_01.png"));
        particle.SetColor("_BaseColor", Color.white);
        particle.SetFloat("_Surface", 1f);
        particle.SetFloat("_Blend", 0f);
        particle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        particle.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        particle.SetFloat("_ZWrite", 0f);
        particle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particle.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(particle);

        // Slabs of rain only where the sky is open (LunaManor.RainAreas: round the mansion, the courtyard garden and
        // its open south gallery, the gaps between the wings), so none falls indoors.
        Transform rain = Group("Rain", parent);
        List<Vector4> areas = new List<Vector4>();
        foreach (Rect r in LunaManor.RainAreas())
        {
            Rect w = MRect(r);
            areas.Add(new Vector4(w.xMin, w.xMax, w.yMin, w.yMax));
        }

        foreach (Vector4 area in areas)
        {
            GameObject go = new GameObject("RainArea");
            go.transform.SetParent(rain, false);
            go.transform.position = new Vector3((area.x + area.y) * 0.5f, 14f, (area.z + area.w) * 0.5f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 1.25f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.03f);
            main.startColor = new Color(0.62f, 0.68f, 0.75f, 0.35f);
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = (area.y - area.x) * (area.w - area.z) * 1.4f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.y - area.x, 0.1f, area.w - area.z);
            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(-15f, -13f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2f;
            renderer.sharedMaterial = particle;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // ---------------------------------------------------------------- the story

    private static void BuildStory()
    {
        yellowPaint = Plain("YellowPaint", new Color(0.72f, 0.55f, 0.12f), 0.45f, 0.2f);
        Transform story = Group("Story");

        // Player first: the director and HUD point at it.
        // Just inside the front door, facing into the room: the panel blinks on the left, the desk window ahead.
        Transform introSpawn = Marker("IntroSpawn", story, M(0f, 0f, 0.9f), 0f);
        Transform drownRespawn = Marker("DrownRespawn", story, new Vector3(-4.3f, LandingY, -3.5f), 90f);
        PlayerRig rig = BuildPlayer(story, introSpawn);

        GameObject hudObject = new GameObject("DiveHud");
        hudObject.transform.SetParent(story, false);
        DiveHud hud = hudObject.AddComponent<DiveHud>();
        SetField(hud, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset"));
        SetField(hud, "air", rig.air);
        SetField(rig.interactor, "hud", hud);
        SetField(rig.gear, "hud", hud);
        SetField(rig.suffocation, "hud", hud);
        SetField(rig.respirator, "hud", hud);

        GameObject menuObject = new GameObject("PauseMenu");
        menuObject.transform.SetParent(story, false);
        PauseMenu menu = menuObject.AddComponent<PauseMenu>();
        SetField(menu, "player", rig.controller);
        SetField(menu, "hud", hud);
        SetField(menu, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset"));

        BrightnessApplier brightness = houseVolume.transform.parent.gameObject.AddComponent<BrightnessApplier>();
        SetField(brightness, "volumes", new Object[] { houseVolume, underwaterVolume });

        GameObject dreadObject = new GameObject("Dread");
        dreadObject.transform.SetParent(story, false);
        AudioSource heartbeat = dreadObject.AddComponent<AudioSource>();
        heartbeat.playOnAwake = false;
        heartbeat.spatialBlend = 0f;
        DreadDirector dread = dreadObject.AddComponent<DreadDirector>();
        SetField(dread, "viewCamera", rig.camera);
        SetField(dread, "volume", houseVolume);
        SetField(dread, "heartbeat", heartbeat);

        BuildGroundFloorDressing();
        FurnishManor(story);
        DressHallArt(story);
        GameObject[] gates = BuildBasementDressing();
        SetField(rig.gear, "deepWaterGates", gates);
        BuildBedroomDressing();
        Transform watcher = BuildLabDressing();
        BuildLabLights(watcher, dread);
        CreaturePass creature = BuildCreature("CreaturePass", dread, new Vector3(-5.6f, -8.85f, -5.75f), new Vector3(3.6f, -8.85f, -5.75f), 2.3f, 1f, "monster_03.ogg");

        GameObject stormObject = new GameObject("Storm");
        stormObject.transform.SetParent(story, false);
        stormObject.transform.position = M(0.2f, 7.5f, 14f);
        // Rain and thunder each on their own object: AmbienceSource drives one AudioSource, its level from
        // GameTuning (Som), quieter and muffled while the listener is under a roof.
        AudioSource rain = stormObject.AddComponent<AudioSource>();
        rain.spatialBlend = 0.75f;
        rain.minDistance = 9f;
        rain.maxDistance = 40f;
        stormObject.AddComponent<AmbienceSource>().SetKind(AmbienceSource.Kind.Rain);
        GameObject thunderObject = new GameObject("Thunder");
        thunderObject.transform.SetParent(stormObject.transform, false);
        AudioSource thunder = thunderObject.AddComponent<AudioSource>();
        thunder.playOnAwake = false;
        thunder.spatialBlend = 0f;
        thunderObject.AddComponent<AmbienceSource>().SetKind(AmbienceSource.Kind.Thunder);
        // The sea breaking on the rocks around the house, from two sides; muffled indoors like the rain.
        foreach (Vector3 shore in new[] { M(2f, -1.5f, 45f), M(-31f, -1.5f, 10f), M(33f, -1.5f, 6f) })
        {
            GameObject seaSound = new GameObject("SeaSound");
            seaSound.transform.SetParent(story, false);
            seaSound.transform.position = shore;
            AudioSource surf = seaSound.AddComponent<AudioSource>();
            surf.spatialBlend = 0.85f;
            surf.minDistance = 10f;
            surf.maxDistance = 60f;
            surf.loop = true;
            surf.playOnAwake = false;
            seaSound.AddComponent<AmbienceSource>().SetKind(AmbienceSource.Kind.Sea);
        }

        // The old house settling in the wind, now and then.
        GameObject creaks = new GameObject("HouseCreaks");
        creaks.transform.SetParent(story, false);
        creaks.transform.position = M(0.2f, 4.5f, 12f);
        AudioSource creakAudio = creaks.AddComponent<AudioSource>();
        creakAudio.spatialBlend = 0.6f;
        creakAudio.maxDistance = 25f;
        creakAudio.playOnAwake = false;
        RandomOneShots creakShots = creaks.AddComponent<RandomOneShots>();
        SetField(creakShots, "clips", SoundLibrary.Instance.houseCreaks);
        SetField(creakShots, "intervalSeconds", new Vector2(14f, 40f));
        SetField(creakShots, "volume", new Vector2(0.6f, 1f));

        LightningStorm storm = stormObject.AddComponent<LightningStorm>();
        SetField(storm, "flashLights", new Object[] { lightningSun, lightningSpot });
        SetField(storm, "rainSource", rain);
        SetField(storm, "thunderSource", thunder);

        // From under the basement water line nothing outside can be seen: the exterior and the sky lights
        // switch off there, shadow cascades included (SceneryCulling).
        Transform lighting = root.Find("Lighting");
        SceneryCulling culling = stormObject.AddComponent<SceneryCulling>();
        SetField(culling, "eye", rig.camera.transform);
        SetField(culling, "exterior", new Object[] { root.Find("Exterior").gameObject });
        SetField(culling, "skyLights", new Object[]
        {
            moon, lightningSun, lightningSpot,
            lighting.Find("WindowFill").GetComponent<Light>(), lighting.Find("EastWindowFill").GetComponent<Light>(), lighting.Find("CourtyardFill").GetComponent<Light>(),
        });
        SetField(culling, "hideBelowY", WaterLevel - 0.3f);

        GameObject directorObject = new GameObject("FloodedHouseDirector");
        directorObject.transform.SetParent(story, false);
        FloodedHouseDirector director = directorObject.AddComponent<FloodedHouseDirector>();
        SetField(director, "player", rig.controller);
        SetField(director, "air", rig.air);
        SetField(director, "gear", rig.gear);
        SetField(director, "interactor", rig.interactor);
        SetField(director, "hud", hud);
        SetField(director, "suffocation", rig.suffocation);
        SetField(director, "creature", creature);
        SetField(director, "storm", storm);
        SetField(director, "dread", dread);
        SetField(director, "introSpawn", introSpawn);
        SetField(director, "drownRespawn", drownRespawn);
        SetField(director, "landingZone", Zone(new Vector3(-6f, LandingY - 0.2f, -5f), new Vector3(-3f, -1f, StairBottomZ)));
        SetField(director, "labZone", Zone(new Vector3(-2.2f, -10f, -18f), new Vector3(0.2f, -7.5f, -1.8f)));
        SetField(director, "airPocketZone", Zone(new Vector3(0.25f, AirPocketLevel, -12f), new Vector3(5.25f, -6.5f, -7f)));
        SetField(director, "tankRoomZone", Zone(new Vector3(-4f, -10f, -24f), new Vector3(2f, -7f, -17.9f)));
        SetField(director, "creatureTriggerZone", Zone(new Vector3(-2.1f, -10f, -8.9f), new Vector3(0.1f, -7.5f, -7.2f)));
        AudioSource houseAudio = directorObject.AddComponent<AudioSource>();
        houseAudio.playOnAwake = false;
        houseAudio.spatialBlend = 0f;
        SetField(director, "houseAudio", houseAudio);
        BuildMansionStory(rig, hud, dread, director, houseAudio);
        // The revolver, the backpack, the Drowned of the house and the finale (FloodedHouseCombat).
        BuildCombat(rig, hud, director, dread, storm, houseAudio);
    }

    private static Transform Marker(string name, Transform parent, Vector3 position, float yaw)
    {
        Transform marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return marker;
    }

    private struct PlayerRig
    {
        public FirstPersonController controller;
        public AirSupply air;
        public DiveGear gear;
        public PlayerInteractor interactor;
        public Camera camera;
        public SuffocationEffects suffocation;
        public PlayerItems items;
        public Respirator respirator;
    }

    private static PlayerRig BuildPlayer(Transform parent, Transform spawn)
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(parent, false);
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        CharacterController body = player.AddComponent<CharacterController>();
        body.height = 1.75f;
        body.radius = 0.3f;
        body.center = new Vector3(0f, 0.875f, 0f);
        body.stepOffset = 0.35f;
        body.slopeLimit = 50f;
        body.skinWidth = 0.04f;
        body.minMoveDistance = 0f;
        AudioSource bodyAudio = player.AddComponent<AudioSource>();
        bodyAudio.playOnAwake = false;
        bodyAudio.spatialBlend = 0f;

        GameObject pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        pivot.tag = "MainCamera";
        Camera camera = pivot.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 600f;
        camera.fieldOfView = 68f;
        UniversalAdditionalCameraData data = pivot.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        // The frosted courtyard door (Horror/FrostedGlass) blurs the scene behind it by depth: only this camera needs the
        // opaque copy and the depth texture (the URP assets keep them off; cost about 0.1 ms).
        data.requiresColorOption = CameraOverrideOption.On;
        data.requiresDepthOption = CameraOverrideOption.On;
        pivot.AddComponent<AudioListener>();
        AudioLowPassFilter lowPass = pivot.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;
        AudioSource drone = pivot.AddComponent<AudioSource>();
        drone.spatialBlend = 0f;
        drone.playOnAwake = false;
        AudioSource splash = pivot.AddComponent<AudioSource>();
        splash.spatialBlend = 0f;
        splash.playOnAwake = false;

        // The first person arms (FirstPersonArms, Prefabs/Player/FirstPersonArms.prefab): a rig of the two arms
        // hanging from the camera through the ViewModel pivot (ViewModelMotion: sway, bob, kick). Its fixed clips are
        // where the hands, the torch and the gun are on the screen; the body copies its arms (ArmsFromRig).
        GameObject viewModel = new GameObject("ViewModel");
        viewModel.transform.SetParent(pivot.transform, false);
        ViewModelMotion motion = viewModel.AddComponent<ViewModelMotion>();
        GameObject armsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonRig.PrefabPath);
        if (armsPrefab == null)
        {
            Debug.LogError("[FloodedHouse] Missing " + FirstPersonRig.PrefabPath + " (Horror > Animação > Primeira pessoa: refazer o prefab do rig dos braços)");
        }

        GameObject armsObject = (GameObject)PrefabUtility.InstantiatePrefab(armsPrefab, viewModel.transform);
        FirstPersonArms arms = armsObject.GetComponent<FirstPersonArms>();
        // The clips' camera key (the reload looks down at the cylinder): CameraTilt.
        SetField(viewModel.AddComponent<CameraTilt>(), "arms", arms);

        // The torch hangs on the rig's torch socket (the hand's hold is the clip Torch_Hold). The light sits at the
        // lens; the model lies behind it, casts no shadow and only shows once the gear is picked up.
        Transform torchHand = new GameObject("TorchHand").transform;
        arms.Attach(torchHand, arms.TorchSocket);
        Light torch = PointOrSpot("DiveTorch", torchHand, Vector3.zero, LightType.Spot, Color.white, 38f, 22f, true);
        torch.transform.localPosition = Vector3.zero;
        torch.transform.localRotation = Quaternion.identity;
        ConfigureTorch(torch);
        // DiveTorch.fbx lies along its length with the lens (the wide end, measured) toward +Z once the
        // FBX root's axis conversion is applied, pivot at the bottom centre; the lens centre sits 0.048 m up
        // and 0.119 m forward of it. Put the lens just behind the light.
        GameObject heldTorch = ModelProp("DiveTorch", torchHand, torchHand.position, torchHand.rotation, false, false);
        heldTorch.name = "HeldTorch";
        heldTorch.transform.localPosition = new Vector3(0f, -0.048f, -0.119f - 0.015f);
        foreach (Renderer r in heldTorch.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ~20 cm off the body: light falls off with distance squared, and at 5 cm it blew the metal out.
        Light lensSpill = PointOrSpot("LensSpill", torchHand, Vector3.zero, LightType.Point, new Color(0.85f, 0.92f, 1f), 0.06f, 0.6f);
        lensSpill.transform.localPosition = new Vector3(-0.12f, 0.14f, -0.08f);
        lensSpill.enabled = false;

        ParticleSystem motes = Motes(pivot.transform);
        ParticleSystem bubbles = ExhaleBubbles(pivot.transform);
        ParticleSystem flakes = DirtFlakes(pivot.transform);
        ParticleSystem dust = AirDust(pivot.transform);
        ParticleSystem specks = FallingSpecks(pivot.transform);

        UnderwaterView view = pivot.AddComponent<UnderwaterView>();
        SetField(view, "underwaterVolume", underwaterVolume);
        SetField(view, "motes", motes);
        SetField(view, "exhaleBubbles", bubbles);
        SetField(view, "lowPass", lowPass);
        SetField(view, "droneSource", drone);
        SetField(view, "splashSource", splash);
        SetField(view, "waterParticles", new Object[] { flakes });
        SetField(view, "airParticles", new Object[] { dust, specks });

        FirstPersonController controller = player.AddComponent<FirstPersonController>();
        SetField(controller, "cameraPivot", pivot.transform);
        SetField(controller, "bodyAudio", bodyAudio);
        AirSupply air = player.AddComponent<AirSupply>();
        SetField(air, "controller", controller);
        SetField(air, "breathAudio", bodyAudio);
        // The eye adapting (04/10): looking at a lamp closes it, the dark opens it a little (GameTuning).
        pivot.AddComponent<AutoExposure>();
        SuffocationEffects suffocation = pivot.AddComponent<SuffocationEffects>();
        SetField(suffocation, "air", air);
        SetField(suffocation, "view", view);
        // Aiming, blows, running out of breath and fear on the screen and in the ears (Immersion).
        ImmersionSetup.Add(pivot, controller, view);
        DiveGear gear = player.AddComponent<DiveGear>();
        SetField(gear, "controller", controller);
        SetField(gear, "torch", torch);
        SetField(gear, "audioSource", bodyAudio);
        SetField(gear, "toggleClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Switch.ogg"));
        TorchHold hold = torchHand.gameObject.AddComponent<TorchHold>();
        SetField(hold, "controller", controller);
        SetField(hold, "gear", gear);
        SetField(hold, "arms", arms);
        SetField(hold, "model", heldTorch);
        SetField(hold, "beam", torch);
        SetField(hold, "spill", lensSpill);
        TorchBeam beam = torch.gameObject.AddComponent<TorchBeam>();
        SetField(beam, "beamMaterial", TorchBeamMaterial());
        PlayerBody visualBody = BuildPlayerBody(player, controller, arms);
        SetField(controller, "visualBody", visualBody);
        SetField(motion, "controller", controller);
        SetField(motion, "body", visualBody);
        SetField(motion, "arms", arms);
        WaterTransitions waterFx = player.AddComponent<WaterTransitions>();
        AudioSource waterAudio = player.AddComponent<AudioSource>();
        waterAudio.playOnAwake = false;
        waterAudio.spatialBlend = 0f;
        SetField(waterFx, "controller", controller);
        SetField(waterFx, "source", waterAudio);
        SetField(waterFx, "splash", SplashBurst(parent));
        SetField(waterFx, "runoff", Runoff(player.transform));
        SetField(waterFx, "footsteps", visualBody.GetComponent<FootstepAudio>());
        PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
        SetField(interactor, "controller", controller);

        // What Erick carries and the respirator of the attic suit.
        PlayerItems items = player.AddComponent<PlayerItems>();
        Respirator respirator = player.AddComponent<Respirator>();
        AudioSource maskLoop = player.AddComponent<AudioSource>();
        maskLoop.spatialBlend = 0f;
        maskLoop.loop = true;
        maskLoop.volume = 0f;
        // Breathing recorded inside a real GP-5 gas mask (Nuclearoid, CC0), 39 s so the loop is not noticed.
        maskLoop.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Breath/gas_mask_breathing_loop.wav");
        maskLoop.playOnAwake = true;
        SetField(respirator, "controller", controller);
        SetField(respirator, "breathAudio", bodyAudio);
        SetField(respirator, "maskLoop", maskLoop);
        SetField(respirator, "swapClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Equip_Keys.ogg"));
        // The mask seen from inside while it is on (rim, glass, breath fogging it).
        MaskVisor visor = pivot.AddComponent<MaskVisor>();
        SetField(visor, "respirator", respirator);
        SetField(visor, "controller", controller);

        // Dark vision: below and slightly behind the eye, so it lights the floor, not a headlamp spot.
        GameObject fillObject = new GameObject("DarkVision");
        fillObject.transform.SetParent(pivot.transform, false);
        fillObject.transform.localPosition = new Vector3(0f, -0.4f, -0.15f);
        Light fill = fillObject.AddComponent<Light>();
        fill.intensity = 0f;
        DarkVision darkVision = fillObject.AddComponent<DarkVision>();
        SetField(darkVision, "gear", gear);
        SetField(darkVision, "eye", pivot.transform);
        // Shadow maps for the lamps and glows nearest the player, so light stops passing through walls.
        SetField(player.AddComponent<ShadowBudget>(), "exclude", player.transform);
        player.AddComponent<FillCulling>();

        return new PlayerRig
        {
            controller = controller, air = air, gear = gear, interactor = interactor, camera = camera, suffocation = suffocation,
            items = items, respirator = respirator,
        };
    }

    // Silt hanging in the water around the lens; only emits while the camera is submerged.
    private static ParticleSystem Motes(Transform parent)
    {
        GameObject go = new GameObject("Motes");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
        main.startColor = new Color(0.55f, 0.65f, 0.6f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1200;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 140f;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(6f, 4f, 6f);
        shape.position = new Vector3(0f, 0f, 2f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.3f;
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut();
        // Lit, not unlit: the specks only show inside the torch cone, the backscatter every diver sees.
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = LitParticle();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    private static Material LitParticle()
    {
        Material lit = Material("ParticleLit", Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
        lit.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/light_01.png"));
        lit.SetColor("_BaseColor", new Color(1f, 1f, 1f, 1f));
        lit.SetFloat("_Surface", 1f);
        lit.SetFloat("_Blend", 0f);
        lit.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        lit.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        lit.SetFloat("_ZWrite", 0f);
        lit.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        lit.renderQueue = (int)RenderQueue.Transparent;
        // Specks right against the lens would fill the screen once the torch lights them: fade them
        // out inside the first metre and a half.
        lit.SetFloat("_CameraFadingEnabled", 1f);
        lit.SetFloat("_CameraNearFadeDistance", 0.5f);
        lit.SetFloat("_CameraFarFadeDistance", 1.6f);
        lit.SetVector("_CameraFadeParams", new Vector4(0.5f, 1f / (1.6f - 0.5f), 0f, 0f));
        lit.EnableKeyword("_FADING_ON");
        EditorUtility.SetDirty(lit);
        return lit;
    }

    private static ParticleSystem ExhaleBubbles(Transform parent)
    {
        GameObject go = new GameObject("ExhaleBubbles");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, -0.12f, 0.22f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
        main.startColor = new Color(0.8f, 0.95f, 0.95f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.04f;
        ParticleSystem.VelocityOverLifetimeModule rise = ps.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.World;
        rise.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        rise.y = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
        rise.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        ParticleSystem.NoiseModule wobble = ps.noise;
        wobble.enabled = true;
        wobble.strength = 0.25f;
        wobble.frequency = 1.5f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particle;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    // ---------------------------------------------------------------- dirt in the air and the water
    // The world is contaminated: under water, olive-brown flakes drift and slowly sink (bigger and rarer
    // than the silt motes); on land, dust hangs in the air and specks fall from the swollen ceiling.
    // All lit, so they only show where a lamp, the torch or the lightning reaches, and all emitted around
    // the camera, switched by UnderwaterView between the water set and the air set.

    private static ParticleSystem DirtFlakes(Transform parent)
    {
        ParticleSystem ps = CameraParticles("DirtFlakes", parent, new Vector3(7f, 4f, 7f), 170f, new ParticleSystem.MinMaxCurve(6f, 10f),
            new ParticleSystem.MinMaxCurve(0.02f, 0.07f), new Color(0.75f, 0.68f, 0.45f, 0.85f), LitDirtParticle());
        ParticleSystem.MainModule main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.gravityModifier = 0.004f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        return ps;
    }

    private static ParticleSystem AirDust(Transform parent)
    {
        ParticleSystem ps = CameraParticles("AirDust", parent, new Vector3(6f, 3.5f, 6f), 90f, new ParticleSystem.MinMaxCurve(6f, 9f),
            new ParticleSystem.MinMaxCurve(0.003f, 0.01f), new Color(0.85f, 0.78f, 0.66f, 0.45f), LitParticle());
        ParticleSystem.MainModule main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.005f, 0.03f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.strength = 0.04f;
        noise.frequency = 0.2f;
        return ps;
    }

    private static ParticleSystem FallingSpecks(Transform parent)
    {
        ParticleSystem ps = CameraParticles("FallingSpecks", parent, new Vector3(5f, 0.2f, 5f), 9f, new ParticleSystem.MinMaxCurve(5f, 8f),
            new ParticleSystem.MinMaxCurve(0.004f, 0.011f), new Color(0.3f, 0.27f, 0.2f, 0.8f), LitDirtParticle());
        ps.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        ParticleSystem.MainModule main = ps.main;
        main.startSpeed = 0f;
        main.gravityModifier = 0.012f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.position = new Vector3(0f, 0f, 1.5f);
        return ps;
    }

    private static ParticleSystem CameraParticles(string name, Transform parent, Vector3 box, float rate, ParticleSystem.MinMaxCurve lifetime,
        ParticleSystem.MinMaxCurve size, Color color, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = lifetime;
        main.startSize = size;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2000;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        shape.position = new Vector3(0f, 0f, 2f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.06f;
        noise.frequency = 0.3f;
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut();
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    // Irregular flakes (Kenney dirt splat) instead of soft round dots, same lit set-up as the silt.
    private static Material LitDirtParticle()
    {
        Material lit = LitParticle();
        Material dirt = Material("ParticleLitDirt", lit.shader);
        dirt.CopyPropertiesFromMaterial(lit);
        dirt.shaderKeywords = lit.shaderKeywords;
        dirt.renderQueue = lit.renderQueue;
        dirt.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/dirt_02.png"));
        EditorUtility.SetDirty(dirt);
        return dirt;
    }

    // The basement water is hot and foul: faint wisps of vapour crawl off its surface. Lit, so they only
    // read where the landing lantern or the torch reaches; soft, so they never cut a line into the water.
    private static void BasementSteam(Transform parent)
    {
        GameObject go = new GameObject("BasementSteam");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(-2f, WaterLevel + 0.08f, -2f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
        main.startColor = new Color(0.72f, 0.78f, 0.72f, 0.05f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 12f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(7.4f, 0.05f, 5.4f);
        ParticleSystem.VelocityOverLifetimeModule rise = ps.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.World;
        rise.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        rise.y = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        rise.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.12f;
        noise.frequency = 0.25f;
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        fade.color = FadeInOut();
        ParticleSystem.SizeOverLifetimeModule grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));

        Material steam = Material("SteamLit", Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
        steam.CopyPropertiesFromMaterial(LitParticle());
        steam.shaderKeywords = LitParticle().shaderKeywords;
        steam.renderQueue = (int)RenderQueue.Transparent;
        steam.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/smoke_04.png"));
        steam.SetFloat("_CameraFadingEnabled", 1f);
        steam.SetVector("_CameraFadeParams", new Vector4(0.3f, 1f / 1.2f, 0f, 0f));
        steam.SetFloat("_SoftParticlesEnabled", 1f);
        steam.SetFloat("_SoftParticlesNearFadeDistance", 0f);
        steam.SetFloat("_SoftParticlesFarFadeDistance", 0.6f);
        steam.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.6f, 0f, 0f));
        steam.EnableKeyword("_SOFTPARTICLES_ON");
        EditorUtility.SetDirty(steam);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = steam;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // Droplets thrown up where the body hits the water (WaterTransitions places and fires it).
    private static ParticleSystem SplashBurst(Transform parent)
    {
        GameObject go = new GameObject("WaterSplash");
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
        main.startColor = new Color(0.75f, 0.85f, 0.85f, 0.7f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 200;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.3f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = LitParticle();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    // Water running off the body after climbing out: drips from waist to shoulders, falling.
    private static ParticleSystem Runoff(Transform player)
    {
        GameObject go = new GameObject("Runoff");
        go.transform.SetParent(player, false);
        go.transform.localPosition = new Vector3(0f, 1.05f, 0.05f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
        main.startColor = new Color(0.8f, 0.88f, 0.9f, 0.75f);
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.45f, 0.6f, 0.3f);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = LitParticle();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return ps;
    }

    private static Gradient FadeInOut()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }

    // Electric fittings of the house (Gustavo, 01/10: a mansion on a private island has real lamps, not oil
    // lamps). Modelled the way a shaded lamp actually lights a room (second pass, 01/10, after "the ceiling is a
    // white blob", "the plates are white", "the sconce is a burst"), with three lights per fitting:
    // - key: a wide SPOT pointing down from inside the shade, because the shade stops the light going up and
    //   sideways; with a bare point light the ceiling a few centimetres above got the brightest spot of the room.
    //   Intensity chosen for what is under it (URP lights are point sources, illuminance = I / d^2): the plates
    //   and tablecloth under the chandelier stay below white (at 5 on a point light they clipped).
    // - fill: the light bounced off the floor, table and walls (realtime URP has no GI), as a point light at 60%
    //   of the key's strength 1.6 m above the floor, mid-height and away from every surface, so it lights ceiling,
    //   walls and corners evenly without a hot spot. Key and fill levels measured in Play (01/10): nothing on
    //   the table clips, the ceiling reads as lit by the room and not by the bulb.
    // - glow: a tiny shadowless point at the bulb, a few decimetres of reach, so the shade and arms of the
    //   fitting itself are lit from inside without touching the ceiling.
    // The fixture casts no shadow (the key light sits inside it). ShadowBudget gives key and fill a shadow map
    // when the player is near, so nothing lights the next room through the wall. Tungsten 2700 K, slightly
    // desaturated (raw it turns orange under ACES). Levels from GameTuning > Luminárias da casa (TunedLamp).
    private static readonly Color BulbColour = new Color(1f, 0.8f, 0.6f);

    // 18/10 (Gustavo: "a lâmpada ilumina só um ponto no chão e ela mesma fica escura, no jogo todo"): measured in the
    // hall before the change, 70% of the frame under 10/255 and the globe grey. Three things, all in this builder so
    // every fitting of the house changes together:
    //   - the key cone's soft edge starts at 75% of the cone (was 40%): the disc on the floor spreads instead of a
    //     spotlight, and the walls inside the cone get the light a bare bulb gives them;
    //   - the fill (the room's bounce) is the main light of the room now: GameTuning.lampBounce 0.3 -> 0.8 of the key;
    //   - a lit fitting glows: its shade material gets an emissive copy ("<Material>On", the base colour as the emission
    //     mask, so the globe or shade glows and the metal does not) while the mains are on (PowerGrid swaps it back).
    private const float KeyInnerFraction = 0.75f;

    // A fitting's shade material while the mains are on: the same material plus emission masked by its own colour map.
    private static Material LitFixtureMaterial(Material off, float emission)
    {
        if (off == null || off.name.EndsWith("On"))
        {
            return null;
        }

        string path = ProjectPaths.FloodedHouseMaterials + "/" + off.name + "On.mat";
        Material on = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (on == null)
        {
            on = new Material(off);
            AssetDatabase.CreateAsset(on, path);
        }
        else
        {
            on.CopyPropertiesFromMaterial(off);
        }

        on.name = off.name + "On";
        on.EnableKeyword("_EMISSION");
        on.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        on.SetTexture("_EmissionMap", off.HasProperty("_BaseMap") ? off.GetTexture("_BaseMap") : null);
        on.SetColor("_EmissionColor", BulbColour * emission);
        EditorUtility.SetDirty(on);
        return on;
    }

    // The fitting glows while powered: its renderers get the lit material and the PowerGrid gets the pair to swap back.
    private static void LitFixture(GameObject fixture, float emission)
    {
        foreach (Renderer r in fixture.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material on = LitFixtureMaterial(mats[i], emission);
                if (on != null)
                {
                    gridFixtures.Add(r);
                    gridFixturesOff.Add(mats[i]);
                    gridFixturesOn.Add(on);
                    mats[i] = on;
                    changed = true;
                }
            }

            if (changed)
            {
                r.sharedMaterials = mats;
            }
        }
    }

    private static void ElectricLight(string name, Transform parent, GameObject fixture, Vector3 bulbAt, float bulbSize, Vector3 lightAt,
        float intensity, float range, bool shadows, float spotAngle, Vector3 fillAt)
    {
        fixture.name = name;
        foreach (Renderer r in fixture.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // The bulb seen through the opening of the shade: warm and bright, kept under the bloom threshold after
        // exposure so it reads as a bulb, not a ball of light. No bulb where the fitting hides it (bulbSize 0).
        if (bulbSize > 0f)
        {
            Material bulbMat = Material("BulbLit", Shader.Find("Universal Render Pipeline/Unlit"));
            bulbMat.SetColor("_BaseColor", BulbColour * 1.1f);
            EditorUtility.SetDirty(bulbMat);
            GameObject bulb = Primitive(PrimitiveType.Sphere, "Bulb", parent, bulbAt, Quaternion.identity, Vector3.one * bulbSize, bulbMat);
            bulb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            gridGlows.Add(bulb.GetComponent<MeshRenderer>());
        }

        Light key = PointOrSpot(name + "Light", parent, lightAt, LightType.Spot, BulbColour, intensity, range, shadows);
        key.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        key.spotAngle = spotAngle;
        key.innerSpotAngle = spotAngle * KeyInnerFraction;
        key.shadowNearPlane = 0.1f;
        gridLights.Add(key);
        Volumetric(key, 0f, 0.2f);
        key.gameObject.AddComponent<TunedLamp>().Configure(intensity, range, false);

        float fillIntensity = intensity * 0.6f;
        Light fill = PointOrSpot(name + "Bounce", parent, fillAt, LightType.Point, BulbColour, fillIntensity, range);
        gridLights.Add(fill);
        fill.gameObject.AddComponent<TunedLamp>().Configure(fillIntensity, range, true);

        float glowIntensity = 0.12f;
        Light glow = PointOrSpot(name + "Glow", parent, bulbAt, LightType.Point, BulbColour, glowIntensity, 0.45f);
        gridLights.Add(glow);
        glow.gameObject.AddComponent<TunedLamp>().Configure(glowIntensity, 0.45f, false);
    }

    // A pendant hung from the ceiling: the model's top touches the ceiling, the bulb sits inside its shade, which
    // opens downward.
    private static void CeilingLamp(string name, Transform parent, float x, float z, float ceilingY, float intensity, float range, bool shadows = false)
    {
        GameObject fixture = ModelProp("CeilingLamp", parent, new Vector3(x, 0f, z), Quaternion.identity, false);
        fixture.transform.position += Vector3.up * (ceilingY - RendererBounds(fixture).max.y);
        float low = RendererBounds(fixture).min.y;
        float floor = ceilingY - 3f;
        // A frosted globe: it glows itself and throws light nearly all round (160 degrees from the bulb at its bottom).
        LitFixture(fixture, 1.6f);
        ElectricLight(name, parent, fixture, new Vector3(x, low + 0.07f, z), 0.05f, new Vector3(x, low + 0.04f, z), intensity, range, shadows, 160f,
            new Vector3(x, floor + 1.6f, z));
    }

    // The chandelier over the dinner table: open fabric shades, light mostly down onto the table.
    private static void Chandelier(string name, Transform parent, float x, float z, float ceilingY, float intensity, float range)
    {
        GameObject fixture = ModelProp("Chandelier", parent, new Vector3(x, 0f, z), Quaternion.identity, false);
        fixture.transform.position += Vector3.up * (ceilingY - RendererBounds(fixture).max.y);
        Bounds b = RendererBounds(fixture);
        float shadesY = b.min.y + b.size.y * 0.25f;
        float floor = ceilingY - 3f;
        // No visible bulb: the candle bulbs sit inside the shades (a sphere in the middle read as a ball of light).
        LitFixture(fixture, 0.9f);
        ElectricLight(name, parent, fixture, new Vector3(x, shadesY, z), 0f, new Vector3(x, shadesY, z), intensity, range, true, 155f,
            new Vector3(x, floor + 1.6f, z));
    }

    // A wall sconce: its back plate against the wall at `wallAt`, the bell shade out along `facing`, open below (or, for
    // a tulip shade like Dayse's Victorian lamp, open above: `shadeUp`, the bulb up inside it).
    private static void WallSconce(string name, Transform parent, Vector3 wallAt, Vector3 facing, float floorY, float intensity, float range,
        string model = "WallSconce", bool shadeUp = false)
    {
        float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        GameObject fixture = ModelProp(model, parent, wallAt, Quaternion.Euler(0f, yaw, 0f), false, false);
        // Pivot at the base of the model: drop it so its middle is at the given height, back plate on the wall.
        Bounds b = RendererBounds(fixture);
        fixture.transform.position += Vector3.up * (wallAt.y - b.center.y);
        b = RendererBounds(fixture);
        float back = Vector3.Dot(b.center - wallAt, facing) - Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(facing.x), Mathf.Abs(facing.y), Mathf.Abs(facing.z))));
        fixture.transform.position -= facing * back;
        b = RendererBounds(fixture);
        float depth = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(facing.x), 0f, Mathf.Abs(facing.z))));
        Vector3 bulbAt = new Vector3(wallAt.x, shadeUp ? b.max.y - 0.12f : b.min.y + 0.07f, wallAt.z) + facing * (depth * 0.7f);
        // The fill sits a metre out from the wall, halfway down: lighting the wall from centimetres away was the burst.
        Vector3 fillAt = new Vector3(bulbAt.x, Mathf.Lerp(floorY, bulbAt.y, 0.5f), bulbAt.z) + facing * 1.0f;
        LitFixture(fixture, 0.8f);
        ElectricLight(name, parent, fixture, bulbAt, 0.035f, bulbAt + Vector3.up * 0.02f, intensity, range, false, 110f, fillAt);
    }

    // ---------------------------------------------------------------- rooms

    // The front rooms of the mansion (GDD floor plan): the radio desk in the dining room's bay, the Lunas' living room
    // and the library. The dinner table, the hall and the other rooms are FloodedHouseMansionStory's.
    private static void BuildGroundFloorDressing()
    {
        Transform g = Group("GroundFloorDressing");

        // The radio desk in the dining room's bay window (the first recording lies on it; the tentacle passes outside).
        Transform desk = Group("RadioDesk", g);
        Box("Top", desk, M(-8.55f, 0.74f, 2.05f), M(-7.05f, 0.78f, 2.85f), doorPaint);
        foreach (Vector2 leg in new[] { V2(-8.5f, 2.1f), V2(-7.1f, 2.1f), V2(-8.5f, 2.8f), V2(-7.1f, 2.8f) })
        {
            Box("Leg", desk, M(leg.x - 0.03f, 0f, leg.y - 0.03f), M(leg.x + 0.03f, 0.74f, leg.y + 0.03f), doorPaint);
        }

        Box("Drawers", desk, M(-7.54f, 0.45f, 2.06f), M(-7.1f, 0.74f, 2.84f), doorPaint);

        // Meshy shortwave set (game-3d-assets: 2.5k triangles); its dials face the model's +Z (into the room).
        GameObject radioProp = ModelProp("ShortwaveRadio", g, M(-7.45f, 0.78f, 2.35f), Quaternion.identity);
        // It runs on batteries: turned on, it catches the Defesa Civil emergency bulletin (the news of year 15 reaches
        // the house by radio and leaflets, not newspapers; voice_process.py builds "radio" from the radio_* lines).
        AudioSource radioVoice = radioProp.AddComponent<AudioSource>();
        radioVoice.spatialBlend = 0.8f;
        radioVoice.minDistance = 1.5f;
        radioVoice.maxDistance = 14f;
        radioVoice.playOnAwake = false;
        Recorder radio = radioProp.AddComponent<Recorder>();
        radio.SetPrompt("prompt.radio");
        radio.Configure("radio_defesa", "story.radio.title", Voice("radio"), VoiceLines("radio"), null, null);
        SetField(radio, "speaker", "hud.speaker.radio");
        SetField(radio, "source", radioVoice);
        SetField(radio, "buttonClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Switch.ogg"));
        Prop("book_encyclopedia_set_01", g, M(-8.4f, 0.78f, 2.2f), 0f, false);
        // Pushed back from the desk, as if someone got up in a hurry.
        MakeSeat(Prop("painted_wooden_chair_01", g, M(-7.1f, 0f, 3.6f), 245f));
        Prop("hanging_picture_frame_01", g, M(-11.49f, 1.9f, 4.6f), Quaternion.Euler(0f, 90f, -3f), false);
        Prop("hanging_picture_frame_01", g, M(-4.11f, 1.95f, 7.4f), Quaternion.Euler(0f, -90f, 2f), false);

        // The Lunas' sitting room (Gustavo, 01/10: a private-island house should look wealthy). Poly Haven
        // pieces already inside the budget (sofa 4.1k, side table 2.8k, vase 2.5k, frame 0.9k); the coffee
        // table and the console went through game-3d-assets (10.4k and 7.6k to 5k).
        MakeSeat(Prop("Sofa_01", g, M(7.8f, 0f, 3.9f), 0f));
        ModelProp("CoffeeTable", g, M(7.8f, 0f, 4.95f), Quaternion.identity);
        GameObject sideTable = Prop("side_table_01", g, M(9.05f, 0f, 3.9f), 0f);
        Prop("ceramic_vase_01", g, M(9.05f, RendererBounds(sideTable).max.y, 3.9f), 30f, false);
        MakeSeat(Prop("ArmChair_01", g, M(5.4f, 0f, 6.4f), 117f));
        MakeSeat(Prop("Rockingchair_01", g, M(10.3f, 0f, 7.4f), 200f));
        GameObject console = ModelProp("Console", g, M(8.2f, 0f, 8.55f), Quaternion.Euler(0f, 180f, 0f));
        Prop("ceramic_vase_01", g, M(8.6f, RendererBounds(console).max.y, 8.55f), 0f, false, 0.8f);
        // (The mantel clock came out of the optimiser with its glass baked black over the dial: left out.)
        ModelProp("Candleholders", g, M(7.6f, RendererBounds(console).max.y, 8.5f), Quaternion.Euler(0f, 180f, 0f), false);
        Prop("fancy_picture_frame_01", g, M(8.2f, 1.75f, 8.74f), 180f, false);
        Prop("vintage_wooden_drawer_01", g, M(5.2f, 0f, 8.5f), 180f);
        Prop("wooden_bookshelf_worn", g, M(11.32f, 0f, 4.4f), -90f);
        Prop("wall_clock", g, M(4.11f, 2.2f, 3.2f), 90f, false);
        ModelProp("DiningChair", g, M(5.6f, 0f, 3.0f), Quaternion.Euler(0f, 40f, 88f));

        // The library: shelves along the walls, a reading table.
        // Shelves between the windows of the east wall, along the north wall and either side of the south window, each
        // full of the encyclopaedias (the metal office desk that stood here read as a filing cabinet: out).
        foreach ((Vector2 at, float yaw) in new[] { (V2(19.74f, 8.4f), -90f), (V2(12f, 13.24f), 180f), (V2(14f, 13.24f), 180f), (V2(16f, 13.24f), 180f), (V2(12.9f, 3.56f), 0f), (V2(17.6f, 3.56f), 0f) })
        {
            ShelfBooks(g, Prop("wooden_bookshelf_worn", g, M(at, 0f), yaw));
        }

        MakeSeat(Prop("ArmChair_01", g, M(13f, 0f, 5f), 40f));
    }

    // Book sets on the shelves of a bookcase: three rows, at the shelf boards of wooden_bookshelf_worn.
    private static void ShelfBooks(Transform parent, GameObject shelf)
    {
        Bounds b = RendererBounds(shelf);
        foreach (float t in new[] { 0.06f, 0.38f, 0.69f })
        {
            Vector3 at = new Vector3(b.center.x, b.min.y + b.size.y * t + 0.02f, b.center.z);
            Prop("book_encyclopedia_set_01", parent, at, shelf.transform.eulerAngles.y, false);
        }
    }

    private static GameObject[] BuildBasementDressing()
    {
        Transform b = Group("BasementDressing");

        // The bench: GDD step 4, pick up the kit = the interaction tutorial.
        Prop("tool_cart", b, new Vector3(-4.6f, LandingY, -4.55f), 0f);
        Prop("portable_generator", b, new Vector3(-3.65f, LandingY, -4.5f), 200f);
        Transform kit = Group("DiveKit", b);
        float benchTop = LandingY + 0.96f;
        // Meshy models through the game-3d-assets skill: mask 3.1k, pony bottle 2k triangles. The mask comes
        // lying down from Meshy; the bottle is tipped onto its side. The torch was found in the house.
        GameObject mask = ModelProp("DiveMask", kit, new Vector3(-5f, benchTop, -4.5f), Quaternion.Euler(0f, 20f, 0f), false);
        GameObject cylinder = ModelProp("PonyBottle", kit, new Vector3(-4.55f, benchTop, -4.55f), Quaternion.Euler(0f, 75f, 90f), false);
        BoxCollider kitReach = kit.gameObject.AddComponent<BoxCollider>();
        kitReach.isTrigger = true;
        kitReach.center = new Vector3(-4.6f, benchTop + 0.1f, -4.5f);
        kitReach.size = new Vector3(1.1f, 0.3f, 0.5f);
        GearPickup pickup = kit.gameObject.AddComponent<GearPickup>();
        pickup.SetPrompt("prompt.take_gear");
        SetField(pickup, "visuals", new Object[] { mask, cylinder });
        SetField(pickup, "pickupClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Pickup_Metal.ogg"));

        BasementSteam(b);

        // A lantern hung over the landing: the last warm light before the water.
        Box("Chain", b, new Vector3(-4.51f, -1.0f, -3.61f), new Vector3(-4.49f, -0.25f, -3.59f), metal, false);
        // Dayse's Victorian wall lamp (Sketchfab, Javier Carceller, CC BY; brass set back to metal by hand after the bake).
        WallSconce("LandingSconce", b, new Vector3(-5.99f, 0.35f, -3.6f), Vector3.right, LandingY, 1.3f, 6f, "VictorianWallLamp", true);

        Prop("steel_frame_shelves_01", b, new Vector3(0f, -4.5f, 0.72f), 180f);
        Prop("worn_metal_rack", b, new Vector3(1.66f, -4.5f, -4.1f), -90f);
        Floating(Prop("Barrel_01", b, new Vector3(-1.3f, WaterLevel - 0.5f, -0.5f), Quaternion.Euler(12f, 30f, 8f)));
        Floating(Prop("plastic_crate_01", b, new Vector3(0.9f, WaterLevel - 0.12f, -3.1f), Quaternion.Euler(4f, 70f, -3f)));
        Floating(Prop("plastic_crate_02", b, new Vector3(-2.6f, WaterLevel - 0.1f, 0.2f), Quaternion.Euler(-5f, 15f, 6f)));

        GameObject drips = new GameObject("Drips");
        drips.transform.SetParent(b, false);
        drips.transform.position = new Vector3(-1f, -0.6f, -1.5f);
        AudioSource dripAudio = drips.AddComponent<AudioSource>();
        dripAudio.spatialBlend = 1f;
        dripAudio.maxDistance = 16f;
        dripAudio.playOnAwake = false;
        drips.AddComponent<RandomOneShots>();

        // M. shoved a cabinet against the low breach into the sunken bedroom (east wall, z -2.5..-0.7,
        // y -4.5..-3.1). It stays put until the slate in the tank room gives the player a reason.
        Transform cabinet = Group("BedroomBlocker", b);
        cabinet.position = new Vector3(1.66f, -4.5f, -1.6f);
        GameObject cabinetBody = Prop("painted_wooden_cabinet", cabinet, new Vector3(1.66f, -4.5f, -1.6f), -90f, false);
        GameObject blockerBox = Box("Blocker", cabinet, new Vector3(1.45f, -4.5f, -2.65f), new Vector3(2f, -2.9f, -0.55f), glass);
        blockerBox.GetComponent<MeshRenderer>().enabled = false;
        bedroomBlocker = cabinet.gameObject.AddComponent<PassageBlocker>();
        SetField(bedroomBlocker, "body", cabinetBody.transform);
        SetField(bedroomBlocker, "blocker", blockerBox.GetComponent<Collider>());
        // Tipped over onto the floor, clear of the gap (and of the shelves by the north wall).
        SetField(bedroomBlocker, "pushedOffset", new Vector3(-0.4f, 0f, 1.4f));
        SetField(bedroomBlocker, "pushedEuler", new Vector3(0f, 0f, 75f));
        SetField(bedroomBlocker, "pushClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Ambience/tree_creak_0.ogg"));

        // Invisible gates along the open edges of the landing, lifted once the kit is taken.
        GameObject gateEast = Box("DeepWaterGate_East", b, new Vector3(-3.05f, LandingY, -5f), new Vector3(-2.95f, LandingY + 1.5f, StairBottomZ), glass);
        GameObject gateNorth = Box("DeepWaterGate_North", b, new Vector3(-4.8f, LandingY, StairBottomZ - 0.05f), new Vector3(-3f, LandingY + 1.5f, StairBottomZ + 0.05f), glass);
        gateEast.GetComponent<MeshRenderer>().enabled = false;
        gateNorth.GetComponent<MeshRenderer>().enabled = false;
        return new[] { gateEast, gateNorth };
    }

    // The sunken room east of the basement is the pantry of Dayse's script, flooded for days: shelves of
    // tins and jars, crates and barrels, things afloat against the ceiling, and the green locker in the far
    // corner (FloodedHouseMansionStory). The window to the drowned garden keeps its cold glow.
    private static void BuildBedroomDressing()
    {
        Transform r = Group("PantryDressing");
        Prop("steel_frame_shelves_01", r, new Vector3(3.2f, -6f, -3.7f), 0f);
        Prop("steel_frame_shelves_01", r, new Vector3(4.6f, -6f, -3.7f), 0f);
        Prop("worn_metal_rack", r, new Vector3(8.0f, -6f, -2.2f), -90f);
        Prop("wooden_barrels_01", r, new Vector3(6.4f, -6f, -3.4f), 30f);
        Prop("wooden_crate_01", r, new Vector3(5.6f, -6f, -1.6f), 15f);
        Prop("old_military_crate", r, new Vector3(3.4f, -6f, -0.4f), 80f);
        foreach (Vector3 at in new[] { new Vector3(3.1f, -4.9f, -3.75f), new Vector3(3.4f, -4.9f, -3.7f), new Vector3(4.7f, -5.4f, -3.75f), new Vector3(4.4f, -4.3f, -3.7f) })
        {
            Prop("can_rusted", r, at, Random.Range(0f, 360f), false);
        }

        Prop("ornate_mirror_01", r, new Vector3(8.29f, -4.3f, -3.4f), -90f, false);
        Prop("wall_clock", r, new Vector3(5.6f, -4f, -3.99f), 0f, false);

        // Afloat against the ceiling: crates, a bucket, the kitchen chair that ended up down here.
        Floating(Prop("plastic_crate_01", r, new Vector3(4.6f, -3.3f, -2.6f), Quaternion.Euler(172f, 20f, 8f)));
        Floating(Prop("painted_wooden_chair_01", r, new Vector3(6.2f, -3.3f, -0.9f), Quaternion.Euler(176f, 80f, -8f)));
        Floating(Prop("plastic_crate_02", r, new Vector3(3.4f, -3.3f, -3.1f), Quaternion.Euler(166f, -40f, 14f)));
        Floating(Prop("wooden_bucket_01", r, new Vector3(7.1f, -3.35f, -2.8f), Quaternion.Euler(150f, 10f, 20f)));
    }

    private static void Floating(GameObject prop)
    {
        prop.AddComponent<FloatingBob>();
        GameObjectUtility.SetStaticEditorFlags(prop, 0);
    }

    private static Transform BuildLabDressing()
    {
        Transform lab = Group("LabDressing");
        foreach (Vector3 at in new[] { new Vector3(-1.99f, -8.05f, -3.3f), new Vector3(-1.99f, -8.05f, -10.5f), new Vector3(-1.99f, -8.05f, -16.5f) })
        {
            Prop("industrial_wall_lamp", lab, at, 90f, false);
        }

        Prop("industrial_wall_lamp", lab, new Vector3(-3.99f, -7.85f, -19f), 90f, false);
        Prop("industrial_wall_lamp", lab, new Vector3(1.99f, -7.85f, -23.5f), -90f, false);
        Prop("modular_industrial_pipes_01", lab, new Vector3(-0.02f, -8.6f, -14f), -90f, false);
        Prop("modular_industrial_pipes_01", lab, new Vector3(-0.02f, -8.6f, -3.6f), -90f, false);
        Prop("plastic_crate_01", lab, new Vector3(-1.5f, -10f, -14.2f), 30f);

        // Air pocket: a working lamp above the water. Relief has a colour, and it is not red.
        Prop("metal_office_desk", lab, new Vector3(3.4f, -10f, -11.35f), 0f);
        Floating(Prop("metal_stool_01", lab, new Vector3(2.2f, AirPocketLevel - 0.55f, -8.6f), Quaternion.Euler(80f, 0f, 20f)));
        Floating(Prop("plastic_crate_02", lab, new Vector3(4.2f, AirPocketLevel - 0.1f, -8.1f), Quaternion.Euler(0f, 25f, 0f)));
        Prop("industrial_wall_lamp", lab, new Vector3(3f, -6.95f, -7.01f), 180f, false);
        Light pocketLamp = PointOrSpot("PocketLamp", lab, new Vector3(3f, -6.95f, -7.25f), LightType.Point, new Color(1f, 0.86f, 0.62f), 1.3f, 6f);
        pocketLamp.gameObject.AddComponent<FlickerLight>();

        // The tank room: three tanks, the third one broken open from inside.
        Transform tanks = Group("Tanks", lab);
        Transform watcher = null;
        // Loc keys (msg.tank1..3): "Tanque 1. Água turva e algo mole encostado no vidro." and so on.
        string[] messages = { "msg.tank1", "msg.tank2", "msg.tank3" };
        // Meshy tanks (Art/Models/Tank, made by SourceAssets/Tools/tank_process.py + tank_textures.py): one
        // frame with three baked rust variants (light, medium, heavy flaking; tank 3, the broken one, is the
        // oldest), a clean glass tube and, for tank 3, that tube burst open from inside with shards on the
        // floor. Models are in Meshy units (1.9 m tall); TankScale makes a tank 2.66 m under the 3 m ceiling.
        const float TankScale = 1.4f;
        Material specimenSkin = ModelMaterial("Tank_Specimen", ProjectPaths.TankModels + "/Tank_Specimen", false, 0.6f);
        for (int i = 0; i < 3; i++)
        {
            Vector3 at = new Vector3(-2.7f + i * 1.7f, -10f, -22.4f);
            Transform tank = Group("Tank" + (i + 1), tanks);
            tank.position = at;
            Material frameMat = ModelMaterial("Tank_Frame_Rust" + (i + 1), ProjectPaths.TankModels + "/Tank_Frame_Rust" + (i + 1), true, 1f);
            TankModel("Tank_Frame", tank, at, 0f, TankScale, frameMat);
            TankModel(i < 2 ? "Tank_Glass" : "Tank_GlassBroken", tank, at, 0f, TankScale, i < 2 ? tankGlass : tankGlassBroken);
            if (i < 2)
            {
                // The specimen hangs in the liquid from a pivot whose +Z is its face (LabLightSwitch turns
                // the pivot). Tank 1 starts facing the back wall, so its turn toward the player reads.
                Transform pivot = Group("SpecimenPivot", tank);
                pivot.position = at + Vector3.up * (1.5f - i * 0.15f);
                pivot.rotation = Quaternion.Euler(i == 0 ? 0f : 12f, i == 0 ? 190f : 140f, i == 0 ? 0f : -8f);
                float size = i == 0 ? 0.52f : 0.38f;
                GameObject body = TankModel("Tank_Specimen", pivot, pivot.position - pivot.up * (0.95f * size), 0f, size, specimenSkin);
                body.transform.rotation = pivot.rotation * body.transform.rotation;
                if (i == 0)
                {
                    watcher = pivot;
                }

                PointOrSpot("Glow", tank, at + Vector3.up * 1.5f, LightType.Point, new Color(0.3f, 0.9f, 0.5f), 0.7f, 2.6f);
            }

            BoxCollider shell = tank.gameObject.AddComponent<BoxCollider>();
            shell.center = Vector3.up * 1.33f;
            shell.size = new Vector3(1.3f, 2.66f, 1.3f);

            InspectableObject inspect = tank.gameObject.AddComponent<InspectableObject>();
            inspect.SetPrompt("prompt.tank" + (i + 1));
            SetField(inspect, "message", messages[i]);
        }

        // Hot vents: bubbles, a dull orange glow, and air burning faster nearby.
        foreach (Vector3 at in new[] { new Vector3(1.2f, -10f, -19.3f), new Vector3(-3.2f, -10f, -20.6f) })
        {
            Transform vent = Group("HotVent", lab);
            vent.position = at;
            Box("Grate", vent, at + new Vector3(-0.45f, 0f, -0.45f), at + new Vector3(0.45f, 0.04f, 0.45f), ventGrate, false);
            vent.gameObject.AddComponent<HotVent>().Configure(2.2f);
            Light ventGlow = PointOrSpot("Glow", vent, at + Vector3.up * 0.3f, LightType.Point, new Color(1f, 0.38f, 0.12f), 0.8f, 3.2f);
            Volumetric(ventGlow, 0.6f, 0.1f);
            VentBubbles(vent, at);
        }

        Prop("metal_office_desk", lab, new Vector3(-3.5f, -10f, -20.6f), 90f);

        // The real Alex's dive slate on the desk: white plastic in pencil (paper would have dissolved). His
        // notes trail off mid-sentence, the way he left everything at the end (Dayse's decision, 30/09),
        // unlike the creature's fluent recordings upstairs. Optional, for whoever dives this deep.
        Transform journal = Group("LabJournal", lab);
        journal.position = new Vector3(-3.35f, -9.19f, -20.4f);
        // Meshy slate with its pencil on a cord (game-3d-assets: 2.5k triangles), lying face up.
        journal.rotation = Quaternion.Euler(0f, 20f, 0f);
        ModelProp("DiveSlate", journal, new Vector3(-3.35f, -9.2f, -20.4f), journal.rotation * Quaternion.Euler(-90f, 0f, 0f), false);
        BoxCollider journalReach = journal.gameObject.AddComponent<BoxCollider>();
        journalReach.isTrigger = true;
        journalReach.size = new Vector3(0.45f, 0.15f, 0.45f);
        labJournal = journal.gameObject.AddComponent<ReadableNote>();
        labJournal.SetPrompt("prompt.slate");
        // story.slate.body: "O 3 NÃO É SEDIMENTO. CRESCE COM O CALOR. VIRA PARA A LUZ. / SEPAREI UM FRAGMENTO PARA O
        // ERICK. CAIXA LACRADA, ELE VAI SABER O QUE FAZER COM / OUVI A MINHA VOZ NO CORREDOR ONTEM. NÃO ERA"
        labJournal.SetText("story.slate.body", "A.", true);
        labJournal.SetArchive("lousa_alex", "story.slate.title", PlayerItems.EntryKind.Note);
        Prop("metal_stool_01", lab, new Vector3(-2.4f, -10f, -19.3f), Quaternion.Euler(0f, 30f, 88f));
        return watcher;
    }

    // Fluorescent tubes over the tanks and the switch by the door: the light the note forbids.
    private static void BuildLabLights(Transform watcher, DreadDirector dread)
    {
        Transform room = Group("TankRoomLights");
        Material tubeMat = Material("FluorescentTube", Shader.Find("Universal Render Pipeline/Lit"));
        tubeMat.SetColor("_BaseColor", new Color(0.8f, 0.82f, 0.8f));
        tubeMat.EnableKeyword("_EMISSION");
        tubeMat.SetColor("_EmissionColor", Color.black);
        tubeMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(tubeMat);

        Light[] lights = new Light[2];
        Renderer[] tubes = new Renderer[2];
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -2.2f : 0.4f;
            Box("Fixture" + i, room, new Vector3(x - 0.7f, -7.1f, -21.6f), new Vector3(x + 0.7f, -7f, -21.3f), rustyMetal, false);
            GameObject tube = Box("Tube" + i, room, new Vector3(x - 0.62f, -7.16f, -21.5f), new Vector3(x + 0.62f, -7.1f, -21.4f), tubeMat, false);
            tubes[i] = tube.GetComponent<Renderer>();
            lights[i] = PointOrSpot("TubeLight" + i, room, new Vector3(x, -7.35f, -21.45f), LightType.Point, new Color(0.85f, 0.95f, 1f), 5f, 9f);
            Volumetric(lights[i], 0.8f, 0.3f);
        }

        Transform box = Group("LightSwitch", room);
        box.position = new Vector3(-3.97f, -8.5f, -18.7f);
        Box("Plate", box, new Vector3(-4f, -8.7f, -18.82f), new Vector3(-3.93f, -8.35f, -18.58f), rustyMetal);
        Box("Lever", box, new Vector3(-3.93f, -8.6f, -18.72f), new Vector3(-3.86f, -8.45f, -18.68f), metal, false);
        Box("Conduit", box, new Vector3(-3.99f, -8.35f, -18.72f), new Vector3(-3.95f, -7f, -18.68f), metal, false);

        GameObject sparkObject = new GameObject("Sparks");
        sparkObject.transform.SetParent(box, false);
        sparkObject.transform.position = new Vector3(-3.9f, -8.5f, -18.7f);
        ParticleSystem sparks = sparkObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = sparks.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startColor = new Color(1f, 0.85f, 0.5f);
        main.gravityModifier = 0.3f;
        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40), new ParticleSystem.Burst(0.25f, 20) });
        sparkObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.Vfx + "/Spark.mat");

        AudioSource roomAudio = box.gameObject.AddComponent<AudioSource>();
        roomAudio.spatialBlend = 1f;
        roomAudio.maxDistance = 20f;
        roomAudio.playOnAwake = false;

        GameObject far = new GameObject("DistantVoice");
        far.transform.SetParent(room, false);
        far.transform.position = new Vector3(-1f, -8.8f, -5f);
        AudioSource distant = far.AddComponent<AudioSource>();
        distant.spatialBlend = 1f;
        distant.minDistance = 4f;
        distant.maxDistance = 40f;
        distant.playOnAwake = false;

        LabLightSwitch lightSwitch = box.gameObject.AddComponent<LabLightSwitch>();
        BoxCollider reach = box.gameObject.AddComponent<BoxCollider>();
        reach.isTrigger = true;
        reach.center = Vector3.zero;
        reach.size = new Vector3(0.3f, 0.5f, 0.4f);
        SetField(lightSwitch, "tubeLights", lights);
        SetField(lightSwitch, "tubeRenderers", tubes);
        SetField(lightSwitch, "sparks", sparks);
        SetField(lightSwitch, "roomAudio", roomAudio);
        SetField(lightSwitch, "clickClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Switch.ogg"));
        SetField(lightSwitch, "watcher", watcher);
        SetField(lightSwitch, "distantVoice", distant);
        SetField(lightSwitch, "groan", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Creature/roar_02.ogg"));
        SetField(lightSwitch, "dread", dread);
    }

    private static void VentBubbles(Transform parent, Vector3 at)
    {
        GameObject go = new GameObject("Bubbles");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
        main.startColor = new Color(0.85f, 0.95f, 0.95f, 0.65f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 45f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.7f, 0.05f, 0.7f);
        ParticleSystem.VelocityOverLifetimeModule rise = ps.velocityOverLifetime;
        rise.enabled = true;
        rise.space = ParticleSystemSimulationSpace.World;
        rise.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        rise.y = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
        rise.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        ParticleSystem.NoiseModule wobble = ps.noise;
        wobble.enabled = true;
        wobble.strength = 0.3f;
        wobble.frequency = 1.2f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particle;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // A long dark body that crosses a spot once, fast (placeholder: no model, no AI). Used twice: the
    // corridor junction on the way back from the lab (GDD step 6) and outside the sunken bedroom's
    // window when the player takes the vial.
    // One of the tank models, keeping the FBX root's axis conversion (Blender z-up to Unity y-up).
    private static GameObject TankModel(string file, Transform parent, Vector3 position, float yaw, float scale, Material material)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.TankModels + "/" + file + ".fbx");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        model.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation);
        model.transform.localScale = asset.transform.localScale * scale;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = material;
        }

        return model;
    }

    // URP Lit from a processed model's texture set: prefix + _BaseColor, _Normal (optional), _MetallicSmoothness.
    private static Material ModelMaterial(string name, string prefix, bool normal, float smoothness)
    {
        Material m = Material(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        if (normal)
        {
            // Every rust variant shares the frame's baked normal map (the bolts of the 844k-triangle original).
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.TankModels + "/Tank_Frame_Normal.png"));
            m.EnableKeyword("_NORMALMAP");
        }

        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_MetallicSmoothness.png"));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static readonly Quaternion PieceAxes = Quaternion.Inverse(Quaternion.LookRotation(Vector3.down, Vector3.forward));

    // One link of the creature chain. scale multiplies the mesh along its own axes (x width, y length,
    // z height); forwardOffset slides the mesh along the link so neighbours overlap.
    private static Transform CreaturePiece(Transform group, string name, string part, Vector3 scale, float forwardOffset)
    {
        // The body segment came out lighter and glossier than the head; tint and gloss pull it in line.
        bool body = part == "body";
        Transform link = Group(name, group);
        string prefix = ProjectPaths.CreatureModels + "/Creature_" + part;
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(prefix + ".fbx");
        Material skin = Material("Creature_" + part, Shader.Find("Universal Render Pipeline/Lit"));
        skin.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_BaseColor.png"));
        skin.SetColor("_BaseColor", body ? new Color(0.62f, 0.64f, 0.66f) : Color.white);
        skin.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Normal.png"));
        skin.EnableKeyword("_NORMALMAP");
        skin.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_MetallicSmoothness.png"));
        skin.EnableKeyword("_METALLICSPECGLOSSMAP");
        skin.SetFloat("_Smoothness", body ? 0.55f : 0.8f);
        EditorUtility.SetDirty(skin);

        GameObject piece = new GameObject("Mesh");
        piece.transform.SetParent(link, false);
        piece.transform.localPosition = new Vector3(0f, 0f, forwardOffset);
        piece.transform.localRotation = PieceAxes;
        piece.transform.localScale = scale;
        piece.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = skin;
        return link;
    }

    // Meshy model in three pieces (Art/Models/Creature, see CREDITS): head, one body segment reused five
    // times with a taper, tail. Each piece hangs under a chain link that CreaturePass / CreatureChase moves,
    // so the body snakes. The meshes keep Blender's axes (mouth toward -Y, back toward +Z); PieceAxes turns
    // that into the link's forward +Z / up +Y. A tentacle is the same chain without the head, thinner and
    // tapering to the tail tip (the arm seen at the window).
    private static Object[] CreatureChain(Transform group, float size, bool tentacle)
    {
        const int bodyCount = 5;
        List<Object> segments = new List<Object>();
        // Scales per mesh axis (width, length, height). Pieces are 1.9 m long raw: head 1.5 m, body links
        // 1.25 m (0.45 m overlap at 0.8 m spacing hides the cut ends), tail 1.4 m; widths chosen so neck,
        // body and tail meet (first preview: body wider than the neck read as a row of cans).
        if (!tentacle)
        {
            segments.Add(CreaturePiece(group, "Head", "head", new Vector3(0.79f, 0.79f, 0.79f) * size, 0.35f * size));
        }

        for (int i = 0; i < bodyCount; i++)
        {
            float taper = tentacle ? Mathf.Lerp(0.55f, 0.95f, i / (float)(bodyCount - 1)) : Mathf.Lerp(1f, 0.72f, i / (float)(bodyCount - 1));
            segments.Add(CreaturePiece(group, "Body" + i, "body", new Vector3(0.98f * taper, 0.66f, 0.82f * taper) * size, 0f));
        }

        if (tentacle)
        {
            // The tip leads: the chain is moved head-first, so the thin tail piece goes in front, reversed.
            Transform tip = CreaturePiece(group, "Tip", "tail", new Vector3(0.6f, 0.74f, 0.6f) * size, 0.3f * size);
            tip.GetChild(0).localRotation = Quaternion.Euler(0f, 180f, 0f) * tip.GetChild(0).localRotation;
            segments.Insert(0, tip);
        }
        else
        {
            segments.Add(CreaturePiece(group, "Tail", "tail", new Vector3(0.9f, 0.74f, 0.9f) * size, -0.3f * size));
        }

        return segments.ToArray();
    }

    private static CreaturePass BuildCreature(string name, DreadDirector dread, Vector3 start, Vector3 end, float duration, float size, string groanClip, bool tentacle = false)
    {
        Transform group = Group(name);
        float spacing = 0.8f * size;
        Object[] segments = CreatureChain(group, size, tentacle);

        AudioSource voice = group.gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 3f;
        voice.maxDistance = 30f;
        CreaturePass pass = group.gameObject.AddComponent<CreaturePass>();
        SetField(pass, "segments", segments);
        SetField(pass, "startPoint", start);
        SetField(pass, "endPoint", end);
        SetField(pass, "duration", duration);
        SetField(pass, "segmentSpacing", spacing);
        SetField(pass, "voice", voice);
        SetField(pass, "groan", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Creature/" + groanClip));
        SetField(pass, "dread", dread);
        return pass;
    }
}
