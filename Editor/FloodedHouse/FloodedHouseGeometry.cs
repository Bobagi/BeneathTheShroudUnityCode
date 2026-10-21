using UnityEngine;

// Walls, floors, stairs and water of the flooded house. Coordinates follow the layout comment in
// FloodedHouseBuilder; every wall is 0.25 to 0.3 m thick and sits outside the interior span it
// bounds, so interiors read as clean rectangles in the numbers below.
public static partial class FloodedHouseBuilder
{
    // Ground floor interior: x -6..6, z -5..5, floor 0, ceiling 3.
    // Stair hole in the floor: x -6..-4.8, z -2.24..1.0 (12 steps down to the landing at -2.3).
    private const float StairTopZ = 1.0f;
    private const float StairBottomZ = -2.24f;
    // The slab opening runs past the last step, over the landing: at the bottom of the flight the
    // body (radius 0.3 m ahead of the feet) otherwise hits its head on the ground-floor slab.
    private const float StairwellSouthZ = -3.1f;
    private const float FloodStartY = -0.1f;

    private static WaterVolume floodVolume;
    private static Transform floodSurfaces;
    private const float LandingY = -2.3f;
    private const int StairSteps = 12;

    private static void WindowFrame(Transform parent, string name, bool alongX, float from, float to, float depth, float bottom, float top, int panes)
    {
        Transform frame = Group(name, parent);
        const float t = 0.07f;
        // (run, height, depth) to world, whichever axis the wall runs along.
        System.Func<float, float, float, Vector3> P = (r, y, d) => alongX ? new Vector3(r, y, d) : new Vector3(d, y, r);
        // Everything inside the opening: the sill sits on the wall below it (it used to sink into the
        // tiled wall and flicker against it) and the end posts stand against the jambs, not half in them.
        Box("Sill", frame, P(from, bottom, depth - 0.2f), P(to, bottom + t * 0.6f, depth + 0.2f), doorPaint);
        Box("Head", frame, P(from, top - t, depth - 0.12f), P(to, top, depth + 0.12f), doorPaint);
        for (int i = 0; i <= panes; i++)
        {
            float r = Mathf.Lerp(from + t * 0.5f, to - t * 0.5f, i / (float)panes);
            Box("Mullion" + i, frame, P(r - t * 0.5f, bottom + t * 0.6f, depth - 0.05f), P(r + t * 0.5f, top - t, depth + 0.05f), doorPaint);
        }

        float mid = Mathf.Lerp(bottom, top, 0.55f);
        Box("Transom", frame, P(from, mid - t * 0.5f, depth - 0.05f), P(to, mid + t * 0.5f, depth + 0.05f), doorPaint);
        GameObject pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pane.name = "Glass";
        Object.DestroyImmediate(pane.GetComponent<Collider>());
        pane.transform.SetParent(frame, false);
        pane.transform.position = P((from + to) * 0.5f, (bottom + top) * 0.5f, depth);
        pane.transform.rotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
        pane.transform.localScale = new Vector3(to - from, top - bottom, 1f);
        pane.GetComponent<MeshRenderer>().sharedMaterial = glass;
    }

