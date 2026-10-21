using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Builds the "Flooded House" test scene from the GDD (Parte II, "O porão alagado") entirely from
// code, like the battlefield: Horror > Flooded House > Rebuild Everything recreates it. Everything
// generated lives under one root; do not edit it by hand.
//
// Layout in metres (y up, north = +z). The rest of the mansion (east wing, attic, generator room) is in
// FloodedHouseMansion. Ground floor 12 x 10 at y 0 (the dining and living room); basement
// 8 x 6 below it, half flooded (surface y -2.6) with a stair landing and the dive bench above the
// water; a sunken bedroom east of the basement, flooded to the ceiling; a shaft in the basement floor
// down to the lab: a 16 m corridor, an air-pocket room and the tank room with hot vents.
public static partial class FloodedHouseBuilder
{
    private const string ScenePath = ProjectPaths.Scenes + "/Flooded_House.unity";
    private const string RootName = "FloodedHouse_Generated";

    private const float WaterLevel = -2.6f;
    private const float AirPocketLevel = -7.5f;

    private static readonly Dictionary<Material, float> TileSizes = new Dictionary<Material, float>();
    private static Transform root;

    [MenuItem("Horror/Flooded House/Rebuild Everything")]
    public static void RebuildAll()
    {
        // Same seed, same dead trees: rebuilding should not reshuffle the scene.
        Random.InitState(1879);
        // Feel values (speeds, fog, torch, sea) come from the shared tuning asset.
        GameTuningMenu.Ensure();
        // Recorded sounds by role (splashes, steps, drips, rain on the roof, sea).
        SoundLibraryMenu.Refresh();
        if (PropsNeedSetup())
        {
            BattlefieldAssetSetup.SetupProps();
        }

        CreateMaterials();
        ConfigurePipeline();
        OpenScene();

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        root = new GameObject(RootName).transform;
        gridLights.Clear();
        gridGlows.Clear();
        gridFixtures.Clear();
        gridFixturesOff.Clear();
        gridFixturesOn.Clear();
        // The mansion of Dayse's floor plan (GDD, 11/10), both floors; the basement, the sunken pantry and the lab under it.
        BuildManor();
        BuildBasement();
        BuildBedroom();
        BuildLab();
        BuildExterior();
        BuildWater();
        BuildLighting();
        BuildReflectionProbes();
        BuildStory();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        PutFirstInBuild();
        Debug.Log("[FloodedHouse] Rebuilt everything.");
    }

