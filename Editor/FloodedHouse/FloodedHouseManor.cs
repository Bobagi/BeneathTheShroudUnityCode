using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The Luna mansion of Dayse's floor plan (GDD "Planta da Casa", 11/10) in the game scene: LunaManor builds it,
// placed so that the plan's basement stair (in the laundry, "escada para o porão") lands on the basement that was
// already there (FloodedHouseGeometry: stair x -6..-4.8, top at z 1.0). Everything above ground is the manor's;
// the basement, the sunken pantry and the lab stay where they were, under the laundry, the service passage and
// the library.
//
// Local manor coordinates (LunaManor: front door at the origin, north +z) to the scene: M(x, y, z).
//
// Plan scale (29/10, Gustavo: "deixe os espaços maiores, tudo está muito apertado"): the plan is built PlanScale times
// larger in x and z, heights unchanged (LunaManor carries it in the manor's transform; models keep their size). It
// scales about the basement stair's corner in the laundry (LunaManor.StairX0, StairNorthZ), so that corner, the
// basement, the sunken pantry and the lab under it stay where they were in the scene.
public static partial class FloodedHouseBuilder
{
    public const float PlanScale = 1.25f;

    // Where the plan's origin (the front door) lay at scale 1.
    private static readonly Vector3 ManorOffset = new Vector3(-16.32f, 0f, -18.92f);

    private static readonly Vector2 PlanPivot = new Vector2(LunaManor.StairX0, LunaManor.StairNorthZ);

    private static Vector3 M(float x, float y, float z) =>
        new Vector3(PlanPivot.x + (x - PlanPivot.x) * PlanScale, y, PlanPivot.y + (z - PlanPivot.y) * PlanScale) + ManorOffset;

    private static Vector3 M(Vector2 at, float y) => M(at.x, y, at.y);

    // The same for code outside the builder (HouseShots and the editor tools).
    public static Vector3 PlanToWorld(Vector3 plan) => M(plan.x, plan.y, plan.z);

    // A local rectangle as world bounds between two heights.
    private static Bounds MZone(Rect r, float y0, float y1) => Zone(M(r.xMin, y0, r.yMin), M(r.xMax, y1, r.yMax));

    private static Rect MRect(Rect r)
    {
        Vector3 a = M(r.xMin, 0f, r.yMin), b = M(r.xMax, 0f, r.yMax);
        return Rect.MinMaxRect(a.x, a.z, b.x, b.z);
    }

    private const float UF = LunaManor.UpperFloor;
    private const float UC = LunaManor.UpperCeiling;
    private const float GC = LunaManor.GroundCeiling;
    private const float AF = LunaManor.AtticFloor;

    private static Material pantryTile, stoneFloor, gardenMat;

