using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The growths the GDD's gallery draws ("Ambiente e inimigos", Dayse 11/10), where the enemy map places them ("Sugestão de
// localização de inimigos na casa"):
//  - the courtyard garden's flora: the swamp tree standing in contaminated water with clouds of glowing poisonous spores
//    ("esporos flutuam ao redor como nuvens, venenoso, bioluminescente"), two giant blue bioluminescent mushrooms whose
//    translucent stems hold boiling water ("tubo que armazena água fervente"), the red palm that beats like a heart;
//    a small palm on the balcony ("flora"). Models: Meshy from Dayse's own drawings (image to 3D) and text, through the
//    game-3d-assets optimiser (SourceAssets/Downloads/AI/meshy/flora).
//  - the blue fungus as the drawing shows it: colonies of glowing powder on walls, ceilings and floors, not mushrooms
//    (pantry and china room, the second bathroom, the fungus light of the upper gallery).
//  - the red "spider web" fungus in the library's north-west corner: silk-like strands from the ceiling down the walls to
//    the floor, holding its eggs (amber, a green embryo inside, dark red veins) that drip the corrosive red liquid.
//  - the anemone worms: colonies of glassy tubes standing out of the floor (kitchen, pantry, single room 2) that sink
//    into the ground when they feel steps and bite whoever stands over them (AnemoneColony).
public static partial class FloodedHouseBuilder
{
    private static Material sporeGlow, sporeBubble, silk, egg, colony, anemone, contaminated, corrosive;

    private static void FloraMaterials()
    {
        string fungus = ProjectPaths.Textures + "/Fungus";
        // Horror/Spore (Art/Shaders/Spore.shader, 29/10): a hot core in a faint halo, twinkling, faded against walls
        // and near the camera. Tint white here: the colour of each spore comes from its particle.
        sporeGlow = Material("SporeGlow", Shader.Find("Horror/Spore"));
        sporeGlow.SetColor("_Color", Color.white);
        sporeGlow.SetFloat("_Intensity", 4.5f);
        sporeGlow.SetFloat("_CoreSize", 0.2f);
        sporeGlow.SetFloat("_HaloAmount", 0.42f);
        sporeGlow.SetFloat("_HaloPower", 2.6f);
        sporeGlow.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(sporeGlow);
        // The bubbles in the mushroom stems: same speck, less halo, a steadier glint.
        sporeBubble = Material("SporeBubble", Shader.Find("Horror/Spore"));
        sporeBubble.SetColor("_Color", Color.white);
        sporeBubble.SetFloat("_Intensity", 1.8f);
        sporeBubble.SetFloat("_HaloAmount", 0.12f);
        sporeBubble.SetFloat("_Twinkle", 0.3f);
        sporeBubble.SetFloat("_Flare", 0.4f);
        sporeBubble.SetFloat("_SoftFade", 0.05f);
        sporeBubble.SetVector("_NearFade", new Vector4(0.15f, 0.6f, 0f, 0f));
        sporeBubble.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(sporeBubble);

        silk = Material("FungusSilk", Shader.Find("Universal Render Pipeline/Lit"));
        silk.SetTexture("_BaseMap", AlphaTexture(fungus + "/FungusSilk.png"));
        silk.SetColor("_BaseColor", Color.white);
        silk.SetFloat("_Smoothness", 0.35f);
        silk.SetFloat("_AlphaClip", 1f);
        silk.SetFloat("_Cutoff", 0.3f);
        silk.EnableKeyword("_ALPHATEST_ON");
        silk.SetFloat("_Cull", (float)CullMode.Back);
        silk.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(silk);

        egg = Material("FungusEgg", Shader.Find("Horror/FungusEgg"));
        egg.SetTexture("_VeinMap", AlphaTexture(fungus + "/FungusEgg.png"));
        // The drawing's egg: deep amber, the green embryo plain to see, veins of dried blood.
        egg.SetColor("_Shell", new Color(0.82f, 0.42f, 0.08f));
        egg.SetColor("_Embryo", new Color(0.16f, 0.32f, 0.1f));
        egg.SetColor("_Vein", new Color(0.3f, 0.02f, 0.015f));
        egg.SetFloat("_Glow", 0.5f);
        EditorUtility.SetDirty(egg);

        colony = Material("LuminaColony", Shader.Find("Horror/LuminaColony"));
        colony.SetTexture("_BaseMap", AlphaTexture(fungus + "/LuminaColony.png"));
        colony.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(colony);

        anemone = Material("AnemoneTube", Shader.Find("Horror/AnemoneTube"));
        EditorUtility.SetDirty(anemone);
        contaminated = Transparent("ContaminatedWater", new Color(0.22f, 0.34f, 0.1f, 0.82f), 0.88f, null);
        corrosive = Plain("CorrosiveLiquid", new Color(0.32f, 0.02f, 0.015f), 0.9f, 0f);
    }

