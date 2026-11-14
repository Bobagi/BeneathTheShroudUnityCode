using UnityEngine;

// The three rooms that stood empty until 14/11 (Gustavo: "mobilie o bunker, o depósito e a sala do gerador 1"), furnished
// with models the project already has (nothing new imported; the heavy Poly Haven props, 15 to 42 k triangles, and the kit-bash sheets CablesKit and PipesKit left out):
// Alex's lab in the old bunker (concrete, titanium door: benches with his samples, a cot, the radio corner, a generator),
// the boat store (shelving, crates, drums, dive gear, a workbench) and generator room 1 (the other generator, its fuel,
// and the crates that jam its door from inside: "algo pesado do lado de dentro"). Local plan coordinates (LunaManor);
// models rest on real surfaces (TopAt) and against the walls' real faces (Against probes the wall: the bunker's walls
// are 0.5 plan thick, the house's 0.2 and 0.3). All doors stay clear: bunker south (18.0 to 19.0) and north (17.7 to
// 18.7), store east (27.5 to 28.5) and north (8.7 to 12.6, the wide door), generator room 1 east (19.9 to 20.9).
public static partial class FloodedHouseBuilder
{
    private static void ServiceRooms(Transform furniture)
    {
        Bunker(Group("Bunker", furniture));
        Store(Group("Deposito", furniture));
        GeneratorRoom1(Group("Gerador1", furniture));
    }

    // ---------------------------------------------------------------- Alex's lab (the bunker, 8.25 x 10.1 m, ceiling 3.2)

