using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// The mansion furnished (Gustavo 11/10: "preencha a casa com móveis, itens"): Poly Haven models through the
// game-3d-assets optimiser (Art/Models/Props, SourceAssets/Tools/furniture_batch.sh) and Blender placeholders for
// what no free library had (SourceAssets/Tools/house_placeholders.py: bath, toilet, basin, washing machine, fridge,
// kitchen units, wardrobe, curtain, boat). Rugs, curtains at the windows, and water stains running down from the
// ceilings (ambientCG Leaking decals, CC0). Story objects stay in FloodedHouseMansionStory.
// Models face +Z at yaw 0; Against() puts a model's back on a wall (local plan coordinates, LunaManor).
// 16/10: the rest of Dayse's Sketchfab list came in (CREDITS.md, "Segunda leva"): the Soviet fridge and the gas stove
// replace the Blender placeholders, Victorian curtains replace the curtain slabs, real carpets replace the rug boxes,
// the upright piano, the 1920s lamps, the radio, the mirror, the vanity, the dresser, Alex's room furnished, the
// armoured cabinet in generator room 2, the sofa set in the vestibule. Kits that come as several loose pieces in one
// mesh (OldKitchenSet, KitchenAppliances, OldSofas, PostApocFurniture) stay in Art/Models/Props unused.
public static partial class FloodedHouseBuilder
{
    private const float WallHalf = 0.15f;

