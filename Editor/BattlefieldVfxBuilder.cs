using UnityEditor;
using UnityEngine;

// Builds the particle effects of the battlefield as prefabs: smoke plumes on the horizon, ground
// mist, fires, shell explosions, the revolver muzzle flash and bullet impact dust.
public static class BattlefieldVfxBuilder
{
    private const string VfxFolder = ProjectPaths.Vfx;
    private const string TextureFolder = VfxFolder + "/Textures";

    [MenuItem("Horror/Battlefield/VFX Prefabs")]
    public static void BuildAll()
    {
        BattlefieldTerrainBuilder.EnsureFolder(VfxFolder);
        ConfigureTextures();

        Material smoke = ParticleMaterial("Smoke", "smoke_04", false);
        Material darkSmoke = ParticleMaterial("DarkSmoke", "blackSmoke04", false);
        Material mist = ParticleMaterial("Mist", "smoke_10", false);
        Material fire = ParticleMaterial("Fire", "flame_01", true);
        Material spark = ParticleMaterial("Spark", "spark_01", true);
        Material muzzle = ParticleMaterial("Muzzle", "muzzle_01", true);
        Material dirt = ParticleMaterial("Dirt", "dirt_01", false);
        Material blast = ParticleMaterial("Blast", "explosion02", true);

        SavePrefab(BuildSmokePlume(darkSmoke, smoke), "SmokePlume");
        SavePrefab(BuildGroundMist(mist), "GroundMist");
        SavePrefab(BuildFire(fire, spark, smoke), "Fire");
        SavePrefab(BuildShellExplosion(blast, dirt, darkSmoke), "ShellExplosion");
        SavePrefab(BuildMuzzleFlash(muzzle, smoke), "MuzzleFlash");
        SavePrefab(BuildImpactDust(dirt), "ImpactDust");
        AssetDatabase.SaveAssets();
        Debug.Log("[Battlefield] VFX prefabs built.");
    }