    private static void Bunker(Transform f)
    {
        // East wall: two steel benches, the samples and the reading on the first, the medical kit and the batteries on
        // the second; the fuse box south of them.
        GameObject benchA = Against(f, "MetalDesk", V2(23.7f, 21.5f), Vector2.left, 0f);
        GameObject benchB = Against(f, "MetalDesk", V2(23.7f, 18.4f), Vector2.left, 0f);
        ModelProp("SpecimenVial", f, OnTop(benchA, V2(23.32f, 21.95f)), Quaternion.Euler(0f, 15f, 0f), false);
        ModelProp("SpecimenVial", f, OnTop(benchA, V2(23.22f, 21.82f)), Quaternion.Euler(0f, 60f, 0f), false);
        ModelProp("SpecimenVial", f, OnTop(benchA, V2(23.36f, 21.68f)), Quaternion.identity, false);
        ModelProp("Microwave", f, OnTop(benchA, V2(23.3f, 20.95f)), Quaternion.Euler(0f, 270f, 0f), false);
        ModelProp("BookStack", f, OnTop(benchA, V2(23.3f, 22.15f)), Quaternion.Euler(0f, 80f, 0f), false);
        ModelProp("OilLamp", f, OnTop(benchA, V2(23.15f, 21.3f)), Quaternion.identity, false);
        Prop("medical_box", f, OnTop(benchB, V2(23.3f, 18.65f)), 80f, false);
        ModelProp("Bandage", f, OnTop(benchB, V2(23.18f, 18.25f)), Quaternion.Euler(0f, 30f, 0f), false);
        ModelProp("CellBattery", f, OnTop(benchB, V2(23.35f, 17.95f)), Quaternion.Euler(0f, 10f, 0f), false);
        ModelProp("CellBattery", f, OnTop(benchB, V2(23.28f, 17.82f)), Quaternion.Euler(0f, 40f, 0f), false);
        Prop("ammo_box", f, OnTop(benchB, V2(23.2f, 19.0f)), 20f, false);
        Model(f, "MercuryChair", V2(22.35f, 21.5f), 90f, 0f);
        Hung(f, "ElectricBox", V2(23.7f, 16.4f), Vector2.left, 1.1f);

        // North wall (door at 17.7 to 18.7): the pipe run (the Poly Haven run, as in the laundry; the PipesKit sheet read
        // as a wall of loose fittings), the drums and a crate under it.
        OffWall(Prop("modular_industrial_pipes_01", f, M(V2(21.3f, 23.5f), 0.9f), 180f, false), V2(21.3f, 23.5f), Vector2.down);
        Model(f, "Barrel2", V2(20.1f, 22.7f), 0f, 0f);
        Model(f, "Barrel2", V2(20.65f, 22.65f), 30f, 0f);
        Model(f, "WoodenCrate2", V2(22.3f, 22.55f), 90f, 0f);
        Prop("wooden_crate_01", f, M(V2(19.4f, 23.0f), 0f), 5f);

        // West wall: the cot, his suitcases with the first-aid kit on them, the rack with what he ate.
        Against(f, "DayBed", V2(17.1f, 21.6f), Vector2.right, 0f);
        GameObject suitcases = Against(f, "Suitcases", V2(17.1f, 19.5f), Vector2.right, 0f);
        Prop("medical_box", f, OnTop(suitcases, V2(17.5f, 19.4f)), 10f, false);
        GameObject rack = Against(f, "WornRack", V2(17.1f, 17.6f), Vector2.right, 0f);
        OnLevel(f, "LongLifeFood", rack, 1.1f, 0f, 0f, 0f);
        OnLevel(f, "FoodCans", rack, 0.6f, 0.2f, 0f, 20f);
        Prop("can_rusted", f, LevelAt(rack, 1.6f, -0.2f, 0f), 30f, false);
        Prop("can_rusted", f, LevelAt(rack, 1.6f, 0.05f, 0.1f), 120f, false);

        // South wall (door at 18.0 to 19.0): the generator corner and the tool chest.
        Against(f, "PortableGenerator", V2(21.0f, 15.4f), Vector2.up, 0f);
        GameObject chest = Against(f, "ToolChest", V2(22.9f, 15.4f), Vector2.up, 0f);
        On(f, "Toolbox", chest, 0f);
        Model(f, "FuelCanister", V2(21.8f, 16.0f), 20f, 0f);
        Model(f, "Jerrycan", V2(20.2f, 16.05f), -30f, 0f);
        Model(f, "LpgTank", V2(22.2f, 16.2f), 0f, 0f);

        // The middle: the long work table under the industrial lamp, a chair pushed back.
        GameObject table = Model(f, "KitchenTable", V2(20.3f, 19.5f), 0f, 0f);
        ModelProp("SpecimenVial", f, OnTop(table, V2(19.8f, 19.6f)), Quaternion.identity, false);
        ModelProp("SpecimenVial", f, OnTop(table, V2(19.95f, 19.42f)), Quaternion.Euler(0f, 45f, 0f), false);
        ModelProp("SpecimenVial", f, OnTop(table, V2(20.72f, 19.7f)), Quaternion.Euler(0f, 90f, 0f), false);
        ModelProp("BookStack", f, OnTop(table, V2(20.95f, 19.38f)), Quaternion.Euler(0f, 20f, 0f), false);
        ModelProp("OilLamp", f, OnTop(table, V2(20.3f, 19.78f)), Quaternion.identity, false);
        Prop("painted_wooden_chair_01", f, M(V2(20.0f, 18.65f), 0f), 10f);
        Ceiling(f, "IndustrialLamp", V2(20.3f, 19.5f), 3.2f);

        // Loose on the floor.
        GameObject box = Model(f, "CardboardBox", V2(18.9f, 21.9f), 25f, 0f);
        On(f, "CardboardBox", box, 0.02f, -30f, -0.02f);
        Model(f, "MetalCase", V2(22.9f, 17.2f), 15f, 0f);
        Model(f, "Bucket2", V2(17.6f, 16.3f), 0f, 0f);
        Prop("plastic_crate_02", f, M(V2(21.9f, 22.0f), 0f), -15f);
    }

    // ---------------------------------------------------------------- the boat store (9.9 x 9.1 m, ceiling 3.6)