    // Basement interior: x -6..2, z -5..1, floor -4.5, ceiling -0.25 (the ground-floor slab).
    private static void BuildBasement()
    {
        Transform b = Group("Basement");

        // Floor with the square shaft down to the lab at x -2..0, z -4..-2.
        Box("Floor_N", b, new Vector3(-6.3f, -4.8f, -2f), new Vector3(2.3f, -4.5f, 1.3f), concrete);
        Box("Floor_S", b, new Vector3(-6.3f, -4.8f, -5.3f), new Vector3(2.3f, -4.5f, -4f), concrete);
        Box("Floor_W", b, new Vector3(-6.3f, -4.8f, -4f), new Vector3(-2f, -4.5f, -2f), concrete);
        Box("Floor_E", b, new Vector3(0f, -4.8f, -4f), new Vector3(2.3f, -4.5f, -2f), concrete);

        WallAlongZ("West", b, -5.3f, 1.3f, -6.3f, -6f, -4.8f, -0.25f, concrete);
        WallAlongX("South", b, -6f, 2f, -5.3f, -5f, -4.8f, -0.25f, concrete);
        WallAlongX("North", b, -6f, 2f, 1f, 1.3f, -4.8f, -0.25f, concrete);
        // The east wall reaches down past the sunken bedroom; a breach near the floor opens into it.
        WallAlongZ("East", b, -5.3f, 1.3f, 2f, 2.3f, -6.3f, -0.25f, concrete, new Opening(-2.5f, -0.7f, -4.5f, -3.1f));

        // Stairs down from the ground-floor door to the landing just above the water.
        Transform stairs = Group("Stairs", b);
        float run = (StairTopZ - StairBottomZ) / StairSteps;
        for (int i = 0; i < StairSteps; i++)
        {
            float top = LandingY * (i + 1) / StairSteps;
            float zHigh = StairTopZ - i * run;
            Box("Step" + i, stairs, new Vector3(-6f, top - 0.22f, zHigh - run), new Vector3(-4.8f, top, zHigh), darkPlanks);
            // Invisible rail stop: nobody jumps off the stairs into the black water.
            Box("RailStop" + i, stairs, new Vector3(-4.84f, top, zHigh - run), new Vector3(-4.76f, top + 1.1f, zHigh), darkPlanks)
                .GetComponent<MeshRenderer>().enabled = false;
        }

        float slope = Mathf.Atan2(-LandingY, StairTopZ - StairBottomZ) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(LandingY * LandingY + (StairTopZ - StairBottomZ) * (StairTopZ - StairBottomZ));
        Primitive(PrimitiveType.Cube, "Handrail", stairs, new Vector3(-4.8f, LandingY * 0.5f + 0.95f, (StairTopZ + StairBottomZ) * 0.5f),
            Quaternion.Euler(-slope, 0f, 0f), new Vector3(0.06f, 0.07f, length), darkPlanks);
        for (int i = 0; i <= StairSteps; i += 3)
        {
            float top = LandingY * Mathf.Min(i + 1, StairSteps) / StairSteps;
            float z = StairTopZ - (i + 0.5f) * run;
            Box("Baluster" + i, stairs, new Vector3(-4.83f, top, z - 0.03f), new Vector3(-4.77f, top + 0.95f, z + 0.03f), darkPlanks, false);
        }

        // The landing: the dry end of the house, with the dive bench.
        Box("Landing", b, new Vector3(-6f, LandingY - 0.2f, -5f), new Vector3(-3f, LandingY, StairBottomZ), darkPlanks);
        foreach (Vector2 post in new[] { new Vector2(-3.1f, -4.9f), new Vector2(-3.1f, -2.35f), new Vector2(-5.9f, -4.9f) })
        {
            Box("Post", b, new Vector3(post.x - 0.08f, -4.5f, post.y - 0.08f), new Vector3(post.x + 0.08f, LandingY - 0.2f, post.y + 0.08f), darkPlanks);
        }

        // Rubble where the east wall gave way into the sunken bedroom.
        Transform rubble = Group("BreachRubble", b);
        Primitive(PrimitiveType.Cube, "Chunk0", rubble, new Vector3(2.9f, -5.7f, -1.3f), Quaternion.Euler(20f, 30f, 35f), new Vector3(1.1f, 0.5f, 0.8f), concrete, true);
        Primitive(PrimitiveType.Cube, "Chunk1", rubble, new Vector3(2.6f, -5.2f, -2.1f), Quaternion.Euler(-15f, 10f, 25f), new Vector3(0.8f, 0.4f, 0.6f), concrete, true);
        Primitive(PrimitiveType.Cube, "Chunk2", rubble, new Vector3(1.3f, -4.35f, 0.35f), Quaternion.Euler(5f, 40f, 12f), new Vector3(0.6f, 0.3f, 0.7f), concrete, true);
    }

