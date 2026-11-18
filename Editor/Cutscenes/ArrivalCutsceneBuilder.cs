using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Horror > Cutscenes > Gerar cena da chegada (protótipo): builds Scenes/Cutscenes/Arrival.unity from nothing, the
// opening cutscene asked for by Gustavo and Dayse (02/10): Erick reaches the island by boat at night in the storm,
// the island and the mansion on its hill seen from afar, "Daysavo apresenta", landing at an old pier, the walk up
// to the house, the title, the front door. No subtitles and no story told in words (Gustavo 03/10): sound, music
// and acting only. Blockout geometry (island mesh, mansion of boxes, procedural boat) under the real storm, sea,
// rain and lightning of the game; Erick is the player's own body (the MetaHuman), animated. Shots in
// ArrivalCutsceneBuilder.Timeline.cs.
// Layout (metres): the island's centre at the origin, sea level 0. Hilltop plateau at 12 m with the mansion (a
// rounded rectangle round the house's footprint, x -10..16, z -8.3..6, so no corner hangs over the slope), front
// door facing south (-Z) at z -6; shore around radius 46; the pier on the south shore from z -44 out to z -62;
// the boat comes in from the south-west. Calm water by the island: the swell dies between 50 and 210 m out and
// only a fifth of the chop is left at the rocks (the sea material's _ShoreChop).
public static partial class ArrivalCutsceneBuilder
{
    public const string ScenePath = ProjectPaths.Scenes + "/Cutscenes/Arrival.unity";
    private const string Folder = ProjectPaths.Root + "/Cutscenes";
    private const string MaterialFolder = ProjectPaths.Art + "/Materials/Cutscenes";
    private const float Plateau = 12f;
    private const float PlateauRadius = 11f;
    private const float ShoreRadius = 46f;
    private const float PierDeck = 0.9f;
    private const float PierStart = -43f;
    private const float PierEnd = -62f;
    private const string StarterAnimations = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Character/Animations";
    private static readonly Vector3 DoorPoint = new Vector3(0f, Plateau, -6.3f);

    private static Transform root;