    // Re-importing every prop takes minutes; only do it when a downloaded prop has no materials yet.
    private static bool PropsNeedSetup()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ProjectPaths.Props }))
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(guid)).Replace('\\', '/');
            if (AssetDatabase.FindAssets("t:Material", new[] { folder }).Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void OpenScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.path == ScenePath)
        {
            return;
        }

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        if (System.IO.File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }

    // The house goes in the build if it is not there yet; the order the menu builder set (menu, opening, house) stays.
    private static void PutFirstInBuild()
    {
        if (System.Array.Exists(EditorBuildSettings.scenes, s => s.path == ScenePath))
        {
            return;
        }

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path != ScenePath)
            {
                scenes.Add(scene);
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- materials

    private static Material floorboards, plaster, azulejo, wallpaper, ceiling, concrete, labTile, labFloor, rustyMetal, darkPlanks, doorPaint;
    private static Material water, sea, glass, paper, metal, creatureSkin, rubber, tankGlass, tankGlassBroken, specimenGlow, ventGrate, mud;

    private static void CreateMaterials()
    {
        BattlefieldTerrainBuilder.EnsureFolder(ProjectPaths.FloodedHouseMaterials);
        TileSizes.Clear();
        floorboards = Textured("Floorboards", 2.2f, new Color(0.75f, 0.7f, 0.66f), 0.25f);
        // Clean warm-white plaster: the old peeling texture tiled into horizontal bands that read as boards.
        plaster = Textured("CleanPlaster", 2.5f, new Color(0.9f, 0.87f, 0.82f), 0.08f);
        // Pulled toward Portuguese blue: under the warm lamps the raw texture reads green.
        azulejo = Textured("Azulejo", 1.2f, new Color(0.62f, 0.76f, 1f), 0.4f);
        wallpaper = Textured("WallpaperMold", 1.8f, new Color(0.7f, 0.75f, 0.68f), 0.1f);
        ceiling = Textured("CeilingPlaster", 2.5f, new Color(0.7f, 0.7f, 0.68f), 0.06f);
        concrete = Textured("ConcreteDamaged", 3f, new Color(0.62f, 0.64f, 0.62f), 0.12f);
        labTile = Textured("LabTile", 1.4f, new Color(0.82f, 0.86f, 0.84f), 0.5f);
        labFloor = Textured("LabFloor", 1.6f, new Color(0.7f, 0.72f, 0.72f), 0.4f);
        rustyMetal = Textured("PaintedMetalRust", 1.5f, new Color(0.6f, 0.66f, 0.64f), 0.3f);
        darkPlanks = Textured("DarkPlanks", 1.8f, new Color(0.8f, 0.72f, 0.64f), 0.12f);
        // Doors, window frames and the desk: varnished hardwood, not rough boards (Gustavo, 01/10).
        doorPaint = Textured("PolishedWood", 1.2f, new Color(0.85f, 0.78f, 0.72f), 0.3f);
        mud = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.EnvironmentMaterials + "/Mud.mat");
        TileSizes[mud] = 4f;

        Texture2D ripples = RippleNormalMap();
        water = Transparent("Water", new Color(0.09f, 0.2f, 0.18f, 0.72f), 0.9f, ripples);
        // A second, finer layer of the same ripples (URP Lit's detail normal map, almost free): one scale of
        // smooth ripples read as plastic or glass (10/10).
        WaterDetail(water, ripples);
        // Storm sea: Gerstner waves on the GPU (Art/Shaders/StormSea.shader), ripples on top.
        sea = Material("StormSea", Shader.Find("Horror/StormSea"));
        sea.SetTexture("_BumpMap", ripples);
        // Fine ripples and rain rings near the camera, a tighter moon glint (10/10: the sea read as plastic).
        SeaDetail(sea);
        EditorUtility.SetDirty(sea);
        // Matte enough that the desk lantern's reflection does not read as a setting sun.
        glass = Transparent("WindowGlass", new Color(0.35f, 0.42f, 0.45f, 0.14f), 0.5f, null);
        tankGlass = Transparent("TankGlass", new Color(0.3f, 0.5f, 0.42f, 0.28f), 0.95f, null);
        // Measured 2026-09-29: the tank room's reflection probe kept the glass visible from 19 m away
        // through 7 m of murk. Highlights from real lights stay; probe reflections go.
        tankGlass.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        tankGlass.SetFloat("_EnvironmentReflections", 0f);
        EditorUtility.SetDirty(tankGlass);
        // Tank 3's glass is old, scratched and fogged: denser than the clean tubes so the hole punched through
        // it reads (at 28% opacity a hole and whole glass looked the same).
        tankGlassBroken = Transparent("TankGlassBroken", new Color(0.42f, 0.5f, 0.46f, 0.55f), 0.7f, null);
        tankGlassBroken.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        tankGlassBroken.SetFloat("_EnvironmentReflections", 0f);
        EditorUtility.SetDirty(tankGlassBroken);
        paper = Plain("Paper", new Color(0.85f, 0.8f, 0.68f), 0.1f, 0f);
        metal = Plain("DarkMetal", new Color(0.22f, 0.23f, 0.22f), 0.55f, 0.8f);
        rubber = Plain("Rubber", new Color(0.05f, 0.05f, 0.05f), 0.35f, 0f);
        ventGrate = Plain("VentGrate", new Color(0.08f, 0.07f, 0.06f), 0.3f, 0.6f);
        specimenGlow = Plain("SpecimenGlow", new Color(0.05f, 0.12f, 0.08f), 0.6f, 0f);
        specimenGlow.EnableKeyword("_EMISSION");
        specimenGlow.SetColor("_EmissionColor", new Color(0.1f, 0.45f, 0.25f) * 0.6f);

        creatureSkin = Material("CreatureSkin", Shader.Find("Universal Render Pipeline/Unlit"));
        creatureSkin.SetColor("_BaseColor", new Color(0.01f, 0.012f, 0.012f));
        EditorUtility.SetDirty(creatureSkin);
        AssetDatabase.SaveAssets();
    }

    private static Material Material(string name, Shader shader)
    {
        string path = $"{ProjectPaths.FloodedHouseMaterials}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.enableInstancing = true;
        return material;
    }

    private static Material Textured(string set, float tileMetres, Color tint, float smoothness)
    {
        Material material = Material(set, Shader.Find("Universal Render Pipeline/Lit"));
        string folder = $"{ProjectPaths.Textures}/{set}";
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{set}_Albedo.jpg");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{set}_Normal.jpg");
        if (normal != null)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        material.SetTexture("_BaseMap", albedo);
        material.SetColor("_BaseColor", tint);
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        // Where the set has a URP metal/smoothness map (R metal, A smoothness, from the original's roughness), the map
        // decides: the gloss varies over the surface (worn spots shine, the rest is matte) and the map's mean is the
        // set's smoothness (Gustavo 11/10, "tudo muito reflexivo": a uniform 0.35 to 0.55 read as plastic and every
        // surface mirrored the room's probe). SourceAssets/Tools/surface_smoothness.py builds the maps.
        Texture2D gloss = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{set}_MetallicSmooth.png");
        if (gloss != null)
        {
            TextureImporter glossImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(gloss));
            if (glossImporter.sRGBTexture)
            {
                glossImporter.sRGBTexture = false;
                glossImporter.SaveAndReimport();
            }

            material.SetTexture("_MetallicGlossMap", gloss);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", 1f);
        }
        else
        {
            material.SetTexture("_MetallicGlossMap", null);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
        }

        EditorUtility.SetDirty(material);
        TileSizes[material] = tileMetres;
        return material;
    }

    private static Material Plain(string name, Color color, float smoothness, float metallic)
    {
        Material material = Material(name, Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(material);
        TileSizes[material] = 1f;
        return material;
    }

    // Double-sided alpha-blended Lit: water surfaces must read from above and from below.
    // The sea's look shared by every StormSea material (house, opening, menu): fine ripples near the camera that
    // break the moon into glitter, rain rings, a glint tighter than the old 0.26 (Gustavo 10/10).
    public static void SeaDetail(Material sea)
    {
        sea.SetFloat("_DetailStrength", 0.32f);
        sea.SetFloat("_DetailTiling", 0.4f);
        sea.SetFloat("_RainRipples", 0.6f);
        sea.SetFloat("_RainDensity", 2.5f);
        sea.SetFloat("_GlintRoughness", 0.16f);
        EditorUtility.SetDirty(sea);
    }

    // Indoor water (URP Lit): the ripple map again as the detail normal map, three and a half times finer.
    public static void WaterDetail(Material water, Texture2D ripples)
    {
        water.SetTexture("_DetailNormalMap", ripples);
        water.SetFloat("_DetailNormalMapScale", 0.45f);
        water.SetTextureScale("_DetailAlbedoMap", new Vector2(3.5f, 3.5f));
        water.SetTexture("_DetailMask", Texture2D.whiteTexture);
        water.EnableKeyword("_DETAIL_MULX2");
        EditorUtility.SetDirty(water);
    }

    private static Material Transparent(string name, Color color, float smoothness, Texture2D normal)
    {
        Material material = Material(name, Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        // Preserve Specular (URP's default for transparents) premultiplies alpha: highlights stay at full
        // strength on a 14%-opaque window (glare), and fog is added at full strength over the background,
        // so far tank glass glowed through murky water. Plain alpha blending fixes both (2026-09-29).
        material.SetFloat("_BlendModePreserveSpecular", 0f);
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetTexture("_BumpMap", normal);
        if (normal != null)
        {
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", 0.6f);
        }

        EditorUtility.SetDirty(material);
        TileSizes[material] = 3f;
        return material;
    }

    // Tileable ripple normals from a handful of crossing sine waves (whole periods, so it wraps).
    private static Texture2D RippleNormalMap()
    {
        string folder = ProjectPaths.Textures + "/WaterRipples";
        string path = folder + "/WaterRipples_Normal.png";
        if (!System.IO.File.Exists(path))
        {
            BattlefieldTerrainBuilder.EnsureFolder(folder);
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int[,] waves = { { 3, 1 }, { -2, 5 }, { 7, -3 }, { 1, 9 }, { -11, 4 } };
            float[] amplitude = { 1f, 0.8f, 0.5f, 0.35f, 0.25f };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size, dx = 0f, dy = 0f;
                    for (int w = 0; w < amplitude.Length; w++)
                    {
                        float phase = 2f * Mathf.PI * (waves[w, 0] * u + waves[w, 1] * v);
                        float slope = Mathf.Cos(phase) * amplitude[w] * 0.12f;
                        dx += slope * waves[w, 0];
                        dy += slope * waves[w, 1];
                    }

                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    texture.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            }

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- geometry helpers

    private static Transform Group(string name, Transform parent = null)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent != null ? parent : root, false);
        return group;
    }

    // An axis-aligned box between two world corners, with world-space UVs so neighbouring boxes
    // tile seamlessly, and a matching collider.
    private static GameObject Box(string name, Transform parent, Vector3 a, Vector3 b, Material material, bool collider = true)
    {
        Vector3 min = Vector3.Min(a, b);
        Vector3 max = Vector3.Max(a, b);
        GameObject box = new GameObject(name);
        box.transform.SetParent(parent, false);
        box.transform.position = (min + max) * 0.5f;
        box.AddComponent<MeshFilter>().sharedMesh = BoxMesh(min, max, TileSizes.TryGetValue(material, out float tile) ? tile : 1f);
        box.AddComponent<MeshRenderer>().sharedMaterial = material;
        if (collider)
        {
            box.AddComponent<BoxCollider>();
        }

        GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return box;
    }

    private static Mesh BoxMesh(Vector3 min, Vector3 max, float tile)
    {
        Vector3 center = (min + max) * 0.5f;
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> vertexNormals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        foreach (Vector3 n in normals)
        {
            // u = screen right and v = screen up when the face is seen from outside, so that
            // cross(u, v) = -n and the clockwise triangles below face outward.
            Vector3 u = n.x != 0f ? Vector3.forward * n.x : (n.y != 0f ? Vector3.right : Vector3.right * -n.z);
            Vector3 v = n.y != 0f ? Vector3.forward * n.y : Vector3.up;
            int start = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                float su = (i == 1 || i == 2) ? 1f : -1f;
                float sv = i >= 2 ? 1f : -1f;
                Vector3 corner = new Vector3(
                    Pick(n.x, u.x * su + v.x * sv, min.x, max.x),
                    Pick(n.y, u.y * su + v.y * sv, min.y, max.y),
                    Pick(n.z, u.z * su + v.z * sv, min.z, max.z));
                vertices.Add(corner - center);
                vertexNormals.Add(n);
                uvs.Add(new Vector2(Vector3.Dot(corner, u), Vector3.Dot(corner, v)) / tile);
            }

            triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        Mesh mesh = new Mesh { name = "Box" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(vertexNormals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // The face axis sits on its min or max; the other two axes follow the corner signs.
    private static float Pick(float normalComponent, float sign, float min, float max)
    {
        if (normalComponent != 0f)
        {
            return normalComponent > 0f ? max : min;
        }

        return sign > 0f ? max : min;
    }

    private struct Opening
    {
        public float from, to, bottom, top;

        public Opening(float from, float to, float bottom, float top)
        {
            this.from = from;
            this.to = to;
            this.bottom = bottom;
            this.top = top;
        }
    }

    // A wall slab running along X (at z0..z1) or along Z (at x0..x1), split around rectangular
    // openings (doors, windows, breaches) given in the run axis and in height.
    private static void WallAlongX(string name, Transform parent, float x0, float x1, float z0, float z1, float y0, float y1, Material material, params Opening[] openings)
    {
        WallRun(name, parent, true, x0, x1, z0, z1, y0, y1, material, openings);
    }

    private static void WallAlongZ(string name, Transform parent, float z0, float z1, float x0, float x1, float y0, float y1, Material material, params Opening[] openings)
    {
        WallRun(name, parent, false, z0, z1, x0, x1, y0, y1, material, openings);
    }

    private static void WallRun(string name, Transform parent, bool alongX, float r0, float r1, float d0, float d1, float y0, float y1, Material material, Opening[] openings)
    {
        List<Opening> sorted = new List<Opening>(openings);
        sorted.Sort((p, q) => p.from.CompareTo(q.from));
        float cursor = r0;
        int piece = 0;
        foreach (Opening opening in sorted)
        {
            if (opening.from > cursor)
            {
                WallPiece(name + "_" + piece++, parent, alongX, cursor, opening.from, d0, d1, y0, y1, material);
            }

            if (opening.bottom > y0)
            {
                WallPiece(name + "_" + piece++, parent, alongX, opening.from, opening.to, d0, d1, y0, opening.bottom, material);
            }

            if (opening.top < y1)
            {
                WallPiece(name + "_" + piece++, parent, alongX, opening.from, opening.to, d0, d1, opening.top, y1, material);
            }

            cursor = Mathf.Max(cursor, opening.to);
        }

        if (cursor < r1)
        {
            WallPiece(name + "_" + piece, parent, alongX, cursor, r1, d0, d1, y0, y1, material);
        }
    }

    private static void WallPiece(string name, Transform parent, bool alongX, float r0, float r1, float d0, float d1, float y0, float y1, Material material)
    {
        Vector3 a = alongX ? new Vector3(r0, y0, d0) : new Vector3(d0, y0, r0);
        Vector3 b = alongX ? new Vector3(r1, y1, d1) : new Vector3(d1, y1, r1);
        Box(name, parent, a, b, material);
    }

    // A horizontal quad of water; double-sided through its material.
    private static GameObject WaterSurface(string name, Transform parent, float x0, float x1, float z0, float z1, float y, Material material, float scroll = 1f)
    {
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = name;
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.transform.SetParent(parent, false);
        surface.transform.position = new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
        surface.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        surface.transform.localScale = new Vector3(x1 - x0, z1 - z0, 1f);
        surface.GetComponent<MeshRenderer>().sharedMaterial = material;
        surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        SurfaceScroll scroller = surface.AddComponent<SurfaceScroll>();
        SetField(scroller, "tiling", Mathf.Max(1f, (x1 - x0) / 3f));
        SetField(scroller, "speed", new Vector2(0.012f, 0.008f) * scroll);
        return surface;
    }

    private static WaterVolume Volume(string name, Transform parent, Vector3 a, Vector3 b, float surface, bool deep = true, float visibility = 12f)
    {
        Vector3 min = Vector3.Min(a, b);
        Vector3 max = Vector3.Max(a, b);
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = (min + max) * 0.5f;
        WaterVolume volume = go.AddComponent<WaterVolume>();
        volume.Configure(max - min, surface, deep, visibility);
        return volume;
    }

    // ---------------------------------------------------------------- props

    private static GameObject Prop(string id, Transform parent, Vector3 position, float yaw, bool collider = true, float scale = 1f)
    {
        return Prop(id, parent, position, Quaternion.Euler(0f, yaw, 0f), collider, scale);
    }

    private static GameObject Prop(string id, Transform parent, Vector3 position, Quaternion rotation, bool collider = true, float scale = 1f)
    {
        string path = $"{ProjectPaths.Props}/{id}/{id}_1k.fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning("[FloodedHouse] Missing prop " + path);
            return new GameObject(id + "_MISSING");
        }

        GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        // Some FBX files keep their mesh on the root with a unit-conversion scale or an axis
        // rotation: compose with it instead of overwriting it.
        prop.transform.SetPositionAndRotation(position, rotation * asset.transform.localRotation);
        prop.transform.localScale = asset.transform.localScale * scale;
        SetupLods(prop);
        if (collider)
        {
            FitCollider(prop);
        }

        return prop;
    }

    // A model optimised by the game-3d-assets skill (Props/<name>/<name>.fbx + _BaseColor, _Normal,
    // _MetallicSmoothness). The FBX is already in metres with the pivot at its base. rotation is applied on
    // top of the FBX root's axis conversion; then the model is dropped so its lowest point sits at
    // position.y (restOnSurface) or left where the pivot says.
    internal static GameObject ModelProp(string name, Transform parent, Vector3 position, Quaternion rotation, bool collider = true, bool restOnSurface = true)
    {
        string folder = ProjectPaths.PropModels + "/" + name;
        EnsureModelImport(folder, name);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + ".fbx");
        if (asset == null)
        {
            Debug.LogWarning("[FloodedHouse] Missing model " + folder);
            return new GameObject(name + "_MISSING");
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        model.transform.SetPositionAndRotation(position, rotation * asset.transform.localRotation);
        model.transform.localScale = asset.transform.localScale;
        Material material = ModelPropMaterial(folder, name);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = material;
        }

        if (restOnSurface)
        {
            model.transform.position += Vector3.up * (position.y - RendererBounds(model).min.y);
        }

        if (collider)
        {
            FitCollider(model);
        }

        return model;
    }

    private static Bounds RendererBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        return bounds;
    }

    private static Material ModelPropMaterial(string folder, string name)
    {
        Material m = Material(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_Normal.png"));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_MetallicSmoothness.png"));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        // Dust: an abandoned house dulls every surface (Gustavo 11/10, "tudo muito reflexivo"). The baked maps keep the
        // original's new-object sheen (Poly Haven oil lamp 0.73, candleholders 0.71): scaled to 55%.
        m.SetFloat("_Smoothness", 0.55f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Import settings from the skill's checklist: no imported materials, the exported split normals kept,
    // Mikk tangents (the Blender bake uses them), normal map and linear metallic/smoothness textures.
    private static void EnsureModelImport(string folder, string name)
    {
        ModelImporter model = AssetImporter.GetAtPath(folder + "/" + name + ".fbx") as ModelImporter;
        if (model != null && (model.materialImportMode != ModelImporterMaterialImportMode.None
            || model.importNormals != ModelImporterNormals.Import || model.importTangents != ModelImporterTangents.CalculateMikk))
        {
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.SaveAndReimport();
        }

        TextureImporter normal = AssetImporter.GetAtPath(folder + "/" + name + "_Normal.png") as TextureImporter;
        if (normal != null && normal.textureType != TextureImporterType.NormalMap)
        {
            normal.textureType = TextureImporterType.NormalMap;
            normal.SaveAndReimport();
        }

        TextureImporter ms = AssetImporter.GetAtPath(folder + "/" + name + "_MetallicSmoothness.png") as TextureImporter;
        if (ms != null && ms.sRGBTexture)
        {
            ms.sRGBTexture = false;
            ms.SaveAndReimport();
        }
    }

    // Poly Haven FBX files carry every detail level (name_LOD0..LOD3) and the importer already builds a
    // LODGroup, but with default transitions: a coast rock filling a third of the window view still drew
    // LOD0 (1.1 to 1.26 million triangles each, plus every shadow pass). These props sit far away behind
    // windows, so the transitions favour the light levels; the last level never culls.
    private static readonly float[] LodHeights = { 0.95f, 0.8f, 0.5f };

    private static void SetupLods(GameObject prop)
    {
        LODGroup group = prop.GetComponent<LODGroup>();
        if (group == null)
        {
            return;
        }

        LOD[] lods = group.GetLODs();
        for (int i = 0; i < lods.Length; i++)
        {
            bool last = i == lods.Length - 1;
            lods[i].screenRelativeTransitionHeight = last
                ? 0.0001f
                : LodHeights[Mathf.Min(i, LodHeights.Length - 1)] * Mathf.Pow(0.5f, Mathf.Max(0, i - LodHeights.Length + 1));
        }

        group.SetLODs(lods);
    }

    // Scenery only ever seen through the windows, at night, in rain: keep the lightest detail level only
    // and take it out of every shadow pass. Measured 2026-09-29 in the lab corridor, where the rocks are not
    // even on screen: the moon and lightning shadow cascades still drew them at full detail, 38 of the
    // frame's 41 million triangles (2.9 million without them).
    private static GameObject Backdrop(GameObject prop)
    {
        LODGroup group = prop.GetComponent<LODGroup>();
        if (group != null)
        {
            LOD[] lods = group.GetLODs();
            HashSet<Renderer> keep = new HashSet<Renderer>(lods[lods.Length - 1].renderers);
            for (int i = 0; i < lods.Length - 1; i++)
            {
                foreach (Renderer r in lods[i].renderers)
                {
                    if (r != null && !keep.Contains(r))
                    {
                        Object.DestroyImmediate(r.gameObject);
                    }
                }
            }

            Object.DestroyImmediate(group);
        }

        foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        return prop;
    }

    private static void FitCollider(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        BoxCollider box = target.AddComponent<BoxCollider>();
        box.center = target.transform.InverseTransformPoint(bounds.center);
        Vector3 size = target.transform.InverseTransformVector(bounds.size);
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool collider = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // ---------------------------------------------------------------- serialized fields

    private static void SetField(Object target, string field, object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[FloodedHouse] {target.GetType().Name} has no field '{field}'");
            return;
        }

        switch (value)
        {
            case Object reference:
                property.objectReferenceValue = reference;
                break;
            case float f:
                property.floatValue = f;
                break;
            case int i:
                property.intValue = i;
                break;
            case bool b:
                property.boolValue = b;
                break;
            case string s:
                property.stringValue = s;
                break;
            case Vector2 v2:
                property.vector2Value = v2;
                break;
            case Vector3 v3:
                property.vector3Value = v3;
                break;
            case Color c:
                property.colorValue = c;
                break;
            case Bounds bounds:
                property.boundsValue = bounds;
                break;
            case Object[] array:
                property.arraySize = array.Length;
                for (int k = 0; k < array.Length; k++)
                {
                    property.GetArrayElementAtIndex(k).objectReferenceValue = array[k];
                }

                break;
            default:
                Debug.LogError($"[FloodedHouse] Unsupported value for {field}");
                break;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Bounds Zone(Vector3 a, Vector3 b)
    {
        Bounds bounds = new Bounds((a + b) * 0.5f, Vector3.zero);
        bounds.Encapsulate(a);
        bounds.Encapsulate(b);
        return bounds;
    }
}