    private static void FurnishManor(Transform parent)
    {
        Transform f = Group("Furniture", parent);
        Material rug = Rug();

        // ---------------- hall
        Against(f, "GrandfatherClock", V2(-4f, 7.6f), Vector2.right, 0f);
        GameObject console = Against(f, "Console", V2(4f, 3.1f), Vector2.left, 0f);
        On(f, "ClayPot", console, 0f);
        On(f, "OldLamp", console, 0.45f);
        Hung(f, "OrnateMirror", V2(4f, 3.1f), Vector2.left, 1.45f);
        // Old sockets and switches on the hall's west wall, north of the opening (Sketchfab "Sockets & Switches old",
        // AmongModels, CC BY). The plate lies flat in its file (its thin side is up): stood up with a 90 degree roll,
        // its back 3 cm off the wall face. At z 3.2 it landed on the security panel's lower edge (16/10).
        GameObject switches = ModelProp("OldSwitches", f, M(V2(-4f + WallHalf + 0.03f, 6.6f), 1.25f), Quaternion.Euler(0f, 0f, 90f), false, false);
        switches.transform.position += Vector3.right * (switches.transform.position.x - RendererBounds(switches).min.x);
        // No rug: the concept art's hall shows its wide planks bare (GDD, 13/10).

        // ---------------- dining room
        Against(f, "GothicCabinet", V2(-9.7f, 8.9f), Vector2.down, 0f);
        GameObject commode = Against(f, "Commode", V2(-5.7f, 8.9f), Vector2.down, 0f);
        GameObject bottles = On(f, "WineBottles", commode, 0.42f);
        // The optimiser's bake makes see-through glass opaque and pale: tinted back to dark old glass, dusty.
        if (bottles != null)
        {
            Material glassBottles = bottles.GetComponentInChildren<Renderer>().sharedMaterial;
            glassBottles.SetColor("_BaseColor", new Color(0.32f, 0.26f, 0.2f));
            EditorUtility.SetDirty(glassBottles);
        }
        On(f, "Candleholders", commode, -0.38f);
        // Dayse's old Persian carpet under the dining table (Sketchfab, Mehdi Shahsavan, CC BY), 4 m along the table.
        Floor(f, "PersianCarpet", V2(-7.8f, 5.8f), 90f, 0f);
        Curtains(f, -7.8f, 1.9f, 2.6f, 0f);
        // Paintings: real ones from museum open access now (FloodedHouseHallArt.HousePaintings).

        // ---------------- living room
        Model(f, "Ottoman", V2(6.4f, 5.4f), 20f, 0f);
        // The sofa against the north wall under the storm painting, Dayse's carved table (Poly Haven gothic_coffee_table)
        // in front of it, an armchair and the rocking chair by the bay window (Poly Haven, CC0, 13/10).
        Against(f, "Sofa3", V2(7.8f, 8.9f), Vector2.down, 0f);
        Model(f, "CarvedTable", V2(7.8f, 6.9f), 0f, 0f);
        Model(f, "ArmChair", V2(5.5f, 6.9f), 90f, 0f);
        Model(f, "RockingChair", V2(9.2f, 3.1f), 200f, 0f);
        GameObject tall = Against(f, "SideTableTall", V2(11.6f, 7.9f), Vector2.left, 0f);
        On(f, "Television", tall, 0f);
        Model(f, "GreenChair", V2(9.4f, 6.2f), 200f, 0f);
        Carpet(f, rug, V2(7.8f, 5f), new Vector2(3.6f, 2.8f), 0f, 0f);
        Curtains(f, 7.8f, 1.9f, 2.6f, 0f);
        // The upright piano against the hall wall, south of the opening (Sketchfab "Ludovico Einaudi piano", Doc Arnault,
        // CC BY; 1.9 m wide with its stool), and a 1920s standing lamp between the sofa and the side table
        // (Mad_Lobster_Workshop, CC BY).
        Against(f, "Piano", V2(4f, 2.9f), Vector2.right, 0f);
        Model(f, "FloorLamp1920", V2(10.3f, 8.4f), 0f, 0f);

        // ---------------- kitchen: units along the north wall, the fridge, the table
        // The units are 1 m wide side by side on the north wall: laid out in metres from the wall's face, not in plan
        // units (under the plan scale a plan step of 1 left 25 cm gaps between them).
        Vector3 unitsAt = M(V2(-16.2f, 16f), 0f) + Vector3.back * (WallHalf * PlanScale);
        ModelProp("KitchenSink", f, unitsAt + Vector3.back * 0.31f, Quaternion.Euler(0f, 180f, 0f));
        for (int i = 0; i < 4; i++)
        {
            ModelProp("KitchenBase", f, unitsAt + new Vector3(1f + i, 0f, -0.31f), Quaternion.Euler(0f, 180f, 0f));
        }

        for (int i = 0; i < 5; i++)
        {
            ModelProp("KitchenWall", f, unitsAt + new Vector3(i, 1.5f, -0.17f), Quaternion.Euler(0f, 180f, 0f));
        }

        // Dayse's old Soviet fridge (Sketchfab "Old Soviet Fridge MINSK 12E", Turbo, CC BY) in place of the placeholder.
        Against(f, "SovietFridge", V2(-19.5f, 9.8f), Vector2.right, 0f);
        Model(f, "OldPot", V2(-18.9f, 4.4f), 20f, 0f);
        Hung(f, "OldClock", V2(-9.7f, 14.5f), Vector2.left, 1.9f);
        // Dayse's "Old table and chair set" (Sketchfab, AndreiVNK, CC BY): one model, table and four chairs. Baked at
        // 1.7 m its table stood 0.85 m high; 0.9 brings it to 0.76.
        GameObject kitchenTable = Model(f, "KitchenTableSet", V2(-15.4f, 8.3f), 0f, 0f);
        if (kitchenTable != null)
        {
            kitchenTable.transform.localScale *= 0.9f;
            // The set's box top is the chair backs and its table top is only 0.85 x 1.5 m (plan x -15.72 to -15.04, z 7.7 to
            // 8.9, measured 14/11): the jug and the pan stood beside it in the air. On the top itself now (TopAt).
            ModelProp("Jug", f, OnTop(kitchenTable, V2(-15.55f, 8.5f)), Quaternion.identity, false);
            ModelProp("BrassPan", f, OnTop(kitchenTable, V2(-15.3f, 8.1f)), Quaternion.Euler(0f, 60f, 0f), false);
        }

        Model(f, "EnamelPot", V2(-17.3f, 15.5f), 30f, 1.0f);
        Model(f, "CuttingBoard", V2(-13.6f, 15.5f), 10f, 0.92f);
        // West of the batteries (the story's microwave stands at -13, 15.5: the kettle was inside it, 14/11).
        Model(f, "Kettle", V2(-14.6f, 15.55f), 200f, 0.92f);
        // The house's cooling (GDD: fitted against the heat): a ceiling cassette over the kitchen (Sketchfab, Mehdi
        // Shahsavan, CC BY), its grille flush with the ceiling and its box up in the slab.
        // The model has its grille on top and the box below: turned over, grille 4 cm under the ceiling.
        GameObject cassette = Ceiling(f, "CeilingAC", V2(-15.4f, 11.6f), GC);
        if (cassette != null)
        {
            cassette.transform.rotation = Quaternion.Euler(0f, 0f, 180f) * cassette.transform.rotation;
            cassette.transform.position += Vector3.up * (M(V2(-15.4f, 11.6f), GC).y - 0.04f - RendererBounds(cassette).min.y);
        }

        // ---------------- pantry and china room
        GameObject shelves1 = Against(f, "SteelShelves2", V2(-14.4f, 18.4f), Vector2.right, 0f);
        GameObject shelves2 = Against(f, "SteelShelves2", V2(-14.4f, 21.6f), Vector2.right, 0f);
        Against(f, "WornRack", V2(-9.7f, 19.2f), Vector2.left, 0f);
        GameObject lowerBox = Model(f, "CardboardBox", V2(-11.4f, 23.6f), 20f, 0f);
        // The second box on the first (it hung 40 cm beside it, in the air, 14/11).
        On(f, "CardboardBox", lowerBox, 0.03f, -35f, -0.02f);
        Model(f, "LongLifeFood", V2(-12.6f, 17.2f), 0f, 0f);
        // Cans on the units' planks (0.62, 1.12 and 1.64 m, measured 14/11): they stood 13 cm east of the units at heights
        // between the planks, floating.
        OnLevel(f, "FoodCans", shelves1, 0.9f, 0f, -0.1f, 0f);
        OnLevel(f, "FoodCans", shelves2, 1.4f, 0f, 0.15f, 40f);
        Model(f, "Bucket2", V2(-10.6f, 22.8f), 0f, 0f);
        Model(f, "Barrel2", V2(-13.6f, 23.8f), 0f, 0f);
        // A second, abandoned fridge in the pantry (Sketchfab "Old Fridge", kai9987kai, CC BY).
        Against(f, "OldFridge", V2(-9.7f, 17f), Vector2.left, 0f);
        // Its front is the file's -X (measured 14/11: it stood with its narrow end on the wall, sticking 1.1 m into the room).
        GameObject display = Against(f, "DisplayShelves", V2(-8.5f, 24.6f), Vector2.down, 0f, 90f);
        On(f, "MedievalJug", display, 0f, 0f, 0.3f);
        GameObject chinaShelves = Against(f, "PaintedShelves", V2(-5.9f, 24.6f), Vector2.down, 0f);
        // The unit has no top board (measured 14/11): on its upper plank, inside.
        OnLevel(f, "TeaSet", chinaShelves, 1f, 0f, 0f, 0f);
        // An old cabinet on the china room's east wall (Sketchfab "Cabinet_old", Shintokin, CC BY).
        // Its back is the file's +X (14/11: it stood across the room with its end on the wall).
        Against(f, "OldCabinet", V2(-4.9f, 22.4f), Vector2.left, 0f, 90f);

        // ---------------- vestibule, power room, laundry
        Against(f, "Bench", V2(-4.9f, 23.2f), Vector2.right, 0f);
        Model(f, "ClayPot", V2(1.8f, 25.6f), 90f, 0f);
        Model(f, "ClayVase", V2(3.6f, 25.4f), 0f, 0f);
        // Dayse's classic leather sofa set (Sketchfab, chris_models, CC BY: sofa, two armchairs and their table in one
        // mesh) on the vestibule's west side, facing east; the way from the hall to the north door stays clear.
        Model(f, "ClassicSofaSet", V2(-2.6f, 24.2f), 90f, 0f);
        Hung(f, "PowerBox", V2(7.6f, 24.6f), Vector2.down, 1.1f);
        // The power room (Dayse's list: an emergency generator, fuel, an industrial lamp).
        Against(f, "PortableGenerator", V2(10.1f, 23.9f), Vector2.left, 0f);
        Model(f, "LpgTank", V2(5.7f, 24f), 0f, 0f);
        Hung(f, "IndustrialWallLamp", V2(5.1f, 22.8f), Vector2.right, 1.9f);
        GameObject chest = Against(f, "ToolChest", V2(10.1f, 22.6f), Vector2.left, 0f);
        On(f, "Toolbox", chest, 0f);
        Model(f, "Jerrycan", V2(6f, 22.1f), 30f, 0f);
        Against(f, "WashingMachine", V2(15f, 20.4f), Vector2.left, 0f);
        Against(f, "WashingMachine", V2(15f, 21.2f), Vector2.left, 0f);
        Against(f, "KitchenSink", V2(13.6f, 15.4f), Vector2.up, 0f);
        Model(f, "Bleach", V2(12.8f, 16f), 20f, 0f);
        Model(f, "Cleaner", V2(14.5f, 16.1f), 0f, 0f);
        Model(f, "Bucket2", V2(14.3f, 19.4f), 0f, 0f);
        Model(f, "CardboardBox", V2(12.4f, 21.6f), 30f, 0f);

        // ---------------- library: worn bookshelves on the north wall (the one nailed over the hidden door is the door
        // itself), the encyclopedias on the drawer cabinet by the west wall
        // The north-west corner is the red silk fungus's (FloodedHouseFlora.SilkFungus, 2.4 m each way): the first shelf
        // stood inside it (14/11). The river painting moved south on the west wall for the same reason.
        Against(f, "Bookshelf", V2(12.9f, 13.4f), Vector2.down, 0f);
        Against(f, "Bookshelf", V2(16.6f, 13.4f), Vector2.down, 0f);
        GameObject drawers = Against(f, "DrawerCabinet", V2(19.9f, 11.4f), Vector2.left, 0f);
        GameObject table = Model(f, "RoundTable", V2(15.8f, 10.6f), 0f, 0f);
        On(f, "ChessSet", table, -0.2f);
        On(f, "OilLamp", table, 0.45f);
        // The encyclopedias on the reading table, behind the chess set: the cabinet's top (1.13 m inside its rim) does not
        // hold them and the radio both (14/11).
        On(f, "Encyclopedias", table, 0f, 90f, 0.42f);
        Model(f, "GreenChair", V2(15.8f, 9.4f), 0f, 0f);
        Model(f, "GreenChair", V2(15.8f, 11.8f), 180f, 0f);
        Floor(f, "RugCarpet", V2(15.6f, 9.2f), 90f, 0f);
        // Dayse's radio on the drawer cabinet (Sketchfab "Vintage Graetz Radio", nsadoun, CC BY), her glazed bookcase on the
        // south wall east of the octagonal window, the dusty bookshelf on the west wall north of the living-room door and a
        // 1920s standing lamp between the east windows.
        On(f, "GraetzRadio", drawers, 0f);
        Against(f, "Bookcase", V2(18.2f, 3.4f), Vector2.up, 0f);
        Against(f, "DustyBookshelf", V2(11.6f, 8f), Vector2.right, 0f);
        // South of the dressing's shelf between the east windows (it stood inside it, 14/11).
        Model(f, "FloorLamp1920", V2(19.45f, 7.6f), 0f, 0f);

        // ---------------- the courtyard's gallery and garden
        Against(f, "Bench", V2(-9.7f, 17.4f), Vector2.right, 0f);
        Against(f, "Bench", V2(10.1f, 17.4f), Vector2.left, 0f);
        // No planters in the garden since 29/10: Dayse's "Varanda" drawings show the lawn bare and flooded.

        // ---------------- outside: the refrigeration system the house was fitted with against the heat (GDD), its
        // condenser on the kitchen's west wall between the windows
        // On the ground outside (GardenY), not the house floor: it hung 30 cm over the dirt (14/11).
        Against(f, "AirconUnit", V2(-19.5f, 9.8f), Vector2.left, LunaManor.GardenY);

        // ---------------- garage
        // On the two timber sleepers LunaManor lays in the garage (their tops at 0.2).
        Model(f, "BoatPlaceholder", V2(-10.8f, 30.2f), 90f, 0.2f);
        Against(f, "ToolChest", V2(-14.7f, 28.2f), Vector2.right, 0f);
        Against(f, "WornRack", V2(-6.9f, 28.8f), Vector2.left, 0f);
        Model(f, "Barrel2", V2(-7.6f, 27.4f), 0f, 0f);
        Model(f, "Barrel2", V2(-8.4f, 27.3f), 40f, 0f);
        Model(f, "Jerrycan", V2(-13.9f, 32.8f), 10f, 0f);
        // Dayse's metal case (Sketchfab "Sci-fi Case/box", TechnoCraft, CC BY) by the tool chest.
        Model(f, "MetalCase", V2(-13.9f, 29.6f), 15f, 0f);
        Hung(f, "Lifebuoy", V2(-14.7f, 31f), Vector2.right, 1.4f);

        // ---------------- upper floor: bedrooms
        Bedroom(f, rug, LunaManor.Solteiro1, -14.4f, true);
        Bedroom(f, rug, LunaManor.Solteiro2, -14.4f, true);
        Bedroom(f, rug, LunaManor.Solteiro3, 15f, false);
        // Master bedrooms: the bed's head on the side wall, a commode or a wardrobe opposite, a curtain.
        Against(f, "GothicBed", V2(-7.5f, 23.1f), Vector2.right, UF);
        GameObject nightstand1 = Against(f, "Nightstand", V2(-7.5f, 21.95f), Vector2.right, UF);
        On(f, "TableLamp1920", nightstand1, 0f);
        GameObject clock = Against(f, "Commode", V2(-2.6f, 23.1f), Vector2.left, UF);
        On(f, "AlarmClock", clock, 0.2f);
        Against(f, "GothicBed", V2(7.9f, 23.1f), Vector2.left, UF);
        GameObject nightstand2 = Against(f, "Nightstand", V2(7.9f, 24.25f), Vector2.left, UF);
        On(f, "AntiqueLamp", nightstand2, 0f);
        Against(f, "VintageCabinet", V2(2.8f, 23.1f), Vector2.right, UF);
        // Dayse's gothic mirror (a cheval mirror on its own stand) on the second master bedroom's south wall, west of
        // its door (Sketchfab, croklin, CC BY).
        Against(f, "GothicMirror", V2(3.9f, 21.6f), Vector2.up, UF);
        // Her gothic cabinet in the balcony passage (Console Art Cybernetic, Free Standard).
        Against(f, "GothicCabinetSF", V2(2.8f, 23.2f), Vector2.left, UF);
        // The old vanity in bedroom 2, on the wall by the wardrobe (wolfgar74, CC BY).
        Against(f, "OldVanity", V2(-11.2f, LunaManor.Solteiro2.yMin), Vector2.up, UF);
        // Alex's room: his day bed by the door, the dresser on the north wall, the table with his books, the cork board
        // (Dayse's list: the board with Alex's notes) on the east wall north of the window, a flashlight on the table.
        Against(f, "DayBed", V2(LunaManor.QuartoAlex.xMin, 9.95f), Vector2.right, UF);
        // The dresser model is long along its own z: turned to run along the north wall, its back 15 cm + half its 0.42 m depth off it.
        GameObject dresser = Model(f, "Dresser", V2(12.7f, LunaManor.QuartoAlex.yMax - WallHalf - 0.21f), 90f, UF);
        // Along the dresser (its length is its own z, turned to world x): with `along` it stood 40 cm off the dresser's
        // front, in the air (14/11).
        On(f, "BookStack", dresser, 0f, 0f, 0.4f);
        GameObject bookTable = Model(f, "BookTable", V2(12.7f, 11.4f), 15f, UF);
        On(f, "TacticalFlashlight", bookTable, -0.3f, 70f);
        // Alex's cracked tablet (Meshy 09/10, 3.9 k triangles, 0.26 x 0.19 m, its screen the file's +z) lying screen up
        // on the table to the right of the flashlight; scenery for now, the news page on its screen comes as a Readable.
        FlatOn(f, "AlexTablet", bookTable, 0.25f, 20f);
        GameObject cork = Hung(f, "CorkBoard", V2(LunaManor.QuartoAlex.xMax, 13.3f), Vector2.left, UF + 1.3f);
        // One-sided mesh whose cork faces its own -Z: turned about its centre so the cork faces the room (16/10, only the
        // outline showed before).
        cork.transform.RotateAround(RendererBounds(cork).center, Vector3.up, 180f);
        // Grown to 1.5 m and filled (10/11): clippings of years 0 to 10, photos, the map, Alex's notes, red string.
        AlexBoard(f, cork, V2(LunaManor.QuartoAlex.xMax, 13.3f), Vector2.left, UF + 1.5f);
        // Generator room 2: the armoured cabinet on the south wall (Ray Ferric, CC BY).
        Against(f, "ArmoredCabinet", V2(13.2f, LunaManor.Gerador2.yMin), Vector2.up, UF);
        Carpet(f, rug, V2(-5f, 23.1f), new Vector2(2.4f, 2.2f), 0f, UF);
        Curtains(f, -5f, 24.6f, 1.2f, UF, true);
        Curtains(f, 5.3f, 24.6f, 1.2f, UF, true);
        Against(f, "Suitcases", V2(0.1f, 21.6f), Vector2.up, UF);
        // Ceiling fans over the beds (the climate went hot: Poly Haven ceiling_fan), a vase and a photo on the commode.
        // Over the bed, north of the room's pendant (at the centre they shared one point, audit 22/10).
        Ceiling(f, "CeilingFan", LunaManor.Casal1.center + new Vector2(0f, 1.05f), UC);
        Ceiling(f, "CeilingFan", LunaManor.Casal2.center + new Vector2(0f, 1.05f), UC);
        // and a split unit over each bed's head (Sketchfab "Air Conditioner", jexxuwu, CC BY).
        Hung(f, "SplitAC", V2(-7.5f, 23.1f), Vector2.right, UF + 2.3f);
        Hung(f, "SplitAC", V2(7.9f, 23.1f), Vector2.left, UF + 2.3f);
        On(f, "AntiqueVase", clock, -0.35f);

        // Bathrooms: the bath under the window, the toilet, the basin by the door.
        Against(f, "Bathtub", V2(-11.4f, 24.6f), Vector2.down, UF);
        Against(f, "Toilet", V2(-7.5f, 23.6f), Vector2.left, UF);
        Against(f, "Washbasin", V2(-10.6f, 21.6f), Vector2.up, UF);
        Against(f, "Bathtub", V2(11.6f, 24.6f), Vector2.down, UF);
        Against(f, "Toilet", V2(15f, 22.6f), Vector2.left, UF);
        Against(f, "Washbasin", V2(10.6f, 21.6f), Vector2.up, UF);

        // Galleries: an armchair looking down into the courtyard.
        Model(f, "ArmChair", V2(-8.6f, 12.4f), 90f, UF);
        GameObject stand = Against(f, "SideTableTall", V2(-10f, 13f), Vector2.right, UF);
        On(f, "ClayPot", stand, 0f);
        Against(f, "Bench", V2(-10f, 18.2f), Vector2.right, UF);

        // The bunker, the store and generator room 1 (14/11, FloodedHouseServiceRooms).
        ServiceRooms(f);

        WaterStains(f);
        // The house left to the damp: grime, leaves, papers, fallen plaster, chairs knocked over (29/10).
        Abandon(parent, f);
    }

