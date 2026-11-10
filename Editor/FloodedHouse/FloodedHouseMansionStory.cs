using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Dayse's demo script placed in the house: what lies where, and the director's wiring. The texts of the
// clippings and recordings are hers (the newspaper headlines of the demo, the recordings of "Alex"); the
// voices are placeholders (SourceAssets/Tools/tts.ps1 + voice_process.py) until recorded.
// Where each headline lives (Gustavo approved, 02/10): 15 years into the collapse daily papers hardly exist, so the
// headlines of years 0 to 5 are clippings Alex kept, and what came after reaches the house as an official notice:
// the government decree, the Polícia Federal poster, the evacuation leaflet, and the Defesa Civil bulletin on
// Alex's shortwave radio. Dayse's wording is unchanged; the decree, poster and leaflet only got a heading naming
// the object.
public static partial class FloodedHouseBuilder
{
    // The seven clippings and notices: Dayse's texts live in the Loc table (Settings/Resources/Localization/ui.txt) as
    // story.clip<n>.body ("Como uma pequena caverna submersa no Atlântico..." to "FOLHETO OFICIAL..."), with the
    // titles in story.clip<n>.title and what each one is in story.clip<n>.sig (the first four are newspaper clippings
    // Alex kept, years 0 to 5; the rest are notices). The components keep the keys and resolve them when read.
    private static ReadableNote Clipping(int index, Transform parent, Vector3 at, float yaw)
    {
        PlayerItems.EntryKind kind = index < 4 ? PlayerItems.EntryKind.Clipping : PlayerItems.EntryKind.Notice;
        string n = (index + 1).ToString();
        return Readable("Clipping" + n, parent, at, yaw, "recorte_" + n, "story.clip" + n + ".title", kind,
            "story.clip" + n + ".body", "story.clip" + n + ".sig");
    }

    // Where each scene of the demo happens in the mansion of the floor plan (GDD "Planta da Casa", 11/10; LunaManor):
    //   1. dining room: the radio desk in the bay window (recording 1; the tentacle passes outside), the dinner table;
    //      the security panel by the front door in the hall; the graduation photo on the drawer in the living room.
    //   2/3. upper floor: the glow of the fungus in the north gallery (the safe light), thick spores past it in the east
    //      wing (mask only); the blackout fires on reaching that light.
    //   torch on the cabinet in the hall, batteries on the kitchen counter, the dehumidifier key on the kitchen floor.
    //   4. the laundry and service area (the dehumidifier; its outer wall breaks, the Drowned comes in), the hook pole.
    //   5. the attic over the west wing (hatch in the west gallery's ceiling, pulled with the pole): suit, map, filter;
    //      generator room 2 (keypad 2014) in the spores: Alex's note, the basement key.
    //   6. the basement stair in the laundry; the flooded pantry below (unchanged).
    private static void BuildMansionStory(PlayerRig rig, DiveHud hud, DreadDirector dread, FloodedHouseDirector director, AudioSource houseAudio)
    {
        Transform s = Group("MansionStory");
        // The spring and creak of a folding attic ladder (rucisko, CC0).
        AudioClip creak = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/attic_ladder.wav");
        // Recorded door creaks and an old key turning (Gustavo's second batch, 01/10): each door its own creak.
        AudioClip[] doorCreaks = { DoorSound("door_creak_1"), DoorSound("door_creak_2"), DoorSound("door_creak_3") };
        AudioClip[] keyTurns = { DoorSound("key_unlock_1"), DoorSound("key_unlock_2"), DoorSound("key_unlock_3") };
        // The generator room: a heavy metal door whose bolt the keypad pulls back (third batch, 02/10).
        AudioClip metalDoorOpen = DoorSound("metal_door_open");
        AudioClip metalBoltUnlock = DoorSound("metal_bolt_unlock");

        ManorLamps(s);
        LockedDoors(s, doorCreaks[1], keyTurns[2]);
        PlanFungus(s);
        Garden(s, rig.controller.transform);

        // ---------------- 1. dining room
        Recorder first = MakeRecorder("Recorder1", s, M(-8.05f, 0.781f, 2.55f), -20f, "gravacao_1", "story.rec1.title", "rec1",
            Voice("erick_rec1"), "voice.erick.rec1");

        // The security panel by the front door, blinking red: the first thing that reads in the dark hall.
        SecurityPanel panel = BuildSecurityPanel(s, hud, M(-3.88f, 1.55f, 3.1f), 90f);

        // The dinner table, still laid (Dayse's picks in "Modelos 3D - Sob o Manto", 13/10, through game-3d-assets): the
        // long table, eight Regency chairs, a decorated plate, a goblet and cutlery at every place, a gold dish in the
        // middle between two candlesticks.
        Vector3 t = M(-7.8f, 0f, 6.1f);
        GameObject table = ModelProp("LargeDiningTable", s, t, Quaternion.identity);
        float tableTop = RendererBounds(table).max.y;
        Chandelier("DiningChandelier", s, t.x, t.z, GC, 2.4f, 8f);
        (float x, float z, float yaw)[] seats =
        {
            (-1.2f, -0.95f, 0f), (0f, -0.95f, 0f), (1.2f, -0.95f, 0f), (-1.2f, 0.95f, 180f), (0f, 0.95f, 180f), (1.2f, 0.95f, 180f),
            (-2.2f, 0f, 90f), (2.2f, 0f, -90f),
        };
        foreach ((float x, float z, float yaw) in seats)
        {
            ModelProp("MercuryChair", s, t + new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw + Random.Range(-6f, 6f), 0f));
            // The place: towards the chair from the table's edge.
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 place = t + new Vector3(x, tableTop, z) + dir * 0.52f;
            Quaternion facing = Quaternion.Euler(0f, yaw + 180f, 0f);
            ModelProp("MedievalPlate", s, place, facing, false);
            ModelProp("Goblet", s, place + facing * new Vector3(0.2f, 0f, -0.16f), facing, false);
            ModelProp("Cutlery", s, place + facing * new Vector3(-0.2f, 0.004f, 0f), facing * Quaternion.Euler(0f, 90f, 0f), false);
        }

        ModelProp("GoldDish", s, new Vector3(t.x, tableTop, t.z), Quaternion.identity, false);
        ModelProp("Candlestick", s, new Vector3(t.x - 1.0f, tableTop, t.z), Quaternion.identity, false);
        ModelProp("Candlestick", s, new Vector3(t.x + 1.0f, tableTop, t.z), Quaternion.identity, false);
        Clipping(0, s, new Vector3(t.x + 0.52f, tableTop + 0.003f, t.z + 0.25f), 12f);

        // The graduation photo on the old drawer in the living room; Erick remembers the year.
        GameObject drawer = GameObject.Find("vintage_wooden_drawer_01");
        float drawerTop = drawer != null ? RendererBounds(drawer).max.y : 0.8f;
        Vector3 drawerAt = drawer != null ? RendererBounds(drawer).center : M(5.2f, 0f, 8.5f);
        ReadableNote graduation = Readable("GraduationPhoto", s, new Vector3(drawerAt.x - 0.15f, drawerTop + 0.002f, drawerAt.z), 188f, "foto_formatura", "story.photo_grad.title", PlayerItems.EntryKind.Photo,
            "story.photo_grad.body", string.Empty, PhotoTexture("formatura", true, false));
        graduation.SetComment(Voice("erick_photo"), "voice.erick.photo");
        // The mould recording waits on the dinner table from the start (Gustavo, 01/10: hidden until the
        // blackout, it looked lost). Not on a chair: the seat's collider took the E (only "Sentar" showed).
        MakeRecorder("RecorderMold", s, new Vector3(t.x - 0.18f, tableTop + 0.002f, t.z + 0.2f), 200f, "gravacao_mofo", "story.mold.title", "mold", null, null);

