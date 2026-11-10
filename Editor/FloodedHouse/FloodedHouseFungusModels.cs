using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The fungus of the house from real scans (10/11, Gustavo: "as texturas e modelos dos fungos estavam muito horríveis";
// they were Unity cylinders with squashed spheres on top). Sketchfab, CC BY (CREDITS.md):
//   OysterCluster   blue oyster mushrooms (v.a.c.u.u.m.): shelves on walls and ceilings.
//   BracketFungus   Polyporus (matousekfoto): a bracket on a wall.
//   PuffballCluster a bunch of puffballs (3dhdscan): on floors.
//   BonnetCluster   common bonnets (ljacobson): thin Mycena-like stalks around every clump, the ones that glow most.
// The glow is the scan's own colour as emission, tinted per kind (blue, pink, green as before), so the caps and gills
// light where the real mushroom is pale and stay dark where it is brown: bioluminescence on a real surface, not a
// neon toy. The halo on the surface (SurfaceGlow) and the mould film stay as they were.
public static partial class FloodedHouseBuilder
{
    private static readonly Color[] FungusTints = { new Color(0.1f, 0.45f, 1f), new Color(1f, 0.2f, 0.7f), new Color(0.3f, 1f, 0.38f) };

    // How each model sits once ModelProp has applied the FBX root (identity yaw): which local direction touches the
    // surface it grows from, and which is its top (optimiser report 10/11: the FBX root turns Blender +Z into Unity +Y);
    // `size` takes the real scan size (oyster 0.20 m, bracket 0.37 m, puffballs 0.12 m, bonnets 7.5 cm) to the clump.
    private struct FungusModel
    {
        public string name;
        public Vector3 attach;
        public Vector3 top;
        public float glow;
        public float size;
    }

    private static readonly FungusModel Oyster = new FungusModel { name = "OysterCluster", attach = Vector3.down, top = Vector3.forward, glow = 0.45f, size = 1.6f };
    private static readonly FungusModel Bracket = new FungusModel { name = "BracketFungus", attach = Vector3.back, top = Vector3.up, glow = 0.35f, size = 0.8f };
    private static readonly FungusModel Puffball = new FungusModel { name = "PuffballCluster", attach = Vector3.down, top = Vector3.forward, glow = 0.4f, size = 1.7f };
    private static readonly FungusModel Bonnet = new FungusModel { name = "BonnetCluster", attach = Vector3.down, top = Vector3.forward, glow = 1.0f, size = 1.2f };

    private static bool FungusModelsReady()
    {
        foreach (string n in new[] { Oyster.name, Bracket.name, Puffball.name, Bonnet.name })
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/" + n + "/" + n + ".fbx") == null)
            {
                return false;
            }
        }

        return true;
    }

    // One clump at `at` on a surface whose normal is `outward`: a main piece chosen by the surface (shelves on walls and
    // ceilings, puffballs on floors) and 2 to 4 bonnet tufts around it.
    private static void FungusModelsCluster(Transform cluster, Vector3 at, Vector3 outward, int kind, float scale)
    {
        outward.Normalize();
        bool floor = outward.y > 0.6f;
        FungusModel main = floor ? Puffball : (Random.value < 0.4f ? Oyster : Bracket);
            PlaceFungus(cluster, main, kind, at, outward, Random.Range(0.85f, 1.25f) * scale, Random.Range(0f, 360f));
        int tufts = Random.Range(3, 7);
        Vector3 side = Vector3.Cross(outward, Mathf.Abs(outward.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
        Vector3 side2 = Vector3.Cross(outward, side);
        for (int i = 0; i < tufts; i++)
        {
            Vector2 spot = Random.insideUnitCircle.normalized * Random.Range(0.12f, 0.3f) * scale;
            Vector3 p = at + side * spot.x + side2 * spot.y;
            PlaceFungus(cluster, Bonnet, kind, p, outward, Random.Range(0.8f, 1.5f) * scale, Random.Range(0f, 360f));
        }
    }

    private static void PlaceFungus(Transform parent, FungusModel model, int kind, Vector3 at, Vector3 outward, float size, float spin)
    {
        GameObject m = ModelProp(model.name, parent, at, Quaternion.identity, false, false);
        m.transform.localScale *= size * model.size;
        // Model frame as it stands after ModelProp (attach and top are given in that world frame).
        Quaternion baseRot = m.transform.rotation;
        Vector3 attachW = model.attach;
        Vector3 topW = model.top;
        // Target: the attach side against the surface (towards -outward); the top as close to world up as the surface
        // allows (on a floor any heading, turned by `spin`).
        Vector3 upHint = Mathf.Abs(outward.y) > 0.9f ? Quaternion.AngleAxis(spin, outward) * Vector3.forward : Vector3.up;
        Vector3 targetTop = Vector3.ProjectOnPlane(upHint, outward).normalized;
        Quaternion from = Quaternion.LookRotation(attachW, topW);
        // A stalked piece (attach = down) on a wall or ceiling grows out and up like a shelf, not straight at the
        // viewer: its stalk leans 50 degrees from the surface normal towards the sky (seen face on, the oyster
        // rosette read as a flat flower, 10/11).
        Vector3 growth = outward;
        if (model.attach == Vector3.down && Mathf.Abs(outward.y) < 0.6f)
        {
            growth = Vector3.Slerp(outward, Vector3.up, 0.55f).normalized;
            targetTop = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(outward, growth), growth).normalized;
            if (targetTop.sqrMagnitude < 0.01f)
            {
                targetTop = Vector3.ProjectOnPlane(Vector3.forward, growth).normalized;
            }
        }

        Quaternion to = Quaternion.LookRotation(-growth, targetTop);
        // Wall pieces get a small random roll so the shelves are not all level.
        Quaternion roll = Mathf.Abs(outward.y) > 0.9f ? Quaternion.identity : Quaternion.AngleAxis(Random.Range(-18f, 18f), outward);
        m.transform.rotation = roll * to * Quaternion.Inverse(from) * baseRot;
        // Seat it: the attach-most point of the renderers on the surface point, sunk 5 mm so no gap shows.
        Bounds b = RendererBounds(m);
        float reach = Vector3.Dot(b.center - at, -outward) + Mathf.Abs(Vector3.Dot(b.extents, Abs(outward)));
        Vector3 lateral = Vector3.ProjectOnPlane(b.center - at, outward);
        m.transform.position += outward * (reach - 0.005f) - lateral;

        Material mat = FungusGlowMaterial(model, kind);
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        foreach (Collider c in m.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(c);
        }
    }

    private static Material FungusGlowMaterial(FungusModel model, int kind)
    {
        string folder = ProjectPaths.PropModels + "/" + model.name;
        Material m = Material("Fungus_" + model.name + "_" + kind, Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + model.name + "_BaseColor.png");
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + model.name + "_Normal.png"));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + model.name + "_MetallicSmoothness.png"));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        // Wet flesh: a little sheen over the scan's own.
        m.SetFloat("_Smoothness", 0.7f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", albedo);
        m.SetColor("_EmissionColor", FungusTints[kind] * model.glow);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }
}
