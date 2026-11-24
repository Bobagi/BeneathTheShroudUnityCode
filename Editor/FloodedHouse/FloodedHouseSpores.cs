using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Airborne spores, the bioluminescent haze and the flies (29/10, Gustavo: the glowing green dots were "padrões e
// feias, muito grandes"; "veja se faz sentido com um jogo de terror"). What the research and the GDD's art direction
// asked for, against what was there:
// - Horror games light their spores instead of making them glow: The Last of Us Part II's spore clouds are lit by the
//   flashlight and swirl round the player. Here they are thousands of tiny specks (9 to 30 mm, tuned in Play: at 4 to 14 mm they were under a pixel past 3 m; SporeSpecks.png, a
//   flipbook of grains, clumps, fibres and flakes) in URP's Particles/Simple Lit, so the lamps and the torch show them
//   and the dark hides them; soft against the geometry and faded out near the lens (nothing smears across the view).
// - The GDD: bioluminescence "azul-esverdeado, contínuo, sem pontos de neon". The glow is a faint continuous haze of
//   big soft puffs round the source, not dots.
// - The player pushes them aside as he walks through (a force field on the player; External Forces on each cloud).
// - Flies (All Nature VFX, GreenBugGames, free on the Asset Store, Assets/ThirdParty/GreenBugGames) over the
//   contaminated pool.
public static partial class FloodedHouseBuilder
{
    private static Material sporeMotes, bioHaze, sporeFog;

    private static void SporeMaterials()
    {
        // Lit specks: alpha blended, both sides, soft particles and camera fading (the URP particle shader's own
        // features; the fade parameters are what its material editor would write).
        sporeMotes = Material("SporeMotes", Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
        sporeMotes.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Art + "/VFX/Textures/SporeSpecks.png"));
        sporeMotes.SetColor("_BaseColor", new Color(0.82f, 0.86f, 0.74f, 1f));
        sporeMotes.SetFloat("_Surface", 1f);
        sporeMotes.SetFloat("_Blend", 0f);
        sporeMotes.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        sporeMotes.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        sporeMotes.SetFloat("_ZWrite", 0f);
        sporeMotes.SetFloat("_Cull", (float)CullMode.Off);
        sporeMotes.SetFloat("_Smoothness", 0.2f);
        sporeMotes.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sporeMotes.SetFloat("_SoftParticlesEnabled", 1f);
        sporeMotes.SetFloat("_SoftParticlesNearFadeDistance", 0f);
        sporeMotes.SetFloat("_SoftParticlesFarFadeDistance", 0.25f);
        sporeMotes.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.25f, 0f, 0f));
        sporeMotes.EnableKeyword("_SOFTPARTICLES_ON");
        sporeMotes.SetFloat("_CameraFadingEnabled", 1f);
        sporeMotes.SetFloat("_CameraNearFadeDistance", 0.25f);
        sporeMotes.SetFloat("_CameraFarFadeDistance", 0.9f);
        sporeMotes.SetVector("_CameraFadeParams", new Vector4(0.25f, 1f / (0.9f - 0.25f), 0f, 0f));
        sporeMotes.EnableKeyword("_FADING_ON");
        // A trace of their own light, far under the lamps (Lumina in them), so a cloud is not wholly black in the dark.
        sporeMotes.EnableKeyword("_EMISSION");
        sporeMotes.SetColor("_EmissionColor", new Color(0.14f, 0.3f, 0.25f));
        sporeMotes.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(sporeMotes);

        // The haze: soft smoke puffs (Kenney, CC0), additive and faint, soft and faded near the lens.
        bioHaze = Material("BioHaze", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        bioHaze.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Art + "/VFX/Textures/smoke_07.png"));
        bioHaze.SetColor("_BaseColor", Color.white);
        bioHaze.SetFloat("_Surface", 1f);
        bioHaze.SetFloat("_Blend", 2f);
        bioHaze.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        bioHaze.SetFloat("_DstBlend", (float)BlendMode.One);
        bioHaze.SetFloat("_ZWrite", 0f);
        bioHaze.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        bioHaze.SetFloat("_SoftParticlesEnabled", 1f);
        // Short soft fade: at 1.2 m it erased the haze in the 2.5 m wide galleries (29/10).
        bioHaze.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.3f, 0f, 0f));
        bioHaze.EnableKeyword("_SOFTPARTICLES_ON");
        bioHaze.SetFloat("_CameraFadingEnabled", 1f);
        bioHaze.SetVector("_CameraFadeParams", new Vector4(0.5f, 1f / 1f, 0f, 0f));
        bioHaze.EnableKeyword("_FADING_ON");
        bioHaze.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(bioHaze);

        // The spore wing's thick air: the same puffs, alpha blended and unlit (pale over dark, dark nowhere): an
        // additive glow vanished against the pale walls and a lit one turned black in the dim gallery (29/10).
        sporeFog = Material("SporeFog", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        sporeFog.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Art + "/VFX/Textures/smoke_07.png"));
        sporeFog.SetColor("_BaseColor", Color.white);
        sporeFog.SetFloat("_Surface", 1f);
        sporeFog.SetFloat("_Blend", 0f);
        sporeFog.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        sporeFog.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        sporeFog.SetFloat("_ZWrite", 0f);
        sporeFog.SetFloat("_Cull", (float)CullMode.Off);
        sporeFog.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        sporeFog.SetFloat("_SoftParticlesEnabled", 1f);
        sporeFog.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.3f, 0f, 0f));
        sporeFog.EnableKeyword("_SOFTPARTICLES_ON");
        sporeFog.SetFloat("_CameraFadingEnabled", 1f);
        sporeFog.SetVector("_CameraFadeParams", new Vector4(0.3f, 1f / 1f, 0f, 0f));
        sporeFog.EnableKeyword("_FADING_ON");
        sporeFog.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(sporeFog);
    }

    // A cloud of lit specks filling a box (scene size), `count` alive at once.
    private static ParticleSystem SporeMotes(Transform parent, string name, Vector3 centre, Vector3 size, int count, Color tint, float sizeScale = 1f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 30f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.009f), new Keyframe(0.8f, 0.018f), new Keyframe(1f, 0.03f)));
        main.startSizeMultiplier = sizeScale;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(tint, new Color(tint.r * 0.8f, tint.g * 0.85f, tint.b * 0.8f, tint.a * 0.7f));
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.0004f, 0.0006f);
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = count / 24f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = size;
        // Air that barely moves: slow large swirls, a fine tremble, a slow turn of each speck.
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.06f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.04f;
        noise.octaveCount = 2;
        noise.octaveMultiplier = 0.4f;
        noise.octaveScale = 4f;
        noise.damping = true;
        ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        // One frame of the atlas per speck.
        ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = 4;
        sheet.numTilesY = 4;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
        sheet.cycleCount = 1;
        // Pushed aside by the player (SporeStir on him).
        ParticleSystem.ExternalForcesModule forces = ps.externalForces;
        forces.enabled = true;
        forces.multiplier = 1f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sporeMotes;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.minParticleSize = 0.0005f;
        renderer.maxParticleSize = 0.03f * sizeScale;
        return ps;
    }

    // The bioluminescent haze: a few big faint puffs breathing round a source.
    private static ParticleSystem BioHaze(Transform parent, string name, Vector3 centre, Vector3 size, int count, Color colour, bool litFog = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 3.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = colour;
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = count / 18f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = size;
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.04f;
        noise.frequency = 0.12f;
        ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        // Breathing in and out over its life.
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0.7f, 0.65f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = litFog ? sporeFog : bioHaze;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 2f;
        return ps;
    }

    // The force field that stirs the clouds round the player (a sphere 1.1 m across his body, pushing out a little).
    private static void SporeStir(Transform player)
    {
        if (player == null)
        {
            return;
        }

        Transform old = player.Find("SporeStir");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject go = new GameObject("SporeStir");
        go.transform.SetParent(player, false);
        go.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        ParticleSystemForceField field = go.AddComponent<ParticleSystemForceField>();
        field.shape = ParticleSystemForceFieldShape.Sphere;
        field.endRange = 1.1f;
        field.gravity = new ParticleSystem.MinMaxCurve(-0.25f);
        field.gravityFocus = 0f;
        field.drag = new ParticleSystem.MinMaxCurve(0.6f);
        field.multiplyDragByParticleSize = false;
        field.multiplyDragByParticleVelocity = true;
    }

    // Flies over the contaminated pool (the pack's prefab, scaled to real flies).
    private static void PoolFlies(Transform parent, Vector3 at)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.AllNatureVfx + "/Prefabs/Fly.prefab");
        if (prefab == null)
        {
            return;
        }

        GameObject flies = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        flies.name = "PoolFlies";
        flies.transform.position = at;
        foreach (ParticleSystem ps in flies.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = ps.main;
            main.startSizeMultiplier *= FlySize;
            main.maxParticles = 18;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.scale = new Vector3(2.4f, 0.6f, 1.8f);
        }
    }

    // The pack's fly mesh at start size 1 is about this many times a real fly (measured in the builder's test).
    private const float FlySize = 0.35f;
}
