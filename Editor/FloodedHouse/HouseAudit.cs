using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Audits the generated house for the things the eye catches in Play (Gustavo, 22/10: "texturas clipando", "móveis
// ocupando o mesmo espaço"): pairs of furniture whose boxes overlap, props sunk into walls or floating, and pairs
// of static boxes sharing a face (coplanar faces z-fight). Writes a text report. Measures, never edits.
//   Horror > Flooded House > Auditar a casa (relatório)   or   HouseAudit.Run(path)
public static class HouseAudit
{
    private const string RootName = "FloodedHouse_Generated";

    private struct Item
    {
        public string name;
        public string path;
        public Bounds bounds;
        public Transform root;
    }

    [MenuItem("Horror/Flooded House/Auditar a casa (relatório)")]
    public static void Menu()
    {
        string path = Path.Combine(Application.dataPath, "../Temp/house_audit.txt");
        Run(path);
        Debug.Log("House audit: " + path);
    }

    public static string Run(string outputPath)
    {
        GameObject root = GameObject.Find(RootName);
        StringBuilder sb = new StringBuilder();
        if (root == null)
        {
            sb.AppendLine("no " + RootName);
            File.WriteAllText(outputPath, sb.ToString());
            return sb.ToString();
        }

        List<Item> furniture = Props(root.transform, new[] { "Furniture", "HouseArt", "Story", "Flora", "GardenFlora", "ManorLamps", "LockedDoors", "Props" });
        sb.AppendLine($"props (furniture, art, story): {furniture.Count}");
        FurnitureOverlaps(furniture, sb);
        FloorContact(furniture, root.transform, sb);
        Coplanar(root.transform, sb);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, sb.ToString());
        return sb.ToString();
    }

    // One item per direct child of each listed group (a model, a lamp, a story object): its renderers' world box.
    private static List<Item> Props(Transform root, string[] groups)
    {
        List<Item> items = new List<Item>();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(groups, child.name) < 0)
            {
                continue;
            }

            foreach (Transform prop in Members(child, groups))
            {
                if (!prop.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Renderer[] renderers = prop.GetComponentsInChildren<Renderer>(false);
                bool any = false;
                Bounds b = new Bounds();
                foreach (Renderer r in renderers)
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer)
                    {
                        continue;
                    }

                    if (!any)
                    {
                        b = r.bounds;
                        any = true;
                    }
                    else
                    {
                        b.Encapsulate(r.bounds);
                    }
                }

                if (any && b.size.magnitude > 0.05f)
                {
                    items.Add(new Item { name = prop.name, path = child.name + "/" + prop.name, bounds = b, root = prop });
                }
            }
        }

        return items;
    }

    // The props of a group: its children, except that a child which is itself a listed group is skipped (the story
    // group holds the furniture group) and a child that is only a folder of many things (a flora colony: no renderer of
    // its own, many children) contributes its children instead.
    private static IEnumerable<Transform> Members(Transform group, string[] groups)
    {
        foreach (Transform child in group)
        {
            if (System.Array.IndexOf(groups, child.name) >= 0)
            {
                continue;
            }

            if (IsFolder(child))
            {
                foreach (Transform grandchild in Members(child, groups))
                {
                    yield return grandchild;
                }
            }
            else
            {
                yield return child;
            }
        }
    }

    // A folder of things rather than a thing: no renderer of its own and none on its direct children either (the
    // paintings group holds Painting_X objects which hold the frame and canvas).
    private static bool IsFolder(Transform t)
    {
        if (t.GetComponent<Renderer>() != null || t.childCount < 2)
        {
            return false;
        }

        foreach (Transform c in t)
        {
            if (c.GetComponent<Renderer>() != null)
            {
                return false;
            }
        }

        return true;
    }

    // Two props whose boxes overlap by more than a sliver of the smaller one (a wall lamp over a cabinet is fine, a
    // table through a sofa is not).
    private static void FurnitureOverlaps(List<Item> items, StringBuilder sb)
    {
        sb.AppendLine("--- prop pairs overlapping (fraction of the smaller box, overlap size)");
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = i + 1; j < items.Count; j++)
            {
                Bounds a = items[i].bounds, b = items[j].bounds;
                if (!a.Intersects(b))
                {
                    continue;
                }

                Vector3 min = Vector3.Max(a.min, b.min), max = Vector3.Min(a.max, b.max);
                Vector3 size = max - min;
                if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                {
                    continue;
                }

                float overlap = size.x * size.y * size.z;
                float smaller = Mathf.Min(a.size.x * a.size.y * a.size.z, b.size.x * b.size.y * b.size.z);
                float fraction = smaller > 1e-6f ? overlap / smaller : 0f;
                // Thin overlaps (something resting on something, a frame on a wall) are not the problem.
                float thin = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                if (fraction < 0.12f || thin < 0.06f)
                {
                    continue;
                }

                count++;
                sb.AppendLine($"  {items[i].path} x {items[j].path}: {fraction:P0}, {size.x:F2} x {size.y:F2} x {size.z:F2} m at {min + size * 0.5f}");
            }
        }

        sb.AppendLine($"  total {count}");
    }

    // Props whose box bottom is well under the floor it stands on or floating above it (the floor found by a ray down
    // from the box centre, ignoring the prop itself).
    private static void FloorContact(List<Item> items, Transform root, StringBuilder sb)
    {
        sb.AppendLine("--- props sunk into the floor (> 6 cm) or floating (> 8 cm; wall things above 1.2 m skipped)");
        int count = 0;
        RaycastHit[] hits = new RaycastHit[32];
        foreach (Item it in items)
        {
            Vector3 from = new Vector3(it.bounds.center.x, it.bounds.min.y + 0.5f, it.bounds.center.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float floor = float.NegativeInfinity;
            for (int k = 0; k < n; k++)
            {
                if (hits[k].transform.IsChildOf(it.root))
                {
                    continue;
                }

                if (hits[k].point.y > floor)
                {
                    floor = hits[k].point.y;
                }
            }

            if (float.IsNegativeInfinity(floor))
            {
                continue;
            }

            float gap = it.bounds.min.y - floor;
            if (gap < -0.06f)
            {
                count++;
                sb.AppendLine($"  SUNK {gap * 100f:F0} cm: {it.path} at {it.bounds.center}");
            }
            else if (gap > 0.08f && it.bounds.min.y - floor < 1.2f)
            {
                count++;
                sb.AppendLine($"  FLOATING {gap * 100f:F0} cm: {it.path} at {it.bounds.center}");
            }
        }

        sb.AppendLine($"  total {count}");
    }

    // Static box-shaped renderers (walls, trims, panels, slabs) whose world boxes share a face plane (within 2 mm) over
    // an area: the two surfaces draw at the same depth and flicker. Grouped by the pair of names.
    private static void Coplanar(Transform root, StringBuilder sb)
    {
        sb.AppendLine("--- coplanar face pairs (z-fighting candidates): name pair: count, example");
        List<(string name, Bounds b, bool box)> list = new List<(string, Bounds, bool)>();
        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            MeshFilter f = r.GetComponent<MeshFilter>();
            if (!r.enabled || f == null || f.sharedMesh == null)
            {
                continue;
            }

            // Only axis-aligned, flat-faced things: a box mesh has 24 vertices (or 8); rotated ones are skipped.
            Vector3 e = r.transform.eulerAngles;
            bool aligned = Near(e.x) && Near(e.y) && Near(e.z);
            bool box = f.sharedMesh.vertexCount <= 24 && f.sharedMesh.triangles.Length <= 36;
            if (!aligned || !box)
            {
                continue;
            }

            list.Add((Name(r.transform), r.bounds, box));
        }

        Dictionary<string, (int count, string example)> pairs = new Dictionary<string, (int, string)>();
        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                Bounds a = list[i].b, b = list[j].b;
                Bounds ea = a;
                ea.Expand(0.004f);
                if (!ea.Intersects(b))
                {
                    continue;
                }

                for (int axis = 0; axis < 3; axis++)
                {
                    // One box's face on the same plane as one of the other's (either side), with area in the other two axes.
                    // Faces looking the same way on the same plane (both tops, both fronts): the two surfaces coincide.
                    // A box's top on another's bottom is a joint, not a fight.
                    // Bottoms (min y) are skipped: two bottoms on one plane lie under a floor, unseen.
                    bool share = (axis != 1 && Mathf.Abs(a.min[axis] - b.min[axis]) < 0.002f) || Mathf.Abs(a.max[axis] - b.max[axis]) < 0.002f;
                    if (!share)
                    {
                        continue;
                    }

                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    float ou = Mathf.Min(a.max[u], b.max[u]) - Mathf.Max(a.min[u], b.min[u]);
                    float ov = Mathf.Min(a.max[v], b.max[v]) - Mathf.Max(a.min[v], b.min[v]);
                    if (ou < 0.03f || ov < 0.03f)
                    {
                        continue;
                    }

                    // Both must be thin-ish along that axis or at least one a surface: a slab under a wall sharing its top
                    // plane is only an edge, not a face, unless they overlap in depth too. Require the two boxes to overlap
                    // along the axis as well (one sits inside the other's slab), or one of them to be a sheet (< 2 cm).

                    string key = Pair(list[i].name, list[j].name);
                    string example = $"axis {"xyz"[axis]} at {a.center} area {ou:F2} x {ov:F2}";
                    pairs[key] = pairs.TryGetValue(key, out (int count, string ex) p) ? (p.count + 1, p.ex) : (1, example);
                    break;
                }
            }
        }

        List<string> keys = new List<string>(pairs.Keys);
        keys.Sort((x, y) => pairs[y].count.CompareTo(pairs[x].count));
        int total = 0;
        foreach (string k in keys)
        {
            total += pairs[k].count;
            sb.AppendLine($"  {k}: {pairs[k].count}, {pairs[k].example}");
        }

        sb.AppendLine($"  total {total}");
    }

    private static bool Near(float degrees)
    {
        float r = Mathf.Repeat(degrees, 90f);
        return r < 0.5f || r > 89.5f;
    }

    // The object's name without the numbering the builders add, with its parent group.
    private static string Name(Transform t)
    {
        string n = t.name;
        int cut = n.LastIndexOfAny(new[] { '_', ' ' });
        string stem = n;
        if (cut > 0 && cut < n.Length - 1 && char.IsDigit(n[cut + 1]))
        {
            stem = n.Substring(0, cut);
        }

        return (t.parent != null ? t.parent.name + "/" : "") + stem;
    }

    private static string Pair(string a, string b)
    {
        return string.CompareOrdinal(a, b) <= 0 ? a + " <> " + b : b + " <> " + a;
    }
}
