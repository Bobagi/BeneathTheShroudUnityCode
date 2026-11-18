using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the battlefield terrains from BattlefieldLayout: a detailed inner terrain with the
// trench system and a large coarse outer terrain reaching the horizon. Re-runnable.
public static class BattlefieldTerrainBuilder
{
    private const string TerrainFolder = ProjectPaths.TerrainData;
    private const string LayerFolder = ProjectPaths.TerrainLayers;
    private const int InnerResolution = 1025;
    private const int OuterResolution = 513;

    // Terrain layer order used by the splat painter.
    public static readonly string[] LayerNames = { "Mud", "Dirt", "Churned", "Gravel" };

    [MenuItem("Horror/Battlefield/1 Build Terrains")]
    public static void BuildTerrains()
    {
        EnsureFolder(TerrainFolder);

        Terrain inner = CreateOrUpdateTerrain(
            "Battlefield_Inner",
            TerrainFolder + "/BattlefieldInner.asset",
            InnerResolution,
            BattlefieldLayout.InnerSize,
            BattlefieldLayout.InnerHeightRange,
            true
        );
        Terrain outer = CreateOrUpdateTerrain(
            "Battlefield_Outer",
            TerrainFolder + "/BattlefieldOuter.asset",
            OuterResolution,
            BattlefieldLayout.OuterSize,
            BattlefieldLayout.OuterHeightRange,
            false
        );

        inner.heightmapPixelError = 3f;
        inner.basemapDistance = 120f;
        outer.heightmapPixelError = 6f;
        outer.basemapDistance = 400f;
        outer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        AssetDatabase.SaveAssets();
        Debug.Log("[Battlefield] Terrains built.");
    }