        // ---------------- the entrance hall: Alex's torch on a cabinet
        Transform hall = Group("Hall", s);
        GameObject cabinet = Prop("painted_wooden_cabinet", hall, M(3.72f, 0f, 6.9f), -90f);
        float cabinetTop = RendererBounds(cabinet).max.y;
        // Alex's waterproof dive torch, left on the cabinet (Gustavo, 01/10: a torch, not an oil lamp).
        GameObject torchProp = ModelProp("DiveTorch", hall, M(3.68f, cabinetTop, 6.55f), Quaternion.Euler(0f, 30f, 0f), false);
        Pickup("Flashlight", hall, M(3.68f, cabinetTop, 6.55f), new[] { torchProp }, "lanterna", "prompt.take_torch",
            "msg.torch_taken", new Vector3(0.4f, 0.25f, 0.4f));
        Readable("ResearchPhoto", hall, M(3.65f, cabinetTop + 0.002f, 7.1f), -80f, "foto_pesquisa", "story.photo_research.title", PlayerItems.EntryKind.Photo,
            "story.photo_research.body", string.Empty, PhotoTexture("pesquisa", false, true));
        Clipping(1, hall, M(3.68f, cabinetTop + 0.002f, 7.5f), -95f);

        // ---------------- kitchen
        Transform kitchen = Group("Kitchen", s);
        // The units along the north wall, the fridge and the table are FloodedHouseFurniture's.
        Prop("steel_frame_shelves_01", kitchen, M(-11.95f, 0f, 5.2f), -90f);
        GameObject kitchenTable = GameObject.Find("KitchenTable");
        float kitchenTop = kitchenTable != null ? RendererBounds(kitchenTable).max.y : 0.78f;
        Prop("can_rusted", kitchen, M(-16.0f, kitchenTop, 8.0f), 40f, false);
        // Dayse's old gas stove (Sketchfab "Old Gas Stove and Oven", Glowbox 3D, CC BY; was the Poly Haven electric stove).
        ModelProp("GasStove", kitchen, M(-17.3f, 0f, 15.55f), Quaternion.Euler(0f, 180f, 0f));
        ModelProp("Microwave", kitchen, M(-13f, 0.93f, 15.5f), Quaternion.Euler(0f, 180f, 0f), false);
        // Two big batteries left on the counter, by the sink.
        // Two cells lying on the counter (Sketchfab "Cell battery :: Luna has gone prop", Shorty_Digitan, CC BY; the model
        // already lies along its own z; measured upright at Euler(90, ...), so no tilt). Were two plain cylinders.
        Vector3 cells = M(-14.2f, 0.93f, 15.45f);
        GameObject cellA = ModelProp("CellBattery", kitchen, cells + new Vector3(-0.03f, 0f, -0.03f), Quaternion.Euler(0f, 10f, 0f), false);
        GameObject cellB = ModelProp("CellBattery", kitchen, cells + new Vector3(0.03f, 0f, 0.03f), Quaternion.Euler(0f, 35f, 0f), false);
        cellA.name = "Battery0";
        cellB.name = "Battery1";
        Pickup("Batteries", kitchen, cells, new[] { cellA, cellB }, "pilhas", "prompt.take_batteries",
            "msg.batteries_taken", new Vector3(0.3f, 0.15f, 0.3f));
        Clipping(2, kitchen, M(-14.9f, kitchenTop + 0.003f, 8.0f), 20f);
        GameObject key = KeyModel(kitchen, M(-13.4f, 0.005f, 12.3f), 70f);
        Pickup("DehumidifierKey", kitchen, M(-13.4f, 0f, 12.3f), new[] { key }, "chave_desumidificador", "prompt.take_key",
            "msg.key_taken", new Vector3(0.3f, 0.12f, 0.3f));
        Scratching(kitchen, M(-19.4f, 1.2f, 8.5f));

        // ---------------- 4. the laundry and service area: the dehumidifier
        Transform dehum = Group("DehumidifierRoom", s);
        mansion.dehumidifierDoor = MakeDoor("DehumidifierDoor", dehum, M(10.1f, 0f, 20.2f), M(10.04f, 0.02f, 20.22f), M(10.16f, 2.18f, 21.18f),
            doorPaint, 100f, Door.Lock.Key, "chave_desumidificador", null, null, "msg.locked_common", doorCreaks[0], keyTurns[0]);
        Transform machine = Group("Dehumidifier", dehum);
        // The wrecked industrial dehumidifier (Meshy 09/10, 8 k triangles through game-3d-assets, 1.2 m tall, 0.72 x
        // 0.96 m): standing by the north wall, its torn fan (the file's -x face) turned to the door and the hiding spot.
        // Was three rusty primitives (a knocked-over casing, a torn panel, a hanging duct).
        ModelProp("Dehumidifier", machine, M(13.7f, 0f, 23.75f), Quaternion.Euler(0f, -30f, 0f));
        Prop("modular_industrial_pipes_01", dehum, M(14.85f, 1.8f, 21.4f), -90f, false);
        // The boat hook (Meshy 09/10, 2 k): stood on its handle, hook end up, leaning 10 degrees on the north wall with
        // the hook turned into the room. Was a cylinder with a small bent cylinder for the hook.
        // (measured: at yaw -60 the hook points south-south-west; the foot 0.39 m off the wall face at 24.45 leaves the top
        // 10 cm short of it.)
        GameObject pole = BoatHook(dehum, M(12.2f, 0f, 24.06f), -60f, 10f);
        StoryPickup hookPickup = Pickup("Pole", dehum, M(12.2f, 0f, 24.2f), new[] { pole }, "gancho", "prompt.take_pole",
            "msg.pole_taken", new Vector3(0.4f, 1.9f, 0.4f));
        Clipping(3, dehum, M(13.1f, 0.003f, 19.6f), -30f);
        WallBreak wall = BuildBreakableWall(dehum);
        CreatureChase chase = BuildChase(dread, rig.controller.transform);

        // ---------------- 2 and 3. the upper floor: the fungus light in the north gallery (the creatures will not come
        // into it) and the east wing past it, taken by the fungus: thick spores, mask only.
        Transform glow = Group("FungusLight", s);
        BuildHallFungus(glow);
        FungusZone safe = new GameObject("FungusZone_Hall").AddComponent<FungusZone>();
        safe.transform.SetParent(glow, false);
        safe.Configure(SafeZone, 0.25f, true);
        GameObject sideTable = Prop("side_table_01", glow, M(-1.4f, UF, 24.1f), 180f);
        float sideTop = RendererBounds(sideTable).max.y;
        Recorder safeRecorder = MakeRecorder("Recorder2", glow, M(-1.4f, sideTop + 0.002f, 24.1f), 250f, "gravacao_2", "story.rec2.title", "rec2", null, null);
        FilterPickup("Filter2", glow, M(1.9f, UF, 23.9f), "filtro_2");

        Transform wing = Group("SporeWing", s);
        BuildWingFungus(wing);
        FungusZone dense = new GameObject("FungusZone_Spores").AddComponent<FungusZone>();
        dense.transform.SetParent(wing, false);
        dense.Configure(DenseZone, 1f, true);
        SporeHaze(wing);
        GameObject barrierWall = Box("SporeBarrier", wing, M(2.8f, UF, 19.25f), M(3.0f, UC, 21.55f), glass);
        barrierWall.GetComponent<MeshRenderer>().enabled = false;
        SporeBarrier barrier = barrierWall.AddComponent<SporeBarrier>();
        SetField(barrier, "wall", barrierWall.GetComponent<Collider>());
        SetField(barrier, "respirator", rig.respirator);
        SetField(barrier, "hud", hud);
        SetField(barrier, "player", rig.controller.transform);
        GameObject crate = Prop("wooden_crate_01", wing, M(9.3f, UF, 16.2f), 20f);
        float crateTop = RendererBounds(crate).max.y;
        Clipping(4, wing, M(9.4f, crateTop + 0.002f, 16.3f), 70f);
        WallWorms(wing, M(10.29f, UF, 17.6f), Vector3.forward, new Vector2(2f, 2.4f), rig.controller.transform);