    private static void Store(Transform f)
    {
        // West wall (window at 29.4 to 30.6): four steel units, food and dive gear on their planks.
        GameObject s1 = Against(f, "SteelShelves2", V2(7.1f, 27.6f), Vector2.right, 0f);
        GameObject s2 = Against(f, "SteelShelves2", V2(7.1f, 28.5f), Vector2.right, 0f);
        GameObject s3 = Against(f, "SteelShelves2", V2(7.1f, 31.3f), Vector2.right, 0f);
        GameObject s4 = Against(f, "SteelShelves2", V2(7.1f, 32.2f), Vector2.right, 0f);
        OnLevel(f, "FoodCans", s1, 0.9f, 0f, -0.08f, 10f);
        OnLevel(f, "LongLifeFood", s1, 1.4f, 0f, 0f, 0f);
        Prop("can_rusted", f, LevelAt(s1, 1.9f, 0.1f, 0.05f), 50f, false);
        OnLevel(f, "Jerrycan", s2, 1.4f, 0f, 0f, 90f);
        OnLevel(f, "Bleach", s2, 0.9f, 0.1f, 0f, 20f);
        OnLevel(f, "Cleaner", s2, 0.9f, -0.12f, 0.05f, 0f);
        OnLevel(f, "PonyBottle", s3, 1.4f, 0.1f, 0f, 0f);
        OnLevel(f, "PonyBottle", s3, 1.4f, -0.08f, 0.04f, 20f);
        OnLevel(f, "DiveMask", s3, 0.9f, 0f, 0f, -30f);
        OnLevel(f, "FoodCans", s4, 1.9f, 0f, 0f, 0f);
        Prop("can_rusted", f, LevelAt(s4, 1.4f, -0.1f, 0f), 0f, false);
        Prop("can_rusted", f, LevelAt(s4, 0.9f, 0.08f, -0.05f), 70f, false);
        Hung(f, "Lifebuoy", V2(7.1f, 32.9f), Vector2.right, 1.5f);

        // South wall: two worn racks with the fuel and the tools, crates beside them, the tool chest in the corner.
        GameObject r1 = Against(f, "WornRack", V2(8.3f, 26.4f), Vector2.up, 0f);
        GameObject r2 = Against(f, "WornRack", V2(9.3f, 26.4f), Vector2.up, 0f);
        OnLevel(f, "Jerrycan", r1, 1.1f, 0.15f, 0f, 0f);
        OnLevel(f, "Jerrycan", r1, 1.1f, -0.2f, 0.03f, 15f);
        OnLevel(f, "FuelCanister", r1, 0.6f, 0f, 0f, 0f);
        OnLevel(f, "PipeWrench", r2, 1.1f, 0f, 0f, 10f);
        OnLevel(f, "Toolbox", r2, 0.6f, 0f, 0f, 0f);
        Prop("wooden_crate_01", f, M(V2(11.0f, 27.0f), 0f), 0f);
        Prop("wooden_crate_01", f, M(V2(11.0f, 27.0f), 0.35f), 8f);
        Model(f, "WoodenCrate2", V2(12.3f, 27.1f), 90f, 0f);
        GameObject chest = Against(f, "ToolChest", V2(14.2f, 26.4f), Vector2.up, 0f);
        On(f, "Toolbox", chest, 0f);

        // East wall (door at 27.5 to 28.5): the workbench north of the door, the fuse box and a lamp.
        GameObject bench = Against(f, "MetalDesk", V2(15f, 30.4f), Vector2.left, 0f);
        ModelProp("BlueToolbox", f, OnTop(bench, V2(14.6f, 30.0f)), Quaternion.Euler(0f, 15f, 0f), false);
        ModelProp("PipeWrench", f, OnTop(bench, V2(14.5f, 30.6f)), Quaternion.Euler(0f, 80f, 0f), false);
        ModelProp("OilLamp", f, OnTop(bench, V2(14.62f, 31.0f)), Quaternion.identity, false);
        Hung(f, "ElectricBox", V2(15f, 32.6f), Vector2.left, 1.2f);
        Hung(f, "IndustrialWallLamp", V2(15f, 29.1f), Vector2.left, 2.0f);

        // North wall (the wide door at 8.7 to 12.6): drums east of it, gas bottles west of it.
        Model(f, "Barrel2", V2(13.3f, 33.1f), 0f, 0f);
        Model(f, "Barrel2", V2(13.85f, 33.05f), 40f, 0f);
        Model(f, "Barrel2", V2(13.55f, 32.55f), 70f, 0f);
        Model(f, "LpgTank", V2(7.9f, 33.1f), 0f, 0f);
        Model(f, "LpgTank", V2(8.35f, 33.15f), 30f, 0f);

        // The middle: stacked crates, cases, buckets.
        GameObject crate = Model(f, "WoodenCrate2", V2(10.3f, 30.2f), 0f, 0f);
        On(f, "WoodenCrate2", crate, 0f, 5f);
        Prop("plastic_crate_02", f, M(V2(11.6f, 29.8f), 0f), -15f);
        Prop("plastic_crate_02", f, M(V2(11.6f, 29.8f), 0.25f), 10f);
        GameObject box = Model(f, "CardboardBox", V2(12.7f, 31.3f), 20f, 0f);
        On(f, "CardboardBox", box, 0f, 35f);
        Model(f, "Bucket2", V2(9.2f, 31.7f), 0f, 0f);
        Prop("wooden_bucket_01", f, M(V2(8.8f, 30.4f), 0f), 0f);
        Model(f, "MetalCase", V2(11.0f, 32.3f), 0f, 0f);
        Model(f, "MetalCase", V2(11.45f, 32.35f), 15f, 0f);
        Prop("Barrel_01", f, M(V2(9.6f, 32.9f), 0f), 0f);
        Ceiling(f, "IndustrialLamp", V2(9.8f, 29.2f), 3.6f);
        Ceiling(f, "IndustrialLamp", V2(12.6f, 31.6f), 3.6f);
    }