    // Sunken bedroom interior: x 2.3..8.3, z -4..1, floor -6, ceiling -3.2: under water to the ceiling.
    private static void BuildBedroom()
    {
        Transform r = Group("SunkenBedroom");
        Box("Floor", r, new Vector3(2.3f, -6.3f, -4.25f), new Vector3(8.55f, -6f, 1.25f), floorboards);
        Box("Ceiling", r, new Vector3(2.3f, -3.2f, -4.25f), new Vector3(8.55f, -2.95f, 1.25f), ceiling);
        Opening window = new Opening(4.5f, 6.5f, -4.8f, -3.6f);
        WallAlongZ("East", r, -4.25f, 1.25f, 8.3f, 8.55f, -6f, -3.2f, wallpaper);
        WallAlongX("North", r, 2.3f, 8.3f, 1f, 1.25f, -6f, -3.2f, wallpaper, window);
        WallAlongX("South", r, 2.3f, 8.3f, -4.25f, -4f, -6f, -3.2f, wallpaper);
        WallAlongZ("WestLining", r, -4f, 1f, 2.3f, 2.34f, -6f, -3.2f, wallpaper, new Opening(-2.5f, -0.7f, -4.5f, -3.2f));
        WindowFrame(r, "Window", true, 4.5f, 6.5f, 1.12f, -4.8f, -3.6f, 2);
        // Behind the window: the drowned garden, just a dark bank of silt.
        Box("Silt", r, new Vector3(3.5f, -6.3f, 2.2f), new Vector3(7.5f, -2.95f, 2.5f), concrete);
    }