    // A single bedroom: the day bed along the far wall, a nightstand, a wardrobe in the near corner, a rug.
    private static void Bedroom(Transform f, Material rug, Rect room, float outerX, bool west)
    {
        float far = room.yMax, near = room.yMin, mid = room.center.x;
        Vector2 inward = west ? Vector2.right : Vector2.left;
        Against(f, "DayBed", V2(mid, far), Vector2.down, UF);
        Against(f, "Nightstand", V2(mid + (west ? -1.55f : 1.55f), far), Vector2.down, UF);
        Against(f, "Wardrobe", V2(outerX + inward.x * 0.85f, near), Vector2.up, UF);
        Carpet(f, rug, room.center, new Vector2(1.6f, 2.2f), 0f, UF);
    }

    // ---------------------------------------------------------------- placing

    private static GameObject Model(Transform parent, string name, Vector2 at, float yaw, float y)
    {
        GameObject m = ModelProp(name, parent, M(at, y), Quaternion.Euler(0f, yaw, 0f));
        return m;
    }

    // Lying on the floor of a level (a carpet model): no collider, nothing cast.
    private static GameObject Floor(Transform parent, string name, Vector2 at, float yaw, float y)
    {
        GameObject m = ModelProp(name, parent, M(at, y + 0.002f), Quaternion.Euler(0f, yaw, 0f), false);
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        return m;
    }

