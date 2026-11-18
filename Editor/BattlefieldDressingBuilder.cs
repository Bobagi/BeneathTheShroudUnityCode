using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Dresses the battlefield: timber revetments, duckboards and sandbags along the trenches, barbed
// wire belts in no man's land, shattered trees, graves, supply dumps, a wrecked gun, fires, smoke
// plumes on the horizon and the colossus. Everything lives under one root so it can be rebuilt.
public static class BattlefieldDressingBuilder
{
    private const string RootName = "Battlefield_Dressing";
    private const string Models = ProjectPaths.Models + "/";
    private const string Props = ProjectPaths.Props + "/";
    private const string Vfx = ProjectPaths.Vfx + "/";

    private static Terrain inner;
    private static Terrain outer;
    private static System.Random random;

    [MenuItem("Horror/Battlefield/3 Dress Battlefield")]
    public static void Build()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        inner = GameObject.Find("Battlefield_Inner").GetComponent<Terrain>();
        outer = GameObject.Find("Battlefield_Outer").GetComponent<Terrain>();
        random = new System.Random(1914);

        Transform root = new GameObject(RootName).transform;
        Transform trenches = Group(root, "Trenches");
        Transform wire = Group(root, "BarbedWire");
        Transform nature = Group(root, "DeadTrees");
        Transform clutter = Group(root, "Clutter");
        Transform distant = Group(root, "Distant");

        foreach (BattlefieldLayout.Trench trench in BattlefieldLayout.Trenches)
        {
            DressTrench(trench, trenches);
        }