    // Lab, all under water. Shaft x -2..0, z -4..-2 from the basement floor down to the corridor
    // (x -2..0, z -18..-2, floor -10, ceiling -7.5). Cross passage at z -6.5..-5 (dead ends).
    // Air-pocket room x 0.25..5.25, z -12..-7, ceiling -6.5. Tank room x -4..2, z -24..-18.
    private static void BuildLab()
    {
        Transform lab = Group("Lab");

        Transform shaft = Group("Shaft", lab);
        WallAlongZ("West", shaft, -4f, -2f, -2.25f, -2f, -7.5f, -4.8f, concrete);
        WallAlongZ("East", shaft, -4f, -2f, 0f, 0.25f, -7.5f, -4.8f, concrete);
        WallAlongX("South", shaft, -2.25f, 0.25f, -4.25f, -4f, -7.5f, -4.8f, concrete);
        WallAlongX("North", shaft, -2.25f, 0.25f, -2f, -1.75f, -10f, -4.8f, concrete);
        for (float y = -7.2f; y < -4.8f; y += 0.35f)
        {
            Primitive(PrimitiveType.Cylinder, "Rung", shaft, new Vector3(-1f, y, -2.08f), Quaternion.Euler(0f, 0f, 90f),
                new Vector3(0.04f, 0.4f, 0.04f), metal);
        }

        Transform corridor = Group("Corridor", lab);
        Opening junction = new Opening(-6.5f, -5f, -10f, -7.7f);
        Box("Floor", corridor, new Vector3(-2.25f, -10.3f, -18f), new Vector3(0.25f, -10f, -1.75f), labFloor);
        Box("Ceiling", corridor, new Vector3(-2.25f, -7.5f, -18f), new Vector3(0.25f, -7.25f, -4.25f), labTile);
        // Solid above the corridor ceiling, up to the air pocket's roof: the space left there was open, and a diver
        // switching to the swimming shape at the pocket door got pushed up through the slab into it, then sat on the
        // slab unable to sink (Gustavo, 2026-10-03: "no batente, tento afundar e fica piscando").
        Box("CeilingFill", corridor, new Vector3(-2.25f, -7.25f, -18f), new Vector3(0.25f, -6.25f, -4.25f), concrete);
        WallAlongZ("West", corridor, -18f, -1.75f, -2.25f, -2f, -10f, -7.5f, labTile, junction);
        WallAlongZ("East", corridor, -18f, -1.75f, 0f, 0.25f, -10f, -7.5f, labTile, junction, new Opening(-10f, -9f, -10f, -8f));
        Box("SealedDoor", corridor, new Vector3(-2.02f, -10f, -13f), new Vector3(-1.95f, -8f, -12.1f), rustyMetal);
        Box("SealedDoorFrame", corridor, new Vector3(-2.02f, -8f, -13.1f), new Vector3(-1.92f, -7.88f, -12f), rustyMetal);
        Box("PocketDoorFrame", corridor, new Vector3(-0.06f, -8f, -10.1f), new Vector3(0.3f, -7.85f, -8.9f), rustyMetal);
        Transform pocketDoor = Group("PocketDoor", corridor);
        pocketDoor.position = new Vector3(0.25f, -10f, -9f);
        Box("Leaf", pocketDoor, new Vector3(0.25f, -10f, -9.95f), new Vector3(0.31f, -8.05f, -9.02f), rustyMetal);
        pocketDoor.rotation = Quaternion.Euler(0f, -80f, 0f);

        // Pipes along the ceiling, the lab's arteries.
        foreach (Vector2 pipe in new[] { new Vector2(-1.75f, -7.72f), new Vector2(-1.45f, -7.8f), new Vector2(-0.3f, -9.75f) })
        {
            float diameter = pipe.y < -9f ? 0.18f : 0.13f;
            Primitive(PrimitiveType.Cylinder, "Pipe", corridor, new Vector3(pipe.x, pipe.y, -10.9f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(diameter, 6.9f, diameter), metal);
        }

        // Cross passage: both arms end in collapse a few metres in.
        Transform cross = Group("CrossPassage", lab);
        foreach (Vector2 arm in new[] { new Vector2(-6f, -2.25f), new Vector2(0.25f, 4f) })
        {
            Box("Floor", cross, new Vector3(arm.x, -10.3f, -6.5f), new Vector3(arm.y, -10f, -5f), concrete);
            Box("Ceiling", cross, new Vector3(arm.x, -7.7f, -6.75f), new Vector3(arm.y, -7.45f, -4.75f), concrete);
            WallAlongX("North", cross, arm.x, arm.y, -5f, -4.75f, -10f, -7.7f, concrete);
            WallAlongX("South", cross, arm.x, arm.y, -6.75f, -6.5f, -10f, -7.7f, concrete);
            float end = arm.x < 0f ? arm.x : arm.y;
            float dir = arm.x < 0f ? 1f : -1f;
            Box("End", cross, new Vector3(end - 0.3f * dir, -10f, -6.5f), new Vector3(end, -7.7f, -5f), concrete);
            for (int i = 0; i < 4; i++)
            {
                Primitive(PrimitiveType.Cube, "Collapse" + i, cross, new Vector3(end + dir * (0.5f + i * 0.25f), -9.6f + i * 0.35f, -5.75f + (i % 2 == 0 ? 0.3f : -0.35f)),
                    Quaternion.Euler(15f * i, 35f * i, 20f), new Vector3(0.9f, 0.6f, 0.7f), concrete, true);
            }
        }

        // The air pocket: a taller room whose top metre holds trapped air.
        Transform pocket = Group("AirPocketRoom", lab);
        Box("Floor", pocket, new Vector3(0.25f, -10.3f, -12.25f), new Vector3(5.5f, -10f, -6.75f), labFloor);
        Box("Ceiling", pocket, new Vector3(0f, -6.5f, -12.25f), new Vector3(5.5f, -6.25f, -6.75f), labTile);
        WallAlongX("North", pocket, 0.25f, 5.5f, -7f, -6.75f, -10f, -6.5f, labTile);
        WallAlongX("South", pocket, 0.25f, 5.5f, -12.25f, -12f, -10f, -6.5f, labTile);
        WallAlongZ("East", pocket, -12f, -7f, 5.25f, 5.5f, -10f, -6.5f, labTile);
        WallAlongZ("WestUpper", pocket, -12f, -7f, 0f, 0.25f, -7.5f, -6.5f, labTile);

        Transform tanks = Group("TankRoom", lab);
        Box("Floor", tanks, new Vector3(-4.25f, -10.3f, -24.25f), new Vector3(2.25f, -10f, -17.75f), labFloor);
        Box("Ceiling", tanks, new Vector3(-4.25f, -7f, -24.25f), new Vector3(2.25f, -6.75f, -17.75f), labTile);
        WallAlongX("North", tanks, -4.25f, 2.25f, -18f, -17.75f, -10f, -7f, labTile, new Opening(-2f, 0f, -10f, -7.5f));
        WallAlongX("South", tanks, -4.25f, 2.25f, -24.25f, -24f, -10f, -7f, labTile);
        WallAlongZ("West", tanks, -24f, -18f, -4.25f, -4f, -10f, -7f, labTile);
        WallAlongZ("East", tanks, -24f, -18f, 2f, 2.25f, -10f, -7f, labTile);
    }

    // The island round the mansion: a mud crown (y -0.3, down to -2.9) ringed with coastal rock and dead mangrove,
    // and a dark sea to the horizon. Seen from the windows, the courtyard and the doors. Open over the basement
    // (it lies under the laundry and the service yard; FloodedHouseManor), where a paved yard covers it instead.
    private static void BuildExterior()
    {
        Transform outside = Group("Exterior");
        const float top = -0.3f, bottom = -2.9f;
        // Island, in manor coordinates; the hole over the basement (its walls included).
        Rect island = Rect.MinMaxRect(-30f, -14f, 34f, 44f);
        Rect hole = Rect.MinMaxRect(9.95f, 13.55f, 18.65f, 20.27f);
        Box("Ground_S", outside, M(island.xMin, bottom, island.yMin), M(island.xMax, top, hole.yMin), mud);
        Box("Ground_N", outside, M(island.xMin, bottom, hole.yMax), M(island.xMax, top, island.yMax), mud);
        Box("Ground_W", outside, M(island.xMin, bottom, hole.yMin), M(hole.xMin, top, hole.yMax), mud);
        Box("Ground_E", outside, M(hole.xMax, bottom, hole.yMin), M(island.xMax, top, hole.yMax), mud);
        // The vestibule's north door opens onto the island (the plan's road to the sea): an invisible rail at the
        // top of the bank keeps the player from walking off the edge into the sea (it is not water one can swim in).
        foreach ((Vector3 a, Vector3 b) in new[]
                 {
                     (M(island.xMin, top, island.yMin), M(island.xMax, 4f, island.yMin + 0.3f)), (M(island.xMin, top, island.yMax - 0.3f), M(island.xMax, 4f, island.yMax)),
                     (M(island.xMin, top, island.yMin), M(island.xMin + 0.3f, 4f, island.yMax)), (M(island.xMax - 0.3f, top, island.yMin), M(island.xMax, 4f, island.yMax)),
                 })
        {
            Box("EdgeRail", outside, a, b, mud).GetComponent<MeshRenderer>().enabled = false;
        }

        // The paved service yard between the laundry and the bunker, over the basement's ceiling.
        Box("ServiceYard", outside, M(15.15f, -0.25f, 15.55f), M(17f, -0.05f, 20.27f), concrete);

        // Everything outside is Backdrop(): lightest LOD, no shadow casting (see the note there).
        Backdrop(Prop("coast_rocks_01", outside, M(4f, -2.3f, 46f), 180f, false));
        Backdrop(Prop("coast_rocks_01", outside, M(-18f, -2.3f, 45.5f), 200f, false));
        Backdrop(Prop("coast_rocks_02", outside, M(-32f, -2.2f, 10f), 70f, false));
        Backdrop(Prop("coast_rocks_02", outside, M(36f, -2.2f, 12f), 250f, false));
        Backdrop(Prop("coast_rocks_02", outside, M(-24f, -2.2f, -16f), 20f, false));
        Backdrop(Prop("coast_rocks_02", outside, M(26f, -2.2f, -16f), 150f, false));
        Backdrop(Prop("coast_land_rocks_03", outside, M(-25f, -0.5f, 30f), 20f, false));
        Backdrop(Prop("coast_land_rocks_03", outside, M(27f, -0.5f, 38f), 160f, false));
        Backdrop(Prop("coast_land_rocks_03", outside, M(29f, -0.5f, 2f), 95f, false));

        // Dead mangrove: bleached trunks standing in the mud and out in the shallows.
        Vector3[] trunks =
        {
            M(-24f, top, -6f), M(-26f, top, 22f), M(24f, top, -8f), M(29f, top, 20f), M(-8f, top, 40f), M(14f, top, 41f),
            M(-22f, top, 8f), M(26f, top, 28f), M(-12f, -2.6f, 50f), M(20f, -2.6f, 49f), M(-36f, -2.6f, 0f), M(39f, -2.6f, -4f),
        };
        for (int i = 0; i < trunks.Length; i++)
        {
            GameObject trunk = Backdrop(Prop("dead_quiver_trunk", outside, trunks[i], Quaternion.Euler(Random.Range(-14f, 14f), i * 47f, Random.Range(-14f, 14f)), false, Random.Range(1.1f, 1.7f)));
            trunk.name = "DeadMangrove" + i;
        }

        // Storm sea: a subdivided grid whose vertices the StormSea shader moves on the GPU (Gerstner
        // waves). The island is left out so the sea never cuts through the house or the flooded basement.
        GameObject seaObject = new GameObject("Sea");
        seaObject.transform.SetParent(outside, false);
        seaObject.transform.position = new Vector3(0f, WaterLevel, 0f);
        Vector3 islandMin = M(island.xMin - 0.5f, 0f, island.yMin - 0.5f);
        seaObject.AddComponent<MeshFilter>().sharedMesh = SeaMesh(280f, 2f, new Rect(islandMin.x, islandMin.z, island.width + 1f, island.height + 1f));
        MeshRenderer seaRenderer = seaObject.AddComponent<MeshRenderer>();
        seaRenderer.sharedMaterial = sea;
        seaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        seaObject.AddComponent<SeaWaves>();

        BuildRain(outside);
    }

    // Flat grid (size x size metres, one quad per cell) with a rectangular hole; the waves come from the
    // shader. Bounds are padded so the displaced crests are never frustum-culled.
    private static Mesh SeaMesh(float size, float cell, Rect hole)
    {
        int cells = Mathf.CeilToInt(size / cell);
        float half = size * 0.5f;
        Vector3[] vertices = new Vector3[(cells + 1) * (cells + 1)];
        for (int z = 0; z <= cells; z++)
        {
            for (int x = 0; x <= cells; x++)
            {
                vertices[z * (cells + 1) + x] = new Vector3(-half + x * cell, 0f, -half + z * cell);
            }
        }

        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                Vector2 center = new Vector2(-half + (x + 0.5f) * cell, -half + (z + 0.5f) * cell);
                if (hole.Contains(center))
                {
                    continue;
                }

                int i = z * (cells + 1) + x;
                triangles.AddRange(new[] { i, i + cells + 1, i + 1, i + 1, i + cells + 1, i + cells + 2 });
            }
        }

