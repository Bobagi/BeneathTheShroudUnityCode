using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Everything a shot leaves in the world, built as assets and put on a Revolver (Gustavo, 07/10):
//   Decals (URP Decal Projector, screen-space technique, which works with Forward+): bullet hole, three blood stains,
//     three runs of drips. Textures in Art/VFX/Textures/Decals, made from CC0 sources (CREDITS.md) by
//     the steps in the comment of each; materials in Art/VFX/Combat/Decals.
//   Particles: a smoke puff off walls (Kenney white puff), blood mist (Kenney smoke tinted), a muzzle star and its
//     sparks (Kenney muzzle and spark, additive), the spurt's stretched droplets (blood_droplet).
//   On the creature: four high-velocity spatter decals for the wound (BloodSplat_*). The droplet and the spatters are
//     drawn by SourceAssets/Tools/blood_textures.py.
//   The spent case: the .357 Magnum rimmed brass case (33 mm long, rim 9.8 mm), turned from its profile into
//     a 224-triangle mesh (Art/Models/Props/SpentCase), brass material, rigidbody and a bouncy physics material,
//     saved as Prefabs/Combat/SpentCase.prefab.
// Menu "Horror > Combate > Efeitos de tiro na cena aberta" applies it to the open scene; CombatTestSetup calls Apply.
public static class CombatEffectsSetup
{
    private const string Folder = ProjectPaths.Vfx + "/Combat";
    private const string DecalFolder = Folder + "/Decals";
    private const string DecalTextures = ProjectPaths.Vfx + "/Textures/Decals";
    private const string CaseFolder = ProjectPaths.PropModels + "/SpentCase";
    private const string CasePrefab = ProjectPaths.Root + "/Prefabs/Combat/SpentCase.prefab";