        // ---------------- 5. generator room 2, behind its keypad, in the spores
        Transform gen = Group("GeneratorRoom", s);
        Vector2 gd = LunaManor.Gerador2Door;
        mansion.generatorDoor = MakeDoor("GeneratorDoor", gen, M(gd.x, UF, gd.y - 0.5f), M(gd.x - 0.05f, UF + 0.02f, gd.y - 0.48f), M(gd.x + 0.05f, UF + 2.08f, gd.y + 0.48f),
            rustyMetal, 100f, Door.Lock.Code, null, "2014", "story.code_hint", null, metalDoorOpen, metalBoltUnlock);
        KeypadBox(gen, M(gd.x - 0.13f, UF + 1.35f, gd.y + 0.75f));
        // Dayse's list (13/10): the main generator, the one with the problem (rusted), and the emergency one whose engine
        // parts Alex says to replace; fuel by them, the electric box on the wall, an industrial lamp.
        Against(gen, "RustedGenerator", V2(15f, 19.95f), Vector2.left, UF);
        Against(gen, "GeneratorLP", V2(15f, 21.05f), Vector2.left, UF);
        Model(gen, "FuelCanister", V2(13.9f, 21.3f), 20f, UF);
        Model(gen, "LpgTank", V2(13.5f, 19.55f), 0f, UF);
        Hung(gen, "ElectricBox", V2(12.4f, 19.2f), Vector2.up, 1.2f);
        Model(gen, "PipeWrench", V2(13.2f, 20.6f), 70f, UF);
        GameObject office = ModelProp("MetalDesk", gen, M(12.1f, UF, 21.1f), Quaternion.Euler(0f, 180f, 0f));
        float deskTop = RendererBounds(office).max.y;
        Light emergency = PointOrSpot("EmergencyLight", gen, M(12.7f, UF + 2.8f, 20.4f), LightType.Point, new Color(1f, 0.13f, 0.08f), 1.6f, 6f);
        emergency.gameObject.AddComponent<PulsingLight>();
        ReadableNote alexNote = Readable("AlexNote", gen, M(11.75f, deskTop + 0.002f, 20.95f), 175f, "bilhete_alex", "story.alexnote.title", PlayerItems.EntryKind.Note,
            "story.alexnote.body", "A.");
        GameObject basementKey = KeyModel(gen, M(12.25f, deskTop + 0.005f, 20.95f), 10f);
        Pickup("BasementKey", gen, M(12.25f, deskTop, 20.95f), new[] { basementKey }, "chave_porao", "prompt.take_basement_key",
            "msg.basement_key_taken", new Vector3(0.3f, 0.15f, 0.3f));
        FilterPickup("Filter3", gen, M(12.75f, deskTop, 21.0f), "filtro_3");
        Clipping(5, gen, M(11.35f, deskTop + 0.003f, 21.1f), 190f);

        // ---------------- 5. the attic over the west wing
        Transform attic = Group("AtticStory", s);
        Vector3 suitAt = M(-12.1f, AF, 10.4f);
        GameObject suitBody = Primitive(PrimitiveType.Capsule, "Suit", attic, suitAt + Vector3.up * 1.05f, Quaternion.identity, new Vector3(0.45f, 0.62f, 0.3f), cloth);
        Box("SuitHook", attic, suitAt + new Vector3(-0.05f, 1.65f, -0.2f), suitAt + new Vector3(0.05f, 1.9f, 0.2f), darkPlanks, false);
        GameObject gasMask = Prop("old_gas_mask", attic, suitAt + new Vector3(0f, 1.5f, 0.18f), Quaternion.Euler(-80f, 0f, 0f), false);
        Pickup("Suit", attic, suitAt, new[] { suitBody, gasMask }, "traje", "prompt.wear_suit",
            "msg.suit_taken", new Vector3(0.8f, 1.9f, 0.7f));
        GameObject boxes = Prop("wooden_crate_01", attic, M(-10.4f, AF, 10.1f), 10f);
        float boxTop = RendererBounds(boxes).max.y;
        FilterPickup("Filter1", attic, M(-10.45f, boxTop, 10.15f), "filtro_1");
        Clipping(6, attic, M(-10.2f, boxTop + 0.002f, 10.4f), 15f);
        Vector3 mapAt = M(-11.3f, AF, 13.6f);
        Box("MapTable", attic, mapAt + new Vector3(-0.55f, 0f, -0.4f), mapAt + new Vector3(0.55f, 0.62f, 0.4f), darkPlanks);
        GameObject mapSheet = Box("MapSheet", attic, mapAt + new Vector3(-0.25f, 0.62f, -0.18f), mapAt + new Vector3(0.25f, 0.625f, 0.17f), paper, false);
        Pickup("Map", attic, mapAt + Vector3.up * 0.62f, new[] { mapSheet }, "mapa", "prompt.take_map",
            "msg.map_taken", new Vector3(0.6f, 0.2f, 0.5f));
        Readable("CasualPhoto", attic, mapAt + new Vector3(-0.05f, 0.623f, 0.3f), -10f, "foto_praia", "story.photo_beach.title", PlayerItems.EntryKind.Photo,
            "story.photo_beach.body", string.Empty, PhotoTexture("praia", false, false));