    private static Texture2D AlphaTexture(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer && !importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void BuildFlora(Transform parent, Transform player)
    {
        FloraMaterials();
        SporeMaterials();
        Transform f = Group("Flora", parent);
        SporeStir(player);
        Random.State saved = Random.state;
        Random.InitState(1311);
        GardenFlora(f);
        LuminaColonies(f);
        SilkFungus(f, new Vector2(10.25f, 13.3f), new Vector2(1f, -1f));
        AnemoneWorms(f, M(-10.7f, 0f, 10.4f), 18, player);
        AnemoneWorms(f, M(-12.4f, 0f, 20.6f), 12, player);
        // Away from the vanity against the south wall (it grew through it, audit 22/10).
        AnemoneWorms(f, M(-12.6f, UF, 17.4f), 24, player);
        Random.state = saved;
    }

    // ---------------------------------------------------------------- the courtyard garden

    private static void GardenFlora(Transform parent)
    {
        float y = LunaManor.GardenFloor;
        Transform g = Group("GardenFlora", parent);
        // The swamp tree in its pool of contaminated water, under a cloud of glowing spores. Off the axis from the glass
        // door to the grand stair (29/10: the stair stands where it grew, and the drawing sees the stair from the door),
        // its pool just over the flood.
        Vector3 tree = M(4.4f, y, 13.3f);
        GameObject swampTree = ModelProp("SwampTree", g, tree, Quaternion.Euler(0f, 20f, 0f));
        // Dead wet wood, not bleached bone: at full tint the tree flashed pure white under the lightning (13/11).
        foreach (Renderer r in swampTree.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m != null && m.HasProperty("_BaseColor"))
                {
                    m.SetColor("_BaseColor", new Color(0.66f, 0.63f, 0.58f, 1f));
                    EditorUtility.SetDirty(m);
                }
            }
        }
        GameObject pool = Primitive(PrimitiveType.Cylinder, "ContaminatedPool", g, tree + Vector3.up * 0.09f, Quaternion.identity, new Vector3(3.8f, 0.02f, 2.6f), contaminated);
        pool.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        // Spores drifting off the tree over the whole garden (lit specks), its faint glow round the crown (a continuous
        // haze, not dots), flies over the pool (FloodedHouseSpores, 29/10).
        Rect lawn = LunaManor.Garden;
        Vector3 lawnA = M(lawn.xMin, y, lawn.yMin), lawnB = M(lawn.xMax, y, lawn.yMax);
        SporeMotes(g, "Spores", new Vector3((lawnA.x + lawnB.x) * 0.5f, y + 2.6f, (lawnA.z + lawnB.z) * 0.5f), new Vector3(lawnB.x - lawnA.x, 5f, lawnB.z - lawnA.z), 2600, new Color(0.85f, 0.9f, 0.78f, 0.9f));
        SporeMotes(g, "SporesTree", tree + Vector3.up * 2.8f, new Vector3(5f, 4f, 5f), 6000, new Color(0.8f, 0.95f, 0.75f, 1f));
        BioHaze(g, "SporeGlow", tree + Vector3.up * 3.2f, new Vector3(4.5f, 3.2f, 4.5f), 14, new Color(0.25f, 0.85f, 0.7f, 0.045f));
        PoolFlies(g, tree + Vector3.up * 0.55f);