    private static void BuildManor()
    {
        CreateMansionMaterials();
        Transform g = Group("GroundFloor");
        stoneFloor = concrete;
        pantryTile = kitchenTile;
        gardenMat = mud;
        // Aged surfaces of a wealthy house (Gustavo 11/10: "casa de alto padrão... texturas que mostrem que o tempo
        // passou"): Poly Haven sets, real scale, the smoothness in each set's map (matte, dusty). Frames, skirting and
        // wainscot in the varnished dark wood (Poly Haven's peeling painted timber read nearly black with orange streaks).
        // 13/11: the service rooms in whitewashed brick (RoughPlaster read as street stone) and the courtyard's lawn as
        // forest ground, sparse dry grass over earth (GardenGrass was an even carpet); the kitchen's wall tiles as
        // square_tiles_03 (WallTiles, dirty_tiles, was dark red: a flat brown wall under the torch). Their real sizes:
        // LunaManor.SurfaceTiles.
        Material S(string set) => Textured(set, LunaManor.SurfaceTiles[set], Color.white, 0.1f);
        LunaManor.Palette palette = new LunaManor.Palette
        {
            wall = S("MossyStucco"), plaster = S("InteriorPlaster"), wallpaper = S("OldWallpaper"), azulejo = S("KitchenTiles"), ceiling = S("InteriorCeiling"),
            floor = S("Parquet"), tile = S("KitchenFloor"), stone = S("WhiteStucco"), concrete = S("WornConcreteFloor"), roof = S("RoofTiles"),
            trim = doorPaint, glass = glass, garden = mud, metal = rustyMetal,
            marble = S("MarbleTiles"), woodFloor = S("WornWoodFloor"), terracotta = S("TerracottaFloor"), mosaic = S("MosaicFloor"),
            bathFloor = S("BathMosaic"), bathWall = S("BathWallTiles"), panels = S("DarkPanels"), peeling = S("PeelingWall"),
            service = S("WhitewashedBrick"), roughConcrete = S("RoughConcrete"), roofFlat = S("RoughConcrete"),
            tiling = m => m != null && TileSizes.TryGetValue(m, out float t) ? t : 2f,
        };
        palette.lawn = S("GardenGround");
        palette.pebbles = S("Pebbles");
        palette = WithHouseArt(palette);
        LunaManor.Build(g, M(0f, 0f, 0f), palette, new LunaManor.Options { basementStairHole = true, frontDoorLeaf = true, footing = 0f, interior = true, planScale = PlanScale });
        // The interior doors LunaManor hung (Door on each leaf's pivot) get their sounds here.
        AudioClip creakLeaf = DoorSound("door_creak_1") ?? DoorSound("door_creak_2");
        foreach (Door door in g.GetComponentsInChildren<Door>(true))
        {
            if (door.GetComponent<AudioSource>() == null)
            {
                DoorAudio(door, door.gameObject, creakLeaf, creakLeaf);
            }
        }

        // The basement stair's enclosure in the laundry (scene coordinates, as the basement under it), with the
        // basement door: shut and locked (its key is in generator room 2).
        WallAlongZ("Stair_East", g, StairwellSouthZ - 0.12f, 1.12f, -4.8f, -4.68f, 0f, GC, plaster);
        WallAlongX("Stair_South", g, -6.07f, -4.68f, StairwellSouthZ - 0.12f, StairwellSouthZ, 0f, GC, plaster);
        WallAlongX("Stair_North", g, -6.07f, -4.68f, 1.0f, 1.12f, 0f, GC, plaster, new Opening(-5.85f, -4.9f, 0f, 2.1f));
        Transform hinge = Group("BasementDoor", g);
        hinge.position = new Vector3(-4.9f, 0f, 1.14f);
        Box("Leaf", hinge, new Vector3(-5.84f, 0.02f, 1.12f), new Vector3(-4.92f, 2.07f, 1.17f), doorPaint);
        hinge.rotation = Quaternion.identity;

        // The front door, shut and swollen: the opening's carved double door (the same models Erick pushes in the
        // cutscene), with a blocker so nothing walks out; plain leaves if the models are missing.
        float half = LunaManor.FrontDoorWidth * 0.5f;
        // The door models fill the arch, which grew with the plan: they hang in a parent with the plan's scale.
        Transform frontDoor = Group("FrontDoor", g);
        frontDoor.position = M(0f, 0f, 0f);
        frontDoor.localScale = new Vector3(PlanScale, 1f, PlanScale);
        if (ArrivalCutsceneBuilder.FrontDoorModels(frontDoor, M(0f, 0f, 0f)))
        {
            GameObject stop = Box("FrontDoor_Stop", g, M(-half, 0f, -0.04f), M(half, LunaManor.FrontDoorHeight, 0.04f), doorPaint);
            stop.GetComponent<MeshRenderer>().enabled = false;
        }
        else
        {
            Box("FrontDoor_L", g, M(-half, 0f, -0.05f), M(-0.01f, LunaManor.FrontDoorHeight, 0.05f), doorPaint);
            Box("FrontDoor_R", g, M(0.01f, 0f, -0.05f), M(half, LunaManor.FrontDoorHeight, 0.05f), doorPaint);
            Box("FrontDoor_Handle", g, M(-0.12f, 1.0f, 0.05f), M(-0.08f, 1.25f, 0.09f), metal, false);
        }
    }