    // ---------------------------------------------------------------- generator room 1 (5.5 x 3 m, upstairs)

    private static void GeneratorRoom1(Transform f)
    {
        // West wall (window at 20.0 to 20.8): the two generators either side of the window, the gas bottle by the first.
        Against(f, "GeneratorLP", V2(-14.4f, 19.65f), Vector2.right, UF);
        Against(f, "RustedGenerator", V2(-14.4f, 21.15f), Vector2.right, UF);
        Model(f, "LpgTank", V2(-13.45f, 19.65f), 0f, UF);

        // South wall: the fuse box and the tool chest.
        Hung(f, "ElectricBox", V2(-12.3f, 19.2f), Vector2.up, UF + 1.2f);
        GameObject chest = Against(f, "ToolChest", V2(-11.0f, 19.2f), Vector2.up, UF);
        On(f, "Toolbox", chest, 0f);

        // North wall: the rack with the fuel, the cables on the floor along it.
        GameObject rack = Against(f, "WornRack", V2(-12.6f, 21.6f), Vector2.down, UF);
        OnLevel(f, "Jerrycan", rack, 1.1f, 0f, 0f, 0f);
        OnLevel(f, "FuelCanister", rack, 0.6f, 0.1f, 0f, 0f);
        Prop("can_rusted", f, LevelAt(rack, 1.6f, -0.15f, 0f), 20f, false);

        // Behind the door (east wall, 19.9 to 20.9): the crate and the case that jam it ("algo pesado do lado de dentro").
        GameObject crate = Model(f, "WoodenCrate2", V2(-10.55f, 20.4f), 0f, UF);
        On(f, "MetalCase", crate, 0f, 20f, 0.25f);

        // Loose on the floor, the lamp over it all.
        Model(f, "PipeWrench", V2(-12.0f, 20.1f), 70f, UF);
        Model(f, "Bucket2", V2(-12.9f, 20.5f), 0f, UF);
        Ceiling(f, "IndustrialLamp", V2(-12.2f, 20.4f), UC);
    }
}