        // The giant blue mushrooms: glowing caps, a halo on the ground, bubbles rising in the translucent stems.
        foreach ((Vector3 at, float yaw) in new[] { (M(-5.3f, y, 13.1f), 140f), (M(5.6f, y, 17.9f), -40f) })
        {
            GameObject m = ModelProp("GiantMushroom", g, at, Quaternion.Euler(0f, yaw, 0f));
            Material glow = m.GetComponentInChildren<Renderer>().sharedMaterial;
            glow.EnableKeyword("_EMISSION");
            glow.SetTexture("_EmissionMap", glow.GetTexture("_BaseMap"));
            // 10/11: the Meshy texture is one flat pale blue, and at 0.9 the emission turned the mushroom into a block of
            // lit plastic hiding its veins. Deeper, wetter flesh, the glow at a third, and the skin of a real scan
            // (the oyster cluster's normal map, tiled) as detail, so it reads as flesh under the torch.
            glow.SetColor("_BaseColor", new Color(0.86f, 0.68f, 0.6f));
            glow.SetColor("_EmissionColor", new Color(0.3f, 0.55f, 0.75f) * 0.12f);
            glow.SetFloat("_Smoothness", 0.78f);
            Texture2D skin = AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.PropModels + "/OysterCluster/OysterCluster_Normal.png");
            if (skin != null)
            {
                glow.SetTexture("_DetailNormalMap", skin);
                glow.SetFloat("_DetailNormalMapScale", 0.8f);
                glow.SetTextureScale("_DetailAlbedoMap", new Vector2(5f, 5f));
                glow.EnableKeyword("_DETAIL_MULX2");
            }
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(glow);
            Halo(g, "FungusHaloBlue", at + Vector3.up * 0.02f, Vector3.up, 2.6f, new Color(0.3f, 0.6f, 1f));
            Bubbles(g, at + Vector3.up * 0.15f);
            Loop3D(m.transform, "Ambience/boiling_loop.wav", 0.35f, 6f);
        }

        // The red palms, beating.
        foreach ((Vector3 at, float yaw, float scale) in new[] { (M(-3.4f, y, 12.2f), 200f, 1f), (M(-3.9f, y, 18.1f), 30f, 0.85f), (M(2.3f, UF, 25.4f), 160f, 0.55f) })
        {
            GameObject palm = ModelProp("PulsePalm", g, at, Quaternion.Euler(0f, yaw, 0f));
            palm.transform.localScale *= scale;
            palm.AddComponent<HeartbeatPulse>().Configure(AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Creature/palm_beat.wav"));
        }
    }

    // A looping 3D sound on an object (the mushrooms' boiling stems).
    private static void Loop3D(Transform at, string clip, float volume, float maxDistance)
    {
        AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/" + clip);
        if (c == null)
        {
            return;
        }

        AudioSource s = at.gameObject.AddComponent<AudioSource>();
        s.clip = c;
        s.loop = true;
        s.playOnAwake = true;
        s.volume = volume;
        s.spatialBlend = 1f;
        s.minDistance = 0.8f;
        s.maxDistance = maxDistance;
        s.rolloffMode = AudioRolloffMode.Linear;
    }

    // Position, colour, UV, then StableRandom.x and AgePercent into TEXCOORD0.zw (Horror/Spore reads them).
    private static void SporeStreams(ParticleSystemRenderer renderer)
    {
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.StableRandomX, ParticleSystemVertexStream.AgePercent,
        });
    }

    private static void Bubbles(Transform parent, Vector3 at)
    {
        GameObject go = new GameObject("StemBubbles");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.85f, 1f, 0.7f), new Color(0.8f, 0.95f, 1f, 0.5f));
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 30f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        // A bubble wobbles as it rises.
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.06f;
        noise.frequency = 2f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sporeBubble;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        SporeStreams(renderer);
    }

    private static void Halo(Transform parent, string material, Vector3 at, Vector3 normal, float size, Color colour)
    {
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        halo.name = "Halo";
        Object.DestroyImmediate(halo.GetComponent<Collider>());
        halo.transform.SetParent(parent, false);
        halo.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-normal));
        halo.transform.localScale = Vector3.one * size;
        MeshRenderer r = halo.GetComponent<MeshRenderer>();
        r.sharedMaterial = FungusHalo(material, colour);
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- the blue fungus: colonies of glowing powder

    private static void LuminaColonies(Transform parent)
    {
        Transform c = Group("LuminaColonies", parent);
        (Vector3 at, Vector3 outward, float size)[] patches =
        {
            // Pantry and china room ("fungos bioluminescentes"): walls, the floor, the ceiling corners.
            (M(-14.24f, 1.3f, 18.6f), Vector3.right, 1.6f), (M(-14.24f, 2.6f, 22.4f), Vector3.right, 1.4f), (M(-12.2f, 0.012f, 21.5f), Vector3.up, 1.8f),
            (M(-11.6f, GC - 0.012f, 18.4f), Vector3.down, 2.0f), (M(-9.86f, 1.6f, 20.4f), Vector3.left, 1.2f), (M(-7f, 1.9f, 24.44f), Vector3.back, 1.5f),
            (M(-8.2f, 0.012f, 22.6f), Vector3.up, 1.3f), (M(-6.5f, GC - 0.012f, 23.2f), Vector3.down, 1.6f),
            // The second bathroom upstairs ("fungos").
            (M(14.84f, UF + 1.5f, 23.1f), Vector3.left, 1.5f), (M(12.4f, UC - 0.012f, 23f), Vector3.down, 1.8f), (M(11.2f, UF + 0.012f, 22.4f), Vector3.up, 1.2f),
            // The fungus light in the north gallery (the creatures' safe zone): its glow is this colony.
            (M(1.5f, UF + 1.8f, 21.49f), Vector3.back, 1.6f), (M(-3f, UF + 0.012f, 20.4f), Vector3.up, 1.4f), (M(-1f, UC - 0.012f, 20.5f), Vector3.down, 2.0f),
            (M(-6f, UF + 2.2f, 21.49f), Vector3.back, 1.3f),
        };
        foreach ((Vector3 at, Vector3 outward, float size) in patches)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Colony";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(c, false);
            quad.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-outward) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            quad.transform.localScale = new Vector3(size, size * Random.Range(0.7f, 1f), 1f);
            MeshRenderer r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = colony;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            // Its spill on the surface round it (no light: SurfaceGlow).
            Halo(c, "LuminaHalo", at + outward * 0.004f, outward, size * 1.6f, new Color(0.2f, 0.95f, 0.8f));
        }
    }

    // ---------------------------------------------------------------- the red silk fungus and its eggs

    // A colony in a room's corner: `corner` is where two walls meet (on their room faces), `into` points into the room
    // along both walls (signs of x and z). Strands run from the ceiling near the corner down to the walls and the floor;
    // the eggs hang in them, dripping.
    private static void SilkFungus(Transform parent, Vector2 corner, Vector2 into)
    {
        Transform s = Group("SilkFungus", parent);
        List<(Vector3 top, Vector3 bottom)> strands = new List<(Vector3, Vector3)>();
        for (int i = 0; i < 16; i++)
        {
            Vector2 a = corner + new Vector2(into.x * Random.Range(0.05f, 1.3f), into.y * Random.Range(0.05f, 1.3f));
            Vector3 top = M(a, GC - 0.01f);
            Vector3 bottom;
            int end = i % 3;
            if (end == 0)
            {
                // Down to the floor, out into the room.
                Vector2 b = corner + new Vector2(into.x * Random.Range(0.3f, 1.8f), into.y * Random.Range(0.3f, 1.8f));
                bottom = M(b, 0.01f);
            }
            else if (end == 1)
            {
                // Down the west wall (x on the corner).
                bottom = M(corner.x, Random.Range(0.2f, 1.6f), corner.y + into.y * Random.Range(0.3f, 1.8f));
            }
            else
            {
                bottom = M(corner.x + into.x * Random.Range(0.3f, 1.8f), Random.Range(0.2f, 1.6f), corner.y);
            }

            strands.Add((top, bottom));
            Strand(s, top, bottom, Random.Range(0.12f, 0.35f), Random.Range(0.15f, 0.45f));
        }

        // The eggs, big as the drawing's scale (taller than a child), held in the strands near the corner.
        for (int i = 0; i < 3; i++)
        {
            Vector2 a = corner + new Vector2(into.x * (0.5f + i * 0.4f), into.y * (0.75f + (i % 2) * 0.45f));
            float h = Random.Range(0.85f, 1.15f);
            Vector3 bottom = M(a, Random.Range(0.35f, 0.6f));
            GameObject e = Primitive(PrimitiveType.Sphere, "Egg", s, bottom + Vector3.up * h * 0.5f, Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), 0f),
                new Vector3(h * 0.62f, h, h * 0.62f), egg, true);
            // The stalk up to the ceiling and a strand round it.
            Strand(s, M(a, GC - 0.01f), bottom + Vector3.up * h * 0.95f, 0.22f, 0.05f);
            // The corrosive liquid: a drip from the tip and a puddle under it.
            Drip(s, bottom + Vector3.up * 0.02f);
            GameObject puddle = Primitive(PrimitiveType.Cylinder, "Puddle", s, new Vector3(bottom.x, M(a, 0f).y + 0.004f, bottom.z), Quaternion.identity,
                new Vector3(Random.Range(0.35f, 0.6f), 0.002f, Random.Range(0.3f, 0.5f)), corrosive);
            puddle.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            _ = e;
        }
    }

    // One silk strand: a ribbon sagging from `top` to `bottom`, `width` across, turned to face the room's middle.
    private static void Strand(Transform parent, Vector3 top, Vector3 bottom, float width, float sag)
    {
        const int segments = 12;
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        Vector3 along = bottom - top;
        Vector3 side = Vector3.Cross(along.normalized, Vector3.up);
        if (side.sqrMagnitude < 0.01f)
        {
            side = Vector3.right;
        }

        side = Quaternion.AngleAxis(Random.Range(-40f, 40f), along) * side.normalized;
        Vector3 sagDir = Vector3.Cross(side, along).normalized;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 c = top + along * t + sagDir * Mathf.Sin(t * Mathf.PI) * sag + Vector3.down * Mathf.Sin(t * Mathf.PI) * sag * 0.5f;
            float w = width * (0.55f + 0.45f * Mathf.Sin(t * Mathf.PI));
            verts.Add(c - side * w * 0.5f);
            verts.Add(c + side * w * 0.5f);
            uvs.Add(new Vector2(0f, t));
            uvs.Add(new Vector2(1f, t));
            if (i > 0)
            {
                int k = i * 2;
                tris.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 });
            }
        }

        // Both faces with their own normals (URP Lit does not flip the normal on a back face: seen from behind, a
        // one-sided strand was lit as if from the wrong side and read grey).
        Mesh front = new Mesh();
        front.SetVertices(verts);
        front.SetUVs(0, uvs);
        front.SetTriangles(tris, 0);
        front.RecalculateNormals();
        List<Vector3> normals = new List<Vector3>(front.normals);
        int n = verts.Count;
        for (int i = 0; i < n; i++)
        {
            verts.Add(verts[i]);
            uvs.Add(uvs[i]);
            normals.Add(-normals[i]);
        }

        int count = tris.Count;
        for (int i = 0; i < count; i += 3)
        {
            tris.AddRange(new[] { tris[i] + n, tris[i + 2] + n, tris[i + 1] + n });
        }

        Object.DestroyImmediate(front);
        Mesh mesh = new Mesh { name = "Strand" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        GameObject go = new GameObject("Strand");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = silk;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void Drip(Transform parent, Vector3 at)
    {
        GameObject go = new GameObject("CorrosiveDrip");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 0.6f;
        main.startSpeed = 0f;
        main.gravityModifier = 1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
        main.startColor = new Color(0.35f, 0.02f, 0.01f, 1f);
        main.maxParticles = 10;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particle;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---------------------------------------------------------------- the anemone worms

    // A colony of `count` tubes on the floor at `at` (the drawing: groups of 8 to 32, knee high), one mesh; its
    // AnemoneColony sinks it when steps come near.
    private static void AnemoneWorms(Transform parent, Vector3 at, int count, Transform player)
    {
        Transform root = Group("AnemoneColony", parent);
        root.position = at;
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        const int sides = 8, rings = 7;
        for (int n = 0; n < count; n++)
        {
            Vector2 spot = Random.insideUnitCircle * (0.25f + 0.02f * count);
            float height = Random.Range(0.35f, 0.75f);
            float radius = Random.Range(0.022f, 0.04f);
            float phase = Random.value;
            Vector3 lean = new Vector3(spot.x, 0f, spot.y) * Random.Range(0.15f, 0.5f);
            int start = verts.Count;
            for (int k = 0; k <= rings; k++)
            {
                float v = k / (float)rings;
                // The mouth flares open in the last ring.
                float r = radius * (k == rings ? 1.45f : 1f - 0.15f * v);
                Vector3 centre = new Vector3(spot.x, height * v, spot.y) + lean * v * v;
                for (int j = 0; j < sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts.Add(centre + dir * r);
                    norms.Add(dir);
                    uvs.Add(new Vector2(phase, v));
                }
            }

            for (int k = 0; k < rings; k++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a0 = start + k * sides + j, a1 = start + k * sides + (j + 1) % sides;
                    int b0 = a0 + sides, b1 = a1 + sides;
                    tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            }
        }

        Mesh mesh = new Mesh { name = "AnemoneTubes" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        // The sway moves the mouths up to 6 cm: grow the bounds so culling does not clip them.
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.one * 0.2f);
        GameObject tubes = new GameObject("Tubes");
        tubes.transform.SetParent(root, false);
        // 10/11: the generated tubes (white bent cylinders) gave way to a scan of giant tube worms, Riftia pachyptila
        // (Scientart, CC BY): red plumes out of white sheaths on a dark rock, a deep-sea animal that fits the Lumina.
        // The colony is still what AnemoneColony pulls into the floor and lets back up; the rock is sunk so only its
        // top and the worms stand out of the broken boards. The swaying generated tubes stay as the fallback.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/RiftiaColony/RiftiaColony.fbx") != null)
        {
            float span = 0.5f + 0.01f * count;
            GameObject worms = ModelProp("RiftiaColony", tubes.transform, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), false, false);
            worms.transform.localScale *= span;
            Bounds wb = RendererBounds(worms);
            worms.transform.position += Vector3.up * (at.y - wb.min.y - 0.42f * span) + new Vector3(at.x - wb.center.x, 0f, at.z - wb.center.z);
        }
        else
        {
            tubes.AddComponent<MeshFilter>().sharedMesh = mesh;
            tubes.AddComponent<MeshRenderer>().sharedMaterial = anemone;
        }
        // The broken floor they come out of: a dark damp patch.
        GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Quad);
        patch.name = "Hole";
        Object.DestroyImmediate(patch.GetComponent<Collider>());
        patch.transform.SetParent(root, false);
        patch.transform.localPosition = Vector3.up * 0.006f;
        patch.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        patch.transform.localScale = Vector3.one * (0.9f + 0.03f * count);
        patch.GetComponent<MeshRenderer>().sharedMaterial = moldPatch;
        root.gameObject.AddComponent<AnemoneColony>().Configure(tubes.transform, player);
    }
}