    [MenuItem("Horror/Cutscenes/Gerar cena da chegada (protótipo)")]
    public static void Build()
    {
        EnsureFolder(ProjectPaths.Scenes, "Cutscenes");
        EnsureFolder(ProjectPaths.Root, "Cutscenes");
        EnsureFolder(ProjectPaths.Art + "/Materials", "Cutscenes");
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildWorld("Arrival_Generated", out Renderer sea);
        Transform hull = Boat(sea, out Transform boat, out Animator erickBoat);
        Animator erickLand = Erick("Erick_Land", null, Vector3.zero, false);
        GameObject tentacle = Tentacle();
        Timeline(boat, hull, erickBoat, erickLand, tentacle);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Arrival] cutscene scene built: " + ScenePath);
    }

    // The world without the cutscene: the storm, the sea, the mainland, the island with its forest, the mansion and
    // the pier, under a new root in the open scene. The main menu stands in the same world (MainMenuBuilder).
    internal static Transform BuildWorld(string rootName, out Renderer sea)
    {
        EnsureFolder(ProjectPaths.Root, "Cutscenes");
        EnsureFolder(ProjectPaths.Art + "/Materials", "Cutscenes");
        root = new GameObject(rootName).transform;
        Atmosphere();
        sea = Sea();
        Mainland();
        Island();
        Mansion();
        Vegetation();
        Pier();
        return root;
    }

    // Redo only the island, the house and the plants inside an existing world (the menu keeps its UI and the rest of
    // its scene; MainMenuBuilder.UpdateHouse): the terrain asset is shared with the opening's scene.
    internal static void RebuildHouseAndIsland(Transform worldRoot)
    {
        root = worldRoot;
        foreach (string name in new[] { "Island", "Mansion", "Vegetation", "UpPath", "WindowTarget", "HouseTarget", "IslandTarget", "AerialAim", "DoorTarget", "DoorGrip", "DoorLook" })
        {
            Transform old = worldRoot.Find(name);
            while (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
                old = worldRoot.Find(name);
            }
        }

        Island();
        Mansion();
        Vegetation();
    }

    // ---- island ------------------------------------------------------------------------------------------------

    // The island (Gustavo 03/10: "the island is stupidly small"; it holds the Lunas' house and Alex's lab and has to
    // look the part): the house's own hill, unchanged (LocalHeight: the plateau, the path, the pier's shore), at the
    // south tip of a big island that runs 660 m east to west and 500 m north, with forested hills up to ~55 m behind
    // the house (BigHeight). Near the house the big island stays below the hill, so the path and the plateau are
    // exactly as they were.
    private static readonly Vector2 BigCentre = new Vector2(0f, 215f);
    private const float BigRadiusX = 330f;
    private const float BigRadiusZ = 255f;
    private const float HillTop = 52f;

    public static float Height(float x, float z)
    {
        float local = LocalHeight(x, z);
        float big = BigHeight(x, z);
        // Within ~35 m of the house the big island keeps under 0.5 m (the hill is all the house's); it takes over
        // gradually out to ~105 m.
        float fromHouse = new Vector2(x - 2f, z + 1f).magnitude;
        float w = Smooth(Mathf.InverseLerp(35f, 105f, fromHouse));
        big = Mathf.Lerp(Mathf.Min(big, 0.5f), big, w);
        return Mathf.Max(local, big);
    }

    // The house's hill: a flat top for the mansion, slopes down to a ragged shore, then the sea floor. The shore
    // wanders with the angle so it is not a perfect cone.
    private static float LocalHeight(float x, float z)
    {
        float r = Mathf.Sqrt(x * x + z * z);
        float angle = Mathf.Atan2(z, x);
        float shore = ShoreRadius + 4f * Mathf.Sin(3f * angle + 0.7f) + 2f * Mathf.Sin(5f * angle + 1.9f);
        // The pier's shore stays where the pier is.
        shore = Mathf.Lerp(shore, ShoreRadius, Mathf.Exp(-Mathf.Pow((angle + Mathf.PI * 0.5f) * 2.2f, 2f)));
        float s = r * ShoreRadius / shore;
        // How far outside the flat top (a rounded rectangle round the house, 2.5 m clear of its walls): the house
        // was 26 m wide on a plateau of radius 11, and its corners and the wing hung over the slope.
        float edge = PlateauDistance(x, z);
        float noise = (Mathf.PerlinNoise(x * 0.07f + 30f, z * 0.07f + 11f) - 0.5f) * 1.6f * Mathf.InverseLerp(0f, 6f, edge);
        if (edge <= 0f)
        {
            return Plateau;
        }

        if (s <= ShoreRadius)
        {
            // From the plateau's edge (0) to the shore (1), whatever the direction.
            float t = edge / (edge + (ShoreRadius - s));
            return Mathf.Lerp(Plateau, 0.7f, Smooth(t)) + noise;
        }

        return Mathf.Lerp(0.7f, -4f, Smooth(Mathf.Clamp01((s - ShoreRadius) / 9f))) + noise * 0.3f;
    }

    // The big island: an oval with a coast that wanders (three sizes of bays and capes), a beach 20-30 m deep, then
    // rolling hills of fractal noise rising toward the middle, with ridges; under the sea it shelves off to -7 m.
    // The coast stays put south of the house (the pier's water).
    private static float BigHeight(float x, float z)
    {
        float dx = (x - BigCentre.x) / BigRadiusX;
        float dz = (z - BigCentre.y) / BigRadiusZ;
        float e = Mathf.Sqrt(dx * dx + dz * dz);
        float a = Mathf.Atan2(dz, dx);
        float wander = 0.09f * Mathf.Sin(3f * a + 0.4f) + 0.05f * Mathf.Sin(7f * a + 1.3f) + 0.03f * Mathf.Sin(13f * a + 2.1f)
            + 0.06f * (Mathf.PerlinNoise(Mathf.Cos(a) * 2.2f + 7f, Mathf.Sin(a) * 2.2f + 3f) - 0.5f);
        // No capes in front of the pier (south, a = -90 degrees).
        wander *= 1f - Mathf.Exp(-Mathf.Pow((a + Mathf.PI * 0.5f) * 1.6f, 2f));
        float s = e / (1f + wander);
        float inland = (1f - s) * Mathf.Min(BigRadiusX, BigRadiusZ);
        if (inland <= 0f)
        {
            return Mathf.Lerp(0.5f, -7f, Smooth(-inland / 16f));
        }

        float n = 0f, amp = 1f, freq = 0.006f, sum = 0f;
        for (int o = 0; o < 5; o++)
        {
            float p = Mathf.PerlinNoise(x * freq + 13.7f * o, z * freq + 5.3f * o);
            // Ridged on the lower octaves: hills with crests, not bumps.
            n += (o < 2 ? 1f - Mathf.Abs(p * 2f - 1f) : p) * amp;
            sum += amp;
            amp *= 0.5f;
            freq *= 2.1f;
        }

        n /= sum;
        float rise = Smooth(inland / 140f);
        float beach = Mathf.Lerp(0.5f, 4f, Smooth(inland / 28f));
        return beach + rise * HillTop * Mathf.Pow(n, 1.4f) * 1.25f;
    }

    // Where the land meets the sea along a ray from the island's middle (Height falls under 0.3 m): the coast, for the
    // rocks along it.
    private static Vector3 CoastPoint(float angle)
    {
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 from = new Vector3(BigCentre.x, 0f, BigCentre.y);
        Vector3 last = from;
        for (float d = 0f; d < 600f; d += 2f)
        {
            Vector3 p = from + dir * d;
            if (Height(p.x, p.z) < 0.3f && d > 60f)
            {
                return last;
            }

            last = p;
        }

        return last;
    }

    // Signed distance to the plateau: the mansion's footprint (LunaManor, x -20..24, z -9.9..28.7 with the porch,
    // the garage and the store) grown by 2.5 m, corners rounded.
    private static float PlateauDistance(float x, float z)
    {
        const float rounding = 5f;
        Vector2 centre = new Vector2(2f, 9.4f);
        Vector2 half = new Vector2(22f + 2.5f, 19.3f + 2.5f) - Vector2.one * rounding;
        Vector2 q = new Vector2(Mathf.Abs(x - centre.x), Mathf.Abs(z - centre.y)) - half;
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - rounding;
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static void Island()
    {
        Transform island = Group("Island");
        IslandTerrain(island);

        // Coast rocks in a broken ring, dead trees on the slopes, stumps and roots: Poly Haven scenery cut to the
        // background budget by the game-3d-assets skill (1 to 3 thousand triangles each, Art/Models/Props), out of
        // the shadow passes. They were the Poly Haven files at their lightest LOD, still 100 to 214 thousand
        // triangles apiece (7.4 million in the frame, 03/10), and coast_rocks_01/02 are 46 m beaches of
        // pebbles that lay over the slopes as slabs: only the 9 m rock group stays.
        System.Random random = new System.Random(7);
        // Rocks all round the coast of the big island, lengthwise along it; none in front of the pier.
        for (int i = 0; i < 120; i++)
        {
            float a = i / 120f * Mathf.PI * 2f + (float)random.NextDouble() * 0.03f;
            Vector3 at = CoastPoint(a);
            if (Mathf.Abs(at.x) < 12f && at.z < -20f)
            {
                continue;
            }

            at.y = Height(at.x, at.z) - 0.5f;
            Scenery("CoastRocks", island, at, -a * Mathf.Rad2Deg + (float)random.NextDouble() * 30f, 0.8f + (float)random.NextDouble() * 0.7f);
        }

        // Dead trunks, stumps and roots in the woods and on the house's hill.
        string[] trees = { "DeadTrunk", "DeadTrunk2", "QuiverTrunk", "TreeStump", "RootCluster" };
        Vector3[] trail = PathPoints();
        int deadwood = 0;
        for (int attempt = 0; attempt < 2000 && deadwood < 90; attempt++)
        {
            Vector3 at = new Vector3(Mathf.Lerp(-330f, 330f, (float)random.NextDouble()), 0f, Mathf.Lerp(-40f, 470f, (float)random.NextDouble()));
            at.y = Height(at.x, at.z);
            if (at.y < 2f || PlateauDistance(at.x, at.z) < 2f || DistanceToPolyline(at, trail) < 5f || (Mathf.Abs(at.x) < 20f && at.z < 0f))
            {
                continue;
            }

            at.y -= 0.2f;
            Scenery(trees[deadwood % trees.Length], island, at, (float)random.NextDouble() * 360f, 1f + (float)random.NextDouble() * 0.6f);
            deadwood++;
        }

        // The path up from the pier: flat stones and old boards, following the ground.
        CutscenePath path = PathObject("UpPath", PathPoints());
        Material stone = Mat("PathStone", new Color(0.2f, 0.2f, 0.19f), 0.15f);
        float length = path.Length;
        for (float d = 0.6f; d < length - 0.5f; d += 1.1f)
        {
            Vector3 p = path.Evaluate(d / length);
            Vector3 dir = path.Tangent(d / length);
            p.y = Height(p.x, p.z) + 0.04f;
            Transform slab = Box("Stone", island, p, new Vector3(0.7f + (float)random.NextDouble() * 0.3f, 0.12f, 0.55f), stone, false);
            slab.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z)) * Quaternion.Euler(0f, (float)random.NextDouble() * 20f - 10f, 0f);
        }
    }

    // The ground: a Unity terrain shaped by Height() (0.5 m cells), painted with three real ground photos (Poly Haven,
    // CC0, Art/Textures/Island): grass and stone on the slopes, leaf litter and bare earth along the path and in
    // patches, wet sand and rocks at the shore and on the steepest banks. It was one dark wet-mud material
    // (albedo 0.18, smoothness 0.8) that drank the blue moonlight: the island read as a black hump and the plants
    // vanished against it (03/10).
    private static readonly (string id, float tile)[] GroundLayers =
    {
        ("aerial_grass_rock", 9f), ("forrest_ground_01", 2.5f), ("coast_sand_rocks_02", 7f),
    };

    private static void IslandTerrain(Transform island)
    {
        // 800 x 620 m round the big island (0.8 m cells), from 7.5 m under the sea to 70 m.
        const float originX = -400f;
        const float originZ = -110f;
        const float sizeX = 800f;
        const float sizeZ = 620f;
        const float bottom = -7.5f;
        const float top = 70f;
        const int resolution = 1025;
        const int splat = 1024;
        string path = Folder + "/ArrivalIsland.asset";
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, path);
        }

        data.heightmapResolution = resolution;
        data.size = new Vector3(sizeX, top - bottom, sizeZ);
        float[,] heights = new float[resolution, resolution];
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float px = originX + x / (resolution - 1f) * sizeX;
                float pz = originZ + z / (resolution - 1f) * sizeZ;
                heights[z, x] = (Height(px, pz) - bottom) / (top - bottom);
            }
        }

        data.SetHeights(0, 0, heights);
        data.terrainLayers = GroundLayers.Select(l => GroundLayer(l.id, l.tile)).ToArray();
        data.alphamapResolution = splat;
        Vector3[] trail = PathPoints();
        float[,,] weights = new float[splat, splat, GroundLayers.Length];
        for (int z = 0; z < splat; z++)
        {
            for (int x = 0; x < splat; x++)
            {
                float u = (x + 0.5f) / splat, v = (z + 0.5f) / splat;
                float px = originX + u * sizeX;
                float pz = originZ + v * sizeZ;
                float y = Height(px, pz);
                float steep = data.GetSteepness(u, v);
                float ragged = Mathf.PerlinNoise(px * 0.21f + 5f, pz * 0.21f + 9f);
                // Sand and rock up to about 1.5 m above the sea (ragged), and on banks steeper than ~32 degrees.
                float shore = Mathf.Max(1f - Smooth(Mathf.InverseLerp(0.8f, 2.4f, y + (ragged - 0.5f) * 1.4f)), Smooth(Mathf.InverseLerp(28f, 38f, steep)));
                // Leaf litter: worn into the path, and in patches under the plants.
                float trailWear = 1f - Smooth(Mathf.InverseLerp(0.9f, 2.6f, DistanceToPolyline(new Vector3(px, 0f, pz), trail)));
                float patches = Smooth(Mathf.InverseLerp(0.52f, 0.7f, Mathf.PerlinNoise(px * 0.06f + 40f, pz * 0.06f + 3f)));
                float litter = Mathf.Max(trailWear, patches * 0.8f) * (1f - shore);
                float grass = Mathf.Max(0f, 1f - shore - litter);
                weights[z, x, 0] = grass;
                weights[z, x, 1] = litter;
                weights[z, x, 2] = shore;
            }
        }

        data.SetAlphamaps(0, 0, weights);
        TallGrass(data, weights, originX, originZ, sizeX, sizeZ);
        EditorUtility.SetDirty(data);

        GameObject ground = Terrain.CreateTerrainGameObject(data);
        ground.name = "Ground";
        ground.transform.SetParent(island, false);
        ground.transform.position = new Vector3(originX, bottom, originZ);
        Terrain terrain = ground.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 5f;
        terrain.basemapDistance = 300f;
        terrain.shadowCastingMode = ShadowCastingMode.On;
        // The terrain keeps the grass map but does not draw it (its detail renderer drew nothing here): TerrainGrass
        // draws it, out to 90 m (the aerial, 150 m out, sees the slopes' colour, the closer shots the tufts).
        terrain.detailObjectDistance = 0f;
        TerrainGrass tufts = ground.AddComponent<TerrainGrass>();
        SetField(tufts, "mesh", data.detailPrototypes[0].prototype.GetComponent<MeshFilter>().sharedMesh);
        SetField(tufts, "material", data.detailPrototypes[0].prototype.GetComponent<MeshRenderer>().sharedMaterial);
    }

    // Tall grass over the grassy ground, as terrain grass (crossed cards that sway in the wind): a tuft made from
    // photographed blades (SourceAssets/Tools/grass_tuft.py). Thick where the ground is grass, thin on leaf litter,
    // none on the shore sand, the path or the plateau's lawn by the door (the camera walks there).
    private static void TallGrass(TerrainData data, float[,,] weights, float originX, float originZ, float sizeX, float sizeZ)
    {
        string texturePath = ProjectPaths.Art + "/Textures/Island/grass_tuft.png";
        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp))
        {
            importer.alphaIsTransparency = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        // Drawn as an instanced mesh (three crossed cards), lit like everything else by the URP Lit shader: the
        // terrain's own billboard grass (DetailRenderMode.Grass) drew nothing in this project's URP setup (03/10).
        DetailPrototype grass = new DetailPrototype
        {
            prototype = GrassTuftPrefab(AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)),
            usePrototypeMesh = true,
            useInstancing = true,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = 0.7f,
            maxWidth = 1.3f,
            minHeight = 0.45f,
            maxHeight = 1.05f,
            noiseSpread = 0.4f,
            healthyColor = Color.white,
            dryColor = new Color(0.9f, 0.85f, 0.7f),
        };
        data.detailPrototypes = new[] { grass };
        const int resolution = 1024;
        data.SetDetailResolution(resolution, 16);
        data.wavingGrassStrength = 0.45f;
        data.wavingGrassSpeed = 0.6f;
        data.wavingGrassAmount = 0.5f;
        data.wavingGrassTint = new Color(0.72f, 0.78f, 0.62f);
        int splat = weights.GetLength(0);
        int[,] density = new int[resolution, resolution];
        Vector3[] trail = PathPoints();
        System.Random random = new System.Random(23);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int sx = Mathf.Min(splat - 1, x * splat / resolution);
                int sz = Mathf.Min(splat - 1, z * splat / resolution);
                float px = originX + (x + 0.5f) / resolution * sizeX;
                float pz = originZ + (z + 0.5f) / resolution * sizeZ;
                float cover = weights[sz, sx, 0] + weights[sz, sx, 1] * 0.25f;
                // Tufts only where the camera passes (within ~60 m of the path and the house): from the sea the
                // grass layer's colour does it; tufts over the whole island would be millions, and out to 100 m they
                // were 9.7 million vertices in the last shot (03/10).
                float fromTrail = DistanceToPolyline(new Vector3(px, 0f, pz), trail);
                cover *= 1f - Smooth(Mathf.InverseLerp(30f, 60f, Mathf.Min(fromTrail, Mathf.Max(0f, PlateauDistance(px, pz)))));
                if (cover <= 0f || PlateauDistance(px, pz) < 1f || Height(px, pz) < 1f || fromTrail < 1.3f)
                {
                    cover = 0f;
                }

                // Up to 2 tufts per cell (0.8 x 0.6 m).
                float wanted = cover * 2f;
                int n = Mathf.FloorToInt(wanted);
                if (random.NextDouble() < wanted - n)
                {
                    n++;
                }

                density[z, x] = n;
            }
        }

        data.SetDetailLayer(0, 0, 0, density);
    }

    // Three 1 x 1 m cards crossed at 60 degrees, pivot at the root; normals straight up, so a tuft is lit as one
    // soft mass (with the cards' own normals each card flashed light or dark as it turned to the moon).
    private static GameObject GrassTuftPrefab(Texture2D texture)
    {
        string meshPath = Folder + "/GrassTuft.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "GrassTuft" };
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> t = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            Vector3 side = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.right * 0.5f;
            int b = v.Count;
            v.AddRange(new[] { -side, side, side + Vector3.up, -side + Vector3.up });
            uv.AddRange(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }

        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.SetNormals(Enumerable.Repeat(Vector3.up, v.Count).ToList());
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        Material material = Mat("GrassTuft", new Color(0.78f, 0.86f, 0.66f), 0.2f);
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.45f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Cull", 0f);
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);

        string prefabPath = Folder + "/GrassTuft.prefab";
        GameObject go = new GameObject("GrassTuft");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static TerrainLayer GroundLayer(string id, float tile)
    {
        string folder = ProjectPaths.Art + "/Textures/Island/";
        string normal = folder + id + "_nor_gl_2k.jpg";
        if (AssetImporter.GetAtPath(normal) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        string path = folder + id + ".terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + id + "_diff_2k.jpg");
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
        layer.normalScale = 1f;
        layer.tileSize = new Vector2(tile, tile);
        // Wet with rain, but ground, not a mirror.
        layer.smoothness = 0.28f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    // Plants over the slopes (Poly Haven, CC0, cut to 1 to 2 thousand triangles each by SourceAssets/Tools/
    // foliage_reduce.py, which keeps the leaves' alpha cut-out): ferns and broad leaves where the ground is lower and
    // wetter, shrubs higher up, thinner along the path, none on the plateau or on the path itself. Only plants near the
    // path cast shadows (the porch and pier lights fall on them); the rest are scenery seen from the water.
    private static readonly (string name, float minScale, float maxScale, float weight)[] Plants =
    {
        ("Fern", 1.1f, 1.8f, 3f), ("Calathea", 0.9f, 1.4f, 1.2f), ("Sorrel", 1.4f, 2.2f, 1.5f),
        ("Shrub2", 0.9f, 1.4f, 1.6f), ("Shrub3", 1f, 1.6f, 2f), ("Shrub4", 0.9f, 1.4f, 1.6f),
        // The bushes that give the slopes their bulk from the sea (the low plants above vanish past 30 m).
        ("Searsia", 0.8f, 1.3f, 3.2f),
    };

    // The woods of the big island: island trees (island_tree_02, leaf cards, 4 thousand triangles) and bushes
    // (searsia_lucida, 2.5 thousand), each with a far version (900 and 500 triangles: IslandTreeFar, SearsiaFar) in a
    // LODGroup, on a jittered 9 m grid. Clearings where a broad noise is low, nothing on steep banks, the beach, the
    // house's plateau, the path or in front of the facade (they hid it in the shot from behind Erick, 03/10). Only
    // the ones near the path cast shadows.
    private static void Trees(Transform group, Vector3[] path)
    {
        System.Random random = new System.Random(31);
        int trees = 0, bushes = 0;
        const float step = 7.5f;
        for (float gz = -40f; gz < 480f; gz += step)
        {
            for (float gx = -340f; gx < 340f; gx += step)
            {
                Vector3 at = new Vector3(gx + ((float)random.NextDouble() - 0.5f) * step * 0.9f, 0f, gz + ((float)random.NextDouble() - 0.5f) * step * 0.9f);
                at.y = Height(at.x, at.z);
                float slope = Mathf.Max(Mathf.Abs(Height(at.x + 1.5f, at.z) - Height(at.x - 1.5f, at.z)), Mathf.Abs(Height(at.x, at.z + 1.5f) - Height(at.x, at.z - 1.5f))) / 3f;
                float woods = Mathf.PerlinNoise(at.x * 0.011f + 71f, at.z * 0.011f + 19f);
                bool blocksHouse = Mathf.Abs(at.x) < 20f && at.z < 0f && at.z > -26f;
                if (at.y < 2.5f || slope > 0.7f || woods < 0.34f || PlateauDistance(at.x, at.z) < 5f || DistanceToPolyline(at, path) < 7f || blocksHouse)
                {
                    continue;
                }

                // Denser where the noise is high: some cells left empty at the edges of the woods.
                if (random.NextDouble() > Mathf.InverseLerp(0.34f, 0.55f, woods) + 0.3f)
                {
                    continue;
                }

                bool tree = random.NextDouble() < 0.6;
                float scale = tree ? Mathf.Lerp(0.85f, 1.35f, (float)random.NextDouble()) : Mathf.Lerp(0.8f, 1.3f, (float)random.NextDouble());
                bool shadows = DistanceToPolyline(at, path) < 25f;
                if (Woody(group, tree ? "IslandTree" : "Searsia", tree ? "IslandTreeFar" : "SearsiaFar", at, (float)random.NextDouble() * 360f, scale, shadows))
                {
                    if (tree)
                    {
                        trees++;
                    }
                    else
                    {
                        bushes++;
                    }
                }
            }
        }

        Debug.Log("[Arrival] woods: " + trees + " trees, " + bushes + " bushes");
    }

    // A tree or bush with its far version: both placed the same, under one LODGroup (the near one under ~7% of the
    // screen's height gives way to the far one, which is culled under 0.5%).
    private static bool Woody(Transform group, string near, string far, Vector3 at, float yaw, float scale, bool shadows)
    {
        GameObject root = new GameObject(near);
        root.transform.SetParent(group, false);
        root.transform.position = at;
        GameObject lod0 = PlantObject(near, root.transform, at, yaw, scale, shadows);
        GameObject lod1 = PlantObject(far, root.transform, at, yaw, scale, false);
        if (lod0 == null)
        {
            Object.DestroyImmediate(root);
            return false;
        }

        LODGroup lods = root.AddComponent<LODGroup>();
        Renderer[] nearRenderers = lod0.GetComponentsInChildren<Renderer>();
        Renderer[] farRenderers = lod1 != null ? lod1.GetComponentsInChildren<Renderer>() : new Renderer[0];
        lods.SetLODs(new[] { new LOD(0.07f, nearRenderers), new LOD(0.005f, farRenderers) });
        lods.fadeMode = LODFadeMode.None;
        lods.RecalculateBounds();
        return true;
    }

    private static void Vegetation()
    {
        Transform group = Group("Vegetation");
        Vector3[] path = PathPoints();
        System.Random random = new System.Random(19);
        float total = Plants.Sum(p => p.weight);
        int placed = 0;
        for (int attempt = 0; attempt < 4000 && placed < 600; attempt++)
        {
            float a = (float)random.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Lerp(12f * 12f, 47f * 47f, (float)random.NextDouble()));
            Vector3 at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            float plateau = PlateauDistance(at.x, at.z);
            float fromPath = DistanceToPolyline(at, path);
            if (plateau < 1.5f || fromPath < 1.4f || (Mathf.Abs(at.x) < 2.5f && at.z < PierStart + 2f))
            {
                continue;
            }

            at.y = Height(at.x, at.z);
            if (at.y < 0.9f)
            {
                continue;
            }

            float pick = (float)random.NextDouble() * total;
            int index = 0;
            while (index < Plants.Length - 1 && pick > Plants[index].weight)
            {
                pick -= Plants[index].weight;
                index++;
            }

            var plant = Plants[index];
            // The tall bushes (up to 3 m) keep off the house's front, like the trees (Trees()).
            if ((plant.name == "Searsia" || plant.name == "Shrub2") && Mathf.Abs(at.x) < 20f && at.z < 0f && at.z > -26f)
            {
                continue;
            }

            float scale = Mathf.Lerp(plant.minScale, plant.maxScale, (float)random.NextDouble());
            // Thinner by the path: fewer, smaller.
            if (fromPath < 4f)
            {
                if (random.NextDouble() < 0.5)
                {
                    continue;
                }

                scale *= 0.8f;
            }

            if (Plant(plant.name, group, at, (float)random.NextDouble() * 360f, scale, fromPath < 7f))
            {
                placed++;
            }
        }

        Debug.Log("[Arrival] plants placed: " + placed);
        Trees(group, path);
    }

    private static float DistanceToPolyline(Vector3 point, Vector3[] line)
    {
        Vector2 p = new Vector2(point.x, point.z);
        float best = float.MaxValue;
        for (int i = 0; i < line.Length - 1; i++)
        {
            Vector2 a = new Vector2(line[i].x, line[i].z);
            Vector2 b = new Vector2(line[i + 1].x, line[i + 1].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            best = Mathf.Min(best, (a + ab * t - p).magnitude);
        }

        return best;
    }

    private static readonly Dictionary<string, Material> PlantMaterials = new Dictionary<string, Material>();

    private static bool Plant(string name, Transform parent, Vector3 position, float yaw, float scale, bool shadows)
    {
        return PlantObject(name, parent, position, yaw, scale, shadows) != null;
    }

    private static GameObject PlantObject(string name, Transform parent, Vector3 position, float yaw, float scale, bool shadows)
    {
        string folder = ProjectPaths.Models + "/Vegetation/" + name;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + ".fbx");
        if (asset == null)
        {
            Debug.LogWarning("[Arrival] missing plant " + name);
            return null;
        }

        GameObject plant = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        PrefabUtility.UnpackPrefabInstance(plant, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        plant.transform.SetPositionAndRotation(position - Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation);
        plant.transform.localScale = asset.transform.localScale * scale;
        foreach (MeshRenderer r in plant.GetComponentsInChildren<MeshRenderer>())
        {
            // One material per part of the plant (a tree has trunk, branches and leaves, each with its own maps;
            // the imported slot is named after the Poly Haven material, "<asset>_<part>").
            Material[] slots = r.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                string part = PlantPart(folder, name, slots[i] != null ? slots[i].name : "");
                string key = name + "/" + part;
                if (!PlantMaterials.TryGetValue(key, out Material material) || material == null)
                {
                    material = PlantMaterial(folder, name, part);
                    PlantMaterials[key] = material;
                }

                slots[i] = material;
            }

            r.sharedMaterials = slots;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        return plant;
    }

    // The textures written by foliage_reduce.py are <Name>_<part>_BaseColor.png; a slot whose part has no texture of
    // its own (the bushes share one atlas) uses "main".
    private static string PlantPart(string folder, string name, string slot)
    {
        int cut = slot.LastIndexOf('_');
        string part = cut >= 0 ? slot.Substring(cut + 1) : "";
        return part.Length > 0 && System.IO.File.Exists(folder + "/" + name + "_" + part + "_BaseColor.png") ? part : "main";
    }

    // URP Lit with alpha clip, both faces, the plant's own colour (alpha = the leaf outline) and normal map. The
    // model keeps its imported materials only for their names (PlantPart); they are replaced on every instance.
    private static Material PlantMaterial(string folder, string name, string part)
    {
        ModelImporter model = (ModelImporter)AssetImporter.GetAtPath(folder + "/" + name + ".fbx");
        if (model != null && (model.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription || model.importNormals != ModelImporterNormals.Import))
        {
            model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.SaveAndReimport();
        }

        string basePath = System.IO.Directory.GetFiles(folder, name + "_" + part + "_BaseColor.png").Select(p => p.Replace('\\', '/')).FirstOrDefault();
        string normalPath = System.IO.Directory.GetFiles(folder, name + "_" + part + "_Normal.*").Where(p => !p.EndsWith(".meta")).Select(p => p.Replace('\\', '/')).FirstOrDefault();
        if (basePath != null && AssetImporter.GetAtPath(basePath) is TextureImporter baseImporter && !baseImporter.mipMapsPreserveCoverage)
        {
            // Leaves stay as full at a distance (the mips keep the alpha coverage).
            baseImporter.alphaIsTransparency = true;
            baseImporter.mipMapsPreserveCoverage = true;
            baseImporter.alphaTestReferenceValue = 0.5f;
            baseImporter.SaveAndReimport();
        }

        if (normalPath != null && AssetImporter.GetAtPath(normalPath) is TextureImporter normalImporter && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        string path = folder + "/" + name + (part == "main" ? "" : "_" + part) + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", basePath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(basePath) : null);
        material.SetColor("_BaseColor", Color.white);
        if (normalPath != null)
        {
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.EnableKeyword("_NORMALMAP");
        }

        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;
        material.SetFloat("_Smoothness", 0.25f);
        material.enableInstancing = true;
        material.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(material);
        return material;
    }

    // From the end of the pier, along it, up the slope in a few turns, to the front door.
    private static Vector3[] PathPoints()
    {
        Vector3[] xz =
        {
            new Vector3(0f, 0f, PierEnd + 2.2f), new Vector3(0f, 0f, PierStart), new Vector3(-2.5f, 0f, -38f), new Vector3(-6f, 0f, -30f),
            new Vector3(-3f, 0f, -22f), new Vector3(3f, 0f, -15.5f), new Vector3(1.5f, 0f, -11.5f), new Vector3(0f, 0f, -9.6f), new Vector3(0f, 0f, DoorPoint.z - 1.6f),
        };
        for (int i = 0; i < xz.Length; i++)
        {
            xz[i].y = xz[i].z <= PierStart ? PierDeck : Height(xz[i].x, xz[i].z);
        }

        return xz;
    }

    private static void Pier()
    {
        Transform pier = Group("Pier");
        Material planks = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/DarkPlanks.mat");
        float length = PierStart - PierEnd;
        Box("Deck", pier, new Vector3(0f, PierDeck - 0.06f, (PierStart + PierEnd) * 0.5f), new Vector3(2.2f, 0.12f, length), planks, true);
        for (float z = PierEnd + 0.3f; z <= PierStart; z += 2.6f)
        {
            foreach (float x in new[] { -1.05f, 1.05f })
            {
                Box("Post", pier, new Vector3(x, PierDeck - 2.4f, z), new Vector3(0.24f, 4.8f, 0.24f), planks, false);
            }
        }

        // Mooring posts at the end, taller.
        Box("Bollard", pier, new Vector3(1.05f, PierDeck + 0.4f, PierEnd + 1.2f), new Vector3(0.22f, 0.8f, 0.22f), planks, false);
        Box("Bollard", pier, new Vector3(1.05f, PierDeck + 0.4f, PierEnd + 5f), new Vector3(0.22f, 0.8f, 0.22f), planks, false);
        // An old lamp post at the end of the pier, long dead (abandoned island).
        Box("LampPost", pier, new Vector3(-0.95f, PierDeck + 1.3f, PierEnd + 3f), new Vector3(0.12f, 2.6f, 0.12f), planks, false);
        Box("PierLamp", pier, new Vector3(-0.95f, PierDeck + 2.65f, PierEnd + 3f), new Vector3(0.2f, 0.26f, 0.2f), AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/DarkMetal.mat"), false);

        // Where the pier meets the land: a ramp of boards onto the slope.
        float land = Height(0f, PierStart + 1.5f);
        Transform ramp = Box("Ramp", pier, new Vector3(0f, (PierDeck + land) * 0.5f - 0.05f, PierStart + 0.9f), new Vector3(2.2f, 0.1f, 2.2f), planks, true);
        ramp.rotation = Quaternion.Euler(-Mathf.Atan2(land - PierDeck, 2.2f) * Mathf.Rad2Deg, 0f, 0f);
    }

    // ---- the mansion ---------------------------------------------------------------------------------------------

    // The Luna mansion of Dayse's floor plan (GDD "Planta da Casa", 11/10), the same builder as the game's house
    // (LunaManor): its front door stays where the opening's door was (centre of the wall at z -5.85, on the porch
    // 36 cm over the plateau), the house runs 38 m north from it and 43 m across; only the shell (no partitions).
    private static void Mansion()
    {
        Transform house = Group("Mansion");
        Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/" + name + ".mat");
        Material trim = Load("PolishedWood");
        // Old glass (04/10: the windows were flat black boxes): smooth and dark, it shows the night sky and the glow
        // round the moon.
        Material glass = Mat("WindowGlass", new Color(0.018f, 0.021f, 0.025f), 0.94f);
        Material garden = Mat("CourtyardGround", new Color(0.07f, 0.08f, 0.05f), 0.05f);
        // The same aged surfaces as the game's house (FloodedHouseManor builds the materials); before the house scene was
        // rebuilt once they fall back to the old plaster.
        Material S(string set) => Load(set) ?? Load("CleanPlaster");
        LunaManor.Palette palette = new LunaManor.Palette
        {
            wall = S("MossyStucco"), plaster = S("WornPlaster"), wallpaper = S("OldWallpaper"), azulejo = S("WallTiles"), ceiling = S("InteriorCeiling"),
            floor = S("Parquet"), tile = S("KitchenFloor"), stone = S("WhiteStucco"), concrete = S("WornConcreteFloor"), roof = S("RoofTiles"),
            trim = trim, glass = glass, garden = garden, metal = Load("DarkMetal"),
            marble = S("MarbleTiles"), woodFloor = S("WornWoodFloor"), terracotta = S("TerracottaFloor"), mosaic = S("MosaicFloor"),
            bathFloor = S("BathMosaic"), bathWall = S("BathWallTiles"), panels = S("DarkPanels"), peeling = S("PeelingWall"),
            service = S("RoughPlaster"), roughConcrete = S("RoughConcrete"), roofFlat = S("RoughConcrete"),
            tiling = m => m != null && LunaManor.SurfaceTiles.TryGetValue(m.name, out float t) ? t : 2.5f,
        };
        // The concept art's facade, roofs, stained glass and pediment (GDD, 13/10), the same as the game's house.
        palette = FloodedHouseBuilder.WithHouseArt(palette);

        float y = Plateau + 0.36f;
        LunaManor.Build(house, new Vector3(0f, y, DoorPlane), palette, new LunaManor.Options { footing = 1.4f, interior = false });
        FrontDoor(house, trim, y);

        // Aim points for the cameras.
        // An upstairs window of the west wing (dark, like all of them: the house is abandoned).
        Marker("WindowTarget", new Vector3(-12.2f, y + LunaManor.UpperFloor + 1.6f, DoorPlane + 8.9f));
        Marker("HouseTarget", new Vector3(0f, y + 4.5f, -4f));
        Marker("IslandTarget", new Vector3(0f, 6f, -6f));
        // The aerial aims above the island (04/10): the hills in the middle of the frame, the moon near its top and the
        // boat's lamp still in the lower third.
        Marker("AerialAim", new Vector3(0f, 24f, -6f));
        Marker("DoorTarget", DoorPoint + new Vector3(0f, 1.5f, 0f));
    }

    // Where his right hand closes on the door's handle: the grip for ActorReach (forward = the way the fingers point
    // round the handle, to his left; up = out of the back of his hand, toward him) and where he stands for it.
    // Measured on the door model (SourceAssets/Tools/door_handle2.py on the original): the right leaf's curved pull
    // bar runs from the middle (x 0.05, 1.19 m up) down and out to x 0.6 (0.89 m), 8 cm proud of the door's
    // centre plane; its middle, at x 0.3, is 1.06 m up. The door stands in the middle of the wall (DoorPlane).
    // The right leaf's knob of the arched door (arched_door_placeholder.py: knob at x 0.11, 1.05 m up, 10.7 cm proud of
    // the leaf), seen from the porch (he faces +z). Was the carved door's bar at x 0.3, 1.057 m, 8 cm proud.
    private static readonly Vector3 HandlePoint = new Vector3(0.11f, Plateau + 0.36f + 1.05f, DoorPlane - 0.107f);
    private static readonly Vector3 StandPoint = new Vector3(-0.02f, Plateau + 0.36f, DoorPlane - 0.55f);

    // The door sits in the middle of the front wall's thickness (z -6 to -5.7).
    private const float DoorPlane = -5.85f;

    // The front door, in its doorway, and the handle he grips at the end. "Classic Wooden Double Door" by Mark
    // Peters (Sketchfab, CC-BY; CREDITS.md), in two parts for a door seen from a hand's length (Gustavo 03/10: baked
    // whole to 6250 triangles, its handles were square): the leaves with their pull handles keep their own geometry
    // (only flat areas thinned, 27 to 13.8 thousand triangles) and the original 4k maps, the glass see-through
    // (SourceAssets/Tools/door_parts.py); the carved frame is baked down to 5 thousand by the game-3d-assets skill.
    // Both FBX have the door's base centre at their origin. Without the models, a blockout leaf with a brass bar.
    private static void FrontDoor(Transform house, Material wood, float floor)
    {
        Transform grip = Marker("DoorGrip", HandlePoint);
        // Fingers over the bar and down behind it, across the bar's 24-degree slope; back of the hand toward him.
        grip.rotation = Quaternion.LookRotation(new Vector3(-0.41f, -0.91f, 0f), Vector3.back);
        // Where the first-person camera aims: just above the handle, the way one looks down at a door handle to take
        // it (aimed higher, with his eyes 30 to 40 cm from the door, the handle and his hand were out of the frame).
        Marker("DoorLook", HandlePoint + new Vector3(-0.1f, 0.22f, 0f));
        Vector3 at = new Vector3(0f, floor, DoorPlane);
        if (ArchedDoor(house, at))
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/MansionDoorFrame/MansionDoorFrame.fbx") != null)
        {
            GameObject frame = FloodedHouseBuilder.ModelProp("MansionDoorFrame", house, at, Quaternion.identity, false, false);
            frame.name = "FrontDoorFrame";
            frame.transform.position = at;
        }

        if (DoorLeaves(house, at) != null)
        {
            return;
        }

        Box("Door", house, new Vector3(0f, floor + 1.25f, DoorPlane), new Vector3(1.5f, 2.5f, 0.08f), wood, false);
        Material brass = Mat("DoorBrass", new Color(0.55f, 0.42f, 0.2f), 0.7f);
        brass.SetFloat("_Metallic", 1f);
        Box("Handle", house, HandlePoint, new Vector3(0.025f, 0.3f, 0.025f), brass, false);
    }

    // The same front door in the game's house (FloodedHouseManor): the carved frame and the leaves, base centre at `at`
    // (the middle of the front wall's thickness). False when the models are not in the project.
    public static bool FrontDoorModels(Transform parent, Vector3 at)
    {
        if (ArchedDoor(parent, at))
        {
            return true;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/MansionDoorFrame/MansionDoorFrame.fbx") == null)
        {
            return false;
        }

        GameObject frame = FloodedHouseBuilder.ModelProp("MansionDoorFrame", parent, at, Quaternion.identity, false, false);
        frame.name = "FrontDoorFrame";
        frame.transform.position = at;
        return DoorLeaves(parent, at) != null;
    }

    // The round-arched double door of Dayse's facade (GDD 13/10), one model with its frame, leaves and knobs, base
    // centre at `at`, both sides alike. Today a Blender placeholder (SourceAssets/Tools/arched_door_placeholder.py,
    // 16/10: the Sketchfab candidate needed a login to download); the real model goes in the same folder.
    private static bool ArchedDoor(Transform parent, Vector3 at)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/ArchedDoor/ArchedDoor.fbx") == null)
        {
            return false;
        }

        GameObject door = FloodedHouseBuilder.ModelProp("ArchedDoor", parent, at, Quaternion.identity, false, false);
        door.name = "FrontDoorArched";
        door.transform.position = at;
        return true;
    }

    // The leaves with the original materials: EntranceDoorsMain (wood and brass, its own metal and smoothness map)
    // and EntranceDoorsGlass (see-through).
    private static GameObject DoorLeaves(Transform house, Vector3 at)
    {
        string folder = ProjectPaths.PropModels + "/MansionDoorLeaves";
        string path = folder + "/MansionDoorLeaves.fbx";
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            return null;
        }

        if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription || importer.importNormals != ModelImporterNormals.Import)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
        }

        foreach (string map in new[] { "Main", "Glass" })
        {
            if (AssetImporter.GetAtPath(folder + "/MansionDoor_" + map + "_Normal.png") is TextureImporter n && n.textureType != TextureImporterType.NormalMap)
            {
                n.textureType = TextureImporterType.NormalMap;
                n.SaveAndReimport();
            }

            if (AssetImporter.GetAtPath(folder + "/MansionDoor_" + map + "_MetallicSmoothness.png") is TextureImporter m && m.sRGBTexture)
            {
                m.sRGBTexture = false;
                m.SaveAndReimport();
            }
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject leaves = (GameObject)PrefabUtility.InstantiatePrefab(asset, house);
        PrefabUtility.UnpackPrefabInstance(leaves, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        leaves.name = "FrontDoorLeaves";
        leaves.transform.SetPositionAndRotation(at, asset.transform.localRotation);
        Material main = DoorMaterial(folder, "Main", false);
        Material glass = DoorMaterial(folder, "Glass", true);
        foreach (MeshRenderer r in leaves.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] slots = r.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = slots[i] != null && slots[i].name.Contains("Glass") ? glass : main;
            }

            r.sharedMaterials = slots;
        }

        return leaves;
    }

    private static Material DoorMaterial(string folder, string map, bool transparent)
    {
        Material m = Mat("MansionDoor" + map, Color.white, 1f);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/MansionDoor_" + map + "_BaseColor.png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/MansionDoor_" + map + "_Normal.png"));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/MansionDoor_" + map + "_MetallicSmoothness.png"));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        if (transparent)
        {
            // Old dirty glass: see-through, a little dark; the reflection at its true strength (URP's "preserve
            // specular" made the house's windows glare, CLAUDE.md).
            m.SetColor("_BaseColor", new Color(0.55f, 0.6f, 0.62f, 0.45f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- the sea, sky, storm -----------------------------------------------------------------------------------

    private static void Atmosphere()
    {
        Transform lights = Group("Lighting");
        // A moonlit storm sky (AllSkyFree "Night MoonBurst": the moon low behind broken cloud). Since 04/10 the moon
        // stands in front of the cameras, north-north-east behind the island (Gustavo: "put the moon in this shot";
        // "it is night but you can see everything"): the island and the house are seen against the bright sky round
        // the moon, lit from behind, their faces in the dark, as a real moonlit night reads. MoonAzimuth below.
        RenderSettings.skybox = NightSky(MoonAzimuth);
        // Ambient: what a heavily overcast night sky gives, about a third of what it was (the slopes and the facade
        // read as dusk at 0.2/0.25/0.34; at a quarter the shots facing away from the moon were black, 04/10).
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.07f, 0.085f, 0.115f);
        RenderSettings.ambientEquatorColor = new Color(0.045f, 0.052f, 0.068f);
        RenderSettings.ambientGroundColor = new Color(0.012f, 0.013f, 0.016f);
        RenderSettings.reflectionIntensity = 0.3f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        // Night air: dark, a little lighter than black, so the far hills sink into it instead of turning milky.
        RenderSettings.fogColor = new Color(0.03f, 0.037f, 0.05f);
        RenderSettings.fogDensity = 0.0024f;

        // The moon's light comes from where it is drawn (24 degrees up, see NightSky). Dim and cold, and through
        // cloud: its shadows are soft and not black.
        Light moon = Directional("Moon", lights, Quaternion.Euler(26f, MoonAzimuth - 180f, 0f), new Color(0.55f, 0.65f, 0.86f), 0.6f);
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.75f;
        RenderSettings.sun = moon;
        // The night fill of film lighting: a weak, cold, shadowless light from the cameras' side (south-south-west).
        // With the moon behind the island, the house's front, the pier and the path face away from it, and against
        // the island's own dark hills they do not even make a silhouette: without this the house was 7 of 255 (04/10).
        Light fill = Directional("MoonFill", lights, Quaternion.Euler(32f, 200f - 180f, 0f), new Color(0.48f, 0.56f, 0.78f), 0.2f);
        fill.shadows = LightShadows.None;
        Light flash = Directional("Lightning", lights, Quaternion.Euler(48f, 200f, 0f), new Color(0.78f, 0.85f, 1f), 0f);
        flash.shadows = LightShadows.Soft;

        GameObject stormObject = new GameObject("Storm");
        stormObject.transform.SetParent(root, false);
        LightningStorm storm = stormObject.AddComponent<LightningStorm>();
        AudioSource thunder = stormObject.AddComponent<AudioSource>();
        thunder.spatialBlend = 0f;
        SetField(storm, "flashLights", new[] { flash });
        // No rainSource: the storm would synthesise its rain (noise, which read as static). The rain and the sea are
        // recorded loops below, kept under the music (Gustavo 03/10: "too loud, sounds like static").
        SetField(storm, "thunderSource", thunder);
        SetVector2(storm, "intervalSeconds", new Vector2(14f, 26f));
        SetFloat(storm, "flashIntensity", 4.5f);

        // Rain around the camera.
        GameObject rainObject = new GameObject("Rain");
        rainObject.transform.SetParent(root, false);
        ParticleSystem ps = rainObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 1.3f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.03f);
        main.startColor = new Color(0.62f, 0.68f, 0.75f, 0.35f);
        main.maxParticles = 9000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 5200f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(36f, 0.1f, 36f);
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-1.4f, -0.7f);
        velocity.y = new ParticleSystem.MinMaxCurve(-15f, -13f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        ParticleSystemRenderer pr = rainObject.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.05f;
        pr.lengthScale = 2f;
        pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/Particle.mat");
        pr.shadowCastingMode = ShadowCastingMode.Off;
        rainObject.AddComponent<CameraRain>();

        // Rain on open water and the sea at night, recorded, quiet, under everything.
        Loop(stormObject, SoundLibrary.Instance.rainOutdoorLoop, 0.14f);
        Loop(stormObject, SoundLibrary.Instance.seaNightLoop, 0.18f);

        // Post: a night grade of its own (04/10; it used the house's, whose split toning turned the facade
        // yellow-green and the whole frame one blue), and a sea mist that the moon and the lamps light.
        GameObject post = new GameObject("PostProcess");
        post.transform.SetParent(root, false);
        Volume volume = post.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = NightProfile();
    }

    // The night grade, built here so it can be regenerated (Settings/Rendering/ArrivalNight_Volume.asset):
    //   ACES; the eye at night (scotopic vision): colour drains away and what is left goes blue-grey, so the
    //   saturation drops and the white balance cools; a little more contrast so silhouettes read;
    //   bloom only from real light sources (threshold above 1, no lens dirt: the dirt lit up the whole frame);
    //   a sea mist (volumetric fog, MIT package) thick at the water and thinning to nothing by 22 m up: the low
    //   shots sit in it, the house on its hill stands above it, the moon and the lamps glow in it.
    private static VolumeProfile NightProfile()
    {
        string path = ProjectPaths.RenderingSettings + "/ArrivalNight_Volume.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        Tonemapping tone = Override<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.ACES);
        ColorAdjustments colour = Override<ColorAdjustments>(profile);
        colour.postExposure.Override(0.35f);
        colour.contrast.Override(14f);
        colour.saturation.Override(-38f);
        colour.colorFilter.Override(new Color(0.9f, 0.95f, 1f));
        WhiteBalance balance = Override<WhiteBalance>(profile);
        balance.temperature.Override(-14f);
        balance.tint.Override(4f);
        LiftGammaGain lgg = Override<LiftGammaGain>(profile);
        // Shadows lifted a hair toward blue-grey (never pure black on a screen), highlights left neutral.
        lgg.lift.Override(new Vector4(0.98f, 1f, 1.04f, 0.012f));
        Bloom bloom = Override<Bloom>(profile);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.72f);
        bloom.dirtIntensity.Override(0f);
        Vignette vignette = Override<Vignette>(profile);
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.45f);
        FilmGrain grain = Override<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.28f);
        grain.response.Override(0.75f);

        VolumetricFogVolumeComponent mist = Override<VolumetricFogVolumeComponent>(profile);
        mist.enabled.Override(true);
        mist.distance.Override(200f);
        mist.baseHeight.Override(0f);
        mist.maximumHeight.Override(22f);
        mist.enableGround.Override(false);
        mist.density.Override(0.14f);
        mist.attenuationDistance.Override(90f);
        mist.enableMainLightContribution.Override(true);
        // Toward the moon the mist glows (forward scattering), away from it it only veils. Kept faint: at scattering
        // 0.35 and anisotropy 0.72 the glow round the moon was a milky wall that hid the island and the house (04/10).
        mist.anisotropy.Override(0.25f);
        mist.scattering.Override(0.15f);
        mist.tint.Override(new Color(0.75f, 0.85f, 1f));
        mist.enableAdditionalLightsContribution.Override(true);
        mist.maxSteps.Override(48);
        mist.blurIterations.Override(2);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
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

    // Compass bearing of the moon (0 = +Z, 90 = +X): north-north-east, behind the island seen from the sea, so it
    // is in the frame of the aerial (left of the island) and of the over-the-shoulder shot (right of the house), and
    // the house is in its backlight on the way up. It was at 235 (behind the cameras): everything lit full face.
    private const float MoonAzimuth = 20f;

    // A copy of the pack's cubemap sky turned so its moon stands at `azimuth` (measured: at _Rotation 0 the moon is
    // at bearing 0 and 9 degrees up; _Rotation r puts it at bearing -r), a little brighter than the pack's.
    private static Material NightSky(float azimuth)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThirdParty/AllSkyFree/Night MoonBurst/AllSky_Night_MoonBurst Equirect.mat");
        string path = MaterialFolder + "/ArrivalNightSky.mat";
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

        // The pack's sky turned 15 degrees about the east-west axis (SourceAssets/Tools/sky_tilt.py): its moon stands
        // at 24 degrees instead of 9, clear of the island's hills from the sea (04/10: it hid behind them).
        Texture lifted = AssetDatabase.LoadAssetAtPath<Texture>(ProjectPaths.Textures + "/Sky/ArrivalNightSky.png");
        if (lifted != null)
        {
            sky.SetTexture("_Tex", lifted);
        }

        sky.SetFloat("_Rotation", Mathf.Repeat(360f - azimuth, 360f));
        // 0.85 (04/10): at 2.2 the sky round the moon was a daylight overcast and lit the sea and the mist like day;
        // with the moon in the frame the dark half no longer matters.
        sky.SetFloat("_Exposure", 0.85f);
        EditorUtility.SetDirty(sky);
        return sky;
    }

    // The sea from 300 m out to the horizon (9 km): rings of growing width round the origin (the near grid covers
    // +-320 m), 3 cm under the near grid where they overlap; the same material, so the same waves, which fade out with
    // distance anyway. Without it the sea ended at 320 m and the lower, black half of the sky showed between its edge
    // and the horizon, a dark band that read as land (03/10).
    private static void SeaRing(Material sea)
    {
        const int segments = 160;
        List<float> radii = new List<float>();
        for (float r = 300f; r < 9000f; r *= 1.07f)
        {
            radii.Add(r);
        }

        radii.Add(9000f);
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        foreach (float r in radii)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * r, -0.03f, Mathf.Sin(a) * r));
            }
        }

        for (int ring = 0; ring < radii.Count - 1; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                int a = ring * segments + i;
                int b = ring * segments + (i + 1) % segments;
                int c = a + segments;
                int d = b + segments;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
        }

        Mesh mesh = new Mesh { name = "ArrivalSeaRing", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.SetNormals(Enumerable.Repeat(Vector3.up, v.Count).ToList());
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(18000f, 12f, 18000f));
        GameObject go = new GameObject("SeaRing");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = sea;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // The sky cubemap, its turn, exposure and tint, from the scene's skybox into a material that draws the sky
    // itself (StormSea, DistantLand).
    private static void SkyInto(Material m)
    {
        Material sky = RenderSettings.skybox;
        if (sky == null || !sky.HasProperty("_Tex"))
        {
            return;
        }

        m.SetTexture("_SkyCube", sky.GetTexture("_Tex"));
        m.SetFloat("_SkyRotation", sky.GetFloat("_Rotation"));
        m.SetFloat("_SkyExposure", sky.GetFloat("_Exposure"));
        m.SetColor("_SkyTint", sky.GetColor("_Tint"));
    }

    // The mainland on the horizon (the island is off the coast, in Brazilian waters): two ranges of mountains from
    // south-south-west round to north-west, 8.5 to 15 km out, 150 to 650 m high. Seen behind the boat from the pier
    // and from his face at the tiller, under the moon. Ridged noise along the skyline; the far range paler (DistantLand
    // hazes by distance toward the sky behind).
    private static void Mainland()
    {
        Material land = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/DistantLand.mat");
        if (land == null)
        {
            land = new Material(Shader.Find("Horror/DistantLand"));
            AssetDatabase.CreateAsset(land, MaterialFolder + "/DistantLand.mat");
        }

        SkyInto(land);
        land.SetFloat("_Haze", 0.00007f);
        EditorUtility.SetDirty(land);
        GameObject go = new GameObject("Mainland");
        go.transform.SetParent(root, false);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = land;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        // (distance, base height, peak height, noise seed) for each range.
        (float distance, float low, float high, float seed)[] ranges = { (8500f, 120f, 380f, 3f), (14500f, 250f, 650f, 11f) };
        const int rows = 6;
        foreach (var range in ranges)
        {
            int start = v.Count;
            int columns = 0;
            for (float bearing = 190f; bearing <= 340f; bearing += 0.35f, columns++)
            {
                float a = (90f - bearing) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float u = bearing * 0.06f + range.seed;
                float ridge = 0f, amp = 1f, freq = 1f, sum = 0f;
                for (int o = 0; o < 5; o++)
                {
                    ridge += (1f - Mathf.Abs(Mathf.PerlinNoise(u * freq, range.seed + o * 3.1f) * 2f - 1f)) * amp;
                    sum += amp;
                    amp *= 0.5f;
                    freq *= 2.3f;
                }

                ridge /= sum;
                // Lower toward both ends of the arc, as a coast falls away.
                float ends = Mathf.Sin(Mathf.InverseLerp(190f, 340f, bearing) * Mathf.PI);
                float peak = Mathf.Lerp(range.low, range.high, ridge * ridge) * Mathf.Lerp(0.45f, 1f, ends);
                float dist = range.distance + (Mathf.PerlinNoise(u * 0.5f, range.seed + 9f) - 0.5f) * 2500f;
                for (int row = 0; row <= rows; row++)
                {
                    float k = row / (float)rows;
                    // From under the horizon up the slope to the skyline, leaning back (the far side of the ridge).
                    v.Add(dir * (dist + k * k * 900f) + Vector3.up * Mathf.Lerp(-60f, peak, Mathf.Sqrt(k)));
                }
            }

            for (int c = 0; c < columns - 1; c++)
            {
                for (int row = 0; row < rows; row++)
                {
                    int a = start + c * (rows + 1) + row;
                    int b = a + rows + 1;
                    t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            }
        }

        Mesh mesh = new Mesh { name = "Mainland", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
    }

    private static void Loop(GameObject owner, AudioClip clip, float volume)
    {
        if (clip == null)
        {
            Debug.LogWarning("[Arrival] a loop is missing from the SoundLibrary (Horror > Som > Atualizar biblioteca de sons)");
            return;
        }

        AudioSource source = owner.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f;
        source.volume = volume;
    }

    private static Renderer Sea()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/StormSea.mat");
        string path = MaterialFolder + "/ArrivalSea.mat";
        Material sea = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sea == null)
        {
            sea = new Material(source);
            AssetDatabase.CreateAsset(sea, path);
        }
        else
        {
            sea.CopyPropertiesFromMaterial(source);
        }

        // Little chop left by the rocks: the water at the pier is sheltered.
        sea.SetFloat("_ShoreChop", 0.2f);
        // Wind-wave ripples from a wave spectrum (SourceAssets/Tools/ocean_normals.py), at their own scale (a 24 m
        // tile): the house's ripple map drew a net of bright lines, like a swimming pool's caustics (03/10).
        string ocean = ProjectPaths.Art + "/Textures/Ocean/ocean_normal.png";
        if (AssetImporter.GetAtPath(ocean) is TextureImporter oceanImporter && oceanImporter.textureType != TextureImporterType.NormalMap)
        {
            oceanImporter.textureType = TextureImporterType.NormalMap;
            oceanImporter.SaveAndReimport();
        }

        sea.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ocean));
        sea.SetFloat("_RippleTiling", 1f / 24f);
        // 0.4 and a rough surface (04/10): at 0.55 on a mirror-smooth sea every ripple threw a sharp bit of bright
        // sky back, and the near water was a net of white lines. Storm water is rough: the sky smears in it.
        sea.SetFloat("_RippleStrength", 0.4f);
        sea.SetFloat("_Roughness", 0.17f);
        sea.SetFloat("_FarRoughness", 0.32f);
        sea.SetFloat("_ReflectStrength", 0.85f);
        sea.SetColor("_DeepColor", new Color(0.01f, 0.022f, 0.028f));
        sea.SetColor("_CrestColor", new Color(0.035f, 0.07f, 0.075f));
        sea.SetColor("_Scatter", new Color(0.015f, 0.05f, 0.048f));
        sea.SetColor("_FoamColor", new Color(0.5f, 0.54f, 0.56f));
        sea.SetTexture("_FoamTex", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/foam_patch.png"));
        sea.SetFloat("_FoamTiling", 0.11f);
        // The moon is behind broken cloud: a dim, wide glint (a glitter path, not white blobs by the camera).
        sea.SetFloat("_GlintStrength", 0.25f);
        sea.SetFloat("_GlintRoughness", 0.26f);
        FloodedHouseBuilder.SeaDetail(sea);
        // The scene's own sky in the water and at the horizon (StormSea, _SkyOn), turned like the skybox.
        SkyInto(sea);
        sea.SetFloat("_SkyOn", 1f);
        sea.SetFloat("_HorizonFog", 0.0011f);
        sea.SetVector("_ChopFade", new Vector4(120f, 450f, 0f, 0f));
        EditorUtility.SetDirty(sea);

        GameObject go = new GameObject("Sea");
        go.transform.SetParent(root, false);
        const float size = 640f;
        // 1.6 m: at 3.2 the shading showed the big triangles near the boat.
        const float cell = 1.6f;
        int n = Mathf.CeilToInt(size / cell);
        Vector3[] v = new Vector3[(n + 1) * (n + 1)];
        for (int z = 0; z <= n; z++)
        {
            for (int x = 0; x <= n; x++)
            {
                v[z * (n + 1) + x] = new Vector3(-size * 0.5f + x * cell, 0f, -size * 0.5f + z * cell);
            }
        }

        int[] t = new int[n * n * 6];
        int k = 0;
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x;
                t[k++] = a; t[k++] = a + n + 1; t[k++] = a + 1;
                t[k++] = a + 1; t[k++] = a + n + 1; t[k++] = a + n + 2;
            }
        }

        Mesh mesh = new Mesh { name = "ArrivalSea", indexFormat = IndexFormat.UInt32, vertices = v, triangles = t };
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 12f, size));
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = sea;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        SeaWaves waves = go.AddComponent<SeaWaves>();
        // Calm round the big island (an oval about 330 x 255 m round BigCentre): the swell builds from 300 to 520 m.
        waves.OverrideIsland(BigCentre, 300f, 520f);
        SeaRing(sea);
        return renderer;
    }

    // ---- the boat and Erick ------------------------------------------------------------------------------------

    // An open aluminium boat, 4.2 m, with an outboard at the stern and a lamp on the bow. Erick sits at the stern
    // with the tiller. Returns the hull (the child that rides the waves); boat = the part moved along the course.
    private static Transform Boat(Renderer sea, out Transform boat, out Animator erick)
    {
        boat = new GameObject("Boat").transform;
        boat.SetParent(root, false);
        Transform hull = new GameObject("Hull").transform;
        hull.SetParent(boat, false);
        // Dull grey aluminium, drawn on both faces (the shell is a single sheet).
        Material hullMaterial = Mat("BoatHull", new Color(0.19f, 0.2f, 0.21f), 0.35f);
        hullMaterial.SetFloat("_Metallic", 0.55f);
        hullMaterial.SetFloat("_Cull", 0f);
        hullMaterial.doubleSidedGI = true;

        GameObject shell = new GameObject("Shell");
        shell.transform.SetParent(hull, false);
        shell.AddComponent<MeshFilter>().sharedMesh = HullMesh();
        shell.AddComponent<MeshRenderer>().sharedMaterial = hullMaterial;
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/DarkPlanks.mat");
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.FloodedHouseMaterials + "/DarkMetal.mat");
        Box("SternSeat", hull, hull.TransformPoint(0f, 0.18f, -1.3f), new Vector3(1.35f, 0.06f, 0.36f), wood, false);
        Box("BowSeat", hull, hull.TransformPoint(0f, 0.18f, 0.9f), new Vector3(1.2f, 0.06f, 0.3f), wood, false);
        Box("Floor", hull, hull.TransformPoint(0f, -0.2f, -0.2f), new Vector3(0.9f, 0.04f, 3.2f), wood, false);
        Box("Motor", hull, hull.TransformPoint(0f, 0.5f, -2.32f), new Vector3(0.36f, 0.5f, 0.42f), dark, false);
        Box("MotorShaft", hull, hull.TransformPoint(0f, -0.15f, -2.36f), new Vector3(0.1f, 0.9f, 0.14f), dark, false);
        Transform tiller = Box("Tiller", hull, hull.TransformPoint(0f, 0.55f, -1.85f), new Vector3(0.05f, 0.05f, 0.8f), dark, false);
        tiller.localRotation = Quaternion.Euler(-8f, 0f, 0f);

        // The lamp on the bow: the only warm light on the water.
        // A hot bulb (above the bloom threshold) so the boat reads as a point of light from 40 m out.
        Material bulb = Mat("BoatLampBulb", new Color(1f, 0.75f, 0.45f), 0.3f);
        bulb.EnableKeyword("_EMISSION");
        bulb.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.4f) * 9f);
        bulb.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Transform lamp = Box("BowLamp", hull, hull.TransformPoint(0f, 0.72f, 1.95f), new Vector3(0.2f, 0.24f, 0.2f), bulb, false);
        // Its pole, down to the bow (without it the lamp hung in the air).
        Box("BowLampPole", hull, hull.TransformPoint(0f, 0.42f, 1.95f), new Vector3(0.04f, 0.4f, 0.04f), dark, false);
        Light light = new GameObject("LampLight").AddComponent<Light>();
        light.transform.SetParent(lamp, false);
        light.transform.localPosition = new Vector3(0f, 0.25f, 0.6f);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.68f, 0.38f);
        light.intensity = 2.2f;
        // 14 m (was 6.5): the lamp's column on the water (StormSea lights the sea with the additional lights since
        // 04/10) reaches the camera only if the light does.
        light.range = 14f;
        light.shadows = LightShadows.Soft;
        // Its glow in the sea mist.
        VolumetricAdditionalLight glow = light.gameObject.AddComponent<VolumetricAdditionalLight>();
        SetFloat(glow, "scattering", 0.55f);
        SetFloat(glow, "anisotropy", 0.35f);
        SetFloat(glow, "radius", 0.1f);

        LampHalo(lamp, 1.4f);

        WaveRider rider = hull.gameObject.AddComponent<WaveRider>();
        SetField(rider, "sea", sea);
        hull.gameObject.AddComponent<HullWaterMask>();
        AudioSource motorSound = boat.gameObject.AddComponent<AudioSource>();
        motorSound.spatialBlend = 1f;
        motorSound.minDistance = 4f;
        motorSound.maxDistance = 90f;
        BoatMotor motor = boat.gameObject.AddComponent<BoatMotor>();
        if (SoundLibrary.Instance.outboardMotorLoop != null)
        {
            SetField(motor, "recorded", SoundLibrary.Instance.outboardMotorLoop);
        }

        Wake(boat, rider);

        erick = Erick("Erick_Boat", hull, new Vector3(0f, -0.22f, -1.38f), true);
        Marker("ErickHeadOnBoat", Vector3.zero).SetParent(erick.GetBoneTransform(HumanBodyBones.Head), false);
        return hull;
    }

    // The wake: foam laid behind the stern (by distance, so it stops with the boat), opening into a V and fading,
    // kept on the waves by BoatWake; and spray thrown up at the bow at speed. Lit particles: the moon, the lamp and
    // the lightning light them, they do not glow by themselves at night.
    private static void Wake(Transform boat, WaveRider rider)
    {
        string path = MaterialFolder + "/WakeFoam.mat";
        Material foamMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (foamMaterial == null)
        {
            foamMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            AssetDatabase.CreateAsset(foamMaterial, path);
        }

        // A photo of a real wake (freestocktextures.com, CC0), its white turned into the alpha (see CREDITS.md).
        string foamTexture = ProjectPaths.Vfx + "/Textures/foam_patch.png";
        if (AssetImporter.GetAtPath(foamTexture) is TextureImporter foamImporter && !foamImporter.alphaIsTransparency)
        {
            foamImporter.alphaIsTransparency = true;
            foamImporter.wrapMode = TextureWrapMode.Clamp;
            foamImporter.SaveAndReimport();
        }

        foamMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(foamTexture));
        foamMaterial.SetColor("_BaseColor", new Color(0.85f, 0.9f, 0.92f, 0.85f));
        // Foam is the whitest thing on a night sea: a little of its own light so it reads under a weak moon.
        foamMaterial.EnableKeyword("_EMISSION");
        foamMaterial.SetColor("_EmissionColor", new Color(0.07f, 0.08f, 0.09f));
        foamMaterial.SetFloat("_Surface", 1f);
        foamMaterial.SetFloat("_Blend", 0f);
        foamMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        foamMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        foamMaterial.SetFloat("_ZWrite", 0f);
        foamMaterial.SetFloat("_Smoothness", 0.2f);
        foamMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        foamMaterial.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(foamMaterial);

        GameObject foamObject = new GameObject("WakeFoam");
        foamObject.transform.SetParent(boat, false);
        foamObject.transform.localPosition = new Vector3(0f, 0f, -2.3f);
        ParticleSystem foam = foamObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = foam.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 900;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        ParticleSystem.EmissionModule emission = foam.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 3.2f;
        ParticleSystem.ShapeModule shape = foam.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.2f, 0.01f, 0.3f);
        // Out to both sides: the V of the wake.
        ParticleSystem.VelocityOverLifetimeModule velocity = foam.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0f);
        ParticleSystem.LimitVelocityOverLifetimeModule drag = foam.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.drag = 0.35f;
        ParticleSystem.SizeOverLifetimeModule size = foam.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 3.2f)));
        ParticleSystem.ColorOverLifetimeModule colour = foam.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.06f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        ParticleSystemRenderer foamRenderer = foamObject.GetComponent<ParticleSystemRenderer>();
        foamRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        foamRenderer.sharedMaterial = foamMaterial;
        foamRenderer.shadowCastingMode = ShadowCastingMode.Off;
        foamRenderer.receiveShadows = false;

        GameObject sprayObject = new GameObject("BowSpray");
        sprayObject.transform.SetParent(rider.transform, false);
        sprayObject.transform.localPosition = new Vector3(0f, 0.05f, 1.6f);
        ParticleSystem spray = sprayObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule sprayMain = spray.main;
        sprayMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        sprayMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        sprayMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        sprayMain.gravityModifier = 1f;
        sprayMain.maxParticles = 300;
        sprayMain.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule sprayEmission = spray.emission;
        sprayEmission.rateOverTime = 0f;
        ParticleSystem.ShapeModule sprayShape = spray.shape;
        sprayShape.shapeType = ParticleSystemShapeType.Cone;
        sprayShape.angle = 35f;
        sprayShape.radius = 0.3f;
        sprayShape.rotation = new Vector3(-60f, 0f, 0f);
        ParticleSystem.ColorOverLifetimeModule sprayColour = spray.colorOverLifetime;
        sprayColour.enabled = true;
        Gradient sprayFade = new Gradient();
        sprayFade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        sprayColour.color = sprayFade;
        ParticleSystemRenderer sprayRenderer = sprayObject.GetComponent<ParticleSystemRenderer>();
        sprayRenderer.sharedMaterial = foamMaterial;
        sprayRenderer.shadowCastingMode = ShadowCastingMode.Off;

        BoatWake wake = boat.gameObject.AddComponent<BoatWake>();
        SetField(wake, "rider", rider);
        SetField(wake, "foam", foam);
        SetField(wake, "spray", spray);
    }

    // Hull surface: sections along the length, each a U from gunwale to keel, narrowing to a point at the bow and
    // cut square at the transom. Drawn double sided (the material culls nothing).
    private static Mesh HullMesh()
    {
        const int stations = 16;
        const float half = 2.1f;
        Vector2[] section = { new Vector2(-1f, 0.34f), new Vector2(-0.96f, 0.05f), new Vector2(-0.7f, -0.22f), new Vector2(0f, -0.3f), new Vector2(0.7f, -0.22f), new Vector2(0.96f, 0.05f), new Vector2(1f, 0.34f) };
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        for (int s = 0; s <= stations; s++)
        {
            float z = Mathf.Lerp(-half, half, s / (float)stations);
            float bow = Mathf.Clamp01((z - 0.4f) / (half - 0.4f));
            float width = 0.78f * Mathf.Sqrt(Mathf.Max(0.0004f, 1f - bow * bow));
            float lift = bow * bow * 0.28f;
            foreach (Vector2 p in section)
            {
                v.Add(new Vector3(p.x * width, p.y + (p.y < 0.2f ? lift : 0.06f * bow), z));
            }
        }

        int w = section.Length;
        for (int s = 0; s < stations; s++)
        {
            for (int i = 0; i < w - 1; i++)
            {
                int a = s * w + i;
                t.AddRange(new[] { a, a + w, a + 1, a + 1, a + w, a + w + 1 });
            }
        }

        // Transom: fan over the stern section.
        for (int i = 1; i < w - 1; i++)
        {
            t.AddRange(new[] { 0, i, i + 1 });
        }

        Mesh mesh = new Mesh { name = "BoatHull", vertices = v.ToArray(), triangles = t.ToArray() };
        mesh.RecalculateNormals();
        return mesh;
    }

    // Erick: the player's own body (the MetaHuman of the demo, with its own skin as in the game; Gustavo 03/10), with
    // the cutscene controller (Sit, Walk, Idle) and his head turned to what the shot is about (ActorLook).
    private const string ErickModel = ProjectPaths.CharacterModels + "/PlayerBody/PlayerBody.fbx";
    private const float ErickScale = 0.965f;

    private static Animator Erick(string name, Transform parent, Vector3 localPosition, bool seated)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ErickModel);
        GameObject erick = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(erick, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        erick.name = name;
        erick.transform.SetParent(parent != null ? parent : root, false);
        erick.transform.localPosition = localPosition;
        erick.transform.localRotation = model.transform.localRotation;
        erick.transform.localScale = model.transform.localScale * ErickScale;
        DressAsErick(erick);
        Animator animator = erick.GetComponent<Animator>();
        if (animator == null)
        {
            animator = erick.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = ErickController();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // The pose it starts in (the timeline's Animate cues take over from there).
        erick.AddComponent<StartState>().state = seated ? "Sit" : "Idle";
        erick.AddComponent<ActorLook>();
        if (!seated)
        {
            // His steps: boards on the pier and the porch, wet earth up the path (the walk clip's events).
            AudioSource steps = erick.AddComponent<AudioSource>();
            steps.playOnAwake = false;
            steps.spatialBlend = 1f;
            steps.minDistance = 1.5f;
            steps.maxDistance = 25f;
            CutsceneFootsteps footsteps = erick.AddComponent<CutsceneFootsteps>();
            SetField(footsteps, "source", steps);
            SetField(footsteps, "wood", Clips(ProjectPaths.Audio + "/Footsteps/wood"));
            SetField(footsteps, "earth", Clips(ProjectPaths.Audio + "/Footsteps", "mud_"));

            // The end at the door: his hand on the handle, and the camera in his eyes.
            erick.AddComponent<ActorReach>();
            ActorFirstPerson firstPerson = erick.AddComponent<ActorFirstPerson>();
            string[] headParts = { "Head", "Hair", "Beard" };
            SetField(firstPerson, "head", erick.GetComponentsInChildren<Renderer>(true).Where(r => headParts.Contains(r.name)).Cast<Object>().ToArray());
            Transform eyes = EyePoint(erick, animator);
            Light fill = new GameObject("FirstPersonFill").AddComponent<Light>();
            fill.transform.SetParent(erick.transform, false);
            fill.type = LightType.Point;
            fill.color = new Color(0.72f, 0.8f, 1f);
            fill.range = 6f;
            fill.shadows = LightShadows.None;
            fill.enabled = false;
            SetField(firstPerson, "fill", fill);
            SetField(firstPerson, "eyes", eyes);
        }
        return animator;
    }

    // Dresses a PlayerBody instance as Erick seen from outside (the cutscene, the hand mocap stage of the test scene):
    // the same materials the game's body gets (MetaHumanBody: skin, face, eyes, teeth, lashes), the clothes, hair and
    // beard mounted by MetaHumanOutfit, the bare body gone (the cut ErickBody takes its place).
    internal static void DressAsErick(GameObject erick)
    {
        Material skin = MetaHumanBody.Apply(erick, ProjectPaths.CharacterModels + "/PlayerBody", "Player", false);
        MetaHumanOutfit.Attach(erick, ProjectPaths.CharacterModels + "/ErickOutfit", "Erick", skin, false);
        SkinnedMeshRenderer bare = erick.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Body");
        if (bare != null && erick.transform.Find("Outfit") != null)
        {
            Object.DestroyImmediate(bare.gameObject);
        }
    }

    // "EyePoint" on his head bone, between his eyes (measured on the eyeballs' own vertices in the rest pose), for the
    // first-person camera at the door.
    private static Transform EyePoint(GameObject erick, Animator animator)
    {
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Vector3 sum = Vector3.zero;
        int count = 0;
        // The eyeballs are the "EyeL" and "EyeR" submeshes of the head (the MetaHuman face export).
        SkinnedMeshRenderer headRenderer = erick.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Head");
        if (headRenderer != null)
        {
            Mesh baked = new Mesh();
            headRenderer.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            for (int s = 0; s < headRenderer.sharedMesh.subMeshCount; s++)
            {
                string part = headRenderer.sharedMaterials.Length > s && headRenderer.sharedMaterials[s] != null ? headRenderer.sharedMaterials[s].name : "";
                if (!part.EndsWith("Eyes"))
                {
                    continue;
                }
                Vector3 centre = Vector3.zero;
                int[] tris = headRenderer.sharedMesh.GetTriangles(s);
                foreach (int i in tris)
                {
                    centre += vertices[i];
                }
                // Baked with the renderer's scale already applied: position and rotation only (TransformPoint scaled
                // it twice and put the camera 9 cm to his right).
                Vector3 local = centre / Mathf.Max(1, tris.Length);
                // The MetaHuman face export (07/10) bakes in centimetres (its bones carry the 0.01 scale, the renderer
                // does not): the eye came out 165 m up and the end of the opening looked down on the house from the sky
                // (16/10, measured on CM_FirstPerson). Anything metres away from the renderer is centimetres.
                if (local.magnitude > 5f)
                {
                    local *= 0.01f;
                }

                sum += headRenderer.transform.position + headRenderer.transform.rotation * local;
                count++;
            }
            Object.DestroyImmediate(baked);
        }

        Transform eye = new GameObject("EyePoint").transform;
        eye.SetParent(head, false);
        // Without eyeballs: 9 cm above the head bone and 9 cm forward is about where the MetaHuman's eyes are.
        Vector3 fallback = head.position + erick.transform.up * 0.09f + erick.transform.forward * 0.09f;
        eye.position = count > 0 ? sum / count : fallback;
        // Sanity: the eyes sit within a head of the head bone; anything else is a measuring error, not anatomy.
        if ((eye.position - head.position).magnitude > 0.4f)
        {
            Debug.LogWarning("[Arrival] EyePoint measured " + (eye.position - head.position).magnitude.ToString("F2") + " m from the head bone; using the default offset.");
            eye.position = fallback;
        }

        eye.rotation = erick.transform.rotation;
        return eye;
    }

    private static AnimatorController ErickController()
    {
        string path = Folder + "/Erick_Cutscene.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null && controller.layers[0].iKPass)
        {
            return controller;
        }

        if (controller != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        // IK pass on: ActorLook turns his head.
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AddState(machine, "Idle", StarterAnimations + "/Stand--Idle.anim.fbx");
        AddState(machine, "Walk", StarterAnimations + "/Locomotion--Walk_N.anim.fbx");
        AddState(machine, "Sit", ProjectPaths.PlayerAnimations + "/Sitting.fbx");
        return controller;
    }

    private static void AddState(AnimatorStateMachine machine, string name, string path)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
        if (clip == null)
        {
            Debug.LogWarning("[Arrival] missing animation " + path);
        }

        AnimatorState state = machine.AddState(name);
        state.motion = clip;
    }

    // ---- the tentacle that shows itself beside the boat --------------------------------------------------------

    private static GameObject Tentacle()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.Root + "/Prefabs/Creatures/Tentacle.prefab");
        if (prefab == null)
        {
            return new GameObject("Tentacle_MISSING");
        }

        GameObject tentacle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        tentacle.name = "Tentacle_Teaser";
        // Up out of the water (the prefab's arm runs along its +Z), curling toward the boat.
        tentacle.transform.SetPositionAndRotation(new Vector3(0f, -4f, 0f), Quaternion.Euler(-90f, 0f, 0f));
        return tentacle;
    }

    // ---- helpers -------------------------------------------------------------------------------------------------

    private static Transform Group(string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    private static Transform Marker(string name, Vector3 position)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(root, false);
        t.position = position;
        return t;
    }

    private static CutscenePath PathObject(string name, Vector3[] points)
    {
        CutscenePath path = new GameObject(name).AddComponent<CutscenePath>();
        path.transform.SetParent(root, false);
        path.points = points;
        return path;
    }

    internal static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!collider)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        return go.transform;
    }

    internal static Material Mat(string name, Color color, float smoothness)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Scenery reduced by the game-3d-assets skill (Art/Models/Props/<id>, material and import settings by the Flooded
    // House builder's ModelProp), turned, scaled, its lowest point at `position.y` (callers sink it into the ground),
    // without a collider and out of the shadow passes (scenery seen from afar).
    private static void Scenery(string id, Transform parent, Vector3 position, float yaw, float scale)
    {
        GameObject model = FloodedHouseBuilder.ModelProp(id, parent, position, Quaternion.Euler(0f, yaw, 0f), false, true);
        model.transform.localScale *= scale;
        Bounds bounds = model.GetComponentInChildren<Renderer>().bounds;
        model.transform.position += Vector3.up * (position.y - bounds.min.y);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // The glow a light makes in the rain around it, a soft disc that always faces the camera (one particle that
    // never dies): what makes a lamp read from far off, where the bulb itself is a pixel.
    internal static void LampHalo(Transform lamp, float size)
    {
        string path = MaterialFolder + "/LampHalo.mat";
        Material halo = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (halo == null)
        {
            halo = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(halo, path);
        }

        halo.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/light_01.png"));
        halo.SetColor("_BaseColor", new Color(1f, 0.72f, 0.42f, 0.5f));
        halo.SetFloat("_Surface", 1f);
        halo.SetFloat("_Blend", 2f);
        halo.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        halo.SetFloat("_DstBlend", (float)BlendMode.One);
        halo.SetFloat("_ZWrite", 0f);
        halo.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        halo.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(halo);

        GameObject go = new GameObject("Halo");
        go.transform.SetParent(lamp, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 100000f;
        main.startSpeed = 0f;
        main.startSize = size;
        main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.prewarm = false;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = halo;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Light Directional(string name, Transform parent, Quaternion rotation, Color color, float intensity)
    {
        Light light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.rotation = rotation;
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        return light;
    }

    // The audio clips in a folder (not its subfolders), optionally only those whose name starts with `prefix`.
    private static Object[] Clips(string folder, string prefix = "")
    {
        return AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetDirectoryName(p).Replace('\\', '/') == folder && System.IO.Path.GetFileName(p).StartsWith(prefix))
            .Select(p => (Object)AssetDatabase.LoadAssetAtPath<AudioClip>(p))
            .ToArray();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    internal static void SetField(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetField(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty list = so.FindProperty(field);
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetFloat(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void SetVector2(Object target, string field, Vector2 value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).vector2Value = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