    // Hanging from a ceiling at `ceilingY` (the model's top against it; no collider).
    private static GameObject Ceiling(Transform parent, string name, Vector2 at, float ceilingY)
    {
        GameObject m = ModelProp(name, parent, M(at, ceilingY), Quaternion.identity, false, false);
        Bounds b = RendererBounds(m);
        m.transform.position += Vector3.up * (M(at, ceilingY).y - b.max.y);
        return m;
    }

    // Back against the wall at `wall` (a point on the wall's centre line), facing `into` the room. `modelYaw` turns a
    // model whose front is not its +Z (the china room's display shelves and old cabinet have theirs on +X, 14/11).
    private static GameObject Against(Transform parent, string name, Vector2 wall, Vector2 into, float y, float modelYaw = 0f)
    {
        float yaw = Mathf.Atan2(into.x, into.y) * Mathf.Rad2Deg + modelYaw;
        GameObject m = ModelProp(name, parent, M(wall, y), Quaternion.Euler(0f, yaw, 0f));
        Bounds b = RendererBounds(m);
        Vector3 inward = new Vector3(into.x, 0f, into.y);
        float backExtent = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))));
        float centreOffset = Vector3.Dot(b.center - m.transform.position, inward);
        m.transform.position += inward * (FaceOffset(M(wall, y), inward, b.size.y) + 0.03f + backExtent - centreOffset);
        return m;
    }

    // On a wall, its bottom `height` over the floor of the level (no collider: wall-hung).
    private static GameObject Hung(Transform parent, string name, Vector2 wall, Vector2 into, float height)
    {
        float floorY = M(wall, 0f).y;
        GameObject m = Against(parent, name, wall, into, floorY);
        m.transform.position += Vector3.up * height;
        foreach (Collider c in m.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(c);
        }

        return m;
    }

    // On top of another model, `along` metres to its right (local x) and `across` to its front (local z), turned `yaw`
    // more. The height is the model's real surface at that point (TopAt), not its box top.
    private static GameObject On(Transform parent, string name, GameObject under, float along, float yaw = 0f, float across = 0f)
    {
        if (under == null)
        {
            return null;
        }

        Bounds b = RendererBounds(under);
        Vector3 at = new Vector3(b.center.x, b.max.y, b.center.z) + Right(under) * along + Forward(under) * across;
        at.y = RestHeight(under, at, b) + 0.002f;
        GameObject m = ModelProp(name, parent, at, Quaternion.Euler(0f, under.transform.eulerAngles.y + yaw, 0f), false);
        return m;
    }

    // A model's right and front on the floor plane. Its transform carries the FBX root's axis conversion (a quarter turn
    // about x), so transform.forward can point up or down: the encyclopedias asked for 0.45 m "in front" of the reading
    // table landed on its centre (14/11).
    private static Vector3 Right(GameObject model)
    {
        Vector3 r = model.transform.right;
        r.y = 0f;
        return r.sqrMagnitude > 1e-6f ? r.normalized : Vector3.right;
    }

    private static Vector3 Forward(GameObject model)
    {
        return Vector3.Cross(Right(model), Vector3.up);
    }

    // Where a thing set on top of `under` rests: its real surface there (TopAt), unless that lies deep in the model (the
    // point fell into an open box's inside or a chest's open top, more than 15% of the height under the box top): then
    // the box top, as the eye expects (14/11).
    private static float RestHeight(GameObject under, Vector3 at, Bounds b)
    {
        float y = TopAt(under, at, b.max.y);
        return y < b.max.y - 0.15f * b.size.y ? b.max.y : y;
    }

    // On a shelf of another model: its highest surface under `below` at the model's centre moved `along` (its right)
    // and `across` (its front).
    private static GameObject OnLevel(Transform parent, string name, GameObject under, float below, float along, float across, float yaw)
    {
        if (under == null)
        {
            return null;
        }

        return ModelProp(name, parent, LevelAt(under, below, along, across), Quaternion.Euler(0f, under.transform.eulerAngles.y + yaw, 0f), false);
    }

    // The point on `under`'s highest surface less than `below` metres over its base, at its centre moved `along` (its
    // right) and `across` (its front): a plank of a shelf. `below` counts from the model's base (upstairs too, 14/11).
    private static Vector3 LevelAt(GameObject under, float below, float along, float across)
    {
        Bounds b = RendererBounds(under);
        Vector3 at = new Vector3(b.center.x, b.min.y, b.center.z) + Right(under) * along + Forward(under) * across;
        at.y = TopAt(under, at, b.min.y, b.min.y + below) + 0.002f;
        return at;
    }

    // A world point on the top of `under` at the plan position `at` (for things placed by plan coordinates).
    private static Vector3 OnTop(GameObject under, Vector2 at)
    {
        Vector3 p = M(at, 0f);
        p.y = TopAt(under, p, RendererBounds(under).max.y) + 0.002f;
        return p;
    }

    // The height of `under`'s surface under a vertical line through `at` (x, z): the highest horizontal-ish triangle of
    // its meshes there, below `below`. The box collider hides the real top (a moulded frame round a cabinet top stands
    // 12 cm over it, a table top is narrower than the chairs round it, a shelf has planks): measured 14/11. Falls back
    // to `fallback` with a warning when no triangle is there, which means the thing would hang beside the model.
    private static float TopAt(GameObject under, Vector3 at, float fallback, float below = float.PositiveInfinity)
    {
        float best = float.NegativeInfinity;
        foreach (MeshFilter mf in under.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Vector3[] v = mesh.vertices;
            int[] tri = mesh.triangles;
            Transform t = mf.transform;
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(v[tri[i]]), b = t.TransformPoint(v[tri[i + 1]]), c = t.TransformPoint(v[tri[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Mathf.Abs(n.y) < 0.3f * n.magnitude)
                {
                    continue;
                }

                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f)
                {
                    continue;
                }

                float l1 = ((b.z - c.z) * (at.x - c.x) + (c.x - b.x) * (at.z - c.z)) / d;
                float l2 = ((c.z - a.z) * (at.x - c.x) + (a.x - c.x) * (at.z - c.z)) / d;
                float l3 = 1f - l1 - l2;
                if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f)
                {
                    continue;
                }

                float y = l1 * a.y + l2 * b.y + l3 * c.y;
                if (y > best && y < below)
                {
                    best = y;
                }
            }
        }

        if (float.IsNegativeInfinity(best))
        {
            Debug.LogWarning($"[FloodedHouse] Nothing of {under.name} under ({at.x:F2}, {at.z:F2}): resting on its box top {fallback:F2}");
            return fallback;
        }

        return best;
    }

    // A prop placed by its centre on a wall's line, moved so its back stands 3 cm off the wall's face (the Poly Haven
    // shelves and pipes sat half inside their walls, 14/11). `wall` is a point on the wall's centre line, `into` the room.
    private static GameObject OffWall(GameObject m, Vector2 wall, Vector2 into)
    {
        Bounds b = RendererBounds(m);
        Vector3 inward = new Vector3(into.x, 0f, into.y);
        float backExtent = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(inward.x), 0f, Mathf.Abs(inward.z))));
        float centreOffset = Vector3.Dot(b.center - M(wall, 0f), inward);
        m.transform.position += inward * (FaceOffset(M(wall, b.center.y - b.extents.y), inward, b.size.y) + 0.03f + backExtent - centreOffset);
        return m;
    }

    // How far the wall's face is from its centre line, measured: a ray from 1 m inside the room back to the wall at the
    // model's mid height (props skipped). The walls are not all one thickness (interior 0.2 plan, outer 0.3, the bunker
    // 0.5) and the old constant left things 6 cm off interior walls and 12 cm inside the bunker's (14/11). Falls back
    // to the outer walls' half thickness.
    private static float FaceOffset(Vector3 wallWorld, Vector3 inward, float modelHeight)
    {
        Vector3 from = wallWorld + inward * 1.0f + Vector3.up * Mathf.Clamp(modelHeight * 0.5f, 0.25f, 1.2f);
        if (WallFace(from, -inward, 1.3f, out Vector3 hit))
        {
            float d = Vector3.Dot(hit - wallWorld, inward);
            if (d > 0.02f && d < 0.6f)
            {
                return d;
            }
        }

        return WallHalf * PlanScale;
    }

    // The first static surface a ray meets (the house's walls and floors, not the props): where a thing on a wall hangs.
    internal static bool WallFace(Vector3 from, Vector3 direction, float maxDistance, out Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(from, direction, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (p, q) => p.distance.CompareTo(q.distance));
        foreach (RaycastHit h in hits)
        {
            bool prop = false;
            for (Transform t = h.transform; t != null; t = t.parent)
            {
                string n = t.name;
                if (n == "Furniture" || n == "HouseArt" || n == "Story" || n == "MansionStory" || n == "Flora" || n == "GardenFlora" || n == "ManorLamps" || n == "LockedDoors" || n == "Abandoned" || n == "Player")
                {
                    prop = true;
                    break;
                }
            }

            if (!prop)
            {
                point = h.point;
                return true;
            }
        }

        point = from;
        return false;
    }

    // On top of another model, lying flat with the file's +z face (a screen) turned up, `along` metres to its right,
    // turned `yaw` more; centred on that point.
    private static GameObject FlatOn(Transform parent, string name, GameObject under, float along, float yaw = 0f)
    {
        if (under == null)
        {
            return null;
        }

        Bounds b = RendererBounds(under);
        Vector3 at = new Vector3(b.center.x, b.max.y, b.center.z) + Right(under) * along;
        at.y = RestHeight(under, at, b) + 0.002f;
        GameObject m = ModelProp(name, parent, at, Quaternion.Euler(-90f, under.transform.eulerAngles.y + yaw, 0f), false);
        Vector3 centre = RendererBounds(m).center;
        m.transform.position += new Vector3(at.x - centre.x, 0f, at.z - centre.z);
        return m;
    }

    // ---------------------------------------------------------------- soft things

    private static Material Rug()
    {
        // Poly Haven quatrefoil_jacquard_fabric (CC0): a red patterned rug, the pattern doubled (0.28 m a tile in the
        // original read as upholstery) and faded.
        Material m = Textured("OldRug", 0.6f, new Color(0.8f, 0.72f, 0.68f), 0.05f);
        return m;
    }

    private static void Carpet(Transform parent, Material rug, Vector2 centre, Vector2 size, float yaw, float floorY)
    {
        Vector3 c = M(centre, floorY);
        GameObject r = Box("Rug", parent, c + new Vector3(-size.x * 0.5f, 0.002f, -size.y * 0.5f), c + new Vector3(size.x * 0.5f, 0.012f, size.y * 0.5f), rug, false);
        r.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.transform.RotateAround(c, Vector3.up, yaw);
    }

    // A pair of curtains each side of a window (alongX: the window's wall runs along x and faces -z into the room).
    private static void Curtains(Transform parent, float x, float z, float span, float floorY, bool northWall = false)
    {
        Vector2 into = northWall ? Vector2.down : Vector2.up;
        // Dayse's Victorian curtains (Sketchfab, Abdullah Mohammed, CC BY) when the model is in; the Blender slab otherwise.
        bool victorian = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/VictorianCurtains/VictorianCurtains.fbx") != null;
        foreach (float side in new[] { -1f, 1f })
        {
            GameObject c = Against(parent, victorian ? "VictorianCurtains" : "Curtain", V2(x + side * (span * 0.5f + 0.25f), z), into, floorY);
            foreach (Collider col in c.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(col);
            }

            if (!victorian)
            {
                c.transform.localScale = new Vector3(c.transform.localScale.x * 0.45f, c.transform.localScale.y, c.transform.localScale.z);
            }
        }
    }

    // Water that came in through the roof and ran down the walls: ambientCG "Leaking" decals hung from the ceilings
    // of the rooms the rain reaches (more upstairs, under the roof), at seeded places along the walls.
    private static void WaterStains(Transform parent)
    {
        Transform t = Group("WaterStains", parent);
        string[] leaks = { "Leaking001", "Leaking003", "Leaking005" };
        Material[] materials = new Material[leaks.Length];
        for (int i = 0; i < leaks.Length; i++)
        {
            materials[i] = LeakMaterial(leaks[i]);
        }

        Random.State saved = Random.state;
        Random.InitState(2026);
        foreach ((string name, Vector2[] outline, int level) in LunaManor.RoomOutlines())
        {
            if (name.StartsWith("Bunker") || name.StartsWith("Corredor") || name.StartsWith("Deposito"))
            {
                continue;
            }

            float ceilingY = level == 0 ? GC : UC;
            float floorY = level == 0 ? 0f : UF;
            float chance = level == 0 ? 0.35f : 0.6f;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                float length = Vector2.Distance(a, b);
                if (length < 1.6f || Random.value > chance)
                {
                    continue;
                }

                Vector2 dir = (b - a) / length;
                // Inward normal: towards the polygon's centre.
                Vector2 centre = Vector2.zero;
                foreach (Vector2 q in outline)
                {
                    centre += q;
                }

                centre /= outline.Length;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                Vector2 mid = (a + b) * 0.5f;
                if (Vector2.Dot(centre - mid, normal) < 0f)
                {
                    normal = -normal;
                }

                float width = Random.Range(0.9f, 1.8f);
                float height = Random.Range(1.4f, ceilingY - floorY - 0.2f);
                Vector2 at = a + dir * Random.Range(width * 0.5f + 0.2f, Mathf.Max(width * 0.5f + 0.21f, length - width * 0.5f - 0.2f)) + normal * WallHalf;
                GameObject go = new GameObject("Stain");
                go.transform.SetParent(t, false);
                go.transform.position = M(at, ceilingY - height * 0.5f - 0.02f);
                go.transform.rotation = Quaternion.LookRotation(new Vector3(-normal.x, 0f, -normal.y), Vector3.up);
                DecalProjector d = go.AddComponent<DecalProjector>();
                d.material = materials[Random.Range(0, materials.Length)];
                d.size = new Vector3(width, height, 0.5f);
                d.pivot = Vector3.zero;
                d.fadeFactor = Random.Range(0.55f, 0.9f);
                d.drawDistance = 30f;
            }
        }

        Random.state = saved;
    }

    private static Material LeakMaterial(string name)
    {
        string folder = ProjectPaths.Art + "/VFX/Textures/Decals/Leaking";
        string path = folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Shader Graphs/Decal"));
            AssetDatabase.CreateAsset(m, path);
        }

        foreach ((string file, bool normal) in new[] { (name, false), (name + "_Normal", true) })
        {
            string texturePath = folder + "/" + file + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null && (importer.wrapMode != TextureWrapMode.Clamp || (normal && importer.textureType != TextureImporterType.NormalMap) || (!normal && !importer.alphaIsTransparency)))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.alphaIsTransparency = !normal;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
        }

        m.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + ".png"));
        m.SetTexture("Normal_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_Normal.png"));
        m.SetFloat("Normal_Blend", 0.4f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }
}
