using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The wall of Alex (GDD "O que buscar": the cork board with clippings, photos and string, years 0 to 10). The papers
// are drawn by SourceAssets/Tools/alex_board.py into one atlas (Art/Textures/Story/AlexBoard_Atlas.png, alpha = the
// scissor cut or the tear) with a layout (AlexBoard_Layout.json: each paper's place on the board in metres from its
// centre, its turn, its pins, and which pins the red string joins). Here: the board grows to 1.5 m, one mesh holds every
// paper (a quad each, stacked a fraction of a millimetre apart, the free corners lifted off the cork), one mesh the
// pin heads, one the string. Reading it (E) shows the board and lists the headlines in the player's language.
public static partial class FloodedHouseBuilder
{
    private sealed class BoardItem
    {
        public string key;
        public float u0, v0, u1, v1, w, h, x, y, rot;
        public float[][] pins;
    }

    private sealed class BoardString
    {
        public string a, b;
        public int pin;
    }

    private const float BoardScale = 1.7f;

    // `cork` is the CorkBoard prop as Hung placed it; `into` the room direction in plan space.
    private static void AlexBoard(Transform parent, GameObject cork, Vector2 wallPlan, Vector2 into, float centreHeight)
    {
        string atlasPath = ProjectPaths.Textures + "/Story/AlexBoard_Atlas.png";
        string layoutPath = ProjectPaths.Textures + "/Story/AlexBoard_Layout.json";
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        TextAsset layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(layoutPath);
        if (cork == null || atlas == null || layoutAsset == null)
        {
            Debug.LogWarning("AlexBoard: missing cork board, atlas or layout; the board stays empty.");
            return;
        }

        ImportAtlas(atlasPath);

        // Room-facing normal in world space (the plan may be mirrored: measure it).
        Vector3 n = M(wallPlan + into, 0f) - M(wallPlan, 0f);
        n.y = 0f;
        n.Normalize();
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(n, up);

        // Grow the board about its centre, then put its back on the wall and its centre at the asked height.
        Bounds before = RendererBounds(cork);
        cork.transform.localScale *= BoardScale;
        Bounds grown = RendererBounds(cork);
        float backBefore = Vector3.Dot(before.center, n) - Mathf.Abs(Vector3.Dot(before.extents, Abs(n)));
        float backNow = Vector3.Dot(grown.center, n) - Mathf.Abs(Vector3.Dot(grown.extents, Abs(n)));
        cork.transform.position += n * (backBefore - backNow);
        float floorY = M(wallPlan, 0f).y;
        cork.transform.position += up * (floorY + centreHeight - grown.center.y);
        Bounds b = RendererBounds(cork);

        // The cork surface (inside the frame): a ray from the room onto a temporary collider at the centre.
        MeshCollider probe = cork.GetComponentInChildren<MeshFilter>().gameObject.AddComponent<MeshCollider>();
        Physics.SyncTransforms();
        Vector3 centre = b.center;
        float surface = Vector3.Dot(centre, n) + Mathf.Abs(Vector3.Dot(b.extents, Abs(n)));
        if (probe.Raycast(new Ray(centre + n * 0.5f, -n), out RaycastHit hit, 1f))
        {
            surface = Vector3.Dot(hit.point, n);
        }

        Object.DestroyImmediate(probe);
        Vector3 origin = centre + n * (surface - Vector3.Dot(centre, n));

        // The layout JSON: JsonUtility does not read jagged arrays, so the pins come through a tiny parser.
        List<BoardItem> items = ParseBoard(layoutAsset.text, out List<BoardString> strings);
        // The layout was drawn for the grown board (about 1.5 x 0.78 m, cork 1.45 x 0.70).
        Transform group = Group("AlexBoard", parent);
        // Facing the room (+z of the group points into the wall): the meshes are built in world space and stored in
        // this frame, and the reading trigger sits in it.
        group.SetPositionAndRotation(origin, Quaternion.LookRotation(-n, up));
        Random.State saved = Random.state;
        Random.InitState(1403);

        List<Vector3> v = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<Vector3> nr = new List<Vector3>();
        List<int> tri = new List<int>();
        Dictionary<string, List<Vector3>> pinsOf = new Dictionary<string, List<Vector3>>();
        float top = 0f;
        for (int i = 0; i < items.Count; i++)
        {
            BoardItem it = items[i];
            float lift = 0.0012f + i * 0.0005f;
            top = Mathf.Max(top, lift);
            Quaternion turn = Quaternion.AngleAxis(-it.rot, n); // CCW on the board as the viewer sees it
            Vector3 c = origin + right * it.x + up * it.y;
            Vector3 hx = turn * right * (it.w * 0.5f), hy = turn * up * (it.h * 0.5f);
            int baseIndex = v.Count;
            // Corners: bottom-left, bottom-right, top-right, top-left. Pinned at the top: the bottom corners curl out.
            float curlL = Random.Range(0.001f, 0.006f), curlR = Random.Range(0.001f, 0.006f);
            v.Add(c - hx - hy + n * (lift + curlL));
            v.Add(c + hx - hy + n * (lift + curlR));
            v.Add(c + hx + hy + n * (lift + Random.Range(0f, 0.002f)));
            v.Add(c - hx + hy + n * (lift + Random.Range(0f, 0.002f)));
            uv.Add(new Vector2(it.u0, it.v0));
            uv.Add(new Vector2(it.u1, it.v0));
            uv.Add(new Vector2(it.u1, it.v1));
            uv.Add(new Vector2(it.u0, it.v1));
            for (int k = 0; k < 4; k++)
            {
                nr.Add(n);
            }

            // Wound to face n (towards the room).
            tri.AddRange(new[] { baseIndex, baseIndex + 2, baseIndex + 1, baseIndex, baseIndex + 3, baseIndex + 2 });

            List<Vector3> pins = new List<Vector3>();
            if (it.pins != null)
            {
                foreach (float[] p in it.pins)
                {
                    pins.Add(c + turn * right * (p[0] * it.w) + turn * up * (p[1] * it.h) + n * lift);
                }
            }

            pinsOf[it.key] = pins;
        }

        Mesh papers = new Mesh { name = "AlexBoardPapers" };
        papers.SetVertices(LocalTo(group, v));
        papers.SetNormals(DirLocalTo(group, nr));
        papers.SetUVs(0, uv);
        papers.SetTriangles(tri, 0);
        papers.RecalculateBounds();
        papers.RecalculateTangents();

        Material paperMat = Material("AlexBoardPapers", Shader.Find("Universal Render Pipeline/Lit"));
        paperMat.SetTexture("_BaseMap", atlas);
        paperMat.SetColor("_BaseColor", Color.white);
        paperMat.SetFloat("_AlphaClip", 1f);
        paperMat.SetFloat("_Cutoff", 0.5f);
        paperMat.EnableKeyword("_ALPHATEST_ON");
        paperMat.SetFloat("_Smoothness", 0.12f);
        paperMat.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(paperMat);
        MeshObject("Papers", group, papers, paperMat, false);

        // Pin heads: a short 8-sided cylinder each, 4.5 mm wide, standing 6 mm off the paper.
        float pinTop = top + 0.007f;
        List<Vector3> pv = new List<Vector3>();
        List<int> pt = new List<int>();
        foreach (List<Vector3> pins in pinsOf.Values)
        {
            foreach (Vector3 p in pins)
            {
                PinHead(pv, pt, p, n, right, up, 0.006f, pinTop - Vector3.Dot(p - origin, n));
            }
        }

        Mesh pinMesh = new Mesh { name = "AlexBoardPins" };
        pinMesh.SetVertices(LocalTo(group, pv));
        pinMesh.SetTriangles(pt, 0);
        pinMesh.RecalculateNormals();
        pinMesh.RecalculateBounds();
        Material pinMat = Material("AlexBoardPins", Shader.Find("Universal Render Pipeline/Lit"));
        pinMat.SetColor("_BaseColor", new Color(0.62f, 0.05f, 0.04f));
        pinMat.SetFloat("_Smoothness", 0.7f);
        EditorUtility.SetDirty(pinMat);
        MeshObject("Pins", group, pinMesh, pinMat, false);

        // Red wool string from pin to pin, wrapped round the heads (2 mm square, just under their tops).
        List<Vector3> sv = new List<Vector3>();
        List<int> st = new List<int>();
        foreach (BoardString s in strings)
        {
            if (!pinsOf.TryGetValue(s.a, out List<Vector3> pa) || !pinsOf.TryGetValue(s.b, out List<Vector3> pb) || pa.Count == 0 || pb.Count == 0)
            {
                continue;
            }

            Vector3 a = pa[Mathf.Clamp(s.pin, 0, pa.Count - 1)], z = pb[0];
            a += n * (pinTop - 0.003f - Vector3.Dot(a - origin, n));
            z += n * (pinTop - 0.003f - Vector3.Dot(z - origin, n));
            StringSegment(sv, st, a, z, n, 0.0011f);
        }

        Mesh stringMesh = new Mesh { name = "AlexBoardString" };
        stringMesh.SetVertices(LocalTo(group, sv));
        stringMesh.SetTriangles(st, 0);
        stringMesh.RecalculateNormals();
        stringMesh.RecalculateBounds();
        Material wool = Material("AlexBoardString", Shader.Find("Universal Render Pipeline/Lit"));
        wool.SetColor("_BaseColor", new Color(0.55f, 0.04f, 0.04f));
        wool.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(wool);
        MeshObject("String", group, stringMesh, wool, false);

        Random.state = saved;

        // Reading it: the board as the picture, the headlines in the player's language below.
        BoxCollider reach = group.gameObject.AddComponent<BoxCollider>();
        reach.isTrigger = true;
        reach.center = new Vector3(0f, 0f, -0.08f);
        reach.size = new Vector3(Mathf.Abs(Vector3.Dot(b.size, Abs(right))), b.size.y, 0.18f);
        ReadableNote note = group.gameObject.AddComponent<ReadableNote>();
        note.SetPrompt("prompt.board");
        note.SetText("story.board.body", "story.board.sig");
        Texture2D picture = AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Textures + "/Story/AlexBoard_Photo.png");
        note.SetArchive("parede_alex", "story.board.title", PlayerItems.EntryKind.Note, picture);
    }

    private static Vector3 Abs(Vector3 a) => new Vector3(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z));

    private static List<Vector3> LocalTo(Transform t, List<Vector3> world)
    {
        List<Vector3> o = new List<Vector3>(world.Count);
        foreach (Vector3 p in world)
        {
            o.Add(t.InverseTransformPoint(p));
        }

        return o;
    }

    private static List<Vector3> DirLocalTo(Transform t, List<Vector3> world)
    {
        List<Vector3> o = new List<Vector3>(world.Count);
        foreach (Vector3 d in world)
        {
            o.Add(t.InverseTransformDirection(d));
        }

        return o;
    }

    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat, bool shadows)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        go.isStatic = true;
        return go;
    }

    private static void PinHead(List<Vector3> v, List<int> t, Vector3 at, Vector3 n, Vector3 right, Vector3 up, float radius, float height)
    {
        const int sides = 8;
        int b = v.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            Vector3 d = (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
            v.Add(at + d);
            v.Add(at + d * 0.85f + n * height);
        }

        v.Add(at + n * height);
        int cap = v.Count - 1;
        for (int i = 0; i < sides; i++)
        {
            int i0 = b + i * 2, i1 = b + ((i + 1) % sides) * 2;
            t.AddRange(new[] { i0, i1, i1 + 1, i0, i1 + 1, i0 + 1 });
            t.AddRange(new[] { i0 + 1, i1 + 1, cap });
        }
    }

    private static void StringSegment(List<Vector3> v, List<int> t, Vector3 a, Vector3 z, Vector3 n, float half)
    {
        Vector3 along = (z - a).normalized;
        Vector3 side = Vector3.Cross(n, along).normalized * half;
        Vector3 lift = n * half;
        int b = v.Count;
        v.Add(a - side - lift); v.Add(a + side - lift); v.Add(a + side + lift); v.Add(a - side + lift);
        v.Add(z - side - lift); v.Add(z + side - lift); v.Add(z + side + lift); v.Add(z - side + lift);
        int[] faces = { 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 };
        foreach (int f in faces)
        {
            t.Add(b + f);
        }

        // Both sides: the winding of a thin box seen from any angle (no back-face holes on a 2 mm string).
        for (int i = 0; i < faces.Length; i += 3)
        {
            t.Add(b + faces[i]);
            t.Add(b + faces[i + 2]);
            t.Add(b + faces[i + 1]);
        }
    }

    private static void ImportAtlas(string path)
    {
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null)
        {
            return;
        }

        bool changed = imp.alphaIsTransparency != true || imp.maxTextureSize != 2048 || !imp.mipmapEnabled || imp.anisoLevel != 4;
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 2048;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.mipMapsPreserveCoverage = true;
        imp.alphaTestReferenceValue = 0.5f;
        if (changed)
        {
            imp.SaveAndReimport();
        }
    }

    // {"items":[{"key":..,"u0":..,...,"pins":[[x,y],..]},..],"strings":[{"a":..,"pin":..,"b":..},..]}: the file is ours,
    // written by alex_board.py, so a small field reader is enough (JsonUtility cannot read the nested pin arrays).
    private static List<BoardItem> ParseBoard(string json, out List<BoardString> strings)
    {
        List<BoardItem> items = new List<BoardItem>();
        strings = new List<BoardString>();
        int itemsAt = json.IndexOf("\"items\"");
        int stringsAt = json.IndexOf("\"strings\"");
        string itemsPart = json.Substring(itemsAt, stringsAt - itemsAt);
        string stringsPart = json.Substring(stringsAt);
        foreach (string obj in Objects(itemsPart))
        {
            BoardItem it = new BoardItem
            {
                key = Str(obj, "key"), u0 = Num(obj, "u0"), v0 = Num(obj, "v0"), u1 = Num(obj, "u1"), v1 = Num(obj, "v1"),
                w = Num(obj, "w"), h = Num(obj, "h"), x = Num(obj, "x"), y = Num(obj, "y"), rot = Num(obj, "rot"),
            };
            int p = obj.IndexOf("\"pins\"");
            List<float[]> pins = new List<float[]>();
            if (p >= 0)
            {
                string arr = obj.Substring(obj.IndexOf('[', p) + 1);
                foreach (string pair in arr.Split(new[] { ']' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string clean = pair.Replace("[", "").Replace(",", " ").Trim();
                    string[] parts = clean.Split(new[] { ' ', '\n', '\r', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        pins.Add(new[] { F(parts[0]), F(parts[1]) });
                    }
                }
            }

            it.pins = pins.ToArray();
            items.Add(it);
        }

        foreach (string obj in Objects(stringsPart))
        {
            strings.Add(new BoardString { a = Str(obj, "a"), b = Str(obj, "b"), pin = (int)Num(obj, "pin") });
        }

        return items;
    }

    private static IEnumerable<string> Objects(string s)
    {
        int depth = 0, start = -1;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '{')
            {
                if (depth++ == 0)
                {
                    start = i;
                }
            }
            else if (s[i] == '}' && --depth == 0 && start >= 0)
            {
                yield return s.Substring(start, i - start + 1);
            }
        }
    }

    private static string Str(string obj, string field)
    {
        int i = obj.IndexOf("\"" + field + "\"");
        int q1 = obj.IndexOf('"', obj.IndexOf(':', i) + 1);
        int q2 = obj.IndexOf('"', q1 + 1);
        return obj.Substring(q1 + 1, q2 - q1 - 1);
    }

    private static float Num(string obj, string field)
    {
        int i = obj.IndexOf("\"" + field + "\"");
        if (i < 0)
        {
            return 0f;
        }

        int c = obj.IndexOf(':', i) + 1;
        int e = c;
        while (e < obj.Length && "-+.0123456789eE ".IndexOf(obj[e]) >= 0)
        {
            e++;
        }

        return F(obj.Substring(c, e - c).Trim());
    }

    private static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
