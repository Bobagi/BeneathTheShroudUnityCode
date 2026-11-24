using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Audits the generated house for the things the eye catches in Play (Gustavo, 22/10: "texturas clipando", "móveis
// ocupando o mesmo espaço"; 14/11: "dão overlap uns nos outros, ou ficam flutuando"): pairs of props whose boxes
// overlap, props sunk into the floor or floating, props pushed into a wall, and pairs of static boxes sharing a face
// (coplanar faces z-fight). Writes a text report. Measures, never edits.
// Since 14/11 the items are the leaves of the prop groups (each model, painting, colony, lamp), not the groups: the
// old version took "HallArt" or "LuminaColonies" as one item and matched it against everything. Positions are also
// given in plan coordinates (LunaManor's frame) so the fix goes straight into the builder.
//   Horror > Flooded House > Auditar a casa (relatório)   or   HouseAudit.Run(path, inventory)
public static class HouseAudit
{
    private const string RootName = "FloodedHouse_Generated";

    // Groups whose contents are props (things placed in rooms), as opposed to the house's static geometry.
    private static readonly string[] PropGroups =
    {
        "Furniture", "HouseArt", "Story", "MansionStory", "Flora", "GardenFlora", "ManorLamps", "LockedDoors", "Props", "Abandoned",
        // The dressing passes of 01/10 (Poly Haven props) that the later furniture was laid over: the console inside the
        // sofa and the doubled shelves and frames only showed when these were counted (14/11).
        "GroundFloorDressing", "PantryDressing", "BasementDressing", "LabDressing",
    };

    private struct Item
    {
        public string name;
        public string path;   // group path from the prop group down, e.g. Furniture/Sofa3, Story/HallArt/Paintings/Painting_X
        public string group;  // the top prop group
        public Bounds bounds;
        public Transform root;
        public bool onWall;   // touches a wall box (hung or pushed against it)
    }

    private struct Slab
    {
        public string name;
        public Bounds bounds;
        public int thinAxis;  // the wall's thickness axis (0 x, 2 z), -1 for a floor/ceiling slab
    }

    [MenuItem("Horror/Flooded House/Auditar a casa (relatório)")]
    public static void Menu()
    {
        string path = Path.Combine(Application.dataPath, "../Temp/house_audit.txt");
        Run(path, true);
        Debug.Log("House audit: " + path);
    }

    public static string Run(string outputPath, bool inventory = false)
    {
        GameObject root = GameObject.Find(RootName);
        StringBuilder sb = new StringBuilder();
        if (root == null)
        {
            sb.AppendLine("no " + RootName);
            File.WriteAllText(outputPath, sb.ToString());
            return sb.ToString();
        }

        List<Slab> slabs = Slabs(root.transform);
        List<Item> items = Leaves(root.transform, slabs);
        sb.AppendLine($"props (leaves of {string.Join(", ", PropGroups)}): {items.Count}; static slabs: {slabs.Count}");
        Overlaps(items, sb);
        FloorContact(items, slabs, sb);
        WallPenetration(items, slabs, sb);
        Coplanar(root.transform, sb);
        if (inventory)
        {
            Inventory(items, sb);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(outputPath, sb.ToString());
        return sb.ToString();
    }

    // ---------------------------------------------------------------- collecting

    // The leaves of every prop group: a transform that is a model (a prefab instance root, or something with a renderer
    // on itself or on a direct child). Folders (a story room, a colony group, a paintings group) are descended.
    private static List<Item> Leaves(Transform root, List<Slab> slabs)
    {
        List<Item> items = new List<Item>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
        {
            if (System.Array.IndexOf(PropGroups, t.name) < 0 || InsidePropGroup(t.parent, root))
            {
                continue;
            }

            Collect(t, t.name, t.name, items, slabs);
        }

        return items;
    }

    private static bool InsidePropGroup(Transform t, Transform root)
    {
        for (Transform p = t; p != null && p != root; p = p.parent)
        {
            if (System.Array.IndexOf(PropGroups, p.name) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void Collect(Transform folder, string path, string group, List<Item> items, List<Slab> slabs)
    {
        foreach (Transform child in folder)
        {
            if (!child.gameObject.activeInHierarchy || child.name == "Player")
            {
                continue;
            }

            string childPath = path + "/" + child.name;
            // A prop group inside another (Story holds Furniture and Abandoned): a folder, under its own group name.
            if (System.Array.IndexOf(PropGroups, child.name) >= 0)
            {
                Collect(child, childPath, child.name, items, slabs);
                continue;
            }

            if (IsModelRoot(child))
            {
                if (TryBounds(child, out Bounds b))
                {
                    Item it = new Item { name = child.name, path = childPath, group = group, bounds = b, root = child };
                    Bounds grown = b;
                    grown.Expand(0.1f);
                    foreach (Slab s in slabs)
                    {
                        if (s.thinAxis >= 0 && grown.Intersects(s.bounds))
                        {
                            it.onWall = true;
                            break;
                        }
                    }

                    items.Add(it);
                }
            }
            else
            {
                Collect(child, childPath, group, items, slabs);
            }
        }
    }

    // A model: a prefab instance root, a transform with its own renderer, or a group whose children are all leaves
    // (a painting's canvas and frame bars). A folder has children with children of their own.
    private static bool IsModelRoot(Transform t)
    {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) || HasRenderer(t))
        {
            return true;
        }

        if (t.childCount == 0)
        {
            return false;
        }

        bool anyRenderer = false;
        foreach (Transform c in t)
        {
            if (c.childCount > 0)
            {
                return false;
            }

            anyRenderer |= HasRenderer(c);
        }

        return anyRenderer;
    }

    private static bool HasRenderer(Transform t)
    {
        Renderer r = t.GetComponent<Renderer>();
        return r != null && Counts(r);
    }

    private static bool Counts(Renderer r)
    {
        return r.enabled && !(r is ParticleSystemRenderer) && !(r is LineRenderer) && !(r is TrailRenderer);
    }

    private static bool TryBounds(Transform t, out Bounds b)
    {
        bool any = false;
        b = new Bounds();
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(false))
        {
            if (!Counts(r))
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

        return any && b.size.magnitude > 0.03f;
    }

    // The static boxes of the house: axis-aligned box meshes outside the prop groups. Walls and pillars are the ones
    // at least 0.8 m tall and at most 0.6 m thick in x or z; floors and ceilings are the flat ones.
    private static List<Slab> Slabs(Transform root)
    {
        List<Slab> list = new List<Slab>();
        foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!r.enabled || InsidePropGroup(r.transform, root))
            {
                continue;
            }

            MeshFilter f = r.GetComponent<MeshFilter>();
            if (f == null || f.sharedMesh == null || f.sharedMesh.vertexCount > 24 || f.sharedMesh.triangles.Length > 36)
            {
                continue;
            }

            Vector3 e = r.transform.eulerAngles;
            if (!Near(e.x) || !Near(e.y) || !Near(e.z))
            {
                continue;
            }

            Bounds b = r.bounds;
            int thin = -1;
            if (b.size.y >= 0.8f)
            {
                if (b.size.x <= 0.6f && b.size.x <= b.size.z)
                {
                    thin = 0;
                }
                else if (b.size.z <= 0.6f)
                {
                    thin = 2;
                }
                else
                {
                    continue;
                }
            }
            else if (b.size.y > 0.4f)
            {
                continue;
            }

            list.Add(new Slab { name = Name(r.transform), bounds = b, thinAxis = thin });
        }

        return list;
    }

    // ---------------------------------------------------------------- checks

    // Two props whose boxes overlap by more than a sliver of the smaller one (a wall lamp over a cabinet is fine, a
    // table through a sofa is not). The abandonment dressing (leaves, papers, plaster) lies under and around things
    // on purpose and is left out.
    private static void Overlaps(List<Item> items, StringBuilder sb)
    {
        sb.AppendLine("--- prop pairs overlapping (fraction of the smaller box, overlap size, plan x/z of the overlap)");
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].group == "Abandoned")
            {
                continue;
            }

            for (int j = i + 1; j < items.Count; j++)
            {
                if (items[j].group == "Abandoned")
                {
                    continue;
                }

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
                float thin = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                if (fraction < 0.12f || thin < 0.05f)
                {
                    continue;
                }

                count++;
                Vector3 c = min + size * 0.5f;
                sb.AppendLine($"  {items[i].path} x {items[j].path}: {fraction:P0}, {size.x:F2} x {size.y:F2} x {size.z:F2} m at {Plan(c)}");
            }
        }

        sb.AppendLine($"  total {count}");
    }

    // Props whose bottom is well under the static floor beneath them (sunk) or with nothing under any part of their
    // footprint for more than 8 cm (floating). Things on walls are skipped for floating (hung).
    private static void FloorContact(List<Item> items, List<Slab> slabs, StringBuilder sb)
    {
        sb.AppendLine("--- props sunk into the floor (> 6 cm) or floating (> 8 cm over whatever is under their footprint; things on a wall skipped)");
        int count = 0;
        RaycastHit[] hits = new RaycastHit[64];
        foreach (Item it in items)
        {
            if (it.group == "Abandoned" && (it.name == "Paper" || it.name == "Plaster" || it.name.StartsWith("Leaf")))
            {
                continue;
            }

            Bounds b = it.bounds;
            float bottom = b.min.y;
            // Five sample points: the centre and the four corners of the footprint, 25% in.
            Vector3[] samples =
            {
                new Vector3(b.center.x, 0f, b.center.z),
                new Vector3(Mathf.Lerp(b.min.x, b.max.x, 0.25f), 0f, Mathf.Lerp(b.min.z, b.max.z, 0.25f)),
                new Vector3(Mathf.Lerp(b.min.x, b.max.x, 0.75f), 0f, Mathf.Lerp(b.min.z, b.max.z, 0.25f)),
                new Vector3(Mathf.Lerp(b.min.x, b.max.x, 0.25f), 0f, Mathf.Lerp(b.min.z, b.max.z, 0.75f)),
                new Vector3(Mathf.Lerp(b.min.x, b.max.x, 0.75f), 0f, Mathf.Lerp(b.min.z, b.max.z, 0.75f)),
            };
            float staticFloorAtCentre = float.NegativeInfinity;
            float minGap = float.PositiveInfinity;
            bool anySupport = false;
            for (int s = 0; s < samples.Length; s++)
            {
                Vector3 from = new Vector3(samples[s].x, bottom + 0.3f, samples[s].z);
                int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, 8f, ~0, QueryTriggerInteraction.Ignore);
                float support = float.NegativeInfinity;
                for (int k = 0; k < n; k++)
                {
                    Transform h = hits[k].transform;
                    if (h.IsChildOf(it.root) || h == it.root)
                    {
                        continue;
                    }

                    // Only what is below the bottom counts as support (a hit above it is something the ray passed through).
                    if (hits[k].point.y > bottom + 0.02f)
                    {
                        if (s == 0 && !IsProp(h) && hits[k].point.y > staticFloorAtCentre)
                        {
                            staticFloorAtCentre = hits[k].point.y;
                        }

                        continue;
                    }

                    if (hits[k].point.y > support)
                    {
                        support = hits[k].point.y;
                    }

                    if (s == 0 && !IsProp(h) && hits[k].point.y > staticFloorAtCentre)
                    {
                        staticFloorAtCentre = hits[k].point.y;
                    }
                }

                if (!float.IsNegativeInfinity(support))
                {
                    anySupport = true;
                    minGap = Mathf.Min(minGap, bottom - support);
                }
            }

            if (!float.IsNegativeInfinity(staticFloorAtCentre) && bottom - staticFloorAtCentre < -0.06f)
            {
                count++;
                sb.AppendLine($"  SUNK {(bottom - staticFloorAtCentre) * 100f:F0} cm: {it.path} at {Plan(b.center)} bottom y {bottom:F2}");
            }
            else if (anySupport && minGap > 0.08f && !it.onWall)
            {
                count++;
                sb.AppendLine($"  FLOATING {minGap * 100f:F0} cm: {it.path} at {Plan(b.center)} bottom y {bottom:F2}");
            }
            else if (!anySupport && !it.onWall && b.center.y < 12f)
            {
                count++;
                sb.AppendLine($"  NO SUPPORT within 8 m: {it.path} at {Plan(b.center)} bottom y {bottom:F2}");
            }
        }

        sb.AppendLine($"  total {count}");
    }

    private static bool IsProp(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            if (System.Array.IndexOf(PropGroups, p.name) >= 0)
            {
                return true;
            }

            if (p.name == RootName)
            {
                return false;
            }
        }

        return false;
    }

    // Props whose box goes more than 6 cm into a wall over an area (a model placed by its centre instead of its back,
    // or a model whose front is not +Z so it was turned the wrong way): a painting's 3 cm of frame or a curtain rail is
    // not reported.
    private static void WallPenetration(List<Item> items, List<Slab> slabs, StringBuilder sb)
    {
        sb.AppendLine("--- props pushed into a wall (> 6 cm over > 0.15 x 0.15 m)");
        int count = 0;
        foreach (Item it in items)
        {
            if (it.group == "Abandoned" || it.group == "LockedDoors")
            {
                continue;
            }

            foreach (Slab s in slabs)
            {
                if (s.thinAxis < 0 || !it.bounds.Intersects(s.bounds))
                {
                    continue;
                }

                Vector3 min = Vector3.Max(it.bounds.min, s.bounds.min), max = Vector3.Min(it.bounds.max, s.bounds.max);
                Vector3 size = max - min;
                if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                {
                    continue;
                }

                float depth = size[s.thinAxis];
                int u = s.thinAxis == 0 ? 2 : 0;
                if (depth < 0.06f || size[u] < 0.15f || size.y < 0.15f)
                {
                    continue;
                }

                count++;
                Vector3 c = min + size * 0.5f;
                sb.AppendLine($"  {it.path} into {s.name}: {depth * 100f:F0} cm deep, {size[u]:F2} x {size.y:F2} m at {Plan(c)}");
                break;
            }
        }

        sb.AppendLine($"  total {count}");
    }

    // Every item with its plan position (x, z), bottom and size, grouped: the map to find a thing by what the eye saw.
    private static void Inventory(List<Item> items, StringBuilder sb)
    {
        sb.AppendLine("--- inventory: path | plan x, z | bottom y | size x, y, z | on wall");
        items.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
        foreach (Item it in items)
        {
            if (it.group == "Abandoned")
            {
                continue;
            }

            Vector3 p = PlanXZ(it.bounds.center);
            sb.AppendLine($"  {it.path} | {p.x:F2}, {p.z:F2} | {it.bounds.min.y:F2} | {it.bounds.size.x:F2}, {it.bounds.size.y:F2}, {it.bounds.size.z:F2}{(it.onWall ? " | wall" : "")}");
        }
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

    // ---------------------------------------------------------------- helpers

    // World to plan (LunaManor's frame): PlanToWorld is linear, world = scale * plan + c.
    private static Vector3 PlanXZ(Vector3 world)
    {
        Vector3 c = FloodedHouseBuilder.PlanToWorld(Vector3.zero);
        Vector3 s = FloodedHouseBuilder.PlanToWorld(Vector3.one) - c;
        return new Vector3((world.x - c.x) / s.x, world.y, (world.z - c.z) / s.z);
    }

    private static string Plan(Vector3 world)
    {
        Vector3 p = PlanXZ(world);
        return $"plan ({p.x:F2}, {p.z:F2}) y {world.y:F2}";
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
