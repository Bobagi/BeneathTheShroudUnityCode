using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Grass over a terrain, drawn by GPU instancing from the terrain's own detail map (layer `layer`: how many tufts
// per cell). The terrain's detail renderer drew nothing in this project (URP with the GPU Resident Drawer, 03/10:
// 162 thousand tufts in the map, none on screen, in billboard and in instanced-mesh mode alike), so the map is
// read once here and drawn with Graphics.RenderMeshInstanced: tufts are grouped in square blocks, and each frame
// only the blocks inside the draw distance and the view of the camera are submitted. The terrain's own detail
// distance is set to 0 by the builder so nothing is drawn twice if the terrain ever starts drawing them.
// No allocation after OnEnable. [ExecuteAlways]: also seen in the Scene view and in the editor's captures.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public sealed class TerrainGrass : MonoBehaviour
{
    private const int MaxBatch = 1023;

    [SerializeField]
    private Mesh mesh;

    [SerializeField]
    private Material material;

    [SerializeField]
    private int layer;

    [SerializeField]
    private float drawDistance = 90f;

    [SerializeField]
    private float blockSize = 16f;

    [SerializeField]
    private Vector2 width = new Vector2(0.7f, 1.3f);

    [SerializeField]
    private Vector2 height = new Vector2(0.45f, 1.05f);

    [SerializeField]
    private int seed = 23;

    private sealed class Block
    {
        public Bounds bounds;
        public Matrix4x4[] matrices;
    }

    private readonly List<Block> blocks = new List<Block>();
    private readonly Plane[] planes = new Plane[6];
    private RenderParams parameters;

    private void OnEnable()
    {
        Build();
    }

    private void OnDisable()
    {
        blocks.Clear();
    }

    private void Build()
    {
        blocks.Clear();
        Terrain terrain = GetComponent<Terrain>();
        TerrainData data = terrain.terrainData;
        if (mesh == null || material == null || data == null || data.detailPrototypes.Length <= layer)
        {
            return;
        }

        parameters = new RenderParams(material)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = true,
            layer = gameObject.layer,
        };

        int resolution = data.detailResolution;
        int[,] map = data.GetDetailLayer(0, 0, resolution, resolution, layer);
        Vector3 origin = transform.position;
        Vector3 size = data.size;
        float cell = size.x / resolution;
        int perSide = Mathf.Max(1, Mathf.CeilToInt(size.x / blockSize));
        List<Matrix4x4>[] lists = new List<Matrix4x4>[perSide * perSide];
        System.Random random = new System.Random(seed);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int count = map[z, x];
                for (int i = 0; i < count; i++)
                {
                    float px = origin.x + (x + (float)random.NextDouble()) * cell;
                    float pz = origin.z + (z + (float)random.NextDouble()) * cell;
                    Vector3 at = new Vector3(px, 0f, pz);
                    at.y = terrain.SampleHeight(at) + origin.y - 0.03f;
                    float w = Mathf.Lerp(width.x, width.y, (float)random.NextDouble());
                    float h = Mathf.Lerp(height.x, height.y, (float)random.NextDouble());
                    Quaternion turn = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    int bx = Mathf.Min(perSide - 1, (int)((px - origin.x) / blockSize));
                    int bz = Mathf.Min(perSide - 1, (int)((pz - origin.z) / blockSize));
                    int b = bz * perSide + bx;
                    (lists[b] ??= new List<Matrix4x4>()).Add(Matrix4x4.TRS(at, turn, new Vector3(w, h, w)));
                }
            }
        }

        foreach (List<Matrix4x4> list in lists)
        {
            if (list == null)
            {
                continue;
            }

            Bounds bounds = new Bounds(list[0].GetColumn(3), Vector3.zero);
            foreach (Matrix4x4 m in list)
            {
                bounds.Encapsulate(m.GetColumn(3));
            }

            bounds.Expand(new Vector3(width.y, height.y * 2f, width.y));
            blocks.Add(new Block { bounds = bounds, matrices = list.ToArray() });
        }
    }

    private void Update()
    {
        Camera camera = Camera.main;
        if (camera == null || blocks.Count == 0)
        {
            return;
        }

        Draw(camera);
    }

    private void Draw(Camera camera)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, planes);
        Vector3 eye = camera.transform.position;
        float far = drawDistance * drawDistance;
        for (int i = 0; i < blocks.Count; i++)
        {
            Block block = blocks[i];
            if (block.bounds.SqrDistance(eye) > far || !GeometryUtility.TestPlanesAABB(planes, block.bounds))
            {
                continue;
            }

            parameters.worldBounds = block.bounds;
            for (int start = 0; start < block.matrices.Length; start += MaxBatch)
            {
                Graphics.RenderMeshInstanced(parameters, mesh, 0, block.matrices, Mathf.Min(MaxBatch, block.matrices.Length - start), start);
            }
        }
    }
}