    // The doors drawn red in the plan: kept shut in the demo, each with its own line.
    private static void LockedDoors(Transform parent, AudioClip creak, AudioClip rattle)
    {
        Transform d = Group("LockedDoors", parent);
        // Doors in walls running along z: hinge on the south jamb, the leaf along z.
        LockedAlongZ("PassageDoor", d, LunaManor.PassageDoor, 1f, 0f, 0.3f, "Trancada por dentro. Do outro lado, o corredor de serviço.", creak, rattle);
        LockedAlongZ("Generator1Door", d, LunaManor.Gerador1Door, 1f, UF, 0.2f, "A porta da outra sala do gerador. Emperrada; algo pesado do lado de dentro.", creak, rattle);
        LockedAlongZ("AlexDoor", d, LunaManor.AlexDoor, 1f, UF, 0.2f, "O quarto do Alex. Trancado.", creak, rattle);
        // Along x: hinge on the west jamb.
        LockedAlongX("HiddenDoor", d, LunaManor.HiddenDoor, 1f, 0f, 0.2f, wallpaper, "Uma estante pregada na parede... há uma fresta atrás dela. Não abre.", creak, rattle);
        LockedAlongX("BunkerDoor", d, LunaManor.BunkerDoor, 1f, 0f, 0.3f, rustyMetal, "Porta de titânio. Sem fechadura deste lado.", creak, rattle);
    }

    private static void LockedAlongZ(string name, Transform parent, Vector2 at, float width, float floor, float wall, string message, AudioClip creak, AudioClip rattle)
    {
        Door door = MakeDoor(name, parent, M(at.x, floor, at.y - width * 0.5f), M(at.x - 0.03f, floor + 0.02f, at.y - width * 0.5f + 0.02f),
            M(at.x + 0.03f, floor + 2.08f, at.y + width * 0.5f - 0.02f), doorPaint, 100f, Door.Lock.Story, null, null, null, message, creak, rattle);
        _ = wall;
        _ = door;
    }

    private static void LockedAlongX(string name, Transform parent, Vector2 at, float width, float floor, float wall, Material material, string message, AudioClip creak, AudioClip rattle)
    {
        MakeDoor(name, parent, M(at.x - width * 0.5f, floor, at.y), M(at.x - width * 0.5f + 0.02f, floor + 0.02f, at.y - 0.03f),
            M(at.x + width * 0.5f - 0.02f, floor + 2.08f, at.y + 0.03f), material, 100f, Door.Lock.Story, null, null, null, message, creak, rattle);
        _ = wall;
    }

    // ---------------------------------------------------------------- the rooms' lamps and furniture

    private static void ManorLamps(Transform parent)
    {
        Transform l = Group("ManorLamps", parent);
        // Ground floor: one pendant per room, the chandelier over the dinner table (FloodedHouseMansionStory), lamps
        // along the gallery round the courtyard.
        CeilingLamp("HallLamp_S", l, M(0f, 0f, 2.6f).x, M(0f, 0f, 2.6f).z, GC, 2.9f, 7f);
        CeilingLamp("HallLamp_N", l, M(0f, 0f, 6.8f).x, M(0f, 0f, 6.8f).z, GC, 2.9f, 7f);
        Lamp(l, "LivingLamp", V2(7.8f, 5.2f), GC, 3.4f, 9f, true);
        Lamp(l, "KitchenLamp", V2(-15f, 7f), GC, 3.1f, 8f, false);
        Lamp(l, "KitchenLamp2", V2(-15f, 12.6f), GC, 3.1f, 8f, false);
        Lamp(l, "PantryLamp", V2(-12f, 20f), GC, 2.4f, 6.5f, false);
        Lamp(l, "ChinaLamp", V2(-7.3f, 23f), GC, 2.4f, 5.5f, false);
        Lamp(l, "VestibuleLamp", V2(0.1f, 23.5f), GC, 2.9f, 7f, false);
        Lamp(l, "PowerRoomLamp", V2(7.6f, 23f), GC, 2.4f, 5.5f, false);
        Lamp(l, "LaundryLamp", V2(13.2f, 18.6f), GC, 2.9f, 7f, false);
        Lamp(l, "LaundryLamp2", V2(12.6f, 22.6f), GC, 2.6f, 6f, false);
        Lamp(l, "LibraryLamp", V2(15f, 8.4f), GC, 3.2f, 9f, true);
        foreach (Vector2 at in new[] { V2(-8.6f, 13f), V2(-8.6f, 18f), V2(-3f, 20.3f), V2(3.2f, 20.3f), V2(9f, 13f), V2(9f, 18f) })
        {
            Lamp(l, "GalleryLamp", at, GC, 2.2f, 6f, false);
        }

        // Upper floor: the galleries and the rooms the player can enter.
        // Under the galleries' half ceiling (the inner half is open to the sky since 29/10).
        foreach (Vector2 at in new[] { V2(-9.3f, 11.5f), V2(-9.3f, 19.8f), V2(-4.5f, 21f), V2(0.1f, 21f), V2(5.4f, 21f), V2(9.8f, 17f) })
        {
            Lamp(l, "UpperGalleryLamp", at, UC, 2.4f, 6f, false);
        }

        foreach ((string name, Rect room) in new[]
                 {
                     ("Single1Lamp", LunaManor.Solteiro1), ("Single2Lamp", LunaManor.Solteiro2), ("Bath1Lamp", LunaManor.Banheiro1), ("Master1Lamp", LunaManor.Casal1),
                     ("BalconyPassageLamp", LunaManor.SacadaPassage), ("Master2Lamp", LunaManor.Casal2), ("Bath2Lamp", LunaManor.Banheiro2), ("Single3Lamp", LunaManor.Solteiro3),
                 })
        {
            Lamp(l, name, room.center, UC, 2.6f, 6.5f, false);
        }
    }