    [MenuItem("Horror/Combate/Efeitos de tiro na cena aberta")]
    public static void ApplyToOpenScene()
    {
        EnsureDecalFeature();
        foreach (Revolver revolver in Object.FindObjectsByType<Revolver>(FindObjectsInactive.Include))
        {
            Apply(revolver);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    public static void Apply(Revolver revolver)
    {
        SerializedObject so = new SerializedObject(revolver);
        Transform muzzle = so.FindProperty("muzzle").objectReferenceValue as Transform;
        if (muzzle != null)
        {
            so.FindProperty("flashSprite").objectReferenceValue = MuzzleStar(muzzle);
            so.FindProperty("flashSparks").objectReferenceValue = MuzzleSparks(muzzle);
            so.FindProperty("flashFire").objectReferenceValue = MuzzleFire(muzzle);
        }

        // The cylinder gap: gas and flame out to both sides where the cylinder meets the barrel.
        Transform cylinder = so.FindProperty("cylinder").objectReferenceValue as Transform;
        Transform model = so.FindProperty("model").objectReferenceValue as Transform;
        if (cylinder != null && model != null && muzzle != null)
        {
            Vector3 front = model.InverseTransformPoint(cylinder.position);
            front.z += 0.028f;
            SerializedProperty gaps = so.FindProperty("gapFlash");
            gaps.arraySize = 2;
            gaps.GetArrayElementAtIndex(0).objectReferenceValue = GapFlash(model, "GapFlashRight", front, 1f);
            gaps.GetArrayElementAtIndex(1).objectReferenceValue = GapFlash(model, "GapFlashLeft", front, -1f);
        }

        ImpactEffects impacts = revolver.GetComponent<ImpactEffects>() ?? revolver.gameObject.AddComponent<ImpactEffects>();
        SerializedObject fx = new SerializedObject(impacts);
        fx.FindProperty("bulletHole").objectReferenceValue = Decal("BulletHole_01", 0);
        // Photographed blood (Quixel Megascans on Fab, free, Standard License; SourceAssets/Tools/quixel_blood.py) for
        // the surfaces behind and under a wound, with its relief; the Horror Texture Pack stains stay in the mix.
        Material spatter = Decal("QuixelSpatter", 1, true);
        Material stain = Decal("QuixelStain", 1, true);
        Material drops = Decal("QuixelDrops", 1, true);
        Decal("QuixelHandSmear", 1, true);
        SetArray(fx.FindProperty("bloodStains"), new Object[] { spatter, stain, spatter, Decal("BloodStain_01", 1), Decal("BloodStain_02", 1) });
        SetArray(fx.FindProperty("dropStains"), new Object[] { drops });
        SetArray(fx.FindProperty("bloodDrips"), new Object[] { Decal("BloodDrip_01", 2), Decal("BloodDrip_02", 2), Decal("BloodDrip_03", 2) });
        fx.FindProperty("puffMaterial").objectReferenceValue = Particle("ImpactPuff", "whitePuff00", false);
        fx.FindProperty("mistMaterial").objectReferenceValue = Particle("BloodMist", "smoke_07", false);
        // On the creature: wound spatter (SourceAssets/Tools/blood_textures.py) and the spurt's droplets.
        SetArray(fx.FindProperty("bodySplats"), new Object[] { Decal("BloodSplat_01", 1), Decal("BloodSplat_02", 1), Decal("BloodSplat_03", 1), Decal("BloodSplat_04", 1), stain });
        fx.FindProperty("dropletMaterial").objectReferenceValue = Particle("BloodDroplet", "blood_droplet", false);
        fx.ApplyModifiedPropertiesWithoutUndo();

        so.FindProperty("impacts").objectReferenceValue = impacts;
        so.FindProperty("casePrefab").objectReferenceValue = SpentCasePrefab();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(revolver);
    }

    // The URP decal renderer feature on every renderer of every quality level (screen space: no extra G-buffer).
    public static void EnsureDecalFeature()
    {
        HashSet<UniversalRendererData> done = new HashSet<UniversalRendererData>();
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            if (!(QualitySettings.GetRenderPipelineAssetAt(level) is UniversalRenderPipelineAsset pipeline))
            {
                continue;
            }

            SerializedProperty list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData renderer && done.Add(renderer))
                {
                    AddDecalFeature(renderer);
                }
            }
        }
    }

    private static void AddDecalFeature(UniversalRendererData renderer)
    {
        DecalRendererFeature decals = (DecalRendererFeature)renderer.rendererFeatures.Find(f => f is DecalRendererFeature);
        if (decals == null)
        {
            decals = ScriptableObject.CreateInstance<DecalRendererFeature>();
            decals.name = "Decals";
            AssetDatabase.AddObjectToAsset(decals, renderer);
            renderer.rendererFeatures.Add(decals);
            SerializedObject data = new SerializedObject(renderer);
            SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(decals, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        SerializedObject settings = new SerializedObject(decals);
        settings.FindProperty("m_Settings.technique").enumValueIndex = 2;
        settings.FindProperty("m_Settings.maxDrawDistance").floatValue = 60f;
        settings.FindProperty("m_Settings.screenSpaceSettings.normalBlend").enumValueIndex = 0;
        settings.ApplyModifiedPropertiesWithoutUndo();
        decals.SetActive(true);
        EditorUtility.SetDirty(decals);
        EditorUtility.SetDirty(renderer);
    }

    // A decal material on the URP decal shader graph; draw order keeps holes over blood over drips.
    // quixel: the texture and its normal map are in Decals/Quixel (texture.png and texture_Normal.png).
    private static Material Decal(string texture, int order, bool quixel = false)
    {
        Folders(DecalFolder);
        string path = DecalFolder + "/" + texture + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Shader Graphs/Decal"));
            AssetDatabase.CreateAsset(material, path);
        }

        string folder = quixel ? DecalTextures + "/Quixel" : DecalTextures;
        material.SetTexture("Base_Map", DecalTexture(folder + "/" + texture + ".png"));
        if (quixel)
        {
            material.SetTexture("Normal_Map", NormalTexture(folder + "/" + texture + "_Normal.png"));
            material.SetFloat("Normal_Blend", 0.6f);
        }

        material.SetFloat("_DrawOrder", 2 - order);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D NormalTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.NormalMap || importer.wrapMode != TextureWrapMode.Clamp))
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D DecalTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency))
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Soft particles: alpha blended Simple Lit (smoke and blood stay dark in the dark); additive Unlit for light.
    private static Material Particle(string name, string texture, bool additive)
    {
        Folders(Folder);
        string path = Folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(additive ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Particles/Simple Lit");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/" + texture + ".png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(material);
        return material;
    }

    // The flash itself: one bright star facing the eye for two frames, a little bigger on every shot at random.
    private static ParticleSystem MuzzleStar(Transform muzzle)
    {
        ParticleSystem ps = Child(muzzle, "MuzzleStar");
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 0.05f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.17f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        // Warm and not over-bright: at 3x the star clipped to plain white.
        main.startColor = new Color(1f, 0.62f, 0.28f, 1f) * 1.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Particle("MuzzleStar", "muzzle_01", true);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    // The blast: a short tongue of flame thrown forward out of the muzzle, stretched along its speed, gone in a few
    // hundredths of a second, bright enough to read in daylight.
    private static ParticleSystem MuzzleFire(Transform muzzle)
    {
        ParticleSystem ps = Child(muzzle, "MuzzleFire");
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 0.55f, 0.2f, 1f) * 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 0.005f;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.15f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Particle("MuzzleFire", "flame_03", true);
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.012f;
        renderer.lengthScale = 1.5f;
        return ps;
    }

    // Out of the cylinder gap to one side (side = 1 right, -1 left): a small flash and grains, sideways and a bit up.
    private static ParticleSystem GapFlash(Transform model, string name, Vector3 localFront, float side)
    {
        ParticleSystem ps = Child(model, name);
        ps.transform.localPosition = localFront + Vector3.right * side * 0.012f;
        ps.transform.localRotation = Quaternion.LookRotation(new Vector3(side, 0.35f, 0.15f));
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 0.6f, 0.3f, 1f) * 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.004f;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Particle("MuzzleStar", "muzzle_01", true);
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    // Burning powder grains: short streaks out of the muzzle.
    private static ParticleSystem MuzzleSparks(Transform muzzle)
    {
        ParticleSystem ps = Child(muzzle, "MuzzleSparks");
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.01f);
        main.startColor = new Color(1f, 0.7f, 0.35f, 1f) * 2.5f;
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.004f;
        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Particle("MuzzleSpark", "spark_01", true);
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.025f;
        renderer.lengthScale = 2f;
        return ps;
    }

    private static ParticleSystem Child(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = 32;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    // ---------------------------------------------------------------- spent case

    // The .357 Magnum case of the Colt Python: the procedural mesh and the capsule that fits it (an older prefab may still
    // carry the old gun's cartridge).
    private static void ResetShape(GameObject root)
    {
        root.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CaseFolder + "/SpentCase.asset");
        Material brass = AssetDatabase.LoadAssetAtPath<Material>(CaseFolder + "/Brass.mat");
        if (brass != null)
        {
            root.GetComponent<MeshRenderer>().sharedMaterial = brass;
        }

        CapsuleCollider collider = root.GetComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.radius = CaseRadius;
        collider.height = CaseLength;
        collider.center = new Vector3(0f, CaseLength * 0.5f, 0f);
    }

    private const float CaseRadius = 0.0049f;
    private const float CaseLength = 0.033f;

    public static SpentCase SpentCasePrefab()
    {
        SpentCase existing = AssetDatabase.LoadAssetAtPath<SpentCase>(CasePrefab);
        if (existing != null)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(CasePrefab);
            ResetShape(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, CasePrefab);
            PrefabUtility.UnloadPrefabContents(contents);
            existing = AssetDatabase.LoadAssetAtPath<SpentCase>(CasePrefab);
            // Keep the assets, refresh the shape and the brass.
            Mesh current = AssetDatabase.LoadAssetAtPath<Mesh>(CaseFolder + "/SpentCase.asset");
            if (current != null)
            {
                Mesh fresh = CaseMesh();
                current.Clear();
                current.SetVertices(fresh.vertices);
                current.SetNormals(fresh.normals);
                current.SetTriangles(fresh.triangles, 0);
                current.RecalculateBounds();
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(current);
            }

            Material brassMaterial = AssetDatabase.LoadAssetAtPath<Material>(CaseFolder + "/Brass.mat");
            if (brassMaterial != null)
            {
                BrassLook(brassMaterial);
            }

            return existing;
        }

        Folders(CaseFolder);
        Folders(System.IO.Path.GetDirectoryName(CasePrefab).Replace('\\', '/'));
        Mesh mesh = CaseMesh();
        AssetDatabase.CreateAsset(mesh, CaseFolder + "/SpentCase.asset");
        Material brass = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        AssetDatabase.CreateAsset(brass, CaseFolder + "/Brass.mat");
        BrassLook(brass);
        PhysicsMaterial bounce = new PhysicsMaterial("SpentCase")
        {
            bounciness = 0.35f,
            dynamicFriction = 0.4f,
            staticFriction = 0.5f,
            bounceCombine = PhysicsMaterialCombine.Maximum,
        };
        AssetDatabase.CreateAsset(bounce, CaseFolder + "/SpentCase.physicMaterial");

        GameObject go = new GameObject("SpentCase");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = brass;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.radius = CaseRadius;
        collider.height = CaseLength;
        collider.center = new Vector3(0f, CaseLength * 0.5f, 0f);
        collider.sharedMaterial = bounce;
        Rigidbody body = go.AddComponent<Rigidbody>();
        body.mass = 0.008f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.angularDamping = 0.6f;
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 0.8f;
        source.maxDistance = 12f;
        SpentCase shell = go.AddComponent<SpentCase>();
        SerializedObject so = new SerializedObject(shell);
        so.FindProperty("source").objectReferenceValue = source;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, CasePrefab);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<SpentCase>();
    }

    // Fully metallic, the case only shows what it reflects, and with no reflection probe near it read black on the
    // floor (07/10); half metallic keeps the brass colour from the light itself.
    public static void BrassLook(Material brass)
    {
        brass.SetColor("_BaseColor", new Color(0.86f, 0.64f, 0.3f));
        brass.SetFloat("_Metallic", 0.55f);
        brass.SetFloat("_Smoothness", 0.7f);
        brass.enableInstancing = true;
        EditorUtility.SetDirty(brass);
    }

    // The .357 case turned about +Y from its half-profile (radius, height in metres), base at y = 0, open mouth up with the
    // inside wall 0.4 mm thick down to a flat floor: 16 sides.
    private static Mesh CaseMesh()
    {
        Vector2[] profile =
        {
            new Vector2(0f, 0f), new Vector2(0.0049f, 0f), new Vector2(0.0049f, 0.0013f), new Vector2(0.0044f, 0.0016f),
            new Vector2(0.0044f, 0.0025f), new Vector2(0.0047f, 0.033f), new Vector2(0.0043f, 0.033f), new Vector2(0.0040f, 0.005f),
            new Vector2(0f, 0.005f),
        };
        const int sides = 16;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();
        // One ring of vertices per profile edge end (flat shading along the profile, smooth around).
        for (int e = 0; e < profile.Length - 1; e++)
        {
            Vector2 a = profile[e];
            Vector2 b = profile[e + 1];
            Vector2 edge = (b - a).normalized;
            Vector2 n2 = new Vector2(edge.y, -edge.x);
            int start = vertices.Count;
            for (int s = 0; s <= sides; s++)
            {
                float angle = s * Mathf.PI * 2f / sides;
                Vector3 around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 normal = (around * n2.x + Vector3.up * n2.y).normalized;
                vertices.Add(around * a.x + Vector3.up * a.y);
                normals.Add(normal);
                vertices.Add(around * b.x + Vector3.up * b.y);
                normals.Add(normal);
            }

            for (int s = 0; s < sides; s++)
            {
                int i0 = start + s * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;
                if (a.x > 0f || b.x > 0f)
                {
                    // Clockwise seen from outside (the first version had them the other way round: every face was
                    // culled from outside and the case drew its dark inside).
                    triangles.AddRange(new[] { i0, i1, i2, i1, i3, i2 });
                }
            }
        }

        Mesh mesh = new Mesh { name = "SpentCase" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SetArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void Folders(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