        PlaceWireBelts(wire);
        PlaceTrees(nature);
        PlaceClutter(clutter);
        PlaceDistant(distant);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("[Battlefield] Dressing built.");
    }

    // ---------- helpers ----------

    private static Transform Group(Transform parent, string name)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static float Height(float x, float z)
    {
        Terrain terrain = Mathf.Abs(x) < BattlefieldLayout.InnerSize * 0.5f && Mathf.Abs(z) < BattlefieldLayout.InnerSize * 0.5f
            ? inner
            : outer;
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    private static Vector3 Normal(float x, float z)
    {
        Terrain terrain = Mathf.Abs(x) < BattlefieldLayout.InnerSize * 0.5f && Mathf.Abs(z) < BattlefieldLayout.InnerSize * 0.5f
            ? inner
            : outer;
        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;
        return terrain.terrainData.GetInterpolatedNormal((x - origin.x) / size.x, (z - origin.z) / size.z);
    }

    private static GameObject Spawn(string path, Transform parent, Vector3 position, Quaternion rotation, float scale = 1f, bool collider = true)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning("[Battlefield] Missing " + path);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        instance.transform.SetPositionAndRotation(position, rotation * instance.transform.rotation);
        instance.transform.localScale = instance.transform.localScale * scale;
        if (collider)
        {
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponent<Collider>() == null)
                {
                    MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = filter.sharedMesh;
                }
            }
        }

        instance.isStatic = true;
        foreach (Transform child in instance.GetComponentsInChildren<Transform>())
        {
            child.gameObject.isStatic = true;
        }

        return instance;
    }

    private static Quaternion GroundAligned(float x, float z, float yaw, float tiltAmount)
    {
        Vector3 up = Vector3.Slerp(Vector3.up, Normal(x, z), tiltAmount);
        return Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f);
    }

    // ---------- trenches ----------

    private static void DressTrench(BattlefieldLayout.Trench trench, Transform parent)
    {
        Transform group = Group(parent, trench.name);
        bool friendly = trench.name != "EnemyTrench";
        List<Vector2> points = trench.points;
        float floorOffset = BattlefieldLayout.TrenchFloorHalfWidth + 0.02f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.5f)
            {
                continue;
            }

            Vector2 direction = delta / length;
            Vector2 side = new Vector2(-direction.y, direction.x);
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

            // Timber revetments on both walls, planks facing into the trench.
            int panels = Mathf.Max(1, Mathf.RoundToInt(length / 4f));
            float panelLength = length / panels;
            for (int p = 0; p < panels; p++)
            {
                float t = (p + 0.5f) / panels;
                Vector2 center = Vector2.Lerp(a, b, t);
                float localDepth = -BattlefieldLayout.TrenchOffset(center.x, center.y);
                if (localDepth < 0.9f)
                {
                    continue; // shallow exit ramp: bare earth
                }

                foreach (float s in new[] { 1f, -1f })
                {
                    if (!friendly && random.NextDouble() < 0.4)
                    {
                        continue;
                    }

                    Vector2 wallPoint = center + side * s * floorOffset;
                    float floorY = Height(center.x, center.y);
                    // The panel's flat back goes against the wall, its planked face (+Z) inward.
                    Quaternion facing = Quaternion.LookRotation(new Vector3(-side.x * s, 0f, -side.y * s), Vector3.up)
                        * Quaternion.Euler(Range(-3f, 1f), 0f, Range(-1.5f, 1.5f));
                    GameObject panel = Spawn(Models + "TrenchRevetment.fbx", group,
                        new Vector3(wallPoint.x, floorY - 0.1f, wallPoint.y), facing);
                    if (panel != null)
                    {
                        panel.transform.localScale = new Vector3(panelLength / 4f * 1.02f, localDepth / 2.1f * Range(0.95f, 1.08f), 1f);
                    }
                }
            }

            // Duckboards along the floor.
            int boards = Mathf.Max(1, Mathf.FloorToInt(length / 2f));
            for (int d = 0; d < boards; d++)
            {
                if (random.NextDouble() < 0.12)
                {
                    continue;
                }

                Vector2 at = Vector2.Lerp(a, b, (d + 0.5f) / boards);
                Spawn(Models + "DuckBoard.fbx", group,
                    new Vector3(at.x, Height(at.x, at.y) + 0.01f, at.y),
                    Quaternion.Euler(Range(-2f, 2f), yaw + Range(-3f, 3f), Range(-2f, 2f)));
            }

            // Sandbag parapet on the enemy-facing lip of fire trenches.
            if (trench.hasParapet)
            {
                float enemySign = Vector2.Dot(side, friendly ? Vector2.up : Vector2.down) >= 0f ? 1f : -1f;
                if (friendly && Mathf.Abs(direction.x) > 0.7f)
                {
                    BuildFireStep(group, a, b, side * enemySign, yaw);
                }

                // Modules tile every 2 m (their ragged ends interlock); cover the whole segment plus a
                // little overlap so consecutive segments read as one continuous parapet.
                int walls = Mathf.Max(1, Mathf.CeilToInt((length + 0.6f) / 2f));
                float span = walls * 2f;
                Vector2 wallStart = a + direction * ((length - span) * 0.5f + 1f);
                Quaternion facing = Quaternion.LookRotation(new Vector3(side.x * enemySign, 0f, side.y * enemySign), Vector3.up);
                for (int w = 0; w < walls; w++)
                {
                    Vector2 at = wallStart + direction * (w * 2f) + side * enemySign * (BattlefieldLayout.TrenchTopHalfWidth + 0.45f);
                    float y = Height(at.x, at.y) - 0.18f;
                    string module = friendly ? "SandbagWall_v2.fbx" : (random.NextDouble() < 0.5 ? "SandbagWall_v2.fbx" : "SandbagWall_v2_Low.fbx");
                    Spawn(Models + module, group, new Vector3(at.x, y, at.y), facing);
                }
            }
        }
    }

    // Timber fire-step along the enemy-side wall: stand on it to see (and shoot) over the parapet.
    private static void BuildFireStep(Transform parent, Vector2 a, Vector2 b, Vector2 enemySide, float yaw)
    {
        float length = Vector2.Distance(a, b) - 0.3f;
        if (length < 1f)
        {
            return;
        }

        Vector2 mid = (a + b) * 0.5f;
        float offset = BattlefieldLayout.TrenchFloorHalfWidth - BattlefieldLayout.FireStepDepth * 0.5f;
        Vector2 at = mid + enemySide * offset;
        float floor = Height(mid.x, mid.y);
        float height = BattlefieldLayout.FireStepHeight;

        GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
        step.name = "FireStep";
        step.transform.SetParent(parent, false);
        // Sink it a little so it meets the uneven floor everywhere.
        step.transform.position = new Vector3(at.x, floor + height * 0.5f - 0.1f, at.y);
        step.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        step.transform.localScale = new Vector3(BattlefieldLayout.FireStepDepth, height + 0.2f, length);
        step.GetComponent<Renderer>().sharedMaterial = BattlefieldAssetSetup.GetMaterial("Wood");
        step.isStatic = true;

        // Duckboard decking on top so it reads as built timber rather than a box.
        for (float d = -length * 0.5f + 1f; d <= length * 0.5f - 0.9f; d += 2f)
        {
            Vector2 boardAt = at + (b - a).normalized * d;
            Spawn(Models + "DuckBoard.fbx", parent, new Vector3(boardAt.x, floor + height - 0.02f, boardAt.y),
                Quaternion.Euler(0f, yaw, 0f), 0.95f, false);
        }
    }

    // ---------- no man's land ----------

    private static void PlaceWireBelts(Transform parent)
    {
        foreach (float beltZ in new[] { 11f, 16f, 104f, 99f })
        {
            for (float x = -175f; x < 175f; x += 4.3f)
            {
                if (random.NextDouble() < 0.1)
                {
                    x += Range(3f, 10f); // gaps blown open by shelling
                    continue;
                }

                float z = beltZ + Range(-1.2f, 1.2f);
                if (BattlefieldLayout.DistanceToTrench(new Vector2(x, z), out _) < 3f)
                {
                    continue;
                }

                Spawn(Models + "BarbedWire.fbx", parent, new Vector3(x, Height(x, z) - 0.05f, z),
                    GroundAligned(x, z, 90f + Range(-12f, 12f), 0.7f), Range(0.9f, 1.1f), false);
            }
        }

        // Loose pickets and stakes scattered between the belts.
        for (int i = 0; i < 80; i++)
        {
            float x = Range(-180f, 180f);
            float z = Range(18f, 98f);
            Spawn(Models + "WoodenPost.fbx", parent, new Vector3(x, Height(x, z) - 0.15f, z),
                Quaternion.Euler(Range(-15f, 15f), Range(0f, 360f), Range(-15f, 15f)), Range(0.8f, 1.2f), false);
        }
    }

    private static void PlaceTrees(Transform parent)
    {
        string[] trees = { Models + "DeadTree_A.fbx", Models + "DeadTree_B.fbx", Models + "TreeStump.fbx" };
        for (int i = 0; i < 70; i++)
        {
            float x = Range(-190f, 190f);
            float z = Range(-190f, 190f);
            if (BattlefieldLayout.DistanceToTrench(new Vector2(x, z), out _) < 5f)
            {
                continue;
            }

            string path = trees[random.Next(trees.Length)];
            Spawn(path, parent, new Vector3(x, Height(x, z) - 0.2f, z),
                Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f)), Range(0.8f, 1.35f));
        }

        // Fallen trunks and roots.
        for (int i = 0; i < 25; i++)
        {
            float x = Range(-190f, 190f);
            float z = Range(-150f, 190f);
            if (BattlefieldLayout.DistanceToTrench(new Vector2(x, z), out _) < 4f)
            {
                continue;
            }

            string path = random.NextDouble() < 0.6 ? Props + "dead_tree_trunk_02/dead_tree_trunk_02_2k.fbx" : Props + "root_cluster_02/root_cluster_02_1k.fbx";
            Spawn(path, parent, new Vector3(x, Height(x, z) - 0.1f, z), GroundAligned(x, z, Range(0f, 360f), 1f), Range(0.9f, 1.4f));
        }

        // Silhouettes of a dead forest on the far ridges.
        for (int i = 0; i < 90; i++)
        {
            float angle = Range(-80f, 80f) * Mathf.Deg2Rad;
            float distance = Range(260f, 900f);
            float x = Mathf.Sin(angle) * distance;
            float z = Mathf.Cos(angle) * distance;
            Spawn(trees[random.Next(2)], parent, new Vector3(x, Height(x, z) - 0.3f, z),
                Quaternion.Euler(Range(-6f, 6f), Range(0f, 360f), Range(-6f, 6f)), Range(1.4f, 2.4f), false);
        }
    }

    private static void PlaceClutter(Transform parent)
    {
        // Supplies stacked in the fire trench bays, skipping the spawn bay.
        string[] supplies =
        {
            Models + "AmmoCrate.fbx",
            Props + "wooden_crate_01/wooden_crate_01_1k.fbx",
            Props + "old_military_crate/old_military_crate_1k.fbx",
            Props + "metal_jerrycan_green/metal_jerrycan_green_1k.fbx",
            Props + "wooden_bucket_01/wooden_bucket_01_1k.fbx",
            Props + "ammo_box/ammo_box_1k.fbx",
            Props + "medical_box/medical_box_1k.fbx",
        };

        BattlefieldLayout.Trench fire = null;
        foreach (BattlefieldLayout.Trench trench in BattlefieldLayout.Trenches)
        {
            if (trench.name == "FireTrench")
            {
                fire = trench;
            }
        }

        for (int i = 0; i < fire.points.Count - 1; i += 2)
        {
            Vector2 a = fire.points[i];
            Vector2 b = fire.points[i + 1];
            if (Mathf.Abs(a.x) < 12f || random.NextDouble() < 0.35)
            {
                continue;
            }

            Vector2 direction = (b - a).normalized;
            Vector2 at = Vector2.Lerp(a, b, Range(0.2f, 0.8f)) + new Vector2(-direction.y, direction.x) * 0.45f;
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg + 90f;
            Spawn(supplies[random.Next(supplies.Length)], parent, new Vector3(at.x, Height(at.x, at.y), at.y),
                Quaternion.Euler(0f, yaw + Range(-10f, 10f), 0f));
        }

        // Tools and weapons left behind along the lines.
        string[] leftovers =
        {
            Props + "rusted_spade_01/rusted_spade_01_1k.fbx",
            Props + "bolt_action_rifle_7_62/bolt_action_rifle_7_62_1k.fbx",
            Props + "stick_grenade/stick_grenade_1k.fbx",
            Props + "can_rusted/can_rusted_1k.fbx",
            Props + "old_gas_mask/old_gas_mask_1k.fbx",
        };
        for (int i = 0; i < 70; i++)
        {
            float x = Range(-150f, 150f);
            float z = Range(-60f, 110f);
            Spawn(leftovers[random.Next(leftovers.Length)], parent, new Vector3(x, Height(x, z) + 0.03f, z),
                GroundAligned(x, z, Range(0f, 360f), 1f) * Quaternion.Euler(0f, 0f, 90f), 1f, false);
        }

        // A small cemetery of hasty graves behind the support trench.
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                float x = -70f + col * 2.4f + Range(-0.3f, 0.3f);
                float z = -78f - row * 3f + Range(-0.3f, 0.3f);
                Spawn(Models + "WoodenCross.fbx", parent, new Vector3(x, Height(x, z) - 0.25f, z),
                    Quaternion.Euler(Range(-8f, 8f), 180f + Range(-10f, 10f), Range(-10f, 10f)));
            }
        }

        // Lone crosses where men fell.
        for (int i = 0; i < 18; i++)
        {
            float x = Range(-160f, 160f);
            float z = Range(-120f, 100f);
            Spawn(Models + "WoodenCross.fbx", parent, new Vector3(x, Height(x, z) - 0.25f, z),
                Quaternion.Euler(Range(-15f, 15f), Range(0f, 360f), Range(-15f, 15f)));
        }

        // Rocks, loose sandbags and craters' debris.
        for (int i = 0; i < 60; i++)
        {
            float x = Range(-190f, 190f);
            float z = Range(-190f, 190f);
            bool boulder = random.NextDouble() < 0.35;
            string path = boulder ? Props + "boulder_01/boulder_01_2k.fbx" : (random.NextDouble() < 0.5 ? Models + "SandbagPile_v2.fbx" : Models + "Sandbag_v2.fbx");
            Spawn(path, parent, new Vector3(x, Height(x, z) - (boulder ? 0.3f : 0f), z),
                GroundAligned(x, z, Range(0f, 360f), 1f), boulder ? Range(0.6f, 1.6f) : 1f);
        }

        // Wrecked field gun and a barrel dump behind the lines.
        Spawn(Props + "cannon_01/cannon_01_1k.fbx", parent, new Vector3(38f, Height(38f, -86f) - 0.25f, -86f),
            GroundAligned(38f, -86f, 25f, 1f) * Quaternion.Euler(0f, 0f, 12f), 1.6f);
        Spawn(Props + "wooden_barrels_01/wooden_barrels_01_1k.fbx", parent, new Vector3(-20f, Height(-20f, -62f), -62f),
            GroundAligned(-20f, -62f, 70f, 1f));
        Spawn(Props + "cannon_01/cannon_01_1k.fbx", parent, new Vector3(-62f, Height(-62f, 55f) - 0.6f, 55f),
            GroundAligned(-62f, 55f, 160f, 1f) * Quaternion.Euler(-18f, 0f, 35f), 1.5f);

        PlaceTrenchLanterns(parent);

        // Fires: a brazier near the spawn, burning wreckage out in no man's land and in the enemy line.
        SpawnFire(parent, new Vector2(-6.5f, 0.2f), 0.35f);
        SpawnFire(parent, new Vector2(-62f, 55f), 1.2f);
        SpawnFire(parent, new Vector2(45f, 70f), 0.9f);
        SpawnFire(parent, new Vector2(-30f, 118f), 1.4f);
        SpawnFire(parent, new Vector2(80f, 121f), 1.1f);
        SpawnFire(parent, new Vector2(38f, -86f), 0.7f);
    }

    // Oil lanterns hung on the rear wall of the fire and support trenches every few bays: pools of
    // warm light separated by stretches of real darkness.
    private static void PlaceTrenchLanterns(Transform parent)
    {
        Material lanternMaterial = AssetDatabase.LoadAssetAtPath<Material>(Props + "Lantern_01/Lantern_01_brass.mat");
        if (lanternMaterial != null)
        {
            lanternMaterial.SetFloat("_AlphaClip", 1f);
            lanternMaterial.SetFloat("_Cutoff", 0.5f);
            lanternMaterial.EnableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(lanternMaterial);
        }

        foreach (BattlefieldLayout.Trench trench in BattlefieldLayout.Trenches)
        {
            if (trench.name != "FireTrench" && trench.name != "SupportTrench")
            {
                continue;
            }

            for (int i = 0; i < trench.points.Count - 1; i += 2)
            {
                // Every third bay, plus the bays around the spawn.
                Vector2 a = trench.points[i];
                Vector2 b = trench.points[i + 1];
                bool nearSpawn = trench.name == "FireTrench" && Mathf.Abs(a.x) < 14f;
                if (!nearSpawn && (i / 2) % 3 != 0)
                {
                    continue;
                }

                Vector2 direction = (b - a).normalized;
                Vector2 side = new Vector2(-direction.y, direction.x);
                float rearSign = Vector2.Dot(side, Vector2.up) >= 0f ? -1f : 1f;
                Vector2 middle = Vector2.Lerp(a, b, 0.5f);
                Vector2 at = middle + side * rearSign * (BattlefieldLayout.TrenchFloorHalfWidth - 0.12f);
                Vector3 position = new Vector3(at.x, Height(middle.x, middle.y) + 1.55f, at.y);
                GameObject lamp = Spawn(Props + "Lantern_01/Lantern_01_1k.fbx", parent, position,
                    Quaternion.Euler(0f, Range(0f, 360f), 0f), 1f, false);
                if (lamp == null)
                {
                    continue;
                }

                GameObject glow = new GameObject("LanternLight");
                glow.transform.SetParent(lamp.transform, true);
                glow.transform.position = position + Vector3.up * 0.12f;
                Light light = glow.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.28f);
                light.range = 10f;
                light.intensity = 7f;
                light.shadows = LightShadows.None; // the carried lantern casts the close shadows
                glow.AddComponent<FlickerLight>();
            }
        }
    }

    private static void SpawnFire(Transform parent, Vector2 at, float scale)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(Vfx + "Fire.prefab");
        GameObject fire = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        fire.transform.position = new Vector3(at.x, Height(at.x, at.y), at.y);
        fire.transform.localScale = Vector3.one * scale;
        Light light = fire.GetComponentInChildren<Light>();
        if (light != null)
        {
            light.range *= Mathf.Max(1f, scale * 1.5f);
            light.intensity *= Mathf.Max(1f, scale);
            // Point-light shadows cost six shadow maps each; the lantern already casts the close ones.
            light.shadows = LightShadows.None;
        }

        AudioSource crackle = fire.AddComponent<AudioSource>();
        crackle.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Ambience/fire-1_0.ogg");
        crackle.loop = true;
        crackle.playOnAwake = true;
        crackle.spatialBlend = 1f;
        crackle.minDistance = 1.5f;
        crackle.maxDistance = 25f * scale + 5f;
        crackle.volume = 0.6f;
    }

    // ---------- horizon ----------

    private static void PlaceDistant(Transform parent)
    {
        GameObject plume = AssetDatabase.LoadAssetAtPath<GameObject>(Vfx + "SmokePlume.prefab");
        Vector2[] plumes =
        {
            new Vector2(-420f, 650f), new Vector2(260f, 820f), new Vector2(620f, 480f), new Vector2(-700f, 300f),
            new Vector2(90f, 1150f), new Vector2(-980f, 900f), new Vector2(900f, 1000f), new Vector2(-300f, -700f),
            new Vector2(500f, -500f),
        };
        foreach (Vector2 at in plumes)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(plume, parent);
            instance.transform.position = new Vector3(at.x, Height(at.x, at.y), at.y);
            instance.transform.localScale = Vector3.one * Range(0.8f, 1.3f);
        }
    }
}
