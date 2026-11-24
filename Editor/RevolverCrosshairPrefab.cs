using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds Prefabs/UI/RevolverCrosshair.prefab (the RevolverCrosshair and its pieces) and hands it to every Revolver
// in the open scene. After this the prefab is the source: open it to change sizes or colours.
public static class RevolverCrosshairPrefab
{
    public const string Path = ProjectPaths.Root + "/Prefabs/UI/RevolverCrosshair.prefab";

    private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.7f);

    [MenuItem("Horror/Combate/Mira do revólver (gerar prefab)")]
    public static void BuildAndAssign()
    {
        RevolverCrosshair prefab = Build();
        foreach (Revolver revolver in Object.FindObjectsByType<Revolver>(FindObjectsInactive.Include))
        {
            SerializedObject so = new SerializedObject(revolver);
            so.FindProperty("crosshairPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(revolver);
        }
    }

    // Loads the prefab, building it the first time.
    public static RevolverCrosshair Get()
    {
        RevolverCrosshair existing = AssetDatabase.LoadAssetAtPath<RevolverCrosshair>(Path);
        return existing != null ? existing : Build();
    }

    public static RevolverCrosshair Build()
    {
        string folder = System.IO.Path.GetDirectoryName(Path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        }

        GameObject root = new GameObject("RevolverCrosshair", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Ticks: 2 px wide, 11 long, the inner end at the gap (pivot on the inner end).
        RectTransform up = Piece(root.transform, "Up", new Vector2(2f, 11f), new Vector2(0.5f, 0f), 0f);
        RectTransform right = Piece(root.transform, "Right", new Vector2(11f, 2f), new Vector2(0f, 0.5f), 0f);
        RectTransform down = Piece(root.transform, "Down", new Vector2(2f, 11f), new Vector2(0.5f, 1f), 0f);
        RectTransform left = Piece(root.transform, "Left", new Vector2(11f, 2f), new Vector2(1f, 0.5f), 0f);
        RectTransform dot = Piece(root.transform, "Dot", new Vector2(3f, 3f), new Vector2(0.5f, 0.5f), 0f);

        // Hit marker: four short diagonals out of the centre.
        RectTransform hit = new GameObject("HitMarker", typeof(RectTransform)).GetComponent<RectTransform>();
        hit.SetParent(root.transform, false);
        hit.sizeDelta = Vector2.zero;
        Image[] lines = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            float angle = 45f + 90f * i;
            Vector2 along = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            RectTransform line = Piece(hit, "Hit" + i, new Vector2(10f, 2f), new Vector2(0f, 0.5f), angle);
            line.anchoredPosition = along * 9f;
            lines[i] = line.GetComponent<Image>();
        }

        RevolverCrosshair crosshair = root.AddComponent<RevolverCrosshair>();
        SerializedObject so = new SerializedObject(crosshair);
        so.FindProperty("group").objectReferenceValue = group;
        SerializedProperty ticks = so.FindProperty("ticks");
        ticks.arraySize = 4;
        ticks.GetArrayElementAtIndex(0).objectReferenceValue = up;
        ticks.GetArrayElementAtIndex(1).objectReferenceValue = right;
        ticks.GetArrayElementAtIndex(2).objectReferenceValue = down;
        ticks.GetArrayElementAtIndex(3).objectReferenceValue = left;
        so.FindProperty("dot").objectReferenceValue = dot.GetComponent<Image>();
        so.FindProperty("hitMarker").objectReferenceValue = hit;
        SerializedProperty hits = so.FindProperty("hitLines");
        hits.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            hits.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, Path);
        Object.DestroyImmediate(root);
        return saved.GetComponent<RevolverCrosshair>();
    }

    // A white bar with a dark outline all round, readable on the bright sky and on black water alike.
    private static RectTransform Piece(Transform parent, string name, Vector2 size, Vector2 pivot, float angle)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = Shadow;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        return rect;
    }
}