    private static Vector2 V2(float x, float z) => new Vector2(x, z);

    private static void Lamp(Transform parent, string name, Vector2 local, float ceilingY, float intensity, float range, bool shadows)
    {
        Vector3 w = M(local, 0f);
        CeilingLamp(name, parent, w.x, w.z, ceilingY, intensity, range, shadows);
    }

    // Fungus on the walls the plan marks (the second picture of "Sugestão de localização de inimigos na casa"): blue
    // bioluminescent growth in the pantry and the china room, pink in the living room (the red one with its eggs is FloodedHouseFlora).
    private static void PlanFungus(Transform parent)
    {
        Transform f = Group("PlanFungus", parent);
        (Vector3 at, Vector3 outward, int kind, float scale)[] clumps =
        {
            (M(-14.24f, 0.6f, 18f), Vector3.right, 0, 1.2f), (M(-14.24f, 1.8f, 21.5f), Vector3.right, 2, 1.1f), (M(-12f, 0.01f, 23.6f), Vector3.up, 0, 1.3f),
            (M(-11f, GC - 0.02f, 19f), Vector3.down, 2, 1f), (M(-9.86f, 1.2f, 17.5f), Vector3.left, 0, 1f), (M(-8f, 0.01f, 24f), Vector3.up, 2, 1.1f),
            (M(-6f, 1.5f, 24.44f), Vector3.back, 0, 1f), (M(-5.06f, 0.8f, 22.4f), Vector3.left, 2, 0.9f),
            (M(4.16f, 0.5f, 7.6f), Vector3.right, 1, 1f), (M(10.5f, 1.4f, 8.74f), Vector3.back, 1, 0.9f),
            (M(19.74f, 0.7f, 8f), Vector3.left, 1, 1.2f), (M(17.6f, 0.01f, 12.6f), Vector3.up, 1, 1.1f), (M(18.1f, 1.9f, 13.24f), Vector3.back, 1, 0.9f),
        };
        for (int i = 0; i < clumps.Length; i++)
        {
            FungusCluster("Clump" + i, f, clumps[i].at, clumps[i].outward, clumps[i].kind, clumps[i].scale);
        }

        // The red fungus and its eggs: FloodedHouseFlora.SilkFungus (the library's north-west corner).
    }

    // The courtyard garden's flora, the blue and red fungus and the anemone worms of the GDD's gallery (FloodedHouseFlora),
    // the old roots kept; and the worms in the kitchen walls.
    private static void Garden(Transform parent, Transform player)
    {
        Transform g = Group("GardenFlora", parent);
        float y = LunaManor.GardenFloor;
        Backdrop(Prop("root_cluster_02", g, M(-6.2f, y, 18.4f), 30f, false, 1.4f)).name = "Roots";
        FloodedLawn(g);
        BuildFlora(parent, player);
        WallWorms(parent, M(-19.34f, 0f, 11.5f), Vector3.back, new Vector2(3f, 2.4f), player);
        WallWorms(parent, M(-14.24f, 0f, 19.5f), Vector3.forward, new Vector2(2.4f, 2.4f), player);
    }

