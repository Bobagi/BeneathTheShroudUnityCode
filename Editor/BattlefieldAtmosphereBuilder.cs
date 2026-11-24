using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Mood of the battlefield: overcast dusk sky, a low sun behind the enemy line (so the colossus
// reads as a silhouette), heavy fog, graded post-processing, ground mist, the creature and its
// path, distant artillery and the soundscape. Re-runnable.
public static class BattlefieldAtmosphereBuilder
{
    private const string AudioFolder = ProjectPaths.Audio;
    private const string SystemsName = "Battlefield_Systems";

    [MenuItem("Horror/Battlefield/4 Atmosphere")]
    public static void Build()
    {
        ConfigureLighting();
        ConfigureVolume();
        ConfigureCamera();

        GameObject existing = GameObject.Find(SystemsName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        Transform systems = new GameObject(SystemsName).transform;
        BuildCreature(systems);
        BuildArtillery(systems);
        BuildSoundscape(systems);
        BuildBoundary(systems);
        BuildDread(systems);
        BuildGroundMist();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(systems.gameObject.scene);
        Debug.Log("[Battlefield] Atmosphere built.");
    }

    private static AudioClip Clip(string relative) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{relative}");

    private static AudioClip[] Clips(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { $"{AudioFolder}/{folder}" });
        AudioClip[] clips = new AudioClip[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[i]));
        }

        return clips;
    }

    private static void ConfigureLighting()
    {
        string skyPath = ProjectPaths.SkyMaterials + "/WW1_Sky.mat";
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.AllSkyFree + "/Overcast Low/AllSky_Overcast4_Low.mat");
            sky = new Material(source);
            AssetDatabase.CreateAsset(sky, skyPath);
        }

        // Night after the barrage. Contrast, not darkness: almost no fill light, a faint cold moon,
        // and bright warm practicals (lanterns, braziers, fires, flares) carving pools out of the dark.
        // The sky stays a notch brighter than the land so silhouettes read against it.
        sky.SetColor("_Tint", new Color(0.36f, 0.37f, 0.42f));
        sky.SetFloat("_Exposure", 0.34f);
        sky.SetFloat("_Rotation", 190f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.1f, 0.11f, 0.14f);
        RenderSettings.ambientEquatorColor = new Color(0.06f, 0.062f, 0.07f);
        RenderSettings.ambientGroundColor = new Color(0.015f, 0.014f, 0.013f);
        RenderSettings.reflectionIntensity = 0.3f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.085f, 0.09f, 0.105f);
        RenderSettings.fogDensity = 0.0026f;

        GameObject sunObject = GameObject.Find("SunLight");
        Light sun = sunObject.GetComponent<Light>();
        // Moonlight from behind the enemy line: rims the parapets and backlights the colossus.
        sunObject.transform.rotation = Quaternion.Euler(24f, 200f, 0f);
        sun.color = new Color(0.58f, 0.66f, 0.85f);
        sun.intensity = 0.22f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.9f;
        RenderSettings.sun = sun;
    }

    private static void ConfigureVolume()
    {
        string profilePath = ProjectPaths.RenderingSettings + "/WW1_Volume.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        Tonemapping tonemapping = Get<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = Get<ColorAdjustments>(profile);
        color.postExposure.Override(0.55f);
        color.contrast.Override(28f);
        color.saturation.Override(-28f);
        color.colorFilter.Override(Color.white);

        // Cold shadows against warm highlights sharpens the light/dark split.
        ShadowsMidtonesHighlights split = Get<ShadowsMidtonesHighlights>(profile);
        split.shadows.Override(new Vector4(0.88f, 0.95f, 1.12f, -0.08f));
        split.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
        split.highlights.Override(new Vector4(1.12f, 1.02f, 0.88f, 0.05f));
        split.shadowsStart.Override(0f);
        split.shadowsEnd.Override(0.25f);

        Vignette vignette = Get<Vignette>(profile);
        vignette.intensity.Override(0.36f);
        vignette.smoothness.Override(0.42f);
        vignette.color.Override(Color.black);

        FilmGrain grain = Get<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.3f);
        grain.response.Override(0.8f);

        Bloom bloom = Get<Bloom>(profile);
        bloom.intensity.Override(0.55f);
        bloom.threshold.Override(1.0f);
        bloom.scatter.Override(0.62f);
        bloom.tint.Override(new Color(1f, 0.82f, 0.62f));

        ChromaticAberration aberration = Get<ChromaticAberration>(profile);
        aberration.intensity.Override(0.1f);

        EditorUtility.SetDirty(profile);

        GameObject volumeObject = GameObject.Find("Global Volume");
        Volume volume = volumeObject.GetComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
    }

    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        component.active = true;
        return component;
    }

    private static void ConfigureCamera()
    {
        Camera main = Camera.main;
        main.farClipPlane = 3000f;
        main.nearClipPlane = 0.08f;
        UniversalAdditionalCameraData data = main.GetComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        EditorUtility.SetDirty(main);
        EditorUtility.SetDirty(data);

        // Cinemachine drives the real camera's clip planes from its lens: the horizon (and the
        // colossus ~700 m away) must fit inside it.
        Unity.Cinemachine.CinemachineCamera follow =
            GameObject.Find("PlayerFollowCamera").GetComponent<Unity.Cinemachine.CinemachineCamera>();
        Unity.Cinemachine.LensSettings lens = follow.Lens;
        lens.FarClipPlane = 3000f;
        lens.NearClipPlane = 0.08f;
        follow.Lens = lens;
        EditorUtility.SetDirty(follow);
    }

    private static float Ground(float x, float z)
    {
        Terrain outer = GameObject.Find("Battlefield_Outer").GetComponent<Terrain>();
        return outer.SampleHeight(new Vector3(x, 0f, z)) + outer.transform.position.y;
    }

    private static void BuildCreature(Transform systems)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.Models + "/Creature_Colossus_v2.fbx");
        GameObject creature = (GameObject)PrefabUtility.InstantiatePrefab(asset, systems);
        creature.name = "Colossus";
        Vector3 start = new Vector3(-140f, Ground(-140f, 720f) - 4f, 720f);
        creature.transform.position = start;
        creature.transform.rotation = Quaternion.LookRotation(new Vector3(140f, 0f, -720f).normalized, Vector3.up);

        string silhouettePath = ProjectPaths.EnvironmentMaterials + "/ColossusSilhouette.mat";
        Material silhouette = AssetDatabase.LoadAssetAtPath<Material>(silhouettePath);
        if (silhouette == null)
        {
            silhouette = new Material(Shader.Find("Horror/Silhouette"));
            AssetDatabase.CreateAsset(silhouette, silhouettePath);
        }

        silhouette.SetColor("_BaseColor", new Color(0.012f, 0.012f, 0.016f));
        silhouette.SetFloat("_FogStrength", 0.45f);
        silhouette.SetColor("_RimColor", new Color(0.35f, 0.38f, 0.48f));
        EditorUtility.SetDirty(silhouette);

        foreach (Renderer renderer in creature.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = silhouette;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.updateWhenOffscreen = true;
            }
        }

        Animation animation = creature.GetComponent<Animation>();
        if (animation != null)
        {
            animation.playAutomatically = true;
            animation.wrapMode = WrapMode.Loop;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
        }

        Transform path = new GameObject("ColossusPath").transform;
        path.SetParent(systems, false);
        Vector2[] points = { new Vector2(260f, 680f), new Vector2(120f, 980f), new Vector2(-420f, 820f), new Vector2(-140f, 720f) };
        Object[] waypoints = new Object[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Transform waypoint = new GameObject("Waypoint" + i).transform;
            waypoint.SetParent(path, false);
            waypoint.position = new Vector3(points[i].x, Ground(points[i].x, points[i].y), points[i].y);
            waypoints[i] = waypoint;
        }

        AudioSource voice = creature.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0.55f;
        voice.minDistance = 900f;
        voice.maxDistance = 3000f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.dopplerLevel = 0f;
        voice.volume = 0.9f;

        ColossusCreature behaviour = creature.AddComponent<ColossusCreature>();
        PrototypeItemBuilder.SetField(behaviour, "player", GameObject.Find("PlayerArmature").transform);
        PrototypeItemBuilder.SetField(behaviour, "waypoints", waypoints);
        PrototypeItemBuilder.SetField(behaviour, "head", PrototypeItemBuilder.FindDeep(creature.transform, "Head"));
        PrototypeItemBuilder.SetField(behaviour, "voice", voice);
        PrototypeItemBuilder.SetField(behaviour, "groanClips", Clips("Creature"));
    }

    private static void BuildArtillery(Transform systems)
    {
        GameObject go = new GameObject("DistantArtillery");
        go.transform.SetParent(systems, false);
        DistantArtillery artillery = go.AddComponent<DistantArtillery>();
        GameObject explosion = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.Vfx + "/ShellExplosion.prefab");
        PrototypeItemBuilder.SetField(artillery, "explosionPrefab", explosion.GetComponent<ParticleSystem>());
        PrototypeItemBuilder.SetField(artillery, "explosionClips", Clips("Artillery"));
        PrototypeItemBuilder.SetField(artillery, "flareLaunchClip", Clip("Artillery/cannon_fire_0.ogg"));
    }

    private static void BuildSoundscape(Transform systems)
    {
        GameObject go = new GameObject("Soundscape");
        go.transform.SetParent(systems, false);

        AudioSource music = go.AddComponent<AudioSource>();
        music.clip = Clip("Music/Music_Lost.ogg");
        music.loop = true;
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.volume = 0.32f;

        AudioSource wind = go.AddComponent<AudioSource>();
        wind.clip = Clip("Ambience/Wind_Loop.wav");
        wind.loop = true;
        wind.playOnAwake = false;
        wind.spatialBlend = 0f;
        wind.volume = 0.45f;

        AudioSource rumble = go.AddComponent<AudioSource>();
        rumble.clip = Clip("Ambience/Rumble_Loop.mp3");
        rumble.loop = true;
        rumble.playOnAwake = true;
        rumble.spatialBlend = 0f;
        rumble.volume = 0.25f;

        AmbientSoundscape soundscape = go.AddComponent<AmbientSoundscape>();
        PrototypeItemBuilder.SetField(soundscape, "musicSource", music);
        PrototypeItemBuilder.SetField(soundscape, "windSource", wind);

        SerializedObject serialized = new SerializedObject(soundscape);
        SerializedProperty layers = serialized.FindProperty("randomLayers");
        layers.arraySize = 3;
        SetLayer(layers.GetArrayElementAtIndex(0), "Crows",
            new[] { Clip("Ambience/crow_caw.wav"), Clip("Ambience/crow_0.ogg") }, new Vector2(18f, 45f), new Vector2(0.35f, 0.6f), new Vector2(0.9f, 1.05f), 45f);
        SetLayer(layers.GetArrayElementAtIndex(1), "DistantBattle",
            new[] { Clip("Ambience/Dragout_-_War_sounds_cc0.ogg") }, new Vector2(35f, 80f), new Vector2(0.25f, 0.4f), new Vector2(0.75f, 0.9f), 250f);
        SetLayer(layers.GetArrayElementAtIndex(2), "Creaks",
            new[] { Clip("Ambience/tree_creak_0.ogg") }, new Vector2(25f, 60f), new Vector2(0.4f, 0.7f), new Vector2(0.8f, 1f), 20f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayer(SerializedProperty layer, string label, AudioClip[] clips, Vector2 between, Vector2 volume, Vector2 pitch, float distance)
    {
        layer.FindPropertyRelative("label").stringValue = label;
        SerializedProperty clipArray = layer.FindPropertyRelative("clips");
        clipArray.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            clipArray.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        layer.FindPropertyRelative("secondsBetween").vector2Value = between;
        layer.FindPropertyRelative("volume").vector2Value = volume;
        layer.FindPropertyRelative("pitch").vector2Value = pitch;
        layer.FindPropertyRelative("distance").floatValue = distance;
    }

    private static void BuildDread(Transform systems)
    {
        GameObject go = new GameObject("DreadDirector");
        go.transform.SetParent(systems, false);
        AudioSource heart = go.AddComponent<AudioSource>();
        heart.playOnAwake = false;
        heart.spatialBlend = 0f;
        DreadDirector dread = go.AddComponent<DreadDirector>();
        PrototypeItemBuilder.SetField(dread, "colossus", Object.FindAnyObjectByType<ColossusCreature>());
        PrototypeItemBuilder.SetField(dread, "viewCamera", Camera.main);
        PrototypeItemBuilder.SetField(dread, "volume", GameObject.Find("Global Volume").GetComponent<Volume>());
        PrototypeItemBuilder.SetField(dread, "followCamera",
            GameObject.Find("PlayerFollowCamera").GetComponent<Unity.Cinemachine.CinemachineCamera>());
        PrototypeItemBuilder.SetField(dread, "heartbeat", heart);
    }

    // Invisible walls at the edge of the detailed terrain keep the player inside the playable area.
    private static void BuildBoundary(Transform systems)
    {
        Transform boundary = new GameObject("PlayAreaBoundary").transform;
        boundary.SetParent(systems, false);
        float half = BattlefieldLayout.InnerSize * 0.5f - 12f;
        Vector3[] centers = { new Vector3(0f, 0f, half), new Vector3(0f, 0f, -half), new Vector3(half, 0f, 0f), new Vector3(-half, 0f, 0f) };
        Vector3[] sizes = { new Vector3(half * 2f, 60f, 1f), new Vector3(half * 2f, 60f, 1f), new Vector3(1f, 60f, half * 2f), new Vector3(1f, 60f, half * 2f) };
        for (int i = 0; i < centers.Length; i++)
        {
            GameObject wall = new GameObject("Wall" + i);
            wall.transform.SetParent(boundary, false);
            wall.transform.position = centers[i];
            wall.AddComponent<BoxCollider>().size = sizes[i];
        }
    }

    private static void BuildGroundMist()
    {
        Material mistMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.Shaders + "/GroundMist.mat");
        if (mistMaterial != null)
        {
            // The full-screen mist is not lit, so its colour tracks the night ambient.
            mistMaterial.SetColor("_MistColor", new Color(0.1f, 0.105f, 0.12f));
            mistMaterial.SetFloat("_Density", 0.07f);
            mistMaterial.SetFloat("_BaseHeight", -0.8f);
            mistMaterial.SetFloat("_HeightFalloff", 0.85f);
            mistMaterial.SetFloat("_LightGain", 1f);
            EditorUtility.SetDirty(mistMaterial);
        }

        GameObject player = GameObject.Find("PlayerArmature");
        Transform old = player.transform.Find("GroundMist");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.Vfx + "/GroundMist.prefab");
        GameObject mist = (GameObject)PrefabUtility.InstantiatePrefab(asset, player.transform);
        mist.name = "GroundMist";
        mist.transform.localPosition = new Vector3(0f, 0.4f, 0f);
    }
}
