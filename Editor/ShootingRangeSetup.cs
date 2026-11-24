using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Horror > Testes > Estande de tiro na cena aberta (13/10, Gustavo: "coloque um alvo de tiro na parede, tipo um
// stand de tiro"): three paper targets on the test scene's wall (the one 7.5 m ahead of the start), at eye height,
// a firing line on the floor and lane numbers. The targets are plain colliders, so every shot leaves the game's own
// bullet hole decal (ImpactEffects) where it landed: aim, hip fire and the sights can be judged by the holes.
// The target face is drawn here once (Art/Textures/Range/RangeTarget.png): white card, scoring rings, black aiming
// mark of rings 7 to 10, like a 25 m pistol target.
public static class ShootingRangeSetup
{
    public const string GroupName = "ShootingRange";
    private const string Folder = ProjectPaths.Textures + "/Range";
    private const string TexturePath = Folder + "/RangeTarget.png";
    private const string MaterialPath = Folder + "/RangeTarget.mat";

    // The wall (SuitTestScene: centre z 6, 0.2 m thick) and where the targets hang on it.
    private const float WallFace = 5.9f;
    private const float TargetSize = 0.55f;
    private const float TargetHeight = 1.55f;
    private static readonly float[] Lanes = { -2.2f, 0f, 2.2f };
    private const float FiringLine = -0.6f;

    [MenuItem("Horror/Testes/Estande de tiro na cena aberta")]
    public static void PlaceMenu()
    {
        Place();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void Place()
    {
        GameObject old = GameObject.Find(GroupName);
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }

        Material face = TargetMaterial();
        Material plain = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Transform group = new GameObject(GroupName).transform;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        for (int i = 0; i < Lanes.Length; i++)
        {
            // Backing board (a little larger, dark) and the paper face just in front of it.
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Target_" + (i + 1);
            board.transform.SetParent(group, false);
            board.transform.position = new Vector3(Lanes[i], TargetHeight, WallFace - 0.015f);
            board.transform.localScale = new Vector3(TargetSize + 0.06f, TargetSize + 0.06f, 0.03f);
            board.GetComponent<Renderer>().sharedMaterial = Board();

            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            paper.name = "Face";
            Object.DestroyImmediate(paper.GetComponent<Collider>());
            paper.transform.SetParent(group, false);
            paper.transform.position = new Vector3(Lanes[i], TargetHeight, WallFace - 0.031f);
            paper.transform.localScale = new Vector3(TargetSize, TargetSize, 1f);
            paper.transform.SetParent(board.transform, true);
            paper.GetComponent<Renderer>().sharedMaterial = face;

            Label(group, font, (i + 1).ToString(), new Vector3(Lanes[i], TargetHeight + TargetSize * 0.5f + 0.16f, WallFace - 0.02f), 0.9f);
        }

        // The firing line: a yellow strip on the floor across the lanes.
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "FiringLine";
        Object.DestroyImmediate(line.GetComponent<Collider>());
        line.transform.SetParent(group, false);
        line.transform.position = new Vector3(0f, 0.002f, FiringLine);
        line.transform.localScale = new Vector3(7f, 0.004f, 0.08f);
        Material yellow = new Material(plain) { name = "RangeLine" };
        yellow.SetColor("_BaseColor", new Color(0.85f, 0.68f, 0.1f));
        line.GetComponent<Renderer>().sharedMaterial = yellow;
        Label(group, font, "7,5 m", new Vector3(0f, 0.01f, FiringLine - 0.25f), 0.6f, true);
        Object.DestroyImmediate(plain);
    }

    private static void Label(Transform parent, TMP_FontAsset font, string text, Vector3 position, float size, bool onFloor = false)
    {
        GameObject go = new GameObject("Label_" + text);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = onFloor ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.08f, 0.08f, 0.08f);
        tmp.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
    }

    private static Material Board()
    {
        string path = Folder + "/RangeBoard.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Directory.CreateDirectory(Folder);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.32f, 0.25f, 0.17f));
            m.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(m, path);
        }

        return m;
    }

    private static Material TargetMaterial()
    {
        if (!File.Exists(TexturePath))
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(TexturePath, DrawTarget(1024).EncodeToPNG());
            AssetDatabase.ImportAsset(TexturePath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }

        Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, MaterialPath);
        }

        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // Ten scoring rings (outer radius 0.95 of the card), rings 7 to 10 filled black with white lines, an inner ten,
    // and a thin cross at the centre. Antialiased by the distance to each edge.
    private static Texture2D DrawTarget(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color paper = new Color(0.93f, 0.91f, 0.84f);
        Color ink = new Color(0.06f, 0.06f, 0.06f);
        float px = 2f / size;
        float ring = 0.95f / 10f;
        float black = ring * 4f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                bool inBlack = r < black;
                Color bg = inBlack ? ink : paper;
                Color line = inBlack ? paper : ink;
                float d = 1f;
                for (int k = 1; k <= 10; k++)
                {
                    d = Mathf.Min(d, Mathf.Abs(r - ring * k));
                }

                d = Mathf.Min(d, Mathf.Abs(r - ring * 0.5f));
                float width = 0.0035f;
                float a = 1f - Mathf.Clamp01((d - width) / px);
                Color c = Color.Lerp(bg, line, a);
                // Edge of the black: no line on it, just the step.
                float edge = Mathf.Clamp01((black - r) / px + 0.5f);
                if (Mathf.Abs(r - black) < width + px)
                {
                    c = Color.Lerp(paper, ink, edge);
                }

                pixels[y * size + x] = c;
            }
        }

        t.SetPixels(pixels);
        t.Apply();
        return t;
    }
}