    private static Terrain CreateOrUpdateTerrain(string objectName, string assetPath, int resolution,
        float size, float heightRange, bool isInner)
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, assetPath);
        }

        data.heightmapResolution = resolution;
        data.size = new Vector3(size, heightRange, size);
        data.alphamapResolution = isInner ? 1024 : 512;
        data.baseMapResolution = 1024;

        Vector3 origin = new Vector3(-size * 0.5f, BattlefieldLayout.TerrainBaseY, -size * 0.5f);
        float[,] heights = new float[resolution, resolution];
        float step = size / (resolution - 1);
        float innerHalf = BattlefieldLayout.InnerSize * 0.5f;

        for (int zi = 0; zi < resolution; zi++)
        {
            float z = origin.z + zi * step;
            for (int xi = 0; xi < resolution; xi++)
            {
                float x = origin.x + xi * step;
                float worldHeight;
                if (isInner)
                {
                    worldHeight = BattlefieldLayout.InnerHeight(x, z);
                }
                else
                {
                    worldHeight = BattlefieldLayout.OuterHeight(x, z);
                    // Under the inner terrain the outer one sinks out of sight; vertices exactly on
                    // the seam stay put so both surfaces share the border line.
                    bool strictlyInside = Mathf.Abs(x) < innerHalf - 0.01f && Mathf.Abs(z) < innerHalf - 0.01f;
                    if (strictlyInside)
                    {
                        worldHeight -= 4f;
                    }
                }

                heights[zi, xi] = Mathf.Clamp01((worldHeight - BattlefieldLayout.TerrainBaseY) / heightRange);
            }
        }

        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);

        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            go = Terrain.CreateTerrainGameObject(data);
            go.name = objectName;
        }

        go.transform.position = origin;
        Terrain terrain = go.GetComponent<Terrain>();
        terrain.terrainData = data;
        go.GetComponent<TerrainCollider>().terrainData = data;
        terrain.drawInstanced = true;
        go.isStatic = true;
        return terrain;
    }

    [MenuItem("Horror/Battlefield/2 Paint Terrains")]
    public static void PaintTerrains()
    {
        TerrainLayer[] layers = LoadLayers();
        if (layers == null)
        {
            Debug.LogError("[Battlefield] Missing terrain layers in " + LayerFolder);
            return;
        }

        PaintInner(GameObject.Find("Battlefield_Inner").GetComponent<Terrain>(), layers);
        PaintOuter(GameObject.Find("Battlefield_Outer").GetComponent<Terrain>(), layers);
        AssetDatabase.SaveAssets();
        Debug.Log("[Battlefield] Terrains painted.");
    }

    private static TerrainLayer[] LoadLayers()
    {
        TerrainLayer[] layers = new TerrainLayer[LayerNames.Length];
        for (int i = 0; i < LayerNames.Length; i++)
        {
            layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerFolder + "/" + LayerNames[i] + ".terrainlayer");
            if (layers[i] == null)
            {
                return null;
            }
        }

        return layers;
    }

    private static void PaintInner(Terrain terrain, TerrainLayer[] layers)
    {
        TerrainData data = terrain.terrainData;
        data.terrainLayers = layers;
        int resolution = data.alphamapResolution;
        float[,,] splat = new float[resolution, resolution, layers.Length];
        Vector3 origin = terrain.transform.position;
        float step = data.size.x / resolution;

        for (int zi = 0; zi < resolution; zi++)
        {
            float z = origin.z + (zi + 0.5f) * step;
            for (int xi = 0; xi < resolution; xi++)
            {
                float x = origin.x + (xi + 0.5f) * step;
                float trench = BattlefieldLayout.TrenchOffset(x, z);
                float crater = BattlefieldLayout.CraterOffset(x, z, 0f);
                float normalizedX = (xi + 0.5f) / resolution;
                float normalizedZ = (zi + 0.5f) / resolution;
                float steepness = data.GetSteepness(normalizedX, normalizedZ);
                float noise = Mathf.PerlinNoise(x / 9f + 3f, z / 9f + 8f);
                float fine = Mathf.PerlinNoise(x / 2.3f, z / 2.3f);

                // Weights: wet mud pools in trench floors and crater bowls, raw dirt on steep cuts,
                // churned earth everywhere else with gravel scattered through it.
                float mud = Mathf.Clamp01(-trench / 1.6f) + Mathf.Clamp01(-crater * 1.4f) + (noise > 0.72f ? 0.5f : 0f);
                float dirt = Mathf.Clamp01((steepness - 24f) / 18f) * 1.2f;
                float gravel = Mathf.Clamp01((fine - 0.62f) * 4f) * 0.7f;
                float churned = 0.45f + noise * 0.3f + (trench > 0.05f ? 0.4f : 0f);

                float total = mud + dirt + gravel + churned;
                splat[zi, xi, 0] = mud / total;
                splat[zi, xi, 1] = dirt / total;
                splat[zi, xi, 2] = churned / total;
                splat[zi, xi, 3] = gravel / total;
            }
        }

        data.SetAlphamaps(0, 0, splat);
        EditorUtility.SetDirty(data);
    }

    private static void PaintOuter(Terrain terrain, TerrainLayer[] layers)
    {
        TerrainData data = terrain.terrainData;
        data.terrainLayers = layers;
        int resolution = data.alphamapResolution;
        float[,,] splat = new float[resolution, resolution, layers.Length];
        Vector3 origin = terrain.transform.position;
        float step = data.size.x / resolution;

        for (int zi = 0; zi < resolution; zi++)
        {
            float z = origin.z + (zi + 0.5f) * step;
            for (int xi = 0; xi < resolution; xi++)
            {
                float x = origin.x + (xi + 0.5f) * step;
                float crater = BattlefieldLayout.CraterOffset(x, z, 6f);
                float noise = Mathf.PerlinNoise(x / 60f + 3f, z / 60f + 8f);
                float mud = Mathf.Clamp01(-crater * 0.5f) + (noise > 0.66f ? 0.4f : 0f);
                float churned = 0.6f + noise * 0.2f;
                float dirt = 0.25f;
                float total = mud + churned + dirt;
                splat[zi, xi, 0] = mud / total;
                splat[zi, xi, 1] = dirt / total;
                splat[zi, xi, 2] = churned / total;
                splat[zi, xi, 3] = 0f;
            }
        }

        data.SetAlphamaps(0, 0, splat);
        EditorUtility.SetDirty(data);
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