        Mesh mesh = new Mesh { name = "StormSeaGrid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(size + 10f, 6f, size + 10f));
        return mesh;
    }

    private static void BuildWater()
    {
        Transform w = Group("Water");

        // The ground floor stays dry: the basement right below is only half flooded (decided 2026-09-29).
        WaterSurface("Basement", w, -6f, 2f, -5f, 1f, WaterLevel, water, 0.5f);
        // Visibility (metres until nothing shows): the house water is dirty, the lab water is turbid.
        Volume("BasementWater", w, new Vector3(-6f, -4.8f, -5f), new Vector3(2f, WaterLevel, 1f), WaterLevel, true, 10f);
        Volume("BedroomWater", w, new Vector3(1.9f, -6f, -4f), new Vector3(8.3f, -3.2f, 1f), WaterLevel, true, 9f);
        Volume("ShaftAndCorridorWater", w, new Vector3(-2f, -10f, -18.1f), new Vector3(0f, -4.5f, -1.75f), WaterLevel, true, 6f);
        Volume("CrossPassageWater", w, new Vector3(-6f, -10f, -6.5f), new Vector3(4f, -7.7f, -5f), WaterLevel, true, 6f);
        Volume("TankRoomWater", w, new Vector3(-4f, -10f, -24f), new Vector3(2f, -7f, -17.9f), WaterLevel, true, 6f);

        // The air pocket: the room's own, lower surface.
        WaterSurface("AirPocket", w, 0.25f, 5.25f, -12f, -7f, AirPocketLevel, water, 0.3f);
        Volume("AirPocketWater", w, new Vector3(0f, -10f, -12f), new Vector3(5.25f, -6.5f, -7f), AirPocketLevel, true, 7f);

        // End of the demo: the sea under the ground floor's boards (the front rooms and the kitchen).
        Transform flood = Group("GroundFloorFlood", w);
        Vector3 f0 = M(-10.4f, 0f, 1.95f), f1 = M(10.4f, 0f, 8.9f);
        WaterSurface("Front", flood, f0.x, f1.x, f0.z, f1.z, FloodStartY, water, 0.6f);
        Vector3 k0 = M(-18.2f, 0f, 3.75f), k1 = M(-11.6f, 0f, 16f);
        WaterSurface("Kitchen", flood, k0.x, k1.x, k0.z, k1.z, FloodStartY, water, 0.6f);
        floodVolume = Volume("GroundFloorFloodWater", flood, new Vector3(f0.x, -0.05f, f0.z), new Vector3(f1.x, 0.8f, f1.z), FloodStartY, false, 10f);
        floodSurfaces = flood;
    }
}