        // Alex's room: the last printed edition he kept (GDD, approved 02/10: the year-15 front page), folded on his dresser.
        // The page is Art/Textures/Story/jornal_ano15.png (18/10, composed from a ComfyUI photograph; text by Claude, proposal).
        GameObject dresser = GameObject.Find("Dresser");
        Texture2D frontPage = AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Textures + "/Story/jornal_ano15.png");
        if (dresser != null && frontPage != null)
        {
            Bounds dresserBounds = RendererBounds(dresser);
            Readable("Newspaper15", s, new Vector3(dresserBounds.center.x - 0.5f, dresserBounds.max.y + 0.002f, dresserBounds.center.z - 0.05f), 95f,
                "jornal_ano15", "story.newspaper.title", PlayerItems.EntryKind.Clipping, "story.newspaper.body", "story.newspaper.sig", frontPage);
            // 19/10: a photo Alex took of himself on the island pier (ComfyUI with his LoRA), next to the newspaper. Proposal.
            Readable("IslandPhoto", s, new Vector3(dresserBounds.center.x + 0.25f, dresserBounds.max.y + 0.002f, dresserBounds.center.z + 0.02f), 70f,
                "foto_ilha", "story.photo_island.title", PlayerItems.EntryKind.Photo, "story.photo_island.body", string.Empty, PhotoTexture("ilha", false, false));
        }
        Prop("old_bed_frame", attic, M(-11.4f, AF, 17f), 0f);
        Prop("wooden_barrels_01", attic, M(-10.95f, AF, 12.4f), 0f);
        AtticHatch[] hatches = BuildHatch(s, creak);

        // ---------------- 6. basement
        Transform basement = Group("BasementStory", s);
        Recorder basementRecorder = MakeRecorder("Recorder3", basement, new Vector3(-4.85f, LandingY + 0.97f, -4.3f), 100f, "gravacao_3", "story.rec3.title", "rec3", null, null);
        Transform doorHinge = root.Find("GroundFloor/BasementDoor");
        mansion.basementDoor = doorHinge.gameObject.AddComponent<Door>();
        mansion.basementDoor.Configure(Door.Lock.Key, "chave_porao", null, null, "msg.locked_basement");
        SetField(mansion.basementDoor, "openAngle", 105f);
        DoorAudio(mansion.basementDoor, doorHinge.gameObject, doorCreaks[2], keyTurns[1]);

        // The box, set on the bench once Erick is back on the landing.
        Transform boxGroup = Group("ErickBox", basement);
        Vector3 boxAt = new Vector3(-4.1f, LandingY + 0.97f, -4.45f);
        boxGroup.position = boxAt;
        Box("Case", boxGroup, boxAt + new Vector3(-0.13f, 0f, -0.09f), boxAt + new Vector3(0.13f, 0.12f, 0.09f), metal, false);
        TextMeshPro label = new GameObject("Label").AddComponent<TextMeshPro>();
        label.transform.SetParent(boxGroup, false);
        // Readable from the landing side (north of the bench).
        label.transform.SetPositionAndRotation(boxAt + new Vector3(0f, 0.122f, 0f), Quaternion.Euler(90f, 180f, 0f));
        label.text = "ERICK";
        label.fontSize = 0.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.1f, 0.08f, 0.06f);
        label.rectTransform.sizeDelta = new Vector2(0.24f, 0.08f);
        BoxCollider boxReach = boxGroup.gameObject.AddComponent<BoxCollider>();
        boxReach.isTrigger = true;
        boxReach.center = Vector3.up * 0.06f;
        boxReach.size = new Vector3(0.4f, 0.2f, 0.3f);
        InspectableObject box = boxGroup.gameObject.AddComponent<InspectableObject>();
        box.SetPrompt("prompt.open_box");
        box.SetMessage(string.Empty);
        Transform glowGroup = Group("LuminaGlow", boxGroup);
        GameObject fragment = Primitive(PrimitiveType.Sphere, "Fragment", glowGroup, boxAt + Vector3.up * 0.09f, Quaternion.Euler(20f, 40f, 10f), new Vector3(0.09f, 0.06f, 0.07f), rockGlow);
        fragment.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Light luminaLight = PointOrSpot("Light", glowGroup, boxAt + Vector3.up * 0.25f, LightType.Point, new Color(0.3f, 0.9f, 1f), 3f, 6f);
        luminaLight.gameObject.AddComponent<PulsingLight>();
        Volumetric(luminaLight, 0.8f, 0.2f);
        Transform basementFungus = Group("BasementFungus", basement);
        Vector3[] spots =
        {
            new Vector3(-5.97f, -1.4f, -3.4f), new Vector3(-5.97f, -0.9f, -4.4f), new Vector3(-3.5f, -1.2f, -4.97f), new Vector3(-4.9f, -0.5f, -4.97f),
            new Vector3(-5.97f, -1.9f, -2.6f), new Vector3(-2.2f, -0.7f, -4.97f), new Vector3(-5.97f, -0.4f, -1.4f), new Vector3(-4.2f, -1.7f, -4.97f),
        };
        for (int i = 0; i < spots.Length; i++)
        {
            Vector3 outward = spots[i].x < -5.9f ? Vector3.right : Vector3.forward;
            FungusCluster("Growth" + i, basementFungus, spots[i], outward, i % 3, 1.3f);
        }

        CreaturePass rising = BuildCreature("BasementRising", dread, new Vector3(1.4f, -4.3f, -0.4f), new Vector3(-2.6f, -2.3f, -2.6f), 2.4f, 0.8f, "creature_roar_2.wav");

        // ---------------- 6. the flooded pantry (the old sunken room) and its green locker
        Transform pantry = Group("PantryStory", s);
        StoryPickup locker = BuildGreenLocker(pantry);

        // ---------------- the map, the HUD on top, the wiring
        GameObject mansionHudObject = new GameObject("MansionHud");
        mansionHudObject.transform.SetParent(s, false);
        MansionHud mansionHud = mansionHudObject.AddComponent<MansionHud>();
        SetField(mansionHud, "font", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset"));
        SetField(mansionHud, "player", rig.controller);
        SetField(mansionHud, "hud", hud);
        SetField(mansionHud, "respirator", rig.respirator);
        SetField(mansionHud, "items", rig.items);
        AudioSource uiAudio = mansionHudObject.AddComponent<AudioSource>();
        uiAudio.spatialBlend = 0f;
        uiAudio.playOnAwake = false;
        uiAudio.ignoreListenerPause = true;
        SetField(mansionHud, "uiAudio", uiAudio);
        SetMapRooms(mansionHud);

        PowerGrid grid = s.gameObject.AddComponent<PowerGrid>();
        SetField(grid, "lights", gridLights.ToArray());
        SetField(grid, "glowing", gridGlows.ToArray());
        SetField(grid, "offMaterial", lanternGlassOff);
        SetField(grid, "litFixtures", gridFixtures.ToArray());
        SetField(grid, "litFixturesOff", gridFixturesOff.ToArray());
        SetField(grid, "litFixturesOn", gridFixturesOn.ToArray());
        AudioSource gridAudio = s.gameObject.AddComponent<AudioSource>();
        gridAudio.spatialBlend = 0f;
        gridAudio.playOnAwake = false;
        SetField(grid, "source", gridAudio);

        // The tentacle the player sees behind the window during the first recording, and Erick does not.
        // Outside the dining room's bay window, along the front of the house.
        CreaturePass tentacle = BuildCreature("WindowTentacle", dread, M(-4.6f, 1.4f, -0.7f), M(-11.2f, 2.1f, -0.5f), 1.3f, 0.5f, "roar_02.ogg", true);

        // Shapes outside the laundry's window and the kitchen's after the blackout.
        CreaturePass[] stalkers =
        {
            BuildCreature("WingWindowShape", dread, M(16.1f, 1.5f, 19.8f), M(16.1f, 1.9f, 24.8f), 1.8f, 0.6f, "troll-idle-noises_0.ogg", true),
            BuildCreature("KitchenWindowShape", dread, M(-20.6f, 1.4f, 6f), M(-20.6f, 2.0f, 13f), 1.6f, 0.55f, "roar_03.ogg", true),
        };
        SetField(director, "windowStalkers", stalkers);
        SetField(director, "eastWingZone", MZone(Rect.MinMaxRect(5.1f, 13.4f, 15f, 24.6f), -0.5f, UC));
        // A shape crossing the south gallery right behind the hall's frosted door: a shadow on the glass.
        CreaturePass doorShape = BuildCreature("HallDoorShape", dread, M(-3.6f, 1.2f, 10.25f), M(3.9f, 1.7f, 10.25f), 2.6f, 0.6f, "roar_03.ogg", true);
        // It brushes the door as it goes: the glass rattles instead of a roar.
        AudioClip rattle = DoorSound("glass_door_rattle");
        if (rattle != null)
        {
            SetField(doorShape, "groan", rattle);
        }

        SetField(director, "doorStalkers", new[] { doorShape });
        SetField(director, "hallZone", MZone(LunaManor.Hall, -0.5f, GC));

        Transform chaseRetry = Marker("ChaseRetry", s, M(13.2f, 0f, 21f), 270f);
        Transform safeRetry = Marker("SafeRetry", s, M(-1.2f, UF, 20.4f), 90f);
        AudioSource voiceAudio = director.gameObject.AddComponent<AudioSource>();
        voiceAudio.spatialBlend = 0f;
        voiceAudio.playOnAwake = false;

        SetField(director, "items", rig.items);
        SetField(director, "respirator", rig.respirator);
        SetField(director, "panel", panel);
        SetField(director, "power", grid);
        SetField(director, "voiceAudio", voiceAudio);
        SetField(director, "firstRecorder", first);
        SetField(director, "safeRecorder", safeRecorder);
        SetField(director, "basementRecorder", basementRecorder);
        SetField(director, "windowTentacle", tentacle);
        SetField(director, "wingZone", SafeZone);
        SetField(director, "dehumidifierZone", MZone(LunaManor.Lavanderia, -0.5f, GC));
        SetField(director, "hookPole", hookPickup);
        SetField(director, "dehumidifierWall", wall);
        SetField(director, "chase", chase);
        SetField(director, "chaseRetry", chaseRetry);
        SetField(director, "safeRetry", safeRetry);
        SetField(director, "generatorNote", alexNote);
        SetField(director, "pantryBlocker", bedroomBlocker);
        SetField(director, "box", box);
        SetField(director, "luminaGlow", glowGroup.gameObject);
        SetField(director, "basementFungus", basementFungus);
        SetField(director, "basementRising", rising);
        SetField(director, "stairsTopZone", Zone(new Vector3(-6f, -0.3f, 0.6f), new Vector3(-4.8f, 1.8f, 2.2f)));
        SetField(director, "sysAlert", Voice("sys_alert"));
        SetField(director, "sysPower", Voice("sys_power"));
        SetField(director, "sysHumidity", Voice("sys_humidity"));
        SetField(director, "sysThree", Voice("sys_three"));
        SetField(director, "sysTwo", Voice("sys_two"));
        SetField(director, "erickDehumidifier", Voice("erick_dehum"));
        SetField(director, "erickDark", Voice("erick_dark"));
        SetField(director, "erickBox", Voice("erick_box"));
        SetField(director, "erickLumina", Voice("erick_lumina"));
        SetField(bedroomBlocker, "lockedMessage", "msg.blocker_locked");
        _ = hatches;
        _ = locker;
    }

    // ---------------------------------------------------------------- pieces



    // The wall panel is Meshy's security intercom (09/10, 2.5 k triangles): 0.42 x 0.29 m in the file, screen on the
    // left, keypad, camera lens and speaker, its front the file's +z. Shown at 1.4x so the emergency text reads from
    // the hall. The screen area was measured on an orthographic render of the front: x -0.049..0.126, y 0.115..0.239 m
    // of the file, the glass at z 0.018; a dark quad covers it with the TMP text just in front, and a thin red border
    // round the screen is what blinks (the old frame was a 0.95 x 0.66 m box that blinked whole).
    private const float PanelScale = 1.4f;
    private static readonly Rect PanelScreenRect = new Rect(-0.049f, 0.115f, 0.175f, 0.124f);
    private const float PanelFaceZ = 0.018f;
    private const float PanelHeight = 0.286f;
    private const float PanelDepth = 0.035f;

    private static SecurityPanel BuildSecurityPanel(Transform parent, DiveHud hud, Vector3 at, float yaw)
    {
        Transform pivot = Group("SecurityPanel", parent);
        pivot.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        // `at` is the panel's centre 2 cm in front of the wall face (the old frame's back sat on the wall): the model's
        // origin is its bottom centre, mid-depth, so it hangs with its back 4 mm proud of the wall.
        Vector3 origin = at - pivot.forward * (0.02f - 0.004f - PanelDepth * 0.5f * PanelScale) - Vector3.up * (PanelHeight * 0.5f * PanelScale);
        GameObject body = ModelProp("SecurityPanel", pivot, origin, pivot.rotation, false, false);
        body.name = "Body";
        body.transform.localScale *= PanelScale;
        Vector3 screenCentre = origin + pivot.right * (PanelScreenRect.center.x * PanelScale) + Vector3.up * (PanelScreenRect.center.y * PanelScale) + pivot.forward * (PanelFaceZ * PanelScale);
        Vector2 screenSize = PanelScreenRect.size * PanelScale;
        GameObject border = Primitive(PrimitiveType.Cube, "ScreenBorder", pivot, screenCentre + pivot.forward * 0.002f, pivot.rotation, new Vector3(screenSize.x + 0.016f, screenSize.y + 0.016f, 0.002f), screenFrame);
        // Matte glass: at 0.8 the glow light's reflection sat on the text as a bright blob (capture of 18/10).
        Material screenMat = Plain("PanelScreen", new Color(0.02f, 0.01f, 0.01f), 0.15f, 0f);
        GameObject screenQuad = Primitive(PrimitiveType.Cube, "Screen", pivot, screenCentre + pivot.forward * 0.004f, pivot.rotation, new Vector3(screenSize.x, screenSize.y, 0.002f), screenMat);
        TextMeshPro text = new GameObject("Text").AddComponent<TextMeshPro>();
        text.transform.SetParent(pivot, false);
        text.transform.SetPositionAndRotation(screenCentre + pivot.forward * 0.007f, Quaternion.LookRotation(-pivot.forward, Vector3.up));
        text.rectTransform.sizeDelta = screenSize - new Vector2(0.012f, 0.008f);
        // The screen is small (0.25 x 0.17 m): the text sizes itself to fill it, whatever the emergency list says.
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.06f;
        text.fontSizeMax = 0.2f;
        text.fontSize = 0.14f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.32f, 0.22f);
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        text.textWrappingMode = TextWrappingModes.Normal;
        // The red glow above the panel, off the screen's reflection.
        Light glow = PointOrSpot("Glow", pivot, at + pivot.forward * 0.5f + Vector3.up * 0.35f, LightType.Point, new Color(1f, 0.15f, 0.08f), 0.6f, 3.5f);
        AudioSource speaker = pivot.gameObject.AddComponent<AudioSource>();
        speaker.spatialBlend = 0.25f;
        speaker.playOnAwake = false;
        SecurityPanel panel = pivot.gameObject.AddComponent<SecurityPanel>();
        SetField(panel, "screen", text);
        SetField(panel, "frame", border.GetComponent<Renderer>());
        SetField(panel, "glow", glow);
        SetField(panel, "speaker", speaker);
        SetField(panel, "hud", hud);
        _ = screenQuad;
        return panel;
    }

    // Meshy's boat hook (2 m, lying along its own x in the file: the tip and the hook at -x, the hook bent towards -y,
    // the handle at +x) stood on its handle with the hook up, turned `hookYaw` and leaning `lean` degrees backwards
    // (towards +z before the yaw: put the wall there). `foot` is where its lower end rests on the floor.
    private static GameObject BoatHook(Transform parent, Vector3 foot, float hookYaw, float lean)
    {
        Quaternion stand = Quaternion.Euler(lean, 0f, 0f) * Quaternion.Euler(0f, hookYaw, 0f) * Quaternion.Euler(0f, 0f, -90f);
        GameObject pole = ModelProp("BoatHook", parent, foot, stand, false);
        // The file's pivot is 0.3 m off the pole's axis: re-centre on the foot by the lowest part of the mesh.
        Bounds b = RendererBounds(pole);
        Vector3 lowest = b.center;
        float lowestY = float.MaxValue;
        foreach (MeshFilter filter in pole.GetComponentsInChildren<MeshFilter>())
        {
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 w = filter.transform.TransformPoint(v);
                if (w.y < lowestY)
                {
                    lowestY = w.y;
                    lowest = w;
                }
            }
        }

        pole.transform.position += new Vector3(foot.x - lowest.x, 0f, foot.z - lowest.z);
        return pole;
    }

    private static Door MakeDoor(string name, Transform parent, Vector3 hinge, Vector3 leafMin, Vector3 leafMax, Material material, float openAngle,
        Door.Lock lockType, string keyItem, string code, string hint, string lockedMessage, AudioClip creak, AudioClip unlock)
    {
        Transform pivot = Group(name, parent);
        pivot.position = hinge;
        // The leaf: the wooden door model (or a slab) hung on the pivot, +X along the closed leaf from the hinge.
        Vector3 lo = Vector3.Min(leafMin, leafMax), hi = Vector3.Max(leafMin, leafMax);
        Vector3 size = hi - lo;
        Vector3 centre = (lo + hi) * 0.5f;
        bool alongX = size.x >= size.z;
        Vector3 along = alongX ? Vector3.right * Mathf.Sign(centre.x - hinge.x) : Vector3.forward * Mathf.Sign(centre.z - hinge.z);
        pivot.rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
        float width = alongX ? size.x : size.z, thickness = alongX ? size.z : size.x;
        // Only the painted wooden doors take the model; metal, titanium and the bookcase stay slabs of their own material.
        GameObject leafGo = DoorLeaf("Leaf", pivot, width, size.y, thickness, material, material == doorPaint);
        leafGo.transform.localPosition += Vector3.up * (lo.y - hinge.y);
        Door door = pivot.gameObject.AddComponent<Door>();
        door.Configure(lockType, keyItem, code, hint, lockedMessage ?? "msg.locked");
        SetField(door, "openAngle", openAngle);
        DoorAudio(door, pivot.gameObject, creak, unlock);
        return door;
    }

    private static AudioClip DoorSound(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Doors/" + name + ".wav");
    }

    private static void DoorAudio(Door door, GameObject at, AudioClip creak, AudioClip unlock)
    {
        AudioSource source = at.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.maxDistance = 15f;
        source.playOnAwake = false;
        SetField(door, "source", source);
        SetField(door, "openClip", creak);
        SetField(door, "unlockClip", unlock);
        SetField(door, "lockedClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Drop_Metal.ogg"));
    }

    private static void KeypadBox(Transform parent, Vector3 at)
    {
        Box("Keypad", parent, at + new Vector3(-0.02f, -0.12f, -0.08f), at + new Vector3(0.02f, 0.12f, 0.08f), screenFrame, false);
    }

    private static GameObject KeyModel(Transform parent, Vector3 at, float yaw)
    {
        Transform key = Group("Key", parent);
        key.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        Material brass = Plain("Brass", new Color(0.7f, 0.55f, 0.25f), 0.7f, 1f);
        Primitive(PrimitiveType.Cylinder, "Bow", key, at + key.right * -0.04f, Quaternion.Euler(0f, yaw, 0f), new Vector3(0.035f, 0.003f, 0.035f), brass);
        Primitive(PrimitiveType.Cube, "Blade", key, at + key.right * 0.01f, Quaternion.Euler(0f, yaw, 0f), new Vector3(0.07f, 0.005f, 0.01f), brass);
        return key.gameObject;
    }

    private static void FilterPickup(string name, Transform parent, Vector3 at, string item)
    {
        Material filterMat = Plain("FilterCartridge", new Color(0.45f, 0.43f, 0.35f), 0.35f, 0.2f);
        GameObject cartridge = Primitive(PrimitiveType.Cylinder, "Cartridge", parent, at + Vector3.up * 0.04f, Quaternion.identity, new Vector3(0.09f, 0.04f, 0.09f), filterMat);
        Pickup(name, parent, at, new[] { cartridge }, item, "prompt.take_filter", "msg.filter_taken", new Vector3(0.3f, 0.15f, 0.3f));
    }

    // The folding ladder in the west gallery's ceiling (LunaManor.HatchHinge: the hinge on the hole's north edge,
    // the ladder lands south of it) up to the attic over the west wing.
    private static AtticHatch[] BuildHatch(Transform parent, AudioClip creak)
    {
        Vector2 h = LunaManor.HatchHinge;
        float s = LunaManor.HatchSize;
        float ceilingY = UC - 0.02f;
        float rise = ceilingY - UF;
        Transform bottom = Group("AtticHatch", parent);
        bottom.position = M(h.x, ceilingY, h.y);
        Transform panel = Group("Panel", bottom);
        panel.position = M(h.x, ceilingY, h.y);
        Box("Board", panel, M(h.x - s * 0.5f, ceilingY - 0.02f, h.y - s - 0.02f), M(h.x + s * 0.5f, ceilingY + 0.02f, h.y - 0.02f), darkPlanks, false);
        Box("Ring", panel, M(h.x - 0.04f, ceilingY - 0.08f, h.y - 0.55f), M(h.x + 0.04f, ceilingY - 0.02f, h.y - 0.47f), metal, false);
        // The folding ladder, shown once pulled down: Meshy's attic ladder (09/10, 5 k triangles), 2.8 m tall in the
        // file with its hanging door at the hinge end (the file's -z) and the foot 0.5 m the other way (+z), scaled to
        // the rise so the foot meets the gallery floor. Turned so the door hangs north at the hinge; its frame (0.77 m)
        // centred in the 0.96 m hole. It brings its own door, so the closed board hides while it is down.
        Transform ladder = Group("Ladder", bottom);
        float ladderScale = rise / 2.8f;
        GameObject ladderModel = ModelProp("AtticLadder", ladder, M(h.x, UF, h.y - 0.5f - 0.19f * ladderScale), Quaternion.Euler(0f, 180f, 0f), false, false);
        ladderModel.transform.localScale *= ladderScale;
        ladder.gameObject.SetActive(false);
        BoxCollider reach = bottom.gameObject.AddComponent<BoxCollider>();
        reach.isTrigger = true;
        reach.center = new Vector3(0f, -0.4f, -0.5f);
        reach.size = new Vector3(1f, 1.4f, 1.6f);
        Transform downstairs = Marker("AtticBottom", parent, M(h.x, UF, h.y + 0.9f), 0f);
        // Up in the attic, toward the ridge (the roof is low over the hatch itself).
        Transform upstairs = Marker("AtticTop", parent, M(-10.95f, AF, h.y - 1.6f), 180f);

        Transform top = Group("AtticHatchTop", parent);
        top.position = M(h.x, AF, h.y - 0.5f);
        BoxCollider topReach = top.gameObject.AddComponent<BoxCollider>();
        topReach.isTrigger = true;
        topReach.center = Vector3.up * 0.2f;
        topReach.size = new Vector3(1f, 0.5f, 1f);
        // A rail round the hole so nobody walks into it.
        Box("Rail", top, M(h.x - s * 0.5f - 0.05f, AF, h.y - s - 0.07f), M(h.x + s * 0.5f + 0.05f, AF + 0.9f, h.y - s - 0.02f), darkPlanks);
        Box("RailE", top, M(h.x + s * 0.5f, AF, h.y - s - 0.07f), M(h.x + s * 0.5f + 0.05f, AF + 0.9f, h.y + 0.03f), darkPlanks);
        Box("RailW", top, M(h.x - s * 0.5f - 0.05f, AF, h.y - s - 0.07f), M(h.x - s * 0.5f, AF + 0.9f, h.y + 0.03f), darkPlanks);

        AtticHatch down = bottom.gameObject.AddComponent<AtticHatch>();
        AtticHatch up = top.gameObject.AddComponent<AtticHatch>();
        foreach (AtticHatch hatch in new[] { down, up })
        {
            AudioSource source = hatch.gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.6f;
            source.playOnAwake = false;
            SetField(hatch, "source", source);
            SetField(hatch, "creakClip", creak);
        }

        SetField(down, "panel", panel);
        SetField(down, "ladder", ladder.gameObject);
        SetField(down, "ladderHasDoor", true);
        SetField(down, "destination", upstairs);
        SetField(down, "otherSide", up);
        SetField(up, "destination", downstairs);
        SetField(up, "otherSide", down);
        SetField(up, "needsTool", false);
        SetField(up, "climbPrompt", "prompt.attic_down");
        return new[] { down, up };
    }

    // The laundry's outer wall (east), where the Drowned comes through: a solid stretch until it breaks.
    private static WallBreak BuildBreakableWall(Transform parent)
    {
        Transform group = Group("BreakableWall", parent);
        float z0 = LunaManor.BreakZ0, z1 = LunaManor.BreakZ1, zc = (z0 + z1) * 0.5f;
        GameObject intact = Box("Intact", group, M(14.85f, 0f, z0), M(15.15f, LunaManor.BreakTop, z1), concrete);
        List<Object> chunks = new List<Object>();
        for (int i = 0; i < 8; i++)
        {
            Vector3 at = M(15f, 0.3f + (i / 2) * 0.55f, z0 + 0.3f + (i % 2) * 0.9f + Random.Range(-0.1f, 0.1f));
            GameObject chunk = Primitive(PrimitiveType.Cube, "Chunk" + i, group, at, Random.rotation, new Vector3(Random.Range(0.3f, 0.55f), Random.Range(0.25f, 0.45f), Random.Range(0.2f, 0.35f)), concrete, true);
            Rigidbody body = chunk.AddComponent<Rigidbody>();
            body.mass = 30f;
            chunks.Add(body);
        }

        GameObject dustObject = new GameObject("Dust");
        dustObject.transform.SetParent(group, false);
        dustObject.transform.position = M(14.75f, 1.1f, zc);
        ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = dust.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startColor = new Color(0.45f, 0.44f, 0.4f, 0.25f);
        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });
        ParticleSystem.ShapeModule shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.4f, 2f, 1.8f);
        dustObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = particle;
        AudioSource source = group.gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0.8f;
        source.maxDistance = 30f;
        source.playOnAwake = false;
        WallBreak wall = group.gameObject.AddComponent<WallBreak>();
        SetField(wall, "intact", intact);
        SetField(wall, "chunks", chunks.ToArray());
        SetField(wall, "inward", Vector3.left);
        SetField(wall, "dust", dust);
        SetField(wall, "source", source);
        return wall;
    }

    private static CreatureChase BuildChase(DreadDirector dread, Transform player)
    {
        // The Drowned comes through the wall (Gustavo 03/10: not the eel). Scripted: its own brain is removed and
        // CreatureChase carries it; its hitboxes and ragdoll stay, so it can be shot down on the way.
        Transform group = Group("ChaseCreature");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrownedEnemyBuilder.PrefabPath);
        if (prefab == null)
        {
            prefab = DrownedEnemyBuilder.Build();
        }

        GameObject drowned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        PrefabUtility.UnpackPrefabInstance(drowned, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        drowned.name = "Drowned";
        drowned.transform.position = M(15.4f, LunaManor.GardenY, (LunaManor.BreakZ0 + LunaManor.BreakZ1) * 0.5f);
        Object.DestroyImmediate(drowned.GetComponent<DrownedBrain>());
        AudioSource voice = group.gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 3f;
        voice.maxDistance = 30f;
        CreatureChase chase = group.gameObject.AddComponent<CreatureChase>();
        SetField(chase, "body", drowned.GetComponent<Animator>());
        SetField(chase, "player", player);
        SetField(chase, "hole", M(15.15f, 0.45f, (LunaManor.BreakZ0 + LunaManor.BreakZ1) * 0.5f));
        SetField(chase, "voice", voice);
        SetField(chase, "roar", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Creature/creature_roar_1.wav"));
        SetField(chase, "screech", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Creature/creature_screech.wav"));
        SetField(chase, "dread", dread);
        return chase;
    }

    // The fungus light in the upper north gallery and the passage to the balcony: the creatures do not come into it
    // (light spores only, no mask needed). Past it, the east wing is all spores.
    private static Bounds SafeZone => MZone(Rect.MinMaxRect(-7.5f, 19.2f, 2.8f, 24.6f), UF - 0.5f, UC);
    private static Bounds DenseZone => MZone(Rect.MinMaxRect(2.8f, 14.4f, 15f, 24.6f), UF - 0.5f, UC);

    // The glow spilling out of the spores into the north gallery: a few clumps round the barrier and the passage.
    private static void BuildHallFungus(Transform parent)
    {
        (Vector3 at, Vector3 outward, int kind, float scale)[] clumps =
        {
            (M(2.3f, UF + 0.01f, 20.6f), Vector3.up, 2, 1f),
            (M(2.5f, UF + 1.6f, 21.49f), Vector3.back, 1, 0.9f),
            (M(0.6f, UF + 2.0f, 21.49f), Vector3.back, 0, 1.1f),
            (M(1.2f, UC - 0.02f, 20.4f), Vector3.down, 1, 0.8f),
            (M(-0.6f, UF + 0.01f, 23.4f), Vector3.up, 0, 1.1f),
            (M(2.69f, UF + 0.6f, 23f), Vector3.left, 2, 1f),
        };
        Transform fungus = Group("HallFungus", parent);
        for (int i = 0; i < clumps.Length; i++)
        {
            FungusCluster("Clump" + i, fungus, clumps[i].at, clumps[i].outward, clumps[i].kind, clumps[i].scale);
        }
    }

    // The east wing is taken: dense growth on the walls, the floors and the ceilings of its gallery and rooms.
    private static void BuildWingFungus(Transform wing)
    {
        (Vector3 at, Vector3 outward, int kind, float scale)[] clumps =
        {
            (M(4.2f, UF + 0.6f, 21.49f), Vector3.back, 0, 1.3f),
            (M(6.6f, UF + 1.7f, 21.49f), Vector3.back, 1, 1.1f),
            (M(5.5f, UF + 0.01f, 20.2f), Vector3.up, 2, 1.5f),
            (M(7.5f, UC - 0.02f, 20.4f), Vector3.down, 0, 1.1f),
            (M(10.29f, UF + 1f, 20.9f), Vector3.left, 1, 1.4f),
            (M(9.1f, UF + 0.01f, 19f), Vector3.up, 0, 1.3f),
            (M(10.29f, UF + 0.5f, 15.4f), Vector3.left, 2, 1.1f),
            (M(9.2f, UF + 0.01f, 14.9f), Vector3.up, 1, 1.6f),
            (M(9.2f, UC - 0.02f, 16.4f), Vector3.down, 2, 1.1f),
            (M(5.3f, UF + 0.01f, 23.3f), Vector3.up, 1, 1.4f),
            (M(7.79f, UF + 1.3f, 23f), Vector3.left, 0, 1.2f),
            (M(11.5f, UF + 0.01f, 23.6f), Vector3.up, 2, 1.4f),
            (M(14.84f, UF + 1.5f, 22.8f), Vector3.left, 1, 1.2f),
            (M(12.6f, UF + 1.6f, 24.44f), Vector3.back, 0, 1.4f),
            (M(14.84f, UF + 0.6f, 17f), Vector3.left, 2, 1.1f),
            (M(12.4f, UF + 0.01f, 16.6f), Vector3.up, 1, 1.2f),
        };
        Transform fungus = Group("Fungus", wing);
        for (int i = 0; i < clumps.Length; i++)
        {
            FungusCluster("Clump" + i, fungus, clumps[i].at, clumps[i].outward, clumps[i].kind, clumps[i].scale);
        }

        // Mould films spreading over the walls of the gallery (the east wall's west face, the north wall's south face).
        Transform films = Group("MouldFilms", wing);
        for (int i = 0; i < 10; i++)
        {
            bool east = i % 2 == 0;
            Vector3 at = east ? M(10.295f, UF + Random.Range(0.4f, 2.6f), Random.Range(14.8f, 21.2f)) : M(Random.Range(3.2f, 7.6f), UF + Random.Range(0.4f, 2.6f), 21.495f);
            GameObject film = GameObject.CreatePrimitive(PrimitiveType.Quad);
            film.name = "Film" + i;
            Object.DestroyImmediate(film.GetComponent<Collider>());
            film.transform.SetParent(films, false);
            film.transform.position = at;
            // A quad shows its face to -Z: turned so that face looks out of the wall.
            film.transform.rotation = Quaternion.LookRotation(east ? Vector3.right : Vector3.forward) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            film.transform.localScale = Vector3.one * Random.Range(0.5f, 1.1f);
            film.GetComponent<MeshRenderer>().sharedMaterial = moldPatch;
            film.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // The thick spores filling the east wing (29/10: FloodedHouseSpores): a dense cloud of lit specks the torch
    // shows, and a pale haze hanging in it.
    private static void SporeHaze(Transform parent)
    {
        Bounds dense = DenseZone;
        Vector3 centre = new Vector3(dense.center.x, UF + 1.4f, dense.center.z);
        Vector3 size = new Vector3(dense.size.x, 2.8f, dense.size.z);
        if (sporeMotes == null)
        {
            SporeMaterials();
        }

        // Thick enough to read as a danger at a glance (29/10: at 4500 specks and a 3% haze the wing looked clean).
        SporeMotes(parent, "SporeHaze", centre, size, 12000, new Color(0.85f, 0.92f, 0.72f, 1f), 1.6f);
        BioHaze(parent, "SporeHazeGlow", centre, size, 40, new Color(0.55f, 0.6f, 0.42f, 0.16f), true);
    }

    private static void WallWorms(Transform parent, Vector3 origin, Vector3 right, Vector2 size, Transform player)
    {
        GameObject go = new GameObject("WallWorms");
        go.transform.SetParent(parent, false);
        go.transform.position = origin;
        List<Object> worms = new List<Object>();
        for (int i = 0; i < 4; i++)
        {
            GameObject worm = new GameObject("Worm" + i);
            worm.transform.SetParent(go.transform, false);
            LineRenderer line = worm.AddComponent<LineRenderer>();
            line.sharedMaterial = wormMat;
            line.widthMultiplier = 0.012f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.useWorldSpace = true;
            worms.Add(line);
        }

        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.maxDistance = 10f;
        source.playOnAwake = false;
        WallWorms crawl = go.AddComponent<WallWorms>();
        SetField(crawl, "worms", worms.ToArray());
        SetField(crawl, "right", right);
        SetField(crawl, "size", size);
        SetField(crawl, "player", player);
        SetField(crawl, "source", source);
    }

    // Small things clawing behind a wall now and then.
    private static void Scratching(Transform parent, Vector3 at)
    {
        GameObject go = new GameObject("Scratching");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        AudioSource source = go.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.maxDistance = 12f;
        source.playOnAwake = false;
        RandomOneShots shots = go.AddComponent<RandomOneShots>();
        // Recorded clawing, dulled as if through the wall; the synthesised scratch only if none was converted.
        Object[] claws = AssetDatabase.FindAssets("claws_wall_ t:AudioClip", new[] { ProjectPaths.Audio + "/Creature" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray<Object>();
        SetField(shots, "clips", claws.Length > 0 ? claws : new Object[] { ProceduralScratchAsset() });
        SetField(shots, "intervalSeconds", new Vector2(9f, 22f));
        SetField(shots, "volume", new Vector2(0.4f, 0.8f));
    }

    // The scratch is synthesised; saved once as an asset so a scene object can reference it.
    private static AudioClip ProceduralScratchAsset()
    {
        string path = ProjectPaths.Audio + "/Creature/scratch_procedural.wav";
        if (!System.IO.File.Exists(path))
        {
            AudioClip clip = ProceduralAudio.Scratch();
            float[] data = new float[clip.samples];
            clip.GetData(data, 0);
            using (System.IO.FileStream file = System.IO.File.Create(path))
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(file))
            {
                int bytes = data.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(bytes);
                foreach (float v in data)
                {
                    writer.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767f));
                }
            }

            AssetDatabase.ImportAsset(path);
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // The green metal locker in a corner of the flooded pantry: the case with the parts, and a small,
    // heavy box with "Erick" on it.
    private static StoryPickup BuildGreenLocker(Transform parent)
    {
        Transform locker = Group("GreenLocker", parent);
        Vector3 at = new Vector3(7.8f, -6f, 0.55f);
        locker.position = at;
        Box("Back", locker, at + new Vector3(-0.45f, 0f, 0.15f), at + new Vector3(0.45f, 1.9f, 0.25f), greenPaint);
        Box("SideL", locker, at + new Vector3(-0.47f, 0f, -0.3f), at + new Vector3(-0.43f, 1.9f, 0.25f), greenPaint);
        Box("SideR", locker, at + new Vector3(0.43f, 0f, -0.3f), at + new Vector3(0.47f, 1.9f, 0.25f), greenPaint);
        Box("Top", locker, at + new Vector3(-0.47f, 1.9f, -0.3f), at + new Vector3(0.47f, 1.95f, 0.25f), greenPaint);
        Box("Shelf", locker, at + new Vector3(-0.43f, 0.9f, -0.28f), at + new Vector3(0.43f, 0.93f, 0.15f), greenPaint);
        Box("Bottom", locker, at + new Vector3(-0.43f, 0f, -0.28f), at + new Vector3(0.43f, 0.05f, 0.15f), greenPaint);
        Transform door = Group("Door", locker);
        door.position = at + new Vector3(-0.47f, 0f, -0.3f);
        Box("Leaf", door, at + new Vector3(-0.47f, 0.02f, -0.33f), at + new Vector3(0.45f, 1.88f, -0.3f), greenPaint, false);
        door.rotation = Quaternion.Euler(0f, -110f, 0f);
        // The case with the engine parts: Dayse's tool box (Sketchfab "Tool Box", CGArt, CC BY).
        GameObject caseModel = ModelProp("BlueToolbox", locker, at + new Vector3(-0.15f, 0.93f, -0.05f), Quaternion.Euler(0f, 10f, 0f), false, false);
        GameObject smallBox = Box("ErickBox", locker, at + new Vector3(0.12f, 0.93f, -0.12f), at + new Vector3(0.36f, 1.04f, 0.04f), metal, false);
        return Pickup("Locker", parent, at + new Vector3(0f, 0.93f, -0.05f), new[] { caseModel, smallBox }, "maleta", "prompt.take_case",
            "msg.case_taken", new Vector3(0.9f, 0.4f, 0.6f));
    }

    // The house map (M): the plan's rooms, one drawing per floor; closed rooms hatched darker.
    private static void SetMapRooms(MansionHud hud)
    {
        List<MansionHud.MapRoom> rooms = new List<MansionHud.MapRoom>
        {
            Room("map.hall", LunaManor.Hall, 0), Room("map.dining", LunaManor.Jantar, 0), Room("map.living", LunaManor.Estar, 0),
            Room("map.kitchen", LunaManor.Cozinha, 0), Room("map.pantry", LunaManor.Despensa, 0), Room("map.china", LunaManor.Loucas, 0),
            Room("map.courtyard", LunaManor.Courtyard, 0), Room("map.garden", LunaManor.Garden, 0), Room("map.vestibule", LunaManor.Rotunda, 0),
            Room("map.power", LunaManor.Caixa, 0), Room("map.laundry", LunaManor.Lavanderia, 0), Room("map.library", LunaManor.Biblioteca, 0),
            Room("map.basement", Rect.MinMaxRect(LunaManor.StairX0, LunaManor.StairSouthZ, LunaManor.StairX1, LunaManor.StairNorthZ), 0),
            Room("map.passage", LunaManor.Passagem, 0, true), Room("map.lab", LunaManor.Bunker, 0, true), Room("map.storage", LunaManor.Deposito, 0, true),
            Room("map.boathouse", LunaManor.Garagem, 0),
            Room("map.gallery", LunaManor.GalleryWest, 1), Room("map.gallery", LunaManor.GalleryNorth, 1), Room("map.gallery", LunaManor.GalleryEast, 1),
            Room("map.room1", LunaManor.Solteiro1, 1), Room("map.room2", LunaManor.Solteiro2, 1), Room("map.generator", LunaManor.Gerador1, 1, true),
            Room("map.bathroom", LunaManor.Banheiro1, 1), Room("map.master", LunaManor.Casal1, 1), Room("map.balcony", LunaManor.SacadaPassage, 1),
            Room("map.master", LunaManor.Casal2, 1), Room("map.bathroom", LunaManor.Banheiro2, 1), Room("map.generator", LunaManor.Gerador2, 1),
            Room("map.room3", LunaManor.Solteiro3, 1), Room("map.alex", LunaManor.QuartoAlex, 1, true),
        };
        SerializedObject so = new SerializedObject(hud);
        SerializedProperty list = so.FindProperty("rooms");
        list.arraySize = rooms.Count;
        for (int i = 0; i < rooms.Count; i++)
        {
            SerializedProperty item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("name").stringValue = rooms[i].name;
            item.FindPropertyRelative("area").rectValue = rooms[i].area;
            item.FindPropertyRelative("closed").boolValue = rooms[i].closed;
            item.FindPropertyRelative("floor").intValue = rooms[i].floor;
        }

        so.FindProperty("mapWorld").rectValue = MRect(Rect.MinMaxRect(-20.5f, -3.8f, 24.5f, 34.5f));
        so.FindProperty("floorNames").arraySize = 2;
        so.FindProperty("floorNames").GetArrayElementAtIndex(0).stringValue = "map.floor0";
        so.FindProperty("floorNames").GetArrayElementAtIndex(1).stringValue = "map.floor1";
        so.FindProperty("upperFloorY").floatValue = UF - 0.6f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static MansionHud.MapRoom Room(string name, Rect local, int floor, bool closed = false)
    {
        return new MansionHud.MapRoom { name = name, area = MRect(local), closed = closed, floor = floor };
    }
}