    // Dayse's "grama alagada do centro... pedregulhos nos cantos, tomados pela água" (21/10): a sheet of dark water a
    // hand deep over the whole lawn (wading: splashes and wet steps, FirstPersonController), tufts of grass standing
    // out of it, thicker along the edges, and none under the grand stair or in the paths to the doors.
    private static void FloodedLawn(Transform parent)
    {
        Rect lawn = LunaManor.Garden;
        const float inset = 0.35f, depth = 0.08f;
        float surface = LunaManor.GardenFloor + depth;
        Vector3 a = M(lawn.xMin + inset, 0f, lawn.yMin + inset), b = M(lawn.xMax - inset, 0f, lawn.yMax - inset);
        Material flood = Transparent("GardenFlood", new Color(0.09f, 0.12f, 0.08f, 0.8f), 0.95f, AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Textures + "/WaterRipples/WaterRipples_Normal.png"));
        GameObject sheet = WaterSurface("FloodWater", parent, a.x, b.x, a.z, b.z, surface, flood, 0.4f);
        sheet.name = "FloodWater";
        Volume("FloodVolume", parent, new Vector3(a.x, LunaManor.GardenFloor - 0.05f, a.z), new Vector3(b.x, surface, b.z), surface, false, 3f);

        // The grass: the island's tuft (three crossed photo cards), a lawn gone wild (30 to 85 cm, 13/11: at 25 to 55 cm
        // and 520 tufts the lawn read as a flat green plane under the lightning), merged into one mesh. Its own material:
        // the island tuft's yellow-green, dulled to the olive of grass nobody cut for thirty years.
        Mesh tuft = AssetDatabase.LoadAssetAtPath<Mesh>(ProjectPaths.Root + "/Cutscenes/GrassTuft.asset");
        Material island = AssetDatabase.LoadAssetAtPath<Material>(ProjectPaths.Art + "/Materials/Cutscenes/GrassTuft.mat");
        if (tuft == null || island == null)
        {
            return;
        }

        Material grass = Material("GardenTuft", island.shader);
        grass.CopyPropertiesFromMaterial(island);
        grass.SetColor("_BaseColor", new Color(0.58f, 0.6f, 0.4f));
        EditorUtility.SetDirty(grass);

        List<CombineInstance> parts = new List<CombineInstance>();
        Random.State saved = Random.state;
        Random.InitState(2110);
        Rect stair = Rect.MinMaxRect(LunaManor.GrandStairX0 - 1.1f, LunaManor.GrandStairFootZ - 0.8f, LunaManor.GrandStairX1 + 1.1f, lawn.yMax);
        for (int i = 0; i < 2400 && parts.Count < 1150; i++)
        {
            Vector2 q = new Vector2(Random.Range(lawn.xMin + inset + 0.1f, lawn.xMax - inset - 0.1f), Random.Range(lawn.yMin + inset + 0.1f, lawn.yMax - inset - 0.1f));
            float toEdge = Mathf.Min(Mathf.Min(q.x - lawn.xMin, lawn.xMax - q.x), Mathf.Min(q.y - lawn.yMin, lawn.yMax - q.y));
            // Thick along the edges, sparse in the open middle (the water shows there).
            if (stair.Contains(q) || Random.value > Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(0.6f, 3f, toEdge)))
            {
                continue;
            }

            // The path from the glass door's steps and the back door's foot stay clear.
            if (Mathf.Abs(q.x - 0.2f) < 1.4f && q.y < lawn.yMin + 2.6f)
            {
                continue;
            }

            float w = Random.Range(0.45f, 0.9f), h = Random.Range(0.3f, 0.85f);
            Matrix4x4 m = Matrix4x4.TRS(M(q.x, LunaManor.GardenFloor, q.y), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(w, h, w));
            parts.Add(new CombineInstance { mesh = tuft, transform = m });
        }

        Random.state = saved;
        Mesh combined = new Mesh { name = "GardenGrass", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        combined.CombineMeshes(parts.ToArray(), true, true);
        combined.RecalculateBounds();
        GameObject go = new GameObject("GardenGrass");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = combined;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = grass;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