    private static void ConfigureTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp)
            {
                continue;
            }

            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    public static Material ParticleMaterial(string name, string texture, bool additive)
    {
        string path = $"{VfxFolder}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{texture}.png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive
            ? (float)UnityEngine.Rendering.BlendMode.One
            : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_SoftParticlesEnabled", 1f);
        material.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1.5f, 0f, 0f));
        material.EnableKeyword("_SOFTPARTICLES_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive)
        {
            material.EnableKeyword("_BLENDMODE_ADD");
        }
        else
        {
            material.DisableKeyword("_BLENDMODE_ADD");
        }

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SavePrefab(GameObject root, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(root, $"{VfxFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
    }

    private static ParticleSystem NewSystem(string name, Transform parent, Material material)
    {
        GameObject go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        return system;
    }

    private static Gradient Fade(Color color, float fadeIn, float fadeOutStart, float peakAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha, fadeIn),
                new GradientAlphaKey(peakAlpha, fadeOutStart),
                new GradientAlphaKey(0f, 1f),
            }
        );
        return gradient;
    }

    private static void ColorOverLife(ParticleSystem system, Gradient gradient)
    {
        ParticleSystem.ColorOverLifetimeModule colorModule = system.colorOverLifetime;
        colorModule.enabled = true;
        colorModule.color = gradient;
    }

    private static void SizeOverLife(ParticleSystem system, float start, float end)
    {
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
    }

    private static void Spin(ParticleSystem system, float degreesPerSecond)
    {
        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-degreesPerSecond * Mathf.Deg2Rad, degreesPerSecond * Mathf.Deg2Rad);
    }

    // Towering column of black smoke from a burning village far away, leaning with the wind.
    private static GameObject BuildSmokePlume(Material darkSmoke, Material smoke)
    {
        ParticleSystem column = NewSystem("SmokePlume", null, darkSmoke);
        ParticleSystem.MainModule main = column.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(55f, 75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(28f, 45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.15f), new Color(0.3f, 0.28f, 0.26f));
        main.maxParticles = 160;
        ParticleSystem.EmissionModule emission = column.emission;
        emission.rateOverTime = 1.8f;
        ParticleSystem.ShapeModule shape = column.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.VelocityOverLifetimeModule velocity = column.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        ColorOverLife(column, Fade(Color.white, 0.08f, 0.6f, 0.85f));
        SizeOverLife(column, 0.5f, 3.2f);
        Spin(column, 4f);
        ParticleSystemRenderer renderer = column.GetComponent<ParticleSystemRenderer>();
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.maxParticleSize = 4f;

        // Paler smoke curling at the base.
        ParticleSystem baseSmoke = NewSystem("BaseSmoke", column.transform, smoke);
        ParticleSystem.MainModule baseMain = baseSmoke.main;
        baseMain.loop = true;
        baseMain.prewarm = true;
        baseMain.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f);
        baseMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
        baseMain.startSize = new ParticleSystem.MinMaxCurve(18f, 30f);
        baseMain.startColor = new Color(0.35f, 0.33f, 0.31f);
        baseMain.maxParticles = 60;
        ParticleSystem.EmissionModule baseEmission = baseSmoke.emission;
        baseEmission.rateOverTime = 1.2f;
        ParticleSystem.ShapeModule baseShape = baseSmoke.shape;
        baseShape.shapeType = ParticleSystemShapeType.Sphere;
        baseShape.radius = 20f;
        ColorOverLife(baseSmoke, Fade(Color.white, 0.15f, 0.5f, 0.6f));
        SizeOverLife(baseSmoke, 0.7f, 1.8f);
        baseSmoke.GetComponent<ParticleSystemRenderer>().maxParticleSize = 4f;

        // Ember glow at the foot of the plume.
        GameObject glow = new GameObject("FireGlow");
        glow.transform.SetParent(column.transform, false);
        glow.transform.localPosition = new Vector3(0f, 8f, 0f);
        Light light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.45f, 0.18f);
        light.range = 90f;
        light.intensity = 6f;
        light.shadows = LightShadows.None;
        glow.AddComponent<FlickerLight>();
        return column.gameObject;
    }

    // Low drifting mist that follows the player so the fog feels thick and wet at ground level.
    private static GameObject BuildGroundMist(Material mist)
    {
        ParticleSystem system = NewSystem("GroundMist", null, mist);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 24f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(7f, 13f);
        main.startColor = new Color(0.62f, 0.62f, 0.58f);
        main.maxParticles = 260;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 12f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(90f, 1.5f, 90f);
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.05f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.3f);
        ColorOverLife(system, Fade(Color.white, 0.25f, 0.7f, 0.22f));
        Spin(system, 3f);
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.maxParticleSize = 3f;
        return system.gameObject;
    }

    // Burning wreck / brazier fire with sparks, smoke and a flickering light.
    private static GameObject BuildFire(Material fire, Material spark, Material smoke)
    {
        ParticleSystem flames = NewSystem("Fire", null, fire);
        ParticleSystem.MainModule main = flames.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.35f, 0.1f));
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = flames.emission;
        emission.rateOverTime = 40f;
        ParticleSystem.ShapeModule shape = flames.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.5f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        ColorOverLife(flames, Fade(Color.white, 0.1f, 0.5f, 1f));
        SizeOverLife(flames, 1f, 0.3f);
        Spin(flames, 40f);

        ParticleSystem sparks = NewSystem("Sparks", flames.transform, spark);
        ParticleSystem.MainModule sparkMain = sparks.main;
        sparkMain.loop = true;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.5f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        sparkMain.startColor = new Color(1f, 0.6f, 0.25f);
        sparkMain.maxParticles = 60;
        ParticleSystem.EmissionModule sparkEmission = sparks.emission;
        sparkEmission.rateOverTime = 8f;
        ParticleSystem.ShapeModule sparkShape = sparks.shape;
        sparkShape.shapeType = ParticleSystemShapeType.Cone;
        sparkShape.angle = 25f;
        sparkShape.radius = 0.4f;
        sparkShape.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.NoiseModule noise = sparks.noise;
        noise.enabled = true;
        noise.strength = 1.2f;
        noise.frequency = 0.8f;
        ColorOverLife(sparks, Fade(Color.white, 0.05f, 0.6f, 1f));

        ParticleSystem fireSmoke = NewSystem("Smoke", flames.transform, smoke);
        fireSmoke.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        ParticleSystem.MainModule smokeMain = fireSmoke.main;
        smokeMain.loop = true;
        smokeMain.prewarm = true;
        smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
        smokeMain.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        smokeMain.startColor = new Color(0.18f, 0.17f, 0.16f);
        smokeMain.maxParticles = 50;
        ParticleSystem.EmissionModule smokeEmission = fireSmoke.emission;
        smokeEmission.rateOverTime = 5f;
        ParticleSystem.ShapeModule smokeShape = fireSmoke.shape;
        smokeShape.shapeType = ParticleSystemShapeType.Cone;
        smokeShape.angle = 12f;
        smokeShape.radius = 0.4f;
        smokeShape.rotation = new Vector3(-90f, 0f, 0f);
        ColorOverLife(fireSmoke, Fade(Color.white, 0.1f, 0.5f, 0.7f));
        SizeOverLife(fireSmoke, 0.6f, 2.5f);
        Spin(fireSmoke, 15f);

        GameObject lightObject = new GameObject("FireLight");
        lightObject.transform.SetParent(flames.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.5f, 0.2f);
        light.range = 14f;
        light.intensity = 3f;
        light.shadows = LightShadows.Soft;
        lightObject.AddComponent<FlickerLight>();
        return flames.gameObject;
    }

    // Shell impact: fireball flash, a fountain of earth and a lingering pall of smoke.
    private static GameObject BuildShellExplosion(Material blast, Material dirt, Material darkSmoke)
    {
        ParticleSystem fireball = NewSystem("ShellExplosion", null, blast);
        ParticleSystem.MainModule main = fireball.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startColor = new Color(1f, 0.7f, 0.4f);
        ParticleSystem.EmissionModule emission = fireball.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
        ParticleSystem.ShapeModule shape = fireball.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        ColorOverLife(fireball, Fade(Color.white, 0.05f, 0.3f, 1f));
        SizeOverLife(fireball, 0.6f, 1.4f);

        ParticleSystem earth = NewSystem("Earth", fireball.transform, dirt);
        ParticleSystem.MainModule earthMain = earth.main;
        earthMain.loop = false;
        earthMain.duration = 1f;
        earthMain.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.2f);
        earthMain.startSpeed = new ParticleSystem.MinMaxCurve(14f, 30f);
        earthMain.startSize = new ParticleSystem.MinMaxCurve(2f, 5f);
        earthMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.16f, 0.12f), new Color(0.32f, 0.26f, 0.2f));
        earthMain.gravityModifier = 1.4f;
        ParticleSystem.EmissionModule earthEmission = earth.emission;
        earthEmission.rateOverTime = 0f;
        earthEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });
        ParticleSystem.ShapeModule earthShape = earth.shape;
        earthShape.shapeType = ParticleSystemShapeType.Cone;
        earthShape.angle = 28f;
        earthShape.radius = 1.5f;
        earthShape.rotation = new Vector3(-90f, 0f, 0f);
        ColorOverLife(earth, Fade(Color.white, 0.02f, 0.7f, 1f));
        Spin(earth, 90f);

        ParticleSystem pall = NewSystem("Smoke", fireball.transform, darkSmoke);
        ParticleSystem.MainModule pallMain = pall.main;
        pallMain.loop = false;
        pallMain.duration = 1.5f;
        pallMain.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
        pallMain.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        pallMain.startSize = new ParticleSystem.MinMaxCurve(10f, 18f);
        pallMain.startColor = new Color(0.25f, 0.23f, 0.21f);
        ParticleSystem.EmissionModule pallEmission = pall.emission;
        pallEmission.rateOverTime = 0f;
        pallEmission.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 14) });
        ParticleSystem.ShapeModule pallShape = pall.shape;
        pallShape.shapeType = ParticleSystemShapeType.Hemisphere;
        pallShape.radius = 4f;
        pallShape.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.LimitVelocityOverLifetimeModule limit = pall.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 1f;
        limit.dampen = 0.2f;
        ColorOverLife(pall, Fade(Color.white, 0.05f, 0.4f, 0.8f));
        SizeOverLife(pall, 0.6f, 2f);
        Spin(pall, 8f);
        return fireball.gameObject;
    }

    private static GameObject BuildMuzzleFlash(Material muzzle, Material smoke)
    {
        ParticleSystem flash = NewSystem("MuzzleFlash", null, muzzle);
        ParticleSystem.MainModule main = flash.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.startLifetime = 0.05f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        main.startColor = new Color(1f, 0.8f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = flash.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
        ParticleSystem.ShapeModule shape = flash.shape;
        shape.enabled = false;

        ParticleSystem puff = NewSystem("Puff", flash.transform, smoke);
        ParticleSystem.MainModule puffMain = puff.main;
        puffMain.loop = false;
        puffMain.playOnAwake = false;
        puffMain.duration = 0.2f;
        puffMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        puffMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        puffMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        puffMain.startColor = new Color(0.7f, 0.68f, 0.64f);
        ParticleSystem.EmissionModule puffEmission = puff.emission;
        puffEmission.rateOverTime = 0f;
        puffEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5) });
        ParticleSystem.ShapeModule puffShape = puff.shape;
        puffShape.shapeType = ParticleSystemShapeType.Cone;
        puffShape.angle = 15f;
        puffShape.radius = 0.01f;
        ColorOverLife(puff, Fade(Color.white, 0.05f, 0.3f, 0.5f));
        SizeOverLife(puff, 0.5f, 3f);
        return flash.gameObject;
    }

    private static GameObject BuildImpactDust(Material dirt)
    {
        ParticleSystem dust = NewSystem("ImpactDust", null, dirt);
        ParticleSystem.MainModule main = dust.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startColor = new Color(0.3f, 0.25f, 0.2f);
        main.gravityModifier = 0.8f;
        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.05f;
        ColorOverLife(dust, Fade(Color.white, 0.02f, 0.5f, 0.9f));
        SizeOverLife(dust, 0.6f, 1.8f);
        return dust.gameObject;
    }
}
