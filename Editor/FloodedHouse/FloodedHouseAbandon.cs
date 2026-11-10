using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The abandoned house (29/10, Gustavo: "a casa é pra ser abandonada, mas vc fez ela bem limpinha"; GDD art direction:
// abandoned tropical houses in the damp, mould, rain coming in). Over every room of the plan, seeded so a rebuild is
// the same: grime on the floors (dried mud, water marks with their tide line, mud walked in: SourceAssets/Tools/
// abandon_textures.py, from the dirt and mud photographs), dead leaves blown in (All Nature VFX's photographed leaf
// meshes), loose papers (ambientCG Paper003, aged), plaster fallen from the walls and ceilings, and a few chairs
// knocked over. Nothing here has a collider: it never blocks a path or a pickup.
public static partial class FloodedHouseBuilder
{
    private static void Abandon(Transform parent, Transform furniture)
    {
        Transform a = Group("Abandoned", parent);
        Random.State saved = Random.state;
        Random.InitState(2910);
        Material[] grime = { Grime("GrimeDust"), Grime("GrimeWater"), Grime("GrimeMud") };
        Material paper = PaperMaterial();
        Mesh[] sheets = PaperMeshes();
        GameObject[] leaves = LeafModels();
        Material plaster = Material("FallenPlaster", Shader.Find("Universal Render Pipeline/Lit"));
        plaster.SetColor("_BaseColor", new Color(0.62f, 0.58f, 0.52f));
        plaster.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(plaster);

        foreach ((string name, Vector2[] outline, int level) in LunaManor.RoomOutlines())
        {
            Rect box = Bounds2(outline);
            float area = box.width * box.height * PlanScale * PlanScale;
            float floorY = level == 0 ? 0f : UF;
            bool courtyard = name == "Galeria";
            bool service = name.StartsWith("Bunker") || name.StartsWith("Corredor") || name.StartsWith("Deposito") || name.StartsWith("Garagem");
            Transform room = Group(name, a);

            // Grime: one patch per 12 m2, 1 to 2.6 m across.
            int patches = Mathf.Clamp(Mathf.RoundToInt(area / 12f), 1, 7);
            for (int i = 0; i < patches; i++)
            {
                if (!PointIn(outline, box, courtyard, 0.4f, out Vector2 at))
                {
                    continue;
                }

                GameObject go = new GameObject("Grime");
                go.transform.SetParent(room, false);
                // A shallow box (2 cm under to 1 cm over the floor, 10/11): 0.6 m deep from 25 cm up it painted the mud on
                // the base of every chair, cabinet and small prop standing in the patch (the .357 box was all crack).
                go.transform.SetPositionAndRotation(M(at, floorY - 0.005f), Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
                DecalProjector d = go.AddComponent<DecalProjector>();
                d.material = grime[Random.Range(0, grime.Length)];
                float s = Random.Range(1f, 2.6f);
                d.size = new Vector3(s, s * Random.Range(0.7f, 1f), 0.03f);
                d.pivot = Vector3.zero;
                d.fadeFactor = Random.Range(0.55f, 0.95f);
                d.drawDistance = 35f;
            }

            // Dead leaves: thick by the courtyard and the doors out, a few everywhere else.
            int leafCount = courtyard ? 90 : Mathf.Clamp(Mathf.RoundToInt(area / (service ? 8f : 4f)), 2, 24);
            for (int i = 0; i < leafCount && leaves.Length > 0; i++)
            {
                if (!PointIn(outline, box, courtyard, 0.15f, out Vector2 at))
                {
                    continue;
                }

                Leaf(room, leaves[Random.Range(0, leaves.Length)], M(at, floorY + 0.004f));
            }

            // Loose papers, in the rooms people lived and worked in.
            if (!service && !name.StartsWith("Banheiro") && !name.StartsWith("Gerador"))
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(area / 9f), 1, 6);
                for (int i = 0; i < count; i++)
                {
                    if (!PointIn(outline, box, courtyard, 0.35f, out Vector2 at))
                    {
                        continue;
                    }

                    GameObject sheet = new GameObject("Paper");
                    sheet.transform.SetParent(room, false);
                    sheet.transform.SetPositionAndRotation(M(at, floorY + 0.006f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    sheet.transform.localScale = Vector3.one * Random.Range(0.85f, 1.1f);
                    sheet.AddComponent<MeshFilter>().sharedMesh = sheets[Random.Range(0, sheets.Length)];
                    MeshRenderer r = sheet.AddComponent<MeshRenderer>();
                    r.sharedMaterial = paper;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // Plaster fallen from the walls and ceiling: chunks along the walls, more upstairs (under the roof).
            int chunks = Mathf.Clamp(Mathf.RoundToInt(area / (level == 0 ? 10f : 6f)), 2, 14);
            for (int i = 0; i < chunks; i++)
            {
                Vector2 wallPoint = AlongWall(outline);
                if (!InsideOutline(wallPoint, outline) || (courtyard && LunaManor.Garden.Contains(wallPoint)))
                {
                    continue;
                }

                int pieces = Random.Range(2, 6);
                for (int k = 0; k < pieces; k++)
                {
                    Vector2 q = wallPoint + Random.insideUnitCircle * 0.22f;
                    Vector3 size = new Vector3(Random.Range(0.03f, 0.16f), Random.Range(0.01f, 0.04f), Random.Range(0.03f, 0.12f));
                    GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    chunk.name = "Plaster";
                    Object.DestroyImmediate(chunk.GetComponent<Collider>());
                    chunk.transform.SetParent(room, false);
                    chunk.transform.SetPositionAndRotation(M(q, floorY + size.y * 0.5f), Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f)));
                    chunk.transform.localScale = size;
                    MeshRenderer r = chunk.GetComponent<MeshRenderer>();
                    r.sharedMaterial = plaster;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        KnockOver(furniture);
        Random.state = saved;
    }

    // A random point of the room's outline `inset` metres (plan) from its edge; the courtyard's garden is excluded
    // (the lawn and its water have their own dressing).
    private static bool PointIn(Vector2[] outline, Rect box, bool courtyard, float inset, out Vector2 point)
    {
        for (int tries = 0; tries < 30; tries++)
        {
            point = new Vector2(Random.Range(box.xMin + inset, box.xMax - inset), Random.Range(box.yMin + inset, box.yMax - inset));
            if (!InsideOutline(point, outline) || (courtyard && LunaManor.Garden.Contains(point)))
            {
                continue;
            }

            bool clear = true;
            for (int i = 0; i < outline.Length && clear; i++)
            {
                clear = DistanceToSegment(point, outline[i], outline[(i + 1) % outline.Length]) >= inset;
            }

            if (clear)
            {
                return true;
            }
        }

        point = Vector2.zero;
        return false;
    }

    // A point 0.3 m (plan) inside a random edge of the outline.
    private static Vector2 AlongWall(Vector2[] outline)
    {
        int i = Random.Range(0, outline.Length);
        Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
        Vector2 on = Vector2.Lerp(a, b, Random.Range(0.1f, 0.9f));
        Vector2 dir = (b - a).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        Vector2 inside = on + normal * 0.3f;
        return InsideOutline(inside, outline) ? inside : on - normal * 0.3f;
    }

    private static Rect Bounds2(Vector2[] poly)
    {
        Vector2 min = poly[0], max = poly[0];
        foreach (Vector2 q in poly)
        {
            min = Vector2.Min(min, q);
            max = Vector2.Max(max, q);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static bool InsideOutline(Vector2 q, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    private static Material Grime(string name)
    {
        string folder = ProjectPaths.Art + "/VFX/Textures/Abandon";
        string texturePath = folder + "/" + name + ".png";
        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer && (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency))
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        Material m = Material(name, Shader.Find("Shader Graphs/Decal"));
        m.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material PaperMaterial()
    {
        string texturePath = ProjectPaths.Art + "/VFX/Textures/Abandon/PaperSheets.png";
        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp))
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        Material m = Material("LoosePaper", Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.SetFloat("_Smoothness", 0.08f);
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    // One flat sheet per quarter of the atlas (21 x 29 cm, lying on the floor, slightly curled: the corners lifted).
    private static Mesh[] PaperMeshes()
    {
        Mesh[] meshes = new Mesh[4];
        for (int i = 0; i < 4; i++)
        {
            float u0 = (i % 2) * 0.5f, v0 = 0.5f - (i / 2) * 0.5f;
            float hw = 0.105f, hl = 0.145f, lift = Random.Range(0.004f, 0.015f);
            Mesh m = new Mesh { name = "PaperSheet" + i };
            m.SetVertices(new List<Vector3>
            {
                new Vector3(-hw, lift, -hl), new Vector3(hw, 0f, -hl), new Vector3(hw, lift * 0.5f, hl), new Vector3(-hw, 0f, hl),
            });
            // The sheet sits in the middle 72% of its quarter's width.
            float du = 0.5f * 0.14f;
            m.SetUVs(0, new List<Vector2> { new Vector2(u0 + du, v0), new Vector2(u0 + 0.5f - du, v0), new Vector2(u0 + 0.5f - du, v0 + 0.5f), new Vector2(u0 + du, v0 + 0.5f) });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            meshes[i] = m;
        }

        return meshes;
    }

    private static GameObject[] LeafModels()
    {
        List<GameObject> found = new List<GameObject>();
        for (int i = 1; i <= 4; i++)
        {
            GameObject g = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.AllNatureVfx + "/Art/Models/leaf_0" + i + ".fbx");
            if (g != null)
            {
                found.Add(g);
            }
        }

        return found.ToArray();
    }

    // A leaf lying flat, 6 to 12 cm long, with the pack's photographed leaves material.
    private static void Leaf(Transform parent, GameObject model, Vector3 at)
    {
        GameObject leaf = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        leaf.name = "Leaf";
        leaf.transform.position = at;
        leaf.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.AllNatureVfx + "/Art/Materials/" + (Random.value < 0.5f ? "Leaves.mat" : "Leaves2.mat"));
        foreach (Renderer r in leaf.GetComponentsInChildren<Renderer>())
        {
            if (mat != null)
            {
                r.sharedMaterial = mat;
            }

            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        foreach (Collider c in leaf.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(c);
        }

        Bounds b = RendererBounds(leaf);
        // Laid flat if the file stands it up, then scaled to a real leaf.
        if (b.size.y > Mathf.Min(b.size.x, b.size.z))
        {
            leaf.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            b = RendererBounds(leaf);
        }

        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        leaf.transform.localScale *= Random.Range(0.06f, 0.12f) / Mathf.Max(longest, 1e-4f);
        b = RendererBounds(leaf);
        leaf.transform.position += Vector3.up * (at.y - b.min.y);
    }

    // A few chairs knocked over: turned on their side or back and dropped to the floor.
    private static void KnockOver(Transform furniture)
    {
        if (furniture == null)
        {
            return;
        }

        int done = 0;
        foreach (Transform t in furniture)
        {
            string n = t.name;
            if (!(n.StartsWith("DiningChair") || n.StartsWith("GreenChair") || n.StartsWith("ArmChair")) || Random.value > 0.45f || done >= 4)
            {
                continue;
            }

            Bounds before = RendererBounds(t.gameObject);
            float floorY = before.min.y;
            t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(Random.value < 0.5f ? 90f : -90f, 0f, 0f);
            Bounds after = RendererBounds(t.gameObject);
            t.position += Vector3.up * (floorY - after.min.y);
            foreach (Collider c in t.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(c);
            }

            done++;
        }
    }
}
