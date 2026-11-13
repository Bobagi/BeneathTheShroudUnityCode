using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The Luna mansion from Dayse's floor plan in the GDD ("Planta da Casa", 11/10: "1º andar" and "2º andar"), as a
// blockout built from code. One builder for every scene that shows the house: the game (FloodedHouseManor, with the
// story on top) and the main menu / opening (ArrivalCutsceneBuilder.Mansion, seen from the sea).
//
// Frame (metres, "local"): x east, z north, y up; the front door's centre at the origin, on the house floor (y 0).
// The drawing was traced at 6 cm per pixel (the courtyard 330 px wide = 19.8 m; the front door 50 px = 3 m):
// x = (px - 480) * 0.06, z = (632 - py) * 0.06 on the "1º andar" picture; the "2º andar" one sits 12 px right and
// 5 px down of it.
//
// Ground floor (y 0, ceiling 3.4): the main entrance (round porch with steps, south) into the entrance hall; dining
// room west and living room east of it, each with a bay window; the kitchen beyond the dining room; the pantry and
// the china room north of the kitchen; the courtyard (a gallery with columns round a sunken garden, open to the sky)
// behind the hall; north of it the round vestibule out to the dirt road down to the sea, the power room, the
// laundry/service area (with the basement stair); east the library, and behind it the service passage to Alex's
// lab (the old bunker), the corridor to the store and the store itself; the boat garage on its own, north-west.
// Upper floor (y 3.72, ceiling 6.9): a U round the courtyard: the west arm (single rooms 1 and 2, generator room 1),
// the north bar (bathroom 1, master bedroom 1, the passage to the balcony over the vestibule, master bedroom 2,
// bathroom 2), the east arm (generator room 2, single room 3, Alex's room); a gallery with a parapet looking into the
// courtyard; a stair in each south corner of the courtyard. The east arm's gallery is blocked by fallen ceiling past
// the stair (the plan's "passagem bloqueada"). Red doors in the plan are the ones the game keeps locked.
// Roofs: gables over the three wings (the west one is an attic, reached by a hatch in the west gallery's ceiling),
// flat roofs with a parapet over the single-storey parts.
public static class LunaManor
{
    public struct Palette
    {
        // wall: the outside (aged stucco); plaster: interior walls; stone: columns, low walls and parapets; floor: parquet;
        // tile: kitchen floor; roof: the pitched roofs' tiles; roofFlat: the flat roofs; trim: frames, skirting, rails.
        public Material wall, plaster, wallpaper, azulejo, ceiling, floor, tile, stone, concrete, roof, trim, glass, garden, metal;
        public Material marble, woodFloor, terracotta, mosaic, bathFloor, bathWall, panels, peeling, service, roughConcrete, roofFlat;
        // From the GDD's concept art of the house (Dayse, 13/10; null = the older surface above): the hall's wide plank
        // floor, its dark vertical boards over a golden wallpaper dado; red paper in the dining and living rooms, green in
        // the master bedrooms; the ochre plinth round the cream facade; dark slate on every roof; the stained glass by the
        // front door; the gilded tracery in the pediment.
        public Material hallFloor, boards, goldPaper, redPaper, greenPaper, plinth, slate, stained, gilt;
        // The courtyard of Dayse's "Varanda" drawings (21/10): the flooded lawn and the pebbles in its corners (null = garden).
        public Material lawn, pebbles;
        public System.Func<Material, float> tiling;
    }

    private static Material Or(Material m, Material fallback) => m != null ? m : fallback;

    public struct Options
    {
        // Leave the hole and the room for the basement stair in the laundry floor (the game builds the stair).
        public bool basementStairHole;
        // Close the front doorway with a plain leaf (the menu/opening put the door model there instead).
        public bool frontDoorLeaf;
        // How far the outer walls reach into the ground below the house floor (a footing on a hill).
        public float footing;
        // Interior partitions and furniture-scale detail (the menu, 150 m out at sea, only needs the shell).
        public bool interior;
        // The plan built larger in x and z (heights stay): the manor's transform carries the scale, so every room,
        // door and window grows with it; UVs keep the textures at their real size (0 = 1).
        public float planScale;
    }

    // ---------------------------------------------------------------- heights

    public const float GroundCeiling = 3.4f;
    public const float SlabTop = 3.7f;
    public const float UpperFloor = 3.72f;
    public const float UpperCeiling = 6.9f;
    public const float AtticFloor = 7.05f;
    // The island's ground round the house (outside).
    public const float GardenY = -0.3f;
    // The courtyard lawn, two 18 cm steps under the gallery (Dayse's "Varanda" elevations: the gallery's edge, a step,
    // the lawn).
    public const float GardenFloor = -0.36f;
    private const float Ext = 0.3f;
    private const float Int = 0.2f;

    // ---------------------------------------------------------------- the rooms (interior spans, local)

    public static readonly Rect Despensa = R(-14.4f, 16f, -9.7f, 24.6f);
    public static readonly Rect Loucas = R(-9.7f, 21.4f, -4.9f, 24.6f);
    public static readonly Rect Caixa = R(5.1f, 21.4f, 10.1f, 24.6f);
    public static readonly Rect Lavanderia = R(10.1f, 15.4f, 15f, 24.6f);
    public static readonly Rect Passagem = R(10.1f, 13.4f, 19.6f, 15.4f);
    public static readonly Rect Bunker = R(17.1f, 15.4f, 23.7f, 23.5f);
    public static readonly Rect Deposito = R(7.1f, 26.4f, 15f, 33.7f);
    public static readonly Rect Garagem = R(-14.7f, 26.6f, -6.9f, 33.7f);
    public static readonly Rect Courtyard = R(-9.7f, 8.9f, 10.1f, 21.4f);
    // The sunken garden, edge steps included: from the columns' line on three sides to the south stairs (Dayse's
    // "Varanda vista de cima": the lawn runs to the hall, no gallery on the south side).
    public static readonly Rect Garden = R(-7.5f, 10.3f, 7.9f, 19.2f);
    // The grand wooden stair from the lawn up to the upper floor's north gallery (her "porta dos fundos com a escada"):
    // x span and the z of its foot.
    public const float GrandStairX0 = -1.7f, GrandStairX1 = 1.9f, GrandStairTread = 0.22f;
    public const int GrandStairRisers = 22;
    public static float GrandStairFootZ => Garden.yMax - GrandStairRisers * GrandStairTread;
    // Bounding boxes of the rooms with a shaped outline (for the map, zones and probes).
    public static readonly Rect Hall = R(-4f, 0f, 4f, 8.9f);
    public static readonly Rect Jantar = R(-11.6f, 0.9f, -4f, 8.9f);
    public static readonly Rect Estar = R(4f, 0.9f, 11.6f, 8.9f);
    public static readonly Rect Cozinha = R(-19.5f, 3.7f, -9.7f, 16f);
    public static readonly Rect Biblioteca = R(10.1f, 3.4f, 19.9f, 13.4f);
    public static readonly Rect Rotunda = R(-4.9f, 21.4f, 5.1f, 26.8f);

    // Upper floor.
    public static readonly Rect GalleryWest = R(-10f, 8.9f, -7.5f, 21.6f);
    public static readonly Rect GalleryNorth = R(-7.5f, 19.2f, 7.9f, 21.6f);
    public static readonly Rect GalleryEast = R(7.9f, 8.9f, 10.4f, 21.6f);
    public static readonly Rect Solteiro1 = R(-14.4f, 8.9f, -10f, 14.4f);
    public static readonly Rect Solteiro2 = R(-14.4f, 14.4f, -10f, 19.2f);
    public static readonly Rect Gerador1 = R(-14.4f, 19.2f, -10f, 21.6f);
    public static readonly Rect Banheiro1 = R(-14.4f, 21.6f, -7.5f, 24.6f);
    public static readonly Rect Casal1 = R(-7.5f, 21.6f, -2.6f, 24.6f);
    public static readonly Rect SacadaPassage = R(-2.6f, 21.6f, 2.8f, 24.6f);
    public static readonly Rect Casal2 = R(2.8f, 21.6f, 7.9f, 24.6f);
    public static readonly Rect Banheiro2 = R(7.9f, 21.6f, 15f, 24.6f);
    public static readonly Rect Gerador2 = R(10.4f, 19.2f, 15f, 21.6f);
    public static readonly Rect Solteiro3 = R(10.4f, 14.4f, 15f, 19.2f);
    public static readonly Rect QuartoAlex = R(10.4f, 8.9f, 15f, 14.4f);
    // Where the roofs stop over the galleries (her "com telhado": the eaves cover the galleries' outer half).
    public const float VerandaRoofW = -8.7f, VerandaRoofN = 20.4f, VerandaRoofE = 9.2f;
    // Fallen ceiling in the east gallery, from the stair's landing to single room 3.
    public static readonly Rect Blocked = R(7.9f, 10.35f, 10.4f, 14.3f);

    // The basement stair in the laundry (its geometry is the game's, FloodedHouseGeometry): the hole in the floor.
    public const float StairX0 = 10.25f, StairX1 = 11.52f, StairNorthZ = 19.92f, StairSouthZ = 15.82f;

    // The attic hatch in the west gallery's ceiling (hinge on its north edge, the ladder lands south of it).
    public static readonly Vector2 HatchHinge = new Vector2(-9.4f, 16f);
    public const float HatchSize = 0.96f;

    // Doors the game hangs a leaf in (local hinge side is the first point of the span).
    public static readonly Vector2 FrontDoor = new Vector2(0f, 0f);
    // Front door (Dayse's facade, 13/10: a round-arched double door): the opening is 1.8 m wide with a half-circle top
    // springing at 1.85 m (top 2.75 m, under the porch canopy's rim at 2.8). Props/ArchedDoor fills it (frame and leaves).
    public const float FrontDoorWidth = 1.8f, FrontDoorSpring = 1.85f, FrontDoorHeight = FrontDoorSpring + FrontDoorWidth * 0.5f;
    public static readonly Vector2 LaundryDoor = new Vector2(10.1f, 20.7f);
    public static readonly Vector2 PassageDoor = new Vector2(10.1f, 14.4f);
    public static readonly Vector2 HiddenDoor = new Vector2(18.1f, 13.4f);
    public static readonly Vector2 BunkerDoor = new Vector2(18.5f, 15.4f);
    public static readonly Vector2 Gerador1Door = new Vector2(-10f, 20.4f);
    public static readonly Vector2 Gerador2Door = new Vector2(10.4f, 20.4f);
    public static readonly Vector2 AlexDoor = new Vector2(10.4f, 11.6f);
    // The lab's dehumidifier wall that the creature breaks (laundry east wall), as a span along z.
    public const float BreakZ0 = 17f, BreakZ1 = 18.8f, BreakTop = 2.3f;

    private static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(x0, z0, x1, z1);

    // The mansion's surface sets (Art/Textures/<Set>, SourceAssets/Tools/polyhaven_texture_fetch.py) and the real size
    // in metres one tile of each covers (Poly Haven's dimensions).
    public static readonly Dictionary<string, float> SurfaceTiles = new Dictionary<string, float>
    {
        { "MossyStucco", 1.8f }, { "WornPlaster", 1.8f }, { "OldWallpaper", 2.5f }, { "WallTiles", 2.26f }, { "InteriorCeiling", 2f },
        { "Parquet", 3.4f }, { "KitchenFloor", 2f }, { "WhiteStucco", 1.8f }, { "WornConcreteFloor", 2f }, { "RoofTiles", 2.5f },
        { "PeelingTimber", 0.76f }, { "MarbleTiles", 2f }, { "WornWoodFloor", 2f }, { "TerracottaFloor", 2.08f }, { "MosaicFloor", 2f },
        { "BathMosaic", 2f }, { "BathWallTiles", 1.27f }, { "DarkPanels", 1.4f }, { "PeelingWall", 1.8f }, { "RoughPlaster", 1.5f },
        { "RoughConcrete", 2f },
        // The concept art's surfaces (13/10): ambientCG sets Dayse picked (SourceAssets/Tools/ambientcg_texture_fetch.py) and
        // historic wallpapers cut to one repeat at their real scale (SourceAssets/Tools/wallpaper_tiles.py; width here,
        // height in SurfaceHeights).
        { "HallPlanks", 2.4f }, { "WallBoards", 2.0f }, { "CreamPlaster", 2.2f }, { "OchrePlaster", 2.2f }, { "CreamFacade", 2.2f }, { "OchrePlinth", 2.2f }, { "SlateRoof", 2.2f },
        { "BlackMarble", 1.5f }, { "WoodFloorWide", 2f }, { "WallpaperGold", 0.422f }, { "WallpaperRed", 0.495f }, { "WallpaperGreen", 0.348f },
        { "WallpaperBlue", 0.431f },
        // The courtyard lawn and its corner pebbles (ambientCG Grass004, Gravel022; 29/10).
        { "GardenGrass", 1.6f }, { "Pebbles", 1.2f },
        // The courtyard's surfaces of 13/11 (Poly Haven, sizes from the API's "dimensions"): fine painted plaster for the
        // facade (beige_wall_002, 4k, a 4:1 strip: width here, height in SurfaceHeights), whitewashed brick for the
        // service rooms, forest ground for the lawn, dark slates for the roofs, square tiles for the kitchen's walls. The
        // old sets stay for the scenes still using them.
        { "CreamStucco", 3f }, { "WhitewashedBrick", 2f }, { "GardenGround", 2f }, { "SlateRoof2", 3f }, { "KitchenTiles", 2f }, { "InteriorPlaster", 3f },
    };

    // Tiles that are not square: their real height in metres (the texture's v scale is width / height).
    public static readonly Dictionary<string, float> SurfaceHeights = new Dictionary<string, float>
    {
        { "WallpaperGold", 0.456f }, { "WallpaperRed", 0.568f }, { "WallpaperGreen", 0.612f }, { "WallpaperBlue", 0.538f },
        // beige_wall_002 is 4:1 in every resolution (4k = 4096 x 1024) although the API's dimensions say 3 x 3 m: its grain
        // (5 to 20 px) only reads as stucco at 3 m across, so 0.75 m tall; an even plaster, the repeat does not show.
        { "CreamStucco", 0.75f },
    };

    // ---------------------------------------------------------------- rooms: what each face of a wall shows

    // Trim on a room's walls: none (tiles, concrete), skirting and cornice, or wainscot (panels to dado height with a
    // rail, skirting, cornice): the finish of a wealthy house, painted wood now peeling.
    // Dado: the hall of the concept art, golden wallpaper from the skirting to 1.7 m under a dark rail, the wall's own
    // boards above.
    private const int TrimNone = 0, TrimBase = 1, TrimWainscot = 2, TrimDado = 3;
    public const float DadoHeight = 1.7f;
    private const float PlinthTop = 0.6f;

    private struct Room
    {
        public string name;
        public Vector2[] outline;
        public int level;
        public Material wall;
        public int trim;
    }

    private static readonly List<Room> rooms = new List<Room>();

    private static void Rooms()
    {
        rooms.Clear();
        void Add(string name, Vector2[] outline, int level, Material wall, int trim) => rooms.Add(new Room { name = name, outline = outline, level = level, wall = wall, trim = trim });
        Add("Hall", HallOutline(), 0, Or(p.boards, p.wallpaper), p.boards != null ? TrimDado : TrimWainscot);
        Add("Jantar", JantarOutline(), 0, Or(p.redPaper, p.wallpaper), TrimWainscot);
        Add("Estar", EstarOutline(), 0, Or(p.redPaper, p.wallpaper), TrimWainscot);
        Add("Cozinha", KitchenOutline, 0, p.azulejo, TrimNone);
        Add("Despensa", Box(Despensa), 0, p.service, TrimNone);
        Add("Loucas", Box(Loucas), 0, p.azulejo, TrimNone);
        Add("Rotunda", RotundaOutline(), 0, p.plaster, TrimBase);
        Add("Caixa", Box(Caixa), 0, p.service, TrimNone);
        Add("Lavanderia", Box(Lavanderia), 0, p.service, TrimNone);
        Add("Passagem", Box(Passagem), 0, p.service, TrimNone);
        Add("Biblioteca", LibraryOutline, 0, p.panels, TrimBase);
        // The courtyard is open sky: its walls are facade (13/11; with p.plaster the courtyard shots showed the worn
        // plaster's metre-wide patches on every wall around the garden).
        Add("Galeria", Box(Courtyard), 0, p.wall, TrimBase);
        Add("Bunker", Box(Bunker), 0, p.roughConcrete, TrimNone);
        Add("Corredor", Box(R(15f, 23.5f, 18.8f, 28.6f)), 0, p.roughConcrete, TrimNone);
        Add("Deposito", Box(Deposito), 0, p.roughConcrete, TrimNone);
        Add("Garagem", Box(Garagem), 0, p.service, TrimNone);
        Add("GaleriaO", Box(GalleryWest), 1, p.plaster, TrimBase);
        Add("GaleriaN", Box(GalleryNorth), 1, p.plaster, TrimBase);
        Add("GaleriaL", Box(GalleryEast), 1, p.plaster, TrimBase);
        Add("Solteiro1", Box(Solteiro1), 1, p.peeling, TrimBase);
        Add("Solteiro2", Box(Solteiro2), 1, p.peeling, TrimBase);
        Add("Solteiro3", Box(Solteiro3), 1, p.peeling, TrimBase);
        Add("QuartoAlex", Box(QuartoAlex), 1, p.peeling, TrimBase);
        Add("Gerador1", Box(Gerador1), 1, p.service, TrimNone);
        Add("Gerador2", Box(Gerador2), 1, p.service, TrimNone);
        Add("Banheiro1", Box(Banheiro1), 1, p.bathWall, TrimNone);
        Add("Banheiro2", Box(Banheiro2), 1, p.bathWall, TrimNone);
        Add("Casal1", Box(Casal1), 1, Or(p.greenPaper, p.wallpaper), TrimWainscot);
        Add("Casal2", Box(Casal2), 1, Or(p.greenPaper, p.wallpaper), TrimWainscot);
        Add("Sacada", Box(SacadaPassage), 1, p.wallpaper, TrimBase);
    }

    // The rooms' outlines and floors, for what the scenes add on the walls (stains, FloodedHouseFurniture).
    public static List<(string name, Vector2[] outline, int level)> RoomOutlines()
    {
        List<(string, Vector2[], int)> list = new List<(string, Vector2[], int)>();
        foreach (Room r in rooms)
        {
            list.Add((r.name, r.outline, r.level));
        }

        return list;
    }

    // The room a point is in (null outside: the stucco of the facade). The garden is open air.
    private static Room? RoomAt(Vector2 q, int level)
    {
        if (level == 0 && Garden.Contains(q))
        {
            return null;
        }

        foreach (Room r in rooms)
        {
            if (r.level == level && Inside(q, r.outline))
            {
                return r;
            }
        }

        return null;
    }

    private static bool Inside(Vector2 q, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    // ---------------------------------------------------------------- build

    private static Palette p;
    private static Options o;
    // The plan scale of the build in progress (Options.planScale).
    private static float K = 1f;

    // A local point as the manor's transform stretches it (for UVs in metres).
    private static Vector3 Sc(Vector3 q) => new Vector3(q.x * K, q.y, q.z * K);

    // The basement stair's hole in the laundry floor keeps its size in metres under the plan scale (the stair under it
    // is the game's, in scene coordinates): measured from the corner the game scales about (StairX0, StairNorthZ).
    private static float HoleX1 => StairX0 + (StairX1 - StairX0) / K;
    private static float HoleSouthZ => StairNorthZ - (StairNorthZ - StairSouthZ) / K;
    private static Transform manor;

    public static Transform Build(Transform parent, Vector3 origin, Palette palette, Options options)
    {
        p = palette;
        o = options;
        manor = new GameObject("LunaManor").transform;
        manor.SetParent(parent, false);
        manor.position = origin;
        K = o.planScale > 0f ? o.planScale : 1f;
        manor.localScale = new Vector3(K, 1f, K);
        Rooms();
        trimParts = Group("TrimParts", manor);
        Ground();
        Courtyard_();
        Upper();
        Roofs();
        Outbuildings();
        CombineTrim();
        return manor;
    }

    // ---------------------------------------------------------------- ground floor

    private static Vector2[] HallOutline()
    {
        List<Vector2> h = new List<Vector2> { V(-4f, 1.9f), V(-2.4f, 0f), V(2.4f, 0f), V(4f, 1.9f), V(4f, 8.9f) };
        h.AddRange(Arc(V(0f, 8.9f), 2.4f, 1.1f, 0f, 180f, 3));
        h.Add(V(-4f, 8.9f));
        return h.ToArray();
    }

    private static List<Vector2> Bay(float cx) => Arc(V(cx, 1.9f), 2.6f, 1f, 180f, 360f, 4);

    // The middle of facet i of a faceted arc (where a window or door goes).
    private static Vector2 Mid(List<Vector2> pts, int i) => (pts[i] + pts[i + 1]) * 0.5f;

    private static List<Vector2> NorthCap() => Arc(V(0.1f, 24.6f), 5f, 2.2f, 0f, 180f, 7);

    private static Vector2[] JantarOutline()
    {
        List<Vector2> j = new List<Vector2> { V(-11.6f, 8.9f), V(-11.6f, 2.9f) };
        j.AddRange(Bay(-7.8f));
        j.Add(V(-4f, 1.9f));
        j.Add(V(-4f, 8.9f));
        // Arc runs from (-10.4, 1.9) to (-5.2, 1.9): the chamfer from (-11.6, 2.9) meets its first point.
        return j.ToArray();
    }

    private static Vector2[] EstarOutline()
    {
        List<Vector2> e = new List<Vector2> { V(4f, 8.9f), V(4f, 1.9f) };
        e.AddRange(Bay(7.8f));
        e.Add(V(11.6f, 2.9f));
        e.Add(V(11.6f, 8.9f));
        return e.ToArray();
    }

    private static readonly Vector2[] KitchenOutline =
    {
        V(-19.5f, 5f), V(-18.2f, 3.7f), V(-11.6f, 3.7f), V(-11.6f, 8.9f), V(-9.7f, 8.9f), V(-9.7f, 16f), V(-18.2f, 16f), V(-19.5f, 14.7f),
    };

    private static readonly Vector2[] LibraryOutline =
    {
        V(11.6f, 3.4f), V(18.7f, 3.4f), V(19.9f, 4.6f), V(19.9f, 12.2f), V(18.7f, 13.4f), V(10.1f, 13.4f), V(10.1f, 8.9f), V(11.6f, 8.9f),
    };

    private static Vector2[] RotundaOutline()
    {
        List<Vector2> r = new List<Vector2> { V(-4.9f, 21.4f), V(5.1f, 21.4f) };
        r.AddRange(NorthCap());
        return r.ToArray();
    }

    private static Vector2[] PorchOutline() => Arc(V(0f, 0f), 2.9f, 2.9f, 180f, 360f, 14).ToArray();

    private static void Ground()
    {
        Transform g = Group("Ground", manor);

        // Floors (y -0.25..0) and ceilings (3.4..3.7, the roof or the upper floor on top).
        Floor("Hall", g, HallOutline(), Or(p.hallFloor, p.marble));
        Floor("Jantar", g, JantarOutline(), p.floor);
        Floor("Estar", g, EstarOutline(), p.floor);
        Floor("Cozinha", g, KitchenOutline, p.tile);
        Floor("Despensa", g, Box(Despensa), p.concrete);
        Floor("Loucas", g, Box(Loucas), p.tile);
        Floor("Rotunda", g, RotundaOutline(), p.mosaic);
        Floor("Caixa", g, Box(Caixa), p.concrete);
        Floor("Biblioteca", g, LibraryOutline, p.floor);
        Floor("Passagem", g, Box(Passagem), p.concrete);
        if (o.basementStairHole)
        {
            Floor("Lavanderia_N", g, Box(R(Lavanderia.xMin, StairNorthZ, Lavanderia.xMax, Lavanderia.yMax)), p.concrete);
            Floor("Lavanderia_S", g, Box(R(Lavanderia.xMin, Lavanderia.yMin, Lavanderia.xMax, HoleSouthZ)), p.concrete);
            Floor("Lavanderia_E", g, Box(R(HoleX1, HoleSouthZ, Lavanderia.xMax, StairNorthZ)), p.concrete);
            Floor("Lavanderia_W", g, Box(R(Lavanderia.xMin, HoleSouthZ, StairX0, StairNorthZ)), p.concrete);
        }
        else
        {
            Floor("Lavanderia", g, Box(Lavanderia), p.concrete);
        }

        foreach ((string name, Vector2[] outline) in new[]
                 {
                     ("Hall", HallOutline()), ("Jantar", JantarOutline()), ("Estar", EstarOutline()), ("Cozinha", KitchenOutline), ("Despensa", Box(Despensa)),
                     ("Loucas", Box(Loucas)), ("Rotunda", RotundaOutline()), ("Caixa", Box(Caixa)), ("Lavanderia", Box(Lavanderia)), ("Biblioteca", LibraryOutline),
                     ("Passagem", Box(Passagem)),
                 })
        {
            Slab("Ceiling_" + name, g, outline, GroundCeiling, SlabTop, p.roofFlat, p.ceiling, p.ceiling, true);
        }

        // The whole outer wall of the ground floor, one loop.
        List<Vector2> loop = new List<Vector2> { V(-11.6f, 3.7f), V(-11.6f, 2.9f) };
        loop.AddRange(Bay(-7.8f));
        loop.AddRange(new[] { V(-4f, 1.9f), V(-2.4f, 0f), V(2.4f, 0f), V(4f, 1.9f) });
        loop.AddRange(Bay(7.8f));
        loop.AddRange(new[]
        {
            V(11.6f, 2.9f), V(11.6f, 3.4f), V(18.7f, 3.4f), V(19.9f, 4.6f), V(19.9f, 12.2f), V(18.7f, 13.4f), V(19.6f, 13.4f), V(19.6f, 15.4f),
            V(15f, 15.4f), V(15f, 24.6f), V(10.1f, 24.6f), V(5.1f, 24.6f),
        });
        loop.AddRange(NorthCap());
        loop.AddRange(new[] { V(-14.4f, 24.6f), V(-14.4f, 16f), V(-18.2f, 16f), V(-19.5f, 14.7f), V(-19.5f, 5f), V(-18.2f, 3.7f) });
        float bottom = -0.25f - o.footing;
        // The front of the concept art (GDD, 13/10): the bays of the dining and living rooms are walls of glass in a
        // grid of small panes, nearly floor to ceiling; tall stained glass each side of the door (on the hall's
        // chamfers); a wide window in the kitchen's front, an elongated octagon in the library's.
        List<Hole> outer = new List<Hole>
        {
            Hole.Arch(FrontDoor, FrontDoorWidth, FrontDoorSpring),
            Hole.Stained(V(-3.2f, 0.95f), 0.9f, 0.35f, 3.05f), Hole.Stained(V(3.2f, 0.95f), 0.9f, 0.35f, 3.05f),
            Hole.Grid(Mid(Bay(-7.8f), 0), 0.8f, 0.45f, 2.95f, 2, 5), Hole.Grid(Mid(Bay(-7.8f), 1), 1.6f, 0.45f, 2.95f, 3, 5),
            Hole.Grid(Mid(Bay(-7.8f), 2), 1.6f, 0.45f, 2.95f, 3, 5), Hole.Grid(Mid(Bay(-7.8f), 3), 0.8f, 0.45f, 2.95f, 2, 5),
            Hole.Grid(Mid(Bay(7.8f), 0), 0.8f, 0.45f, 2.95f, 2, 5), Hole.Grid(Mid(Bay(7.8f), 1), 1.6f, 0.45f, 2.95f, 3, 5),
            Hole.Grid(Mid(Bay(7.8f), 2), 1.6f, 0.45f, 2.95f, 3, 5), Hole.Grid(Mid(Bay(7.8f), 3), 0.8f, 0.45f, 2.95f, 2, 5),
            Hole.Window(V(-19.5f, 7.6f), 1.4f), Hole.Window(V(-19.5f, 12f), 1.4f), Hole.Grid(V(-15f, 3.7f), 2.4f, 0.9f, 2.4f, 4, 2),
            Hole.Octagon(V(15.3f, 3.4f), 1.4f, 0.55f, 2.85f),
            Hole.Window(V(-14.4f, 20.4f), 1f), Hole.Window(V(-7.3f, 24.6f), 1f),
            Hole.Door(Mid(NorthCap(), 3), 1.4f, 2.5f),
            Hole.Window(Mid(NorthCap(), 1), 1f), Hole.Window(Mid(NorthCap(), 5), 1f),
            Hole.Window(V(15f, 22.6f), 1.2f), Hole.Window(V(12.6f, 24.6f), 1.2f),
            Hole.Window(V(19.9f, 6.6f), 1.4f), Hole.Window(V(19.9f, 10.2f), 1.4f),
            Hole.Door(BunkerDoor, 1f, 2.2f, false),
            Hole.Open(V(15f, (BreakZ0 + BreakZ1) * 0.5f), BreakZ1 - BreakZ0, 0f, BreakTop),
        };
        Walls("Outer", g, loop.ToArray(), true, bottom, GroundCeiling, Ext, null, null, outer.ToArray());
        // The front block, the kitchen and the library carry pitched slate roofs (FrontRoofs); only the service passage
        // keeps a flat roof behind a parapet.
        Walls("Parapet", g, new[] { V(18.7f, 13.4f), V(19.6f, 13.4f), V(19.6f, 15.4f), V(15f, 15.4f) }, false, SlabTop - 0.3f, SlabTop + 0.45f, Ext + 0.06f, p.stone, p.stone);

        // The front porch (concept art): a half-round of stone in front of the door, steps down, two slender columns
        // and a flared canopy of the roof's slate over it.
        Transform porch = Group("Porch", g);
        Floor("Floor", porch, PorchOutline(), p.marble);
        // Under the pediment's frieze (3.2 m): the canopy meets the wall at 3.15 and its rim rests on the columns.
        Canopy("Canopy", porch, V(0f, 0f), 2.95f, 2.8f, 3.15f);
        foreach (float a in new[] { 212f, 328f })
        {
            Vector2 at = V(0f, 0f) + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 2.55f;
            Column("Column", porch, at, 0f, 2.8f, 0.3f);
        }

        for (int i = 0; i < 3; i++)
        {
            float top = -0.1f * (i + 1);
            Block("Step" + i, porch, new Vector3(0f, (top - 0.9f) * 0.5f, -2.9f - 0.32f * (i + 0.5f)), 0f, new Vector3(3.3f + i * 0.2f, top + 0.9f, 0.32f), p.stone, true);
        }

        if (!o.interior)
        {
            return;
        }

        // Partitions.
        Walls("Hall_W", g, new[] { V(-4f, 1.9f), V(-4f, 8.9f) }, false, 0f, GroundCeiling, Int, null, null, Hole.Open(V(-4f, 5.4f), 1.6f, 0f, 2.6f));
        Walls("Hall_E", g, new[] { V(4f, 1.9f), V(4f, 8.9f) }, false, 0f, GroundCeiling, Int, null, null, Hole.Open(V(4f, 5.4f), 1.6f, 0f, 2.6f));
        Walls("Jantar_W", g, new[] { V(-11.6f, 3.7f), V(-11.6f, 8.9f) }, false, 0f, GroundCeiling, Int, null, null, Hole.Door(V(-11.6f, 6.3f), 1f, 2.2f));
        Walls("Estar_E", g, new[] { V(11.6f, 3.4f), V(11.6f, 8.9f) }, false, 0f, GroundCeiling, Int, null, null, Hole.Door(V(11.6f, 6.3f), 1f, 2.2f));
        Walls("Kitchen_N", g, new[] { V(-14.4f, 16f), V(-9.7f, 16f) }, false, 0f, GroundCeiling, Int, null, null, Hole.Door(V(-11.9f, 16f), 1f, 2.1f));
        Walls("Loucas_W", g, new[] { V(-9.7f, 21.4f), V(-9.7f, 24.6f) }, false, 0f, GroundCeiling, Int, null, null, Hole.Door(V(-9.7f, 23f), 1f, 2.1f));
        Walls("Loucas_E", g, new[] { V(-4.9f, 21.4f), V(-4.9f, 24.6f) }, false, 0f, GroundCeiling, Int, null, null);
        Walls("Caixa_W", g, new[] { V(5.1f, 21.4f), V(5.1f, 24.6f) }, false, 0f, GroundCeiling, Int, null, null);
        Walls("Caixa_E", g, new[] { V(10.1f, 21.4f), V(10.1f, 24.6f) }, false, 0f, GroundCeiling, Int, null, null);
        Walls("Lavanderia_S", g, new[] { V(10.1f, 15.4f), V(15f, 15.4f) }, false, 0f, GroundCeiling, Int, null, null);
        Walls("Library_N", g, new[] { V(10.1f, 13.4f), V(18.7f, 13.4f) }, false, 0f, GroundCeiling, Int, null, null,
            Hole.Door(HiddenDoor, 1f, 2.1f, false));
    }

    // ---------------------------------------------------------------- courtyard

    // Dayse's "Varanda" drawings (GDD "Planta da Casa e Imagens", 21/10): seen from above, the upper floor's U of
    // galleries round the lawn (wood floor, an openwork rail, the roofs over their outer half); from the glass door, the
    // lawn running up to a gallery of square pillars on three sides, a paneled double door at the back and the grand
    // wooden stair rising from the lawn to the upper gallery in front of it. "Grama alagada do centro... pedregulhos nos
    // cantos, tomados pela água": the game floods the lawn (FloodedHouseManor) over these pebbles.
    private static void Courtyard_()
    {
        Transform c = Group("Courtyard", manor);
        Rect cy = Courtyard;
        Rect gd = Garden;
        Material wood = Or(p.hallFloor, p.woodFloor);
        // The gallery floor round the garden (four strips). The south strip carries the two stairs to the upper floor
        // and leaves out the hall's half-round glass porch (its own plank floor).
        Floor("Gallery_SW", c, Box(R(cy.xMin, cy.yMin, -2.4f, gd.yMin)), p.terracotta);
        Floor("Gallery_SE", c, Box(R(2.4f, cy.yMin, cy.xMax, gd.yMin)), p.terracotta);
        List<Vector2> porchGap = Arc(V(0f, 8.9f), 2.4f, 1.1f, 180f, 0f, 3);
        porchGap.AddRange(new[] { V(2.4f, gd.yMin), V(-2.4f, gd.yMin) });
        Floor("Gallery_SM", c, porchGap.ToArray(), p.terracotta);
        Floor("Gallery_N", c, Box(R(cy.xMin, gd.yMax, cy.xMax, cy.yMax)), p.terracotta);
        Floor("Gallery_W", c, Box(R(cy.xMin, gd.yMin, gd.xMin, gd.yMax)), p.terracotta);
        Floor("Gallery_E", c, Box(R(gd.xMax, gd.yMin, cy.xMax, gd.yMax)), p.terracotta);
        // The gallery's roof on three sides (the upper floor's galleries stand on it); the south side is open.
        Slab("GalleryRoof_W", c, Box(R(cy.xMin, cy.yMin, gd.xMin, cy.yMax)), GroundCeiling, SlabTop, p.roofFlat, p.ceiling, p.ceiling, true);
        Slab("GalleryRoof_E", c, Box(R(gd.xMax, cy.yMin, cy.xMax, cy.yMax)), GroundCeiling, SlabTop, p.roofFlat, p.ceiling, p.ceiling, true);
        Slab("GalleryRoof_N", c, Box(R(gd.xMin, gd.yMax, gd.xMax, cy.yMax)), GroundCeiling, SlabTop, p.roofFlat, p.ceiling, p.ceiling, true);

        // The sunken lawn and its edge: one 18 cm step all round (on the south, at the foot of the stairs), the lawn a
        // second step down.
        Material lawn = Or(p.lawn, p.garden);
        Slab("Lawn", c, Box(gd), GardenFloor - 0.3f, GardenFloor, lawn, lawn, p.stone, true);
        const float step = 0.35f, stepTop = GardenFloor * 0.5f;
        Material edge = p.stone;
        Block("Edge_S", c, new Vector3(gd.center.x, (GardenFloor + stepTop) * 0.5f, gd.yMin + step * 0.5f), 0f, new Vector3(gd.width, stepTop - GardenFloor, step), edge, true);
        Block("Edge_W", c, new Vector3(gd.xMin + step * 0.5f, (GardenFloor + stepTop) * 0.5f, gd.center.y + step * 0.5f), 0f, new Vector3(step, stepTop - GardenFloor, gd.height - step), edge, true);
        Block("Edge_E", c, new Vector3(gd.xMax - step * 0.5f, (GardenFloor + stepTop) * 0.5f, gd.center.y + step * 0.5f), 0f, new Vector3(step, stepTop - GardenFloor, gd.height - step), edge, true);
        foreach ((float x0, float x1) in new[] { (gd.xMin + step, GrandStairX0), (GrandStairX1, gd.xMax - step) })
        {
            Block("Edge_N", c, new Vector3((x0 + x1) * 0.5f, (GardenFloor + stepTop) * 0.5f, gd.yMax - step * 0.5f), 0f, new Vector3(x1 - x0, stepTop - GardenFloor, step), edge, true);
        }

        // Pebbles in the corners (the water covers them in the game).
        if (p.pebbles != null)
        {
            Vector2[] corners = { V(gd.xMin + step, gd.yMin + step), V(gd.xMax - step, gd.yMin + step), V(gd.xMin + step, gd.yMax - step), V(gd.xMax - step, gd.yMax - step) };
            float[] reach = { 2.1f, 1.9f, 1.7f, 1.8f };
            Random.State saved = Random.state;
            Random.InitState(2110);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 k = corners[i];
                Vector2 inward = new Vector2(k.x < gd.center.x ? 1f : -1f, k.y < gd.center.y ? 1f : -1f);
                // A ragged quarter disc out from the corner.
                List<Vector2> blob = new List<Vector2> { k };
                const int n = 9;
                for (int j = 0; j <= n; j++)
                {
                    float a = j / (float)n * Mathf.PI * 0.5f;
                    float r = reach[i] * Random.Range(0.62f, 1f);
                    blob.Add(k + new Vector2(Mathf.Cos(a) * inward.x, Mathf.Sin(a) * inward.y) * r);
                }

                Slab("Pebbles_" + i, c, blob.ToArray(), GardenFloor, GardenFloor + 0.012f, p.pebbles, p.pebbles, p.pebbles, false);
            }

            Random.state = saved;
        }

        // The courtyard's walls (the rooms round it), with their doors; at the back, the paneled double door.
        Walls("Court_W", c, new[] { V(cy.xMin, cy.yMin), V(cy.xMin, cy.yMax) }, false, -0.25f, GroundCeiling, Ext, null, null,
            Hole.Door(V(cy.xMin, 12.6f), 1f, 2.2f));
        Walls("Court_N", c, new[] { V(cy.xMin, cy.yMax), V(cy.xMax, cy.yMax) }, false, -0.25f, GroundCeiling, Ext, null, null,
            Hole.Door(V(-7f, cy.yMax), 1f, 2.1f), Hole.Door(V(0.1f, cy.yMax), BackDoorWidth, 2.5f), Hole.Door(V(7.5f, cy.yMax), 1f, 2.1f));
        Walls("Court_E", c, new[] { V(cy.xMax, cy.yMin), V(cy.xMax, cy.yMax) }, false, -0.25f, GroundCeiling, Ext, null, null,
            Hole.Door(V(cy.xMax, 10.4f), 1f, 2.2f), Hole.Door(PassageDoor, 1f, 2.1f, false), Hole.Door(LaundryDoor, 1f, 2.2f, false));
        // South: the dining room, the hall's half-round glass porch (its glass door into the courtyard) and the
        // living room; the wall runs on to the library's corner.
        List<Vector2> south = new List<Vector2> { V(-11.6f, 8.9f), V(-2.4f, 8.9f) };
        south.AddRange(Arc(V(0f, 8.9f), 2.4f, 1.1f, 180f, 0f, 3));
        south.Add(V(11.6f, 8.9f));
        Walls("Court_S", c, south.ToArray(), false, -0.25f, GroundCeiling, Ext, null, null, Hole.Door(V(0f, 8.9f + 1.1f * Mathf.Sin(60f * Mathf.Deg2Rad)), 1.6f, 2.5f));

        // Square pillars on the galleries' edge carrying the upper floor (four on the north as she draws them: the
        // corners and a pair flanking the back door; three more down each side).
        Transform cols = Group("Pillars", c);
        float px0 = gd.xMin - 0.2f, px1 = gd.xMax + 0.2f, pzN = gd.yMax + 0.2f;
        foreach (Vector2 at in new[]
                 {
                     V(px0, pzN), V(-3.2f, pzN), V(3.6f, pzN), V(px1, pzN),
                     V(px0, 10.55f), V(px0, 13.4f), V(px0, 16.3f), V(px1, 10.55f), V(px1, 13.4f), V(px1, 16.3f),
                 })
        {
            Pillar("Pillar", cols, at, 0f, GroundCeiling, 0.4f);
        }

        GrandStair(c, wood);
        if (o.interior)
        {
            BackDoor(c);
        }

        // The two stairs to the upper floor, in the south corners, along the open south gallery: each climbs from
        // the middle out to its wing's gallery (20 risers of 18.6 cm, 25 cm treads).
        Stair("Stair_W", c, -2.6f, -1f, wood);
        Stair("Stair_E", c, 3.0f, 1f, wood);
    }

    // The back door's opening (north wall of the courtyard, into the vestibule): two paneled leaves.
    public const float BackDoorWidth = 1.6f;

    // A square pillar (her elevations draw them square, plain), with a plinth and a cap.
    private static void Pillar(string name, Transform parent, Vector2 at, float y0, float y1, float side)
    {
        Block(name, parent, new Vector3(at.x, (y0 + y1) * 0.5f, at.y), 0f, new Vector3(side, y1 - y0, side), p.stone, true);
        Block(name + "_Base", parent, new Vector3(at.x, y0 + 0.11f, at.y), 0f, new Vector3(side + 0.1f, 0.22f, side + 0.1f), p.stone, false);
        Block(name + "_Cap", parent, new Vector3(at.x, y1 - 0.09f, at.y), 0f, new Vector3(side + 0.12f, 0.18f, side + 0.12f), p.stone, false);
    }

    // The grand stair (her "Varanda na parte da porta dos fundos com a escada"): wooden, as wide as the gap between the
    // middle pillars allows, from the lawn up to the north gallery of the upper floor, its top at the gallery's edge
    // in front of the back door. Solid steps, a stepped cheek wall each side, a sloped rail on balusters and, at the
    // foot, the rails turned out to two newel posts (the curls of the drawing).
    private static void GrandStair(Transform parent, Material wood)
    {
        Transform s = Group("GrandStair", parent);
        float x0 = GrandStairX0, x1 = GrandStairX1, mid = (x0 + x1) * 0.5f;
        float rise = (UpperFloor - GardenFloor) / GrandStairRisers;
        float foot = GrandStairFootZ, topZ = Garden.yMax;
        for (int i = 0; i < GrandStairRisers; i++)
        {
            float top = GardenFloor + rise * (i + 1);
            float za = foot + i * GrandStairTread, zb = za + GrandStairTread;
            Block("Step" + i, s, new Vector3(mid, (GardenFloor + top) * 0.5f, (za + zb) * 0.5f), 0f, new Vector3(x1 - x0, top - GardenFloor, GrandStairTread), wood, true);
        }

        const float cheek = 0.14f, railUp = 0.92f;
        float run = topZ - foot, h = UpperFloor - GardenFloor;
        float len = Mathf.Sqrt(run * run + h * h);
        float slope = Mathf.Atan2(h, run) * Mathf.Rad2Deg;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side < 0f ? x0 - cheek * 0.5f : x1 + cheek * 0.5f;
            for (int i = 0; i < GrandStairRisers; i++)
            {
                float top = GardenFloor + rise * (i + 1) + 0.12f;
                float za = foot + i * GrandStairTread;
                Block("Cheek", s, new Vector3(x, (GardenFloor + top) * 0.5f, za + GrandStairTread * 0.5f), 0f, new Vector3(cheek, top - GardenFloor, GrandStairTread + 0.002f), p.trim, true);
            }

            // The rail along the slope, over the nosing line, on a baluster every third step.
            GameObject rail = Block("Rail", s, new Vector3(x, (GardenFloor + UpperFloor) * 0.5f + railUp, (foot + topZ) * 0.5f), 0f, new Vector3(0.12f, 0.09f, len), p.trim, false);
            rail.transform.rotation = manor.rotation * Quaternion.Euler(-slope, 0f, 0f);
            for (int i = 1; i < GrandStairRisers; i += 3)
            {
                float top = GardenFloor + rise * (i + 1);
                Block("Baluster", s, new Vector3(x, top + 0.12f + (railUp - 0.12f) * 0.5f, foot + (i + 0.5f) * GrandStairTread), 0f, new Vector3(0.05f, railUp - 0.12f, 0.05f), p.trim, false);
            }

            // An invisible rail the height of a person along the open side (nothing walks off the stair's edge).
            GameObject stop = Block("RailStop", s, new Vector3(x, (GardenFloor + UpperFloor) * 0.5f + 0.9f, (foot + topZ) * 0.5f), 0f, new Vector3(0.06f, 1.8f, len), p.trim, true);
            stop.transform.rotation = manor.rotation * Quaternion.Euler(-slope, 0f, 0f);
            stop.GetComponent<MeshRenderer>().enabled = false;

            // The foot: the rail turns out to a newel post, a short curved run of three pieces.
            float outX = x + side * 0.55f, postZ = foot - 0.35f;
            Vector2 a = V(x, foot + GrandStairTread), m1 = V(x + side * 0.12f, foot - 0.1f), m2 = V(x + side * 0.36f, foot - 0.3f), b = V(outX, postZ);
            Vector2[] curl = { a, m1, m2, b };
            for (int k = 0; k < curl.Length - 1; k++)
            {
                Vector2 d = curl[k + 1] - curl[k];
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                Vector2 c = (curl[k] + curl[k + 1]) * 0.5f;
                float y = GardenFloor + 0.12f + railUp * 0.95f;
                Block("Curl", s, new Vector3(c.x, y, c.y), yaw, new Vector3(0.12f, 0.09f, d.magnitude + 0.06f), p.trim, false);
                Block("CurlWall", s, new Vector3(c.x, (GardenFloor + GardenFloor + 0.3f) * 0.5f, c.y), yaw, new Vector3(cheek, 0.3f, d.magnitude + 0.04f), p.trim, true);
            }

            Block("Newel", s, new Vector3(outX, GardenFloor + 0.6f, postZ), 0f, new Vector3(0.26f, 1.2f, 0.26f), p.trim, true);
            Block("NewelCap", s, new Vector3(outX, GardenFloor + 1.24f, postZ), 0f, new Vector3(0.34f, 0.08f, 0.34f), p.trim, false);
        }
    }

    // The paneled double door at the back of the courtyard (into the vestibule): two leaves on the courtyard face of
    // the wall, hinged at the jambs, shut, opening into the courtyard together (Door.Link) and closing again with E.
    private static void BackDoor(Transform parent)
    {
        const float t = 0.07f;
        float half = BackDoorWidth * 0.5f;
        float leaf = half - t - 0.005f, height = 2.5f - t - 0.01f;
        float z = Courtyard.yMax - Ext * 0.5f;
        Door[] leaves = new Door[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Transform pivot = Group(i == 0 ? "BackDoor_L" : "BackDoor_R", parent);
            pivot.position = manor.TransformPoint(new Vector3(0.1f + side * (half - t), 0f, z));
            // The leaf runs along the pivot's +x from the hinge to the meeting edge; its face toward the courtyard.
            pivot.rotation = manor.rotation * Quaternion.Euler(0f, side < 0f ? 0f : 180f, 0f);
            FloodedHouseBuilder.DoorLeaf(pivot.name + "_Leaf", pivot, leaf, height, 0.045f, p.trim);
            Door door = pivot.gameObject.AddComponent<Door>();
            door.Configure(Door.Lock.None, null, null, null, null);
            SerializedObject so = new SerializedObject(door);
            so.FindProperty("openAngle").floatValue = side < 0f ? 95f : -95f;
            so.ApplyModifiedPropertiesWithoutUndo();
            leaves[i] = door;
        }

        Door.Link(leaves[0], leaves[1]);
    }

    // An openwork wooden rail along a polyline on the upper floor: a bottom rail, slats every 13 cm, a capping rail at
    // 1 m, and an invisible wall the same height (nothing falls through the slats). Into TrimParts: one mesh at the end.
    private static void Balustrade(Transform parent, Vector2[] pts)
    {
        const float height = 1f, gap = 0.13f;
        for (int i = 0; i + 1 < pts.Length; i++)
        {
            Vector2 a = pts[i], b = pts[i + 1];
            float len = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / len;
            float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 c = (a + b) * 0.5f;
            Block("RailBottom", trimParts, new Vector3(c.x, UpperFloor + 0.05f, c.y), yaw, new Vector3(len + 0.1f, 0.1f, 0.1f), p.trim, false);
            Block("RailCap", trimParts, new Vector3(c.x, UpperFloor + height - 0.04f, c.y), yaw, new Vector3(len + 0.14f, 0.08f, 0.16f), p.trim, false);
            int slats = Mathf.Max(1, Mathf.RoundToInt(len / gap));
            for (int k = 0; k <= slats; k++)
            {
                Vector2 at = Vector2.Lerp(a, b, k / (float)slats);
                Block("Slat", trimParts, new Vector3(at.x, UpperFloor + height * 0.5f, at.y), yaw, new Vector3(0.045f, height - 0.16f, 0.045f), p.trim, false);
            }

            GameObject stop = Block("RailStop", parent, new Vector3(c.x, UpperFloor + 0.6f, c.y), yaw, new Vector3(len, 1.2f, 0.12f), p.trim, true);
            stop.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    // Wooden like the grand stair and open underneath (29/10: as solid stucco masses they stood in the lawn as a white
    // wall): a block per step the height of its riser, a sloped stringer under the steps on the garden side.
    private static void Stair(string name, Transform parent, float startX, float dir, Material wood)
    {
        Transform s = Group(name, parent);
        const int risers = 20;
        const float tread = 0.25f;
        const float z0 = 8.95f + Ext * 0.5f, z1 = 10.25f;
        float rise = UpperFloor / risers;
        for (int i = 0; i < risers; i++)
        {
            float top = rise * (i + 1);
            float xa = startX + dir * i * tread;
            float xb = xa + dir * tread;
            Block("Step" + i, s, new Vector3((xa + xb) * 0.5f, top - rise * 0.5f, (z0 + z1) * 0.5f), 0f, new Vector3(tread, rise, z1 - z0), wood, true);
        }

        // Handrail and an invisible rail on the open side.
        float run = risers * tread;
        float len = Mathf.Sqrt(run * run + UpperFloor * UpperFloor);
        float slope = Mathf.Atan2(UpperFloor, run) * Mathf.Rad2Deg;
        GameObject stringer = Block("Stringer", s, new Vector3(startX + dir * run * 0.5f, UpperFloor * 0.5f - 0.16f, z1 - 0.03f), 0f, new Vector3(len, 0.34f, 0.06f), p.trim, false);
        stringer.transform.rotation = manor.rotation * Quaternion.Euler(0f, 0f, slope * dir);
        GameObject rail = Block("Handrail", s, new Vector3(startX + dir * run * 0.5f, UpperFloor * 0.5f + 0.95f, z1 - 0.04f), 0f, new Vector3(len, 0.06f, 0.06f), p.trim, false);
        rail.transform.rotation = manor.rotation * Quaternion.Euler(0f, 0f, slope * dir);
        GameObject stop = Block("RailStop", s, new Vector3(startX + dir * run * 0.5f, UpperFloor * 0.5f + 0.6f, z1 + 0.04f), 0f, new Vector3(run, UpperFloor + 1.2f, 0.06f), p.trim, true);
        stop.GetComponent<MeshRenderer>().enabled = false;
        for (int i = 0; i <= risers; i += 4)
        {
            float top = UpperFloor * Mathf.Min(i + 1, risers) / risers;
            Block("Baluster", s, new Vector3(startX + dir * (i + 0.5f) * tread, top + 0.47f, z1 - 0.04f), 0f, new Vector3(0.05f, 0.95f, 0.05f), p.trim, false);
        }
    }

    // ---------------------------------------------------------------- upper floor

    private static void Upper()
    {
        Transform u = Group("Upper", manor);
        Rect[] rooms =
        {
            GalleryWest, GalleryNorth, GalleryEast, Solteiro1, Solteiro2, Gerador1, Banheiro1, Casal1, SacadaPassage, Casal2, Banheiro2, Gerador2, Solteiro3, QuartoAlex,
        };
        // The galleries are the "varanda" of Dayse's drawings: plank floors.
        Material veranda = Or(p.hallFloor, p.woodFloor);
        Material[] floors = { veranda, veranda, veranda, p.woodFloor, p.woodFloor, p.concrete, p.bathFloor, p.floor, p.floor, p.floor, p.bathFloor, p.concrete, p.woodFloor, p.woodFloor };
        for (int i = 0; i < rooms.Length; i++)
        {
            Slab("Floor_" + i, u, Box(rooms[i]), SlabTop, UpperFloor, floors[i], p.ceiling, p.ceiling, true);
            if (rooms[i] == GalleryWest)
            {
                // The ceiling over the outer half (the roof's eave; the inner half is open to the sky), with the attic
                // hatch's hole.
                Rect hole = R(HatchHinge.x - HatchSize * 0.5f, HatchHinge.y - HatchSize - 0.02f, HatchHinge.x + HatchSize * 0.5f, HatchHinge.y - 0.02f);
                Rect gw = R(GalleryWest.xMin, GalleryWest.yMin, VerandaRoofW, GalleryWest.yMax);
                Slab("Ceiling_W_S", u, Box(R(gw.xMin, gw.yMin, gw.xMax, hole.yMin)), UpperCeiling, AtticFloor, p.floor, p.ceiling, p.ceiling, true);
                Slab("Ceiling_W_N", u, Box(R(gw.xMin, hole.yMax, gw.xMax, gw.yMax)), UpperCeiling, AtticFloor, p.floor, p.ceiling, p.ceiling, true);
                Slab("Ceiling_W_W", u, Box(R(gw.xMin, hole.yMin, hole.xMin, hole.yMax)), UpperCeiling, AtticFloor, p.floor, p.ceiling, p.ceiling, true);
                Slab("Ceiling_W_E", u, Box(R(hole.xMax, hole.yMin, gw.xMax, hole.yMax)), UpperCeiling, AtticFloor, p.floor, p.ceiling, p.ceiling, true);
                continue;
            }

            Rect ceiling = rooms[i];
            if (rooms[i] == GalleryNorth)
            {
                ceiling = R(ceiling.xMin, VerandaRoofN, ceiling.xMax, ceiling.yMax);
            }
            else if (rooms[i] == GalleryEast)
            {
                ceiling = R(VerandaRoofE, ceiling.yMin, ceiling.xMax, ceiling.yMax);
            }

            Slab("Ceiling_" + i, u, Box(ceiling), UpperCeiling, AtticFloor, p.floor, p.ceiling, p.ceiling, true);
        }

        // Outer walls of the U (the corners of the bathrooms cut off, as drawn).
        Vector2[] loop =
        {
            V(7.9f, 8.9f), V(15f, 8.9f), V(15f, 23.4f), V(13.8f, 24.6f), V(-13.2f, 24.6f), V(-14.4f, 23.4f), V(-14.4f, 8.9f), V(-7.5f, 8.9f),
        };
        float w = UpperFloor + 0.9f;
        Walls("Outer", u, loop, false, SlabTop, UpperCeiling, Ext, null, null,
            Hole.Window(V(-14.4f, 11.6f), 1.2f, w), Hole.Window(V(-14.4f, 16.8f), 1.2f, w), Hole.Window(V(-14.4f, 20.4f), 0.8f, w),
            Hole.Window(V(-11f, 24.6f), 1f, w), Hole.Window(V(-5f, 24.6f), 1.2f, w), Hole.Door(V(0.1f, 24.6f), 1.4f, UpperFloor + 2.4f, true, SlabTop),
            Hole.Window(V(5.3f, 24.6f), 1.2f, w), Hole.Window(V(11.4f, 24.6f), 1f, w),
            Hole.Window(V(15f, 20.4f), 0.8f, w), Hole.Window(V(15f, 16.8f), 1.2f, w), Hole.Window(V(15f, 11.6f), 1.2f, w),
            Hole.Grid(V(-12.2f, 8.9f), 2.6f, w - 0.2f, w + 1.5f, 4, 3), Hole.Grid(V(12.7f, 8.9f), 2.6f, w - 0.2f, w + 1.5f, 4, 3));

        // The rail on the gallery's courtyard side ("mureta vazada" in the plan, slats in the elevations), open where
        // the stairs arrive: the corner stairs at the south ends, the grand stair in the middle of the north side.
        Balustrade(u, new[] { V(-7.5f, 10.25f), V(-7.5f, 19.2f), V(GrandStairX0 - 0.14f, 19.2f) });
        Balustrade(u, new[] { V(GrandStairX1 + 0.14f, 19.2f), V(7.9f, 19.2f), V(7.9f, 10.25f) });

        // The balcony on the vestibule's roof: the half-round outside the north wall, with an openwork rail.
        List<Vector2> cap = NorthCap();
        Slab("Balcony", u, cap.ToArray(), SlabTop, UpperFloor, p.terracotta, p.ceiling, p.stone, true);
        Walls("BalconyRail", u, cap.ToArray(), false, UpperFloor + 0.9f, UpperFloor + 1f, 0.12f, p.stone, p.stone);
        GameObject railStop = Walls("BalconyStop", u, cap.ToArray(), false, UpperFloor, UpperFloor + 1.2f, 0.1f, p.stone, p.stone);
        foreach (MeshRenderer r in railStop.GetComponentsInChildren<MeshRenderer>())
        {
            r.enabled = false;
        }

        for (int i = 0; i < cap.Count - 1; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector2 at = Vector2.Lerp(cap[i], cap[i + 1], (k + 0.5f) / 3f);
                Block("Baluster", u, new Vector3(at.x, UpperFloor + 0.45f, at.y), 0f, new Vector3(0.07f, 0.9f, 0.07f), p.stone, false);
            }
        }

        if (!o.interior)
        {
            return;
        }

        // Partitions and doors.
        Walls("West_Gallery", u, new[] { V(-10f, 8.9f), V(-10f, 21.6f) }, false, SlabTop, UpperCeiling, Int, null, null,
            Hole.Door(V(-10f, 11.6f), 1f, UpperFloor + 2.1f, true, SlabTop), Hole.Door(V(-10f, 16.8f), 1f, UpperFloor + 2.1f, true, SlabTop),
            Hole.Door(Gerador1Door, 1f, UpperFloor + 2.1f, false, SlabTop));
        Walls("West_14", u, new[] { V(-14.4f, 14.4f), V(-10f, 14.4f) }, false, SlabTop, UpperCeiling, Int, null, null);
        Walls("West_19", u, new[] { V(-14.4f, 19.2f), V(-10f, 19.2f) }, false, SlabTop, UpperCeiling, Int, null, null);
        Walls("North_Gallery", u, new[] { V(-14.4f, 21.6f), V(15f, 21.6f) }, false, SlabTop, UpperCeiling, Int, null, null,
            Hole.Door(V(-8.8f, 21.6f), 1f, UpperFloor + 2.1f, true, SlabTop), Hole.Door(V(-5f, 21.6f), 1f, UpperFloor + 2.1f, true, SlabTop),
            Hole.Open(V(0.1f, 21.6f), 2.4f, SlabTop, UpperFloor + 2.5f), Hole.Door(V(5.3f, 21.6f), 1f, UpperFloor + 2.1f, true, SlabTop),
            Hole.Door(V(8.9f, 21.6f), 1f, UpperFloor + 2.1f, true, SlabTop));
        foreach (float x in new[] { -7.5f, -2.6f, 2.8f, 7.9f })
        {
            Walls("North_" + x, u, new[] { V(x, 21.6f), V(x, 24.6f) }, false, SlabTop, UpperCeiling, Int, null, null);
        }

        Walls("East_Gallery", u, new[] { V(10.4f, 8.9f), V(10.4f, 21.6f) }, false, SlabTop, UpperCeiling, Int, null, null,
            Hole.Door(AlexDoor, 1f, UpperFloor + 2.1f, false, SlabTop), Hole.Door(V(10.4f, 16.8f), 1f, UpperFloor + 2.1f, true, SlabTop),
            Hole.Door(Gerador2Door, 1f, UpperFloor + 2.1f, false, SlabTop));
        Walls("East_14", u, new[] { V(10.4f, 14.4f), V(15f, 14.4f) }, false, SlabTop, UpperCeiling, Int, null, null);
        Walls("East_19", u, new[] { V(10.4f, 19.2f), V(15f, 19.2f) }, false, SlabTop, UpperCeiling, Int, null, null);

        // The east gallery's fallen ceiling: boards, beams, plaster and a solid stop behind them.
        Transform debris = Group("BlockedPassage", u);
        Rect b = Blocked;
        GameObject wall = Block("Stop", debris, new Vector3(b.center.x, (UpperFloor + UpperCeiling) * 0.5f, b.yMin + 0.6f), 0f, new Vector3(b.width, UpperCeiling - UpperFloor, 0.2f), p.plaster, true);
        wall.GetComponent<MeshRenderer>().enabled = false;
        Random.State saved = Random.state;
        Random.InitState(311);
        for (int i = 0; i < 14; i++)
        {
            Vector3 at = new Vector3(Random.Range(b.xMin + 0.3f, b.xMax - 0.3f), UpperFloor + Random.Range(0.1f, 1.6f), Random.Range(b.yMin + 0.4f, b.yMax - 0.2f));
            GameObject piece = Block("Debris" + i, debris, at, Random.Range(0f, 180f),
                i % 3 == 0 ? new Vector3(0.18f, 0.18f, Random.Range(2f, 3.2f)) : new Vector3(Random.Range(0.6f, 1.4f), Random.Range(0.05f, 0.3f), Random.Range(0.5f, 1.2f)),
                i % 3 == 0 ? p.roof : p.ceiling, true);
            piece.transform.rotation = manor.rotation * Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f));
        }

        Random.state = saved;
        // The hole the ceiling came down from, dark against the roof.
        Block("CeilingHole", debris, new Vector3(b.center.x, UpperCeiling - 0.01f, b.center.y + 0.6f), 0f, new Vector3(1.6f, 0.02f, 2f), p.roof, false);
    }

    // ---------------------------------------------------------------- roofs

    // Every roof in dark slate, hipped, as the concept art's front view draws them (GDD, 13/10): the three wings of the
    // upper floor, the gable over the hall with the gilded pediment, a pyramid over each bay room, hips over the kitchen
    // and the library and low lean-tos where those run on under the upper floor's walls.
    private const float RoofOverhang = 0.4f;

    private static void Roofs()
    {
        Transform r = Group("Roofs", manor);
        Material slate = Or(p.slate, p.roof);
        // Upper floor: the north wing's ridge (along x) and the side wings' (along z) all 2.8 m over the attic floor, so
        // the side wings run into the north wing's slopes at its ridge line. The west wing is the attic (walkable, its
        // south hip steep so that the attic keeps its headroom where the suit stands) closed at z 19.2 by a wall.
        float ridge = AtticFloor + 2.8f, eave = AtticFloor + 0.5f;
        // The wings stop over the galleries' outer half (VerandaRoof*, Dayse's "com telhado", 21/10); the side wings
        // end at the north wing's ridge.
        const float wall = 0.15f;
        float northRidge = (VerandaRoofN - wall + 24.75f) * 0.5f;
        HipRoof("Attic", r, R(-14.55f, 8.75f, VerandaRoofW + wall, northRidge), true, eave, ridge, 1.6f, -1f, slate, true);
        Knees("AtticKnees", r, R(-14.55f, 8.75f, VerandaRoofW + wall, 19.2f), AtticFloor, 0.5f, ridge);
        HipRoof("EastWing", r, R(VerandaRoofE - wall, 8.75f, 15.15f, northRidge), true, eave, ridge, 2.4f, -1f, slate, false);
        HipRoof("NorthWing", r, R(-14.55f, VerandaRoofN - wall, 15.15f, 24.75f), false, eave, ridge, 0f, 0f, slate, false);
        // Fascia boards closing the gap between the galleries' half ceiling and the eave above it.
        float fh = eave - UpperCeiling;
        Block("Fascia_E", r, new Vector3(VerandaRoofE + 0.03f, UpperCeiling + fh * 0.5f, (GalleryEast.yMin + GalleryEast.yMax) * 0.5f), 0f, new Vector3(0.06f, fh, GalleryEast.height), p.trim, false);
        Block("Fascia_N", r, new Vector3((GalleryWest.xMax + GalleryEast.xMin) * 0.5f, UpperCeiling + fh * 0.5f, VerandaRoofN + 0.03f), 0f, new Vector3(GalleryEast.xMin - GalleryWest.xMax + 0.1f, fh, 0.06f), p.trim, false);
        Block("Fascia_W", r, new Vector3(VerandaRoofW - 0.03f, UpperCeiling + fh * 0.5f, (19.2f + GalleryWest.yMax) * 0.5f), 0f, new Vector3(0.06f, fh, GalleryWest.yMax - 19.2f), p.trim, false);

        FrontRoofs(r, slate);
    }

    private static void FrontRoofs(Transform r, Material slate)
    {
        // The hall: a gable to the front with the pediment, hipped toward the courtyard. Its gable wall stands in front of
        // the hall's front wall, over a frieze above the porch canopy; soffits close the corners over the stained glass.
        HipRoof("HallGable", r, R(-4.3f, -0.35f, 4.3f, 8.9f), true, SlabTop, SlabTop + 3.4f, 0f, 0f, slate, false, true);
        Pediment("Pediment", r, -0.35f, 4.3f, 3.2f, SlabTop, SlabTop + 3.4f);
        Slab("Soffit_W", r, new[] { V(-4.3f, -0.35f), V(-2.4f, -0.35f), V(-2.4f, 0f), V(-4f, 1.9f), V(-4.3f, 1.9f) }, 3.2f, SlabTop, p.roofFlat, p.ceiling, p.wall, false);
        Slab("Soffit_E", r, new[] { V(2.4f, -0.35f), V(4.3f, -0.35f), V(4.3f, 1.9f), V(4f, 1.9f), V(2.4f, 0f) }, 3.2f, SlabTop, p.roofFlat, p.ceiling, p.wall, false);
        // The bay rooms' tall pyramids, their apex over the room's middle.
        Pyramid("DiningRoof", r, JantarOutline(), SlabTop, SlabTop + 3.4f, slate);
        Pyramid("LivingRoof", r, EstarOutline(), SlabTop, SlabTop + 3.4f, slate);
        // Kitchen and library fronts: hips along x; behind, the parts under the upper floor's walls: lean-tos falling
        // outward from below the bedrooms' sills.
        HipRoof("KitchenRoof", r, R(-19.5f, 3.7f, -11.6f, 8.9f), false, SlabTop, SlabTop + 1.9f, 0f, 0f, slate, false);
        HipRoof("LibraryRoof", r, R(11.6f, 3.4f, 19.9f, 8.9f), false, SlabTop, SlabTop + 1.9f, 0f, 0f, slate, false);
        LeanTo("KitchenLeanTo", r, R(-19.5f, 8.9f, -14.4f, 16f), -1f, SlabTop, SlabTop + 0.75f, slate);
        LeanTo("LibraryLeanTo", r, R(15f, 8.9f, 19.9f, 13.4f), 1f, SlabTop, SlabTop + 0.75f, slate);
    }

    // ---------------------------------------------------------------- roof meshes

    // One planar face of a roof: the slate on top, the boards/ceiling under it (12 cm lower), UVs along the face's
    // first edge (the eave) and up its slope, so the slate courses run level.
    private const float RoofThickness = 0.12f;

    private sealed class RoofMesh
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> norms = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> top = new List<int>(), under = new List<int>(), edge = new List<int>();

        public void Face(Material topMaterial, params Vector3[] pts)
        {
            Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]).normalized;
            if (n.y < 0f)
            {
                System.Array.Reverse(pts);
                n = -n;
            }

            Vector3 along = (pts[1] - pts[0]).normalized;
            Vector3 up = Vector3.Cross(n, along).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f)
            {
                up = -up;
            }

            float tt = Tile(topMaterial), tb = Tile(p.ceiling);
            Vector3 down = Vector3.down * RoofThickness;
            // Top (facing out), counter-clockwise seen from above = clockwise for Unity's front face from outside.
            int s = verts.Count;
            foreach (Vector3 q in pts)
            {
                verts.Add(q);
                norms.Add(n);
                uvs.Add(new Vector2(Vector3.Dot(Sc(q), along), Vector3.Dot(Sc(q), up)) / tt);
            }

            // Unity's front face: normal = Cross(b - a, c - a), so (p0, pi, pi+1) faces up and out.
            for (int i = 1; i < pts.Length - 1; i++)
            {
                top.AddRange(new[] { s, s + i, s + i + 1 });
            }

            int b = verts.Count;
            foreach (Vector3 q in pts)
            {
                verts.Add(q + down);
                norms.Add(-n);
                uvs.Add(new Vector2(Vector3.Dot(Sc(q), along), Vector3.Dot(Sc(q), up)) / tb);
            }

            for (int i = 1; i < pts.Length - 1; i++)
            {
                under.AddRange(new[] { b, b + i + 1, b + i });
            }
        }

        // The thickness along an open edge (eaves, verges): a strip between the top and the underside.
        public void Edge(Vector3 a, Vector3 c)
        {
            Vector3 down = Vector3.down * RoofThickness;
            Vector3 outward = Vector3.Cross(c - a, Vector3.up).normalized;
            int s = verts.Count;
            foreach (Vector3 q in new[] { a, c, c + down, a + down })
            {
                verts.Add(q);
                norms.Add(outward);
                uvs.Add(new Vector2(Vector3.Dot(Sc(q), (c - a).normalized), q.y) / 0.5f);
            }

            edge.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3, s, s + 2, s + 1, s, s + 3, s + 2 });
        }

        public GameObject Build(string name, Transform parent, Material topMaterial, bool collider)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(under, 1);
            mesh.SetTriangles(edge, 2);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(manor.position, manor.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { topMaterial, p.ceiling, p.trim };
            if (collider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            Static(go);
            return go;
        }
    }

    // A hipped roof over a rectangle. alongZ: the ridge runs north-south. The eaves sit at `eave` on the wall line and
    // the slopes rise to `ridge`; the ends are hipped over `hipStart`/`hipEnd` metres from the wall line (0 = the
    // natural hip, at the same pitch as the sides; negative = no hip: the slopes run out to the end, open; gableStart:
    // the start end is a gable, closed by a wall the caller builds (the pediment)). The overhang continues every slope.
    private static void HipRoof(string name, Transform parent, Rect area, bool alongZ, float eave, float ridge, float hipStart, float hipEnd, Material slate,
        bool collider, bool gableStart = false)
    {
        // Work in (u along the ridge, w across), then map to (x, z).
        float u0 = alongZ ? area.yMin : area.xMin, u1 = alongZ ? area.yMax : area.xMax;
        float w0 = alongZ ? area.xMin : area.yMin, w1 = alongZ ? area.xMax : area.yMax;
        float half = (w1 - w0) * 0.5f, wc = (w0 + w1) * 0.5f;
        float rise = ridge - eave;
        float slope = rise / half;
        float o = RoofOverhang;
        float edgeY = eave - o * slope;
        Vector3 P(float u, float w, float y) => alongZ ? new Vector3(w, y, u) : new Vector3(u, y, w);
        float StartDepth(float d) => d == 0f ? half : d;
        bool hipA = hipStart >= 0f && !gableStart, hipB = hipEnd >= 0f;
        float dA = hipA ? StartDepth(hipStart) : 0f, dB = hipB ? StartDepth(hipEnd) : 0f;
        // Along the ridge the overhang drops by the same height as at the sides, so the eaves run level all round.
        float oA = hipA ? o * slope / (rise / dA) : (gableStart ? o : 0f), oB = hipB ? o * slope / (rise / dB) : 0f;
        float ua = u0 - oA, ub = u1 + oB;
        float ra = hipA ? u0 + dA : ua, rb = hipB ? u1 - dB : ub;
        Vector3 e00 = P(ua, w0 - o, edgeY), e10 = P(ub, w0 - o, edgeY), e11 = P(ub, w1 + o, edgeY), e01 = P(ua, w1 + o, edgeY);
        // A gable end without a hip: the verge rises from the eave corner to the ridge's end at the same u.
        if (!hipA)
        {
            e00 = P(ua, w0 - o, edgeY);
            e01 = P(ua, w1 + o, edgeY);
        }

        Vector3 r0 = P(ra, wc, ridge), r1 = P(rb, wc, ridge);
        RoofMesh m = new RoofMesh();
        m.Face(slate, e00, e10, r1, r0);
        m.Face(slate, e11, e01, r0, r1);
        if (hipA)
        {
            m.Face(slate, e01, e00, r0);
        }
        else
        {
            m.Edge(e00, r0);
            m.Edge(r0, e01);
        }

        if (hipB)
        {
            m.Face(slate, e10, e11, r1);
        }

        m.Edge(e00, e10);
        m.Edge(e11, e01);
        if (hipA)
        {
            m.Edge(e01, e00);
        }

        if (hipB)
        {
            m.Edge(e10, e11);
        }

        m.Build(name, parent, slate, collider);
    }

    // A pyramid roof over a convex outline: a face from every edge up to the apex over the outline's centre; the eaves
    // are the outline scaled out about the centre, so every face continues its own plane over the overhang.
    private static void Pyramid(string name, Transform parent, Vector2[] outline, float eave, float apex, Material slate)
    {
        Vector2 c = Vector2.zero;
        foreach (Vector2 q in outline)
        {
            c += q;
        }

        c /= outline.Length;
        float radius = 0f;
        foreach (Vector2 q in outline)
        {
            radius += Vector2.Distance(q, c);
        }

        radius /= outline.Length;
        float k = 1f + RoofOverhang / radius;
        float y = apex - k * (apex - eave);
        RoofMesh m = new RoofMesh();
        Vector3 top = new Vector3(c.x, apex, c.y);
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = c + (outline[i] - c) * k, b = c + (outline[(i + 1) % outline.Length] - c) * k;
            if (Vector2.Distance(a, b) < 0.02f)
            {
                continue;
            }

            Vector3 pa = new Vector3(a.x, y, a.y), pb = new Vector3(b.x, y, b.y);
            m.Face(slate, pa, pb, top);
            m.Edge(pa, pb);
        }

        m.Build(name, parent, slate, false);
    }

    // A lean-to over a rectangle running along z, falling to `side` (-1 west, 1 east) from `high` to `low` at the wall.
    private static void LeanTo(string name, Transform parent, Rect area, float side, float low, float high, Material slate)
    {
        float o = RoofOverhang;
        float slope = (high - low) / area.width;
        float xh = side < 0f ? area.xMax : area.xMin, xl = side < 0f ? area.xMin - o : area.xMax + o;
        float yl = low - o * slope;
        RoofMesh m = new RoofMesh();
        Vector3 a = new Vector3(xl, yl, area.yMin - o), b = new Vector3(xl, yl, area.yMax + o), c2 = new Vector3(xh, high, area.yMax + o), d = new Vector3(xh, high, area.yMin - o);
        m.Face(slate, a, b, c2, d);
        m.Edge(a, b);
        m.Edge(b, c2);
        m.Edge(d, a);
        m.Build(name, parent, slate, false);
        // The triangles under the ends, closed in the facade's plaster.
        foreach (float z in new[] { area.yMin, area.yMax })
        {
            Vector2[] tri = { V(xh, low), V(xh, high), V(side < 0f ? area.xMin : area.xMax, low) };
            GameObject end = Slab("LeanToEnd", parent, tri, -0.1f, 0.1f, p.wall, p.wall, p.wall, false);
            end.transform.SetPositionAndRotation(manor.TransformPoint(new Vector3(0f, 0f, z)), manor.rotation * Quaternion.Euler(-90f, 0f, 0f));
        }
    }

    // The attic's walls (walkable): knee walls along both eaves and under the south hip, and the wall where the attic
    // meets the north wing, in the profile of the wing's own slopes (so it stays inside the roof).
    private static void Knees(string name, Transform parent, Rect area, float floor, float knee, float ridge)
    {
        Transform g = Group(name, parent);
        Vector2 c = area.center;
        float half = area.width * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            Block("Knee", g, new Vector3(c.x + side * (half - 0.1f), floor + knee * 0.5f, c.y), 0f, new Vector3(0.2f, knee, area.height), p.ceiling, true);
        }

        Block("KneeSouth", g, new Vector3(c.x, floor + knee * 0.5f, area.yMin + 0.1f), 0f, new Vector3(area.width, knee, 0.2f), p.ceiling, true);
        Vector2[] outline = { V(-half, 0f), V(half, 0f), V(half, knee), V(0f, ridge - floor), V(-half, knee) };
        GameObject wall = Slab("AtticEnd", g, outline, -0.1f, 0.1f, p.ceiling, p.ceiling, p.ceiling, true);
        wall.transform.SetPositionAndRotation(manor.TransformPoint(new Vector3(c.x, floor, area.yMax - 0.1f)), manor.rotation * Quaternion.Euler(-90f, 0f, 0f));
    }

    // The hall's gable wall facing the front: the cream plaster triangle over a frieze (from `bottom`), its mouldings,
    // and the gilded tracery of the concept art set in it.
    private static void Pediment(string name, Transform parent, float z, float halfWidth, float bottom, float eave, float ridge)
    {
        Transform g = Group(name, parent);
        float slope = (ridge - eave) / halfWidth;
        Vector2[] outline = { V(-halfWidth, bottom), V(halfWidth, bottom), V(halfWidth, eave), V(0f, ridge), V(-halfWidth, eave) };
        GameObject wall = Slab("Wall", g, outline, -0.12f, 0.12f, p.wall, p.wall, p.wall, false);
        wall.transform.SetPositionAndRotation(manor.TransformPoint(new Vector3(0f, 0f, z + 0.15f)), manor.rotation * Quaternion.Euler(-90f, 0f, 0f));
        // Mouldings: the cornice along the frieze's top and up the two rakes, in the trim's dark wood like the art.
        float front = z + 0.15f - 0.12f;
        Block("Cornice", g, new Vector3(0f, eave - 0.06f, front - 0.04f), 0f, new Vector3(halfWidth * 2f + 0.1f, 0.14f, 0.1f), Or(p.plinth, p.stone), false);
        float rake = Mathf.Sqrt(halfWidth * halfWidth + (ridge - eave) * (ridge - eave));
        float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject r = Block("Rake", g, new Vector3(side * halfWidth * 0.5f, (eave + ridge) * 0.5f - 0.04f, front - 0.04f), 0f, new Vector3(rake + 0.1f, 0.14f, 0.1f), Or(p.plinth, p.stone), false);
            r.transform.rotation = manor.rotation * Quaternion.Euler(0f, 0f, -side * angle);
        }

        if (p.gilt == null)
        {
            return;
        }

        // The tracery: one triangle textured with the ornament (SourceAssets/Tools/house_art_textures.py draws it in a
        // 2:1 box, its triangle from (0.03, 0.04) to (0.97, 0.04) and the apex at (0.5, 0.94)), set inside the rakes.
        float inset = 0.35f;
        float baseHalf = halfWidth - inset * 1.6f, baseY = eave + 0.12f, topY = ridge - inset * 1.5f;
        Mesh mesh = new Mesh { name = "PedimentTracery" };
        float zf = front - 0.015f;
        mesh.SetVertices(new List<Vector3> { new Vector3(-baseHalf, baseY, zf), new Vector3(baseHalf, baseY, zf), new Vector3(0f, topY, zf) });
        mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back });
        mesh.SetUVs(0, new List<Vector2> { new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.04f), new Vector2(0.5f, 0.94f) });
        mesh.SetTriangles(new[] { 0, 2, 1 }, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        GameObject tracery = new GameObject("Tracery");
        tracery.transform.SetParent(g, false);
        tracery.transform.SetPositionAndRotation(manor.position, manor.rotation);
        tracery.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = tracery.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = p.gilt;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    // The porch canopy: a half bell of slate from the wall (high) out to the rim over the columns (low), flaring.
    private static void Canopy(string name, Transform parent, Vector2 centre, float radius, float rimY, float topY)
    {
        RoofMesh m = new RoofMesh();
        const int around = 14, rings = 5;
        Vector3 Point(int i, int k)
        {
            float a = Mathf.Lerp(180f, 360f, i / (float)around) * Mathf.Deg2Rad;
            float t = k / (float)rings;
            float r = Mathf.Lerp(0.25f, radius + 0.15f, t);
            // Steep near the wall, flatter toward the rim (a bell): y follows (1 - t)^1.6.
            float y = rimY + (topY - rimY) * Mathf.Pow(1f - t, 1.6f);
            return new Vector3(centre.x + Mathf.Cos(a) * r, y, centre.y + Mathf.Sin(a) * r);
        }

        Material slate = Or(p.slate, p.roof);
        for (int i = 0; i < around; i++)
        {
            for (int k = 0; k < rings; k++)
            {
                m.Face(slate, Point(i, k + 1), Point(i + 1, k + 1), Point(i + 1, k), Point(i, k));
            }

            m.Edge(Point(i, rings), Point(i + 1, rings));
        }

        m.Build(name, parent, slate, false);
    }

    // ---------------------------------------------------------------- outbuildings

    private static void Outbuildings()
    {
        Transform ob = Group("Outbuildings", manor);
        float bottom = -0.25f - o.footing;
        // Alex's lab in the old bunker: thick concrete, titanium doors (locked in the demo).
        Rect bk = Bunker;
        Floor("Bunker", ob, Box(bk), p.concrete);
        Slab("Bunker_Roof", ob, Box(R(bk.xMin - 0.2f, bk.yMin - 0.2f, bk.xMax + 0.2f, bk.yMax + 0.2f)), 3.2f, 3.6f, p.concrete, p.concrete, p.concrete, true);
        Walls("Bunker", ob, new[] { V(19.6f, bk.yMin), V(bk.xMax, bk.yMin), V(bk.xMax, bk.yMax), V(bk.xMin, bk.yMax), V(bk.xMin, bk.yMin) }, false,
            bottom, 3.2f, 0.5f, null, null, Hole.Door(V(18.2f, bk.yMax), 1f, 2.2f, false));
        // The narrow corridor north to the store.
        Rect c1 = R(17.6f, bk.yMax, 18.8f, 28.6f);
        Rect c2 = R(15f, 27.4f, 17.6f, 28.6f);
        Floor("Corridor1", ob, Box(c1), p.concrete);
        Floor("Corridor2", ob, Box(c2), p.concrete);
        Slab("Corridor_Roof1", ob, Box(R(c1.xMin - 0.2f, c1.yMin, c1.xMax + 0.2f, c1.yMax + 0.2f)), 2.6f, 2.9f, p.concrete, p.concrete, p.concrete, true);
        Slab("Corridor_Roof2", ob, Box(R(c2.xMin, c2.yMin - 0.2f, c2.xMax, c2.yMax + 0.2f)), 2.6f, 2.9f, p.concrete, p.concrete, p.concrete, true);
        Walls("Corridor", ob, new[] { V(c1.xMin, c1.yMin), V(c1.xMin, c2.yMin), V(c2.xMin, c2.yMin) }, false, bottom, 2.6f, 0.25f, null, null);
        Walls("Corridor2", ob, new[] { V(c2.xMin, c2.yMax), V(c1.xMax, c2.yMax), V(c1.xMax, c1.yMin) }, false, bottom, 2.6f, 0.25f, null, null);

        // The store, with a wide door to the north and the titanium door 2 on the east (both locked).
        Rect d = Deposito;
        Floor("Deposito", ob, Box(d), p.concrete);
        Slab("Deposito_Roof", ob, Box(R(d.xMin - 0.3f, d.yMin - 0.3f, d.xMax + 0.3f, d.yMax + 0.3f)), 3.6f, 3.9f, Or(p.slate, p.roof), p.concrete, p.trim, true);
        Walls("Deposito", ob, new[] { V(d.xMin, d.yMin), V(d.xMax, d.yMin), V(d.xMax, d.yMax), V(d.xMin, d.yMax) }, true, bottom, 3.6f, Ext, null, null,
            Hole.Door(V(15f, 28f), 1f, 2.2f, false), Hole.Door(V(10.65f, d.yMax), 3.9f, 2.8f, false), Hole.Window(V(d.xMin, 30f), 1.2f));
        Block("DepositoDoor", ob, new Vector3(10.65f, 1.4f, d.yMax), 0f, new Vector3(3.9f, 2.8f, 0.12f), p.metal, true);
        Block("TitaniumDoor2", ob, new Vector3(15f, 1.1f, 28f), 0f, new Vector3(0.1f, 2.2f, 1f), p.metal, true);

        // The boat garage, open to the sea on the north (the slipway), a door on the south.
        Rect gg = Garagem;
        Floor("Garagem", ob, Box(gg), p.concrete);
        Slab("Garagem_Roof", ob, Box(R(gg.xMin - 0.3f, gg.yMin - 0.3f, gg.xMax + 0.3f, gg.yMax + 0.3f)), 4.2f, 4.5f, Or(p.slate, p.roof), p.ceiling, p.trim, true);
        Walls("Garagem", ob, new[] { V(gg.xMin, gg.yMin), V(gg.xMax, gg.yMin), V(gg.xMax, gg.yMax), V(gg.xMin, gg.yMax) }, true, bottom, 4.2f, Ext, null, null,
            Hole.Door(V(-10.6f, gg.yMin), 2f, 2.4f), Hole.Open(V(-10.8f, gg.yMax), 5.4f, -0.25f, 3.6f), Hole.Window(V(gg.xMin, 30f), 1.2f), Hole.Window(V(gg.xMax, 30f), 1.2f));
        // The boat's trailer (the boat itself is a model the game places).
        Block("Trailer", ob, new Vector3(-10.8f, 0.12f, 30.2f), 0f, new Vector3(1.6f, 0.24f, 5.6f), p.metal, true);

        // The dirt road from the vestibule's door down to the sea.
        Slab("Road", ob, Box(R(-1.5f, 26.8f, 1.7f, 34.5f)), GardenY - 0.2f, GardenY + 0.02f, p.garden, p.garden, p.garden, true);
        Block("RoadStep", ob, new Vector3(0.1f, -0.13f, 27.05f), 0f, new Vector3(2f, 0.26f, 0.5f), p.stone, true);
    }

    // ---------------------------------------------------------------- where it rains (local rectangles open to the sky)

    // Rain falls from 14 m and does not collide: only over ground with nothing above it. Outside the house's
    // bounding box all of it; inside, the courtyard garden, the open south gallery and the gaps between the wings.
    public static Rect[] RainAreas()
    {
        return new[]
        {
            R(-40f, 35f, 45f, 55f), R(-40f, -25f, 45f, -3.6f), R(-40f, -3.6f, -20f, 35f), R(24.2f, -3.6f, 45f, 35f),
            R(-20f, -3.6f, -11.8f, 3.5f), R(-11.6f, -3.6f, -3.3f, 0.7f), R(3.3f, -3.6f, 11.6f, 0.7f), R(11.8f, -3.6f, 24.2f, 3.2f),
            R(20.2f, 3.2f, 24.2f, 15.2f), R(15.3f, 15.6f, 16.9f, 25f), R(-20f, 16.2f, -14.9f, 35f), R(-14.6f, 24.9f, -7f, 26.3f),
            R(-6.6f, 24.9f, -5.2f, 35f), R(-5.2f, 27.1f, 6.8f, 35f), R(5.4f, 24.9f, 6.8f, 26.1f), R(7f, 24.9f, 15f, 26.1f),
            R(7f, 34f, 24.2f, 35f), R(15.3f, 23.8f, 17.4f, 27.1f), R(15.3f, 28.9f, 24.2f, 34f), R(19.1f, 23.8f, 24.2f, 28.9f),
            R(-7.3f, 11.6f, 7.7f, 19f), R(-7.4f, 9.2f, -2.7f, 11.3f), R(3.1f, 9.2f, 7.8f, 11.3f),
        };
    }

    // ---------------------------------------------------------------- pieces

    private struct Hole
    {
        public Vector2 at;
        public float width, bottom, top;
        public int kind; // 0 open, 1 door, 2 window
        public bool frame;
        // Windows: 0 a cross (mullion and transom), 1 a grid of cols x rows small panes, 2 stained glass (no bars: the
        // lead is in the glass), 3 an elongated octagon (corners cut) with a grid.
        public int style, cols, rows;

        public static Hole Open(Vector2 at, float width, float bottom, float top) => new Hole { at = at, width = width, bottom = bottom, top = top, kind = 0 };

        public static Hole Door(Vector2 at, float width, float top, bool frame = true, float bottom = 0f) =>
            new Hole { at = at, width = width, bottom = bottom, top = top, kind = 1, frame = frame };

        // A doorway with a half-circle top: the wall is cut as a rectangle up to the arch's top and the two corners
        // above the arc are filled back (Spandrels). No frame: the door model brings its own.
        public static Hole Arch(Vector2 at, float width, float spring) =>
            new Hole { at = at, width = width, bottom = 0f, top = spring + width * 0.5f, kind = 1, frame = false, style = 4 };

        public static Hole Window(Vector2 at, float width, float bottom = 0.9f) =>
            new Hole { at = at, width = width, bottom = bottom, top = bottom + 1.5f, kind = 2, frame = true };

        public static Hole Grid(Vector2 at, float width, float bottom, float top, int cols, int rows) =>
            new Hole { at = at, width = width, bottom = bottom, top = top, kind = 2, frame = true, style = 1, cols = cols, rows = rows };

        public static Hole Stained(Vector2 at, float width, float bottom, float top) =>
            new Hole { at = at, width = width, bottom = bottom, top = top, kind = 2, frame = true, style = 2 };

        public static Hole Octagon(Vector2 at, float width, float bottom, float top) =>
            new Hole { at = at, width = width, bottom = bottom, top = top, kind = 2, frame = true, style = 3, cols = 2, rows = 4 };
    }

    private static Transform Group(string name, Transform parent)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static Vector2 V(float x, float z) => new Vector2(x, z);

    private static Vector2[] Box(Rect r) => new[] { V(r.xMin, r.yMin), V(r.xMax, r.yMin), V(r.xMax, r.yMax), V(r.xMin, r.yMax) };

    // Points on an ellipse arc (degrees, counter-clockwise from +x), both ends included.
    private static List<Vector2> Arc(Vector2 c, float rx, float rz, float from, float to, int segments)
    {
        List<Vector2> pts = new List<Vector2>();
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad;
            pts.Add(c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz));
        }

        return pts;
    }

    private static void Floor(string name, Transform parent, Vector2[] outline, Material top)
    {
        Slab("Floor_" + name, parent, outline, -0.25f, 0f, top, p.concrete, p.concrete, true);
    }

    private static void Column(string name, Transform parent, Vector2 at, float y0, float y1, float diameter)
    {
        GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        col.name = name;
        col.transform.SetParent(parent, false);
        col.transform.localPosition = new Vector3(at.x, (y0 + y1) * 0.5f, at.y);
        col.transform.localScale = new Vector3(diameter, (y1 - y0) * 0.5f, diameter);
        col.GetComponent<MeshRenderer>().sharedMaterial = p.stone;
        Static(col);
        Block(name + "_Base", parent, new Vector3(at.x, y0 + 0.1f, at.y), 0f, new Vector3(diameter + 0.14f, 0.2f, diameter + 0.14f), p.stone, false);
        Block(name + "_Cap", parent, new Vector3(at.x, y1 - 0.1f, at.y), 0f, new Vector3(diameter + 0.14f, 0.2f, diameter + 0.14f), p.stone, false);
    }

    // A box centred at a local point, turned `yaw` degrees about y, with UVs in the manor's space (neighbours tile on).
    private static GameObject Block(string name, Transform parent, Vector3 centre, float yaw, Vector3 size, Material material, bool collider)
    {
        return Block(name, parent, centre, yaw, size, material, material, collider);
    }

    private static GameObject Block(string name, Transform parent, Vector3 centre, float yaw, Vector3 size, Material left, Material right, bool collider)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = manor.TransformPoint(centre);
        go.transform.rotation = manor.rotation * Quaternion.Euler(0f, yaw, 0f);
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        float tl = Tile(left), tr = Tile(right);
        Vector3 h = size * 0.5f;
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> trisL = new List<int>(), trisR = new List<int>();
        Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 n in faces)
        {
            Vector3 u = n.x != 0f ? Vector3.forward * n.x : (n.y != 0f ? Vector3.right : Vector3.right * -n.z);
            Vector3 v = n.y != 0f ? Vector3.forward * n.y : Vector3.up;
            // The +z face is the wall's left side (seen walking from its first point to its second), -z the right.
            bool useRight = n.z < 0f;
            float tile = useRight ? tr : tl;
            int start = verts.Count;
            for (int i = 0; i < 4; i++)
            {
                float su = (i == 1 || i == 2) ? 1f : -1f;
                float sv = i >= 2 ? 1f : -1f;
                Vector3 corner = Vector3.Scale(n + u * su + v * sv, h);
                verts.Add(corner);
                norms.Add(n);
                Vector3 world = Sc(centre + turn * corner);
                Vector3 wu = turn * u, wv = turn * v;
                uvs.Add(new Vector2(Vector3.Dot(world, wu), Vector3.Dot(world, wv)) / tile);
            }

            (useRight ? trisR : trisL).AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        bool two = left != right && trisR.Count > 0;
        mesh.subMeshCount = two ? 2 : 1;
        if (two)
        {
            mesh.SetTriangles(trisL, 0);
            mesh.SetTriangles(trisR, 1);
        }
        else
        {
            trisL.AddRange(trisR);
            mesh.SetTriangles(trisL, 0);
        }

        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = two ? new[] { left, right } : new[] { left };
        if (collider)
        {
            go.AddComponent<BoxCollider>();
        }

        Static(go);
        return go;
    }

    // A right-triangle prism in the wall's frame (x along the run, y up, z across), its right angle at (s, y) and its
    // legs `cut` long toward sx (along the run) and sy (up or down): the solid corner of an octagonal window. Faces
    // +z (left material) and -z (right) with the same world-planar UVs as Block, and the diagonal cut face.
    private static GameObject CornerWedge(string name, Transform group, Vector2 a, Vector2 dir, float yaw, float s, float y, float sx, float sy, float cut, float thickness, Material left, Material right)
    {
        Vector2 c = a + dir * s;
        Vector3 centre = new Vector3(c.x, y, c.y);
        GameObject go = new GameObject(name);
        go.transform.SetParent(group, false);
        go.transform.position = manor.TransformPoint(centre);
        go.transform.rotation = manor.rotation * Quaternion.Euler(0f, yaw, 0f);
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        float tl = Tile(left), tr = Tile(right);
        float hz = thickness * 0.5f;
        Vector3[] tri = { Vector3.zero, new Vector3(sx * cut, 0f, 0f), new Vector3(0f, sy * cut, 0f) };
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> trisL = new List<int>(), trisR = new List<int>();
        System.Action<Vector3[], Vector3, Vector3, Vector3, float, List<int>> face = (pts, n, u, v, tile, tris) =>
        {
            int start = verts.Count;
            foreach (Vector3 pnt in pts)
            {
                verts.Add(pnt);
                norms.Add(n);
                Vector3 world = Sc(centre + turn * pnt);
                uvs.Add(new Vector2(Vector3.Dot(world, turn * u), Vector3.Dot(world, turn * v)) / tile);
            }

            for (int i = 1; i + 1 < pts.Length; i++)
            {
                Vector3 cross = Vector3.Cross(pts[i] - pts[0], pts[i + 1] - pts[0]);
                if (Vector3.Dot(cross, n) >= 0f) { tris.AddRange(new[] { start, start + i, start + i + 1 }); }
                else { tris.AddRange(new[] { start, start + i + 1, start + i }); }
            }
        };
        foreach (float sz in new[] { 1f, -1f })
        {
            Vector3 n = Vector3.forward * sz;
            Vector3[] pts = { tri[0] + n * hz, tri[1] + n * hz, tri[2] + n * hz };
            face(pts, n, Vector3.right * -sz, Vector3.up, sz > 0f ? tl : tr, sz > 0f ? trisL : trisR);
        }

        // The diagonal cut, facing the opening.
        {
            Vector3 n = new Vector3(sx, sy, 0f).normalized;
            Vector3 along = (tri[2] - tri[1]).normalized;
            Vector3[] pts = { tri[1] - Vector3.forward * hz, tri[2] - Vector3.forward * hz, tri[2] + Vector3.forward * hz, tri[1] + Vector3.forward * hz };
            face(pts, n, along, Vector3.forward, tl, trisL);
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        bool two = left != right && trisR.Count > 0;
        mesh.subMeshCount = two ? 2 : 1;
        if (two)
        {
            mesh.SetTriangles(trisL, 0);
            mesh.SetTriangles(trisR, 1);
        }
        else
        {
            trisL.AddRange(trisR);
            mesh.SetTriangles(trisL, 0);
        }

        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = two ? new[] { left, right } : new[] { left };
        Static(go);
        return go;
    }

    // Walls along a polyline (closed or not), centred on the line, `left`/`right` the materials of each face,
    // cut round the holes that lie on a segment (doors, windows with glass and frame, open passages).
    // Walls along a polyline (closed or not), centred on the line, cut round the holes that lie on a segment (doors,
    // windows with glass and frame, open passages). left/right: the materials of each face (seen walking from a point
    // to the next); null for both = each face takes the finish of the room it faces (Rooms()), the wall split where
    // the room changes, with its skirting, wainscot and cornice.
    private static GameObject Walls(string name, Transform parent, Vector2[] pts, bool closed, float y0, float y1, float thickness, Material left, Material right, params Hole[] holes)
    {
        Transform group = Group(name, parent);
        List<Vector2> clean = new List<Vector2>();
        foreach (Vector2 q in pts)
        {
            if (clean.Count == 0 || Vector2.Distance(clean[clean.Count - 1], q) > 0.01f)
            {
                clean.Add(q);
            }
        }

        if (closed && clean.Count > 2 && Vector2.Distance(clean[0], clean[clean.Count - 1]) < 0.01f)
        {
            clean.RemoveAt(clean.Count - 1);
        }

        pts = clean.ToArray();
        bool auto = left == null && right == null;
        int level = y0 > 3f ? 1 : 0;
        int count = closed ? pts.Length : pts.Length - 1;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % pts.Length];
            float len = Vector2.Distance(a, b);
            if (len < 0.01f)
            {
                continue;
            }

            Vector2 dir = (b - a) / len;
            // Extend into the joints just enough to close the corner.
            float extA = 0f, extB = 0f;
            if (closed || i > 0)
            {
                Vector2 prev = pts[(i - 1 + pts.Length) % pts.Length];
                extA = JointExtension(prev, a, b, thickness);
            }

            if (closed || i < count - 1)
            {
                Vector2 next = pts[(i + 2) % pts.Length];
                extB = JointExtension(a, b, next, thickness);
            }

            List<Hole> mine = new List<Hole>();
            foreach (Hole h in holes)
            {
                Vector2 rel = h.at - a;
                float along = Vector2.Dot(rel, dir);
                float off = Mathf.Abs(dir.x * rel.y - dir.y * rel.x);
                if (off < thickness + 0.25f && along > -0.01f && along < len + 0.01f)
                {
                    Hole local = h;
                    local.at = new Vector2(along, 0f);
                    mine.Add(local);
                }
            }

            if (!auto)
            {
                Segment(group, name + "_" + i, a, dir, -extA, len + extB, y0, y1, thickness, left, right, TrimNone, TrimNone, level, mine);
                continue;
            }

            // Which rooms each face looks into, sampled along the segment; a new run wherever that changes.
            Vector2 normal = new Vector2(-dir.y, dir.x);
            float probe = thickness * 0.5f + 0.2f;
            int piece = 0;
            int samples = Mathf.Max(1, Mathf.CeilToInt(len / 0.2f));
            float stepLength = len / samples;
            float runStart = -extA;
            Room? runL = RoomAt(a + dir * (stepLength * 0.5f) + normal * probe, level);
            Room? runR = RoomAt(a + dir * (stepLength * 0.5f) - normal * probe, level);
            for (int k = 1; k < samples; k++)
            {
                Vector2 at = a + dir * ((k + 0.5f) * stepLength);
                Room? l = RoomAt(at + normal * probe, level);
                Room? r = RoomAt(at - normal * probe, level);
                if (Same(l, runL) && Same(r, runR))
                {
                    continue;
                }

                float boundary = k * stepLength;
                Emit(group, name + "_" + i + "_" + piece++, a, dir, runStart, boundary, y0, y1, thickness, runL, runR, level, mine);
                runStart = boundary;
                runL = l;
                runR = r;
            }

            Emit(group, name + "_" + i + "_" + piece++, a, dir, runStart, len + extB, y0, y1, thickness, runL, runR, level, mine);
        }

        return group.gameObject;
    }

    private static bool Same(Room? x, Room? y) => x.HasValue == y.HasValue && (!x.HasValue || x.Value.name == y.Value.name);

    private static void Emit(Transform group, string name, Vector2 a, Vector2 dir, float r0, float r1, float y0, float y1, float thickness, Room? left, Room? right, int level, List<Hole> holes)
    {
        if (r1 - r0 < 0.005f)
        {
            return;
        }

        Segment(group, name, a, dir, r0, r1, y0, y1, thickness, left.HasValue ? left.Value.wall : p.wall, right.HasValue ? right.Value.wall : p.wall,
            left.HasValue ? left.Value.trim : TrimNone, right.HasValue ? right.Value.trim : TrimNone, level, holes);
    }

    private static float JointExtension(Vector2 a, Vector2 b, Vector2 c, float thickness)
    {
        Vector2 d0 = (b - a).normalized, d1 = (c - b).normalized;
        float turn = Vector2.Angle(d0, d1) * Mathf.Deg2Rad;
        return Mathf.Min(thickness * 0.5f * Mathf.Abs(Mathf.Tan(turn * 0.5f)) + 0.002f, thickness);
    }

    // One straight run of wall between r0 and r1 (metres along `dir` from `a`), cut round its holes; the trim of each
    // face follows the solid pieces (none across a doorway, skirting under a window).
    private static void Segment(Transform group, string name, Vector2 a, Vector2 dir, float r0, float r1, float y0, float y1, float thickness,
        Material left, Material right, int trimLeft, int trimRight, int level, List<Hole> allHoles)
    {
        List<Hole> holes = new List<Hole>();
        foreach (Hole h in allHoles)
        {
            if (h.at.x + h.width * 0.5f > r0 && h.at.x - h.width * 0.5f < r1)
            {
                holes.Add(h);
            }
        }

        holes.Sort((x, y) => x.at.x.CompareTo(y.at.x));
        float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        float floor = level == 0 ? 0f : UpperFloor;
        float ceil = level == 0 ? GroundCeiling : UpperCeiling;
        int piece = 0;
        System.Action<float, float, float, float> part = (s0, s1, b0, b1) =>
        {
            if (s1 - s0 < 0.005f || b1 - b0 < 0.005f)
            {
                return;
            }

            Vector2 mid = a + dir * ((s0 + s1) * 0.5f);
            Block(name + "_" + piece++, group, new Vector3(mid.x, (b0 + b1) * 0.5f, mid.y), yaw, new Vector3(s1 - s0, b1 - b0, thickness), left, right, true);
            if (o.interior)
            {
                WallTrim(a, dir, normal, yaw, s0, s1, b0, b1, floor, ceil, thickness, trimLeft);
                WallTrim(a, dir, -normal, yaw, s0, s1, b0, b1, floor, ceil, thickness, trimRight);
            }

            // The facade's ochre plinth (concept art): a band proud of the cream plaster from the ground to 60 cm.
            if (p.plinth != null && level == 0 && b0 < PlinthTop)
            {
                foreach ((Material face, Vector2 side) in new[] { (left, normal), (right, -normal) })
                {
                    if (face != p.wall)
                    {
                        continue;
                    }

                    Vector2 c = a + dir * ((s0 + s1) * 0.5f) + side * (thickness * 0.5f + 0.015f);
                    float top = Mathf.Min(b1, PlinthTop);
                    Block("Plinth", trimParts, new Vector3(c.x, (b0 + top) * 0.5f, c.y), yaw, new Vector3(s1 - s0 + 0.03f, top - b0, 0.03f), p.plinth, false);
                }
            }
        };

        float cursor = r0;
        foreach (Hole h in holes)
        {
            float from = Mathf.Max(r0, h.at.x - h.width * 0.5f), to = Mathf.Min(r1, h.at.x + h.width * 0.5f);
            if (to <= from)
            {
                continue;
            }

            part(cursor, from, y0, y1);
            part(from, to, y0, Mathf.Max(y0, h.bottom));
            part(from, to, Mathf.Min(y1, h.top), y1);
            if (h.kind == 1 && h.style == 4)
            {
                Spandrels(group, name, a, dir, yaw, from, to, h, thickness, left, right);
            }

            cursor = Mathf.Max(cursor, to);
            if ((o.interior || h.kind == 2) && h.at.x - h.width * 0.5f >= r0 - 0.01f && h.at.x + h.width * 0.5f <= r1 + 0.01f)
            {
                Trim(group, name, a, dir, yaw, from, to, h, thickness, left, right);
            }
        }

        part(cursor, r1, y0, y1);
    }

    // Skirting (18 cm), wainscot (panels to 95 cm with a rail) and cornice (16 cm with a step) on one face of a solid
    // piece of wall, in the painted wood of the frames. Collected and merged into one mesh per material (CombineTrim).
    private static Transform trimParts;

    private static void WallTrim(Vector2 a, Vector2 dir, Vector2 face, float yaw, float s0, float s1, float b0, float b1, float floor, float ceil, float thickness, int style)
    {
        if (style == TrimNone || s1 - s0 < 0.05f)
        {
            return;
        }

        float midS = (s0 + s1) * 0.5f, length = s1 - s0;
        Vector2 at = a + dir * midS + face * (thickness * 0.5f);
        System.Action<float, float, float, Material> strip = (y, h, depth, material) =>
        {
            Vector2 c = at + face * (depth * 0.5f);
            Block("Trim", trimParts, new Vector3(c.x, y + h * 0.5f, c.y), yaw, new Vector3(length, h, depth), material, false);
        };
        if (b0 <= floor + 0.02f && b1 > floor + 0.18f)
        {
            if (style == TrimDado && p.goldPaper != null)
            {
                // The concept art's hall: the golden paper up to the rail, the boards above.
                float top = Mathf.Min(b1, floor + DadoHeight);
                strip(floor, top - floor, 0.008f, p.goldPaper);
                if (b1 >= floor + DadoHeight + 0.03f)
                {
                    strip(floor + DadoHeight - 0.04f, 0.08f, 0.04f, p.trim);
                }
            }

            if (style == TrimWainscot && b1 > floor + 0.5f)
            {
                float top = Mathf.Min(b1, floor + 0.95f);
                strip(floor, top - floor, 0.012f, p.trim);
                if (b1 >= floor + 0.98f)
                {
                    strip(floor + 0.9f, 0.07f, 0.035f, p.trim);
                }
            }

            strip(floor, 0.18f, 0.022f, p.trim);
        }

        if (b1 >= ceil - 0.02f && b0 < ceil - 0.2f)
        {
            strip(ceil - 0.16f, 0.16f, 0.04f, p.stone);
            strip(ceil - 0.07f, 0.07f, 0.075f, p.stone);
        }
    }

    // The trim pieces as one mesh per material (thousands of small boxes would each be an object and a draw call).
    private static void CombineTrim()
    {
        Dictionary<Material, List<CombineInstance>> byMaterial = new Dictionary<Material, List<CombineInstance>>();
        Matrix4x4 toManor = manor.worldToLocalMatrix;
        foreach (MeshFilter f in trimParts.GetComponentsInChildren<MeshFilter>())
        {
            Material m = f.GetComponent<MeshRenderer>().sharedMaterial;
            if (!byMaterial.TryGetValue(m, out List<CombineInstance> list))
            {
                byMaterial[m] = list = new List<CombineInstance>();
            }

            list.Add(new CombineInstance { mesh = f.sharedMesh, transform = toManor * f.transform.localToWorldMatrix });
        }

        Transform trim = Group("Trim", manor);
        foreach (KeyValuePair<Material, List<CombineInstance>> kv in byMaterial)
        {
            Mesh mesh = new Mesh { name = "Trim_" + kv.Key.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            GameObject go = new GameObject("Trim_" + kv.Key.name);
            go.transform.SetParent(trim, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            Static(go);
        }

        Object.DestroyImmediate(trimParts.gameObject);
    }

    // The wall above a round arch (Hole.Arch): the two corners between the rectangle cut for the hole and the half
    // circle, as one slab the wall's thickness, both faces and the arc's underside (intrados). Built in the wall's
    // frame (x along the run, y up, z across), UVs in world space at the material's tile like Block.
    private static void Spandrels(Transform group, string name, Vector2 a, Vector2 dir, float yaw, float from, float to, Hole h, float thickness, Material left, Material right)
    {
        float radius = (to - from) * 0.5f;
        float spring = h.top - radius;
        float cx = (from + to) * 0.5f;
        Vector2 mid = a + dir * cx;
        Vector3 centre = new Vector3(mid.x, (spring + h.top) * 0.5f, mid.y);
        GameObject go = new GameObject(name + "_Arch");
        go.transform.SetParent(group, false);
        go.transform.position = manor.TransformPoint(centre);
        go.transform.rotation = manor.rotation * Quaternion.Euler(0f, yaw, 0f);
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        float tl = Tile(left), tr = Tile(right);
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> trisL = new List<int>(), trisR = new List<int>();
        const int steps = 16;
        float hz = thickness * 0.5f;
        float hy = radius * 0.5f;
        // Local y of the spring line and the top, relative to the slab's centre.
        float ys = -hy, yt = hy;
        System.Func<Vector3, Vector3, float, Vector2> uv = (local, n, tile) =>
        {
            Vector3 world = Sc(centre + turn * local);
            Vector3 u = Mathf.Abs(n.z) > 0.5f ? Vector3.right * (n.z > 0f ? 1f : -1f) : Vector3.Cross(Vector3.up, n);
            Vector3 wu = turn * u, wv = Vector3.up;
            return new Vector2(Vector3.Dot(world, wu), Vector3.Dot(world, wv)) / tile;
        };
        // Faces: +z (left side of the wall) and -z (right side), each with a left and a right spandrel fan.
        foreach (float sz in new[] { 1f, -1f })
        {
            Vector3 n = Vector3.forward * sz;
            List<int> tris = sz > 0f ? trisL : trisR;
            float tile = sz > 0f ? tl : tr;
            foreach (float side in new[] { -1f, 1f })
            {
                int corner = verts.Count;
                verts.Add(new Vector3(side * radius, yt, sz * hz)); norms.Add(n); uvs.Add(uv(verts[corner], n, tile));
                verts.Add(new Vector3(side * radius, ys, sz * hz)); norms.Add(n); uvs.Add(uv(verts[corner + 1], n, tile));
                int first = verts.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float ang = Mathf.PI * 0.5f * (i / (float)steps);
                    Vector3 pnt = new Vector3(side * Mathf.Cos(ang) * radius, ys + Mathf.Sin(ang) * radius, sz * hz);
                    verts.Add(pnt); norms.Add(n); uvs.Add(uv(pnt, n, tile));
                }

                // Fan from the corner: (corner, bottom corner, arc 0), then along the arc to the apex.
                bool flip = (sz > 0f) == (side > 0f);
                for (int i = 0; i < steps; i++)
                {
                    int p0 = first + i, p1 = first + i + 1;
                    if (flip) { tris.AddRange(new[] { corner, p0, p1 }); } else { tris.AddRange(new[] { corner, p1, p0 }); }
                }

                // The sliver between the vertical edge and the arc's first point is degenerate (same point): nothing.
            }
        }

        // Intrados: the underside of the arc, a strip across the thickness, normal pointing to the arc's centre.
        {
            int start = verts.Count;
            for (int i = 0; i <= steps * 2; i++)
            {
                float ang = Mathf.PI * (i / (float)(steps * 2));
                Vector3 radial = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                Vector3 pnt = new Vector3(radial.x * radius, ys + radial.y * radius, 0f);
                Vector3 n = -radial;
                verts.Add(pnt + Vector3.forward * hz); norms.Add(n); uvs.Add(new Vector2(ang * radius / tl, hz / tl));
                verts.Add(pnt - Vector3.forward * hz); norms.Add(n); uvs.Add(new Vector2(ang * radius / tl, -hz / tl));
            }

            for (int i = 0; i < steps * 2; i++)
            {
                int k = start + i * 2;
                trisL.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
            }
        }

        Mesh mesh = new Mesh { name = name + "_Arch" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        bool two = left != right && trisR.Count > 0;
        mesh.subMeshCount = two ? 2 : 1;
        if (two)
        {
            mesh.SetTriangles(trisL, 0);
            mesh.SetTriangles(trisR, 1);
        }
        else
        {
            trisL.AddRange(trisR);
            mesh.SetTriangles(trisL, 0);
        }

        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = two ? new[] { left, right } : new[] { left };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        Static(go);
    }

    // The frame of a door or window, and the glass of a window.
    private static void Trim(Transform group, string name, Vector2 a, Vector2 dir, float yaw, float from, float to, Hole h, float thickness, Material left = null, Material right = null)
    {
        if (!h.frame)
        {
            return;
        }

        const float t = 0.07f;
        float depth = thickness + 0.06f;
        System.Action<string, float, float, float, float> box = (n, s0, s1, b0, b1) =>
        {
            Vector2 mid = a + dir * ((s0 + s1) * 0.5f);
            Block(name + n, group, new Vector3(mid.x, (b0 + b1) * 0.5f, mid.y), yaw, new Vector3(s1 - s0, b1 - b0, depth), p.trim, false);
        };
        box("_JambA", from, from + t, h.bottom, h.top);
        box("_JambB", to - t, to, h.bottom, h.top);
        box("_Head", from, to, h.top - t, h.top);
        if (h.kind == 1)
        {
            // The leaf, left open against the wall (95 degrees) on the side its hash picks: a house left in a hurry.
            float width = (to - from) - 2f * t;
            if (width < 0.5f || width > 1.4f)
            {
                return;
            }

            // The leaf hangs on the wall face it opens toward (a door is hinged at a face, not in the jamb's middle),
            // on a pivot the Door component swings; it can be closed and opened again with E (22/10).
            float side = (Mathf.Abs(Mathf.Sin(from * 12.9898f + a.x * 78.233f)) > 0.5f) ? 1f : -1f;
            float angle = 95f * side;
            Vector2 hinge = a + dir * (from + t) + new Vector2(-dir.y, dir.x) * (side * thickness * 0.5f);
            Transform pivot = Group(name + "_Door", group);
            pivot.position = manor.TransformPoint(new Vector3(hinge.x, h.bottom, hinge.y));
            Vector3 alongClosed = manor.rotation * (Quaternion.Euler(0f, yaw, 0f) * Vector3.right);
            pivot.rotation = Quaternion.LookRotation(Vector3.Cross(alongClosed, Vector3.up), Vector3.up);
            GameObject leaf = FloodedHouseBuilder.DoorLeaf(name + "_Leaf", pivot, width, h.top - h.bottom - t - 0.01f, 0.045f, p.trim);
            _ = leaf;
            Door door = pivot.gameObject.AddComponent<Door>();
            door.Configure(Door.Lock.None, null, null, null, null);
            door.SetStartOpen(-angle);
            pivot.localRotation *= Quaternion.Euler(0f, -angle, 0f);
            return;
        }

        if (h.kind != 2)
        {
            return;
        }

        box("_Sill", from - 0.05f, to + 0.05f, h.bottom, h.bottom + t * 0.7f);
        // Thin glazing bars between the small panes (3 cm, thinner than the frame).
        const float bar = 0.03f;
        System.Action<string, float, float, float, float> glazing = (n, s0, s1, b0, b1) =>
        {
            Vector2 mid = a + dir * ((s0 + s1) * 0.5f);
            Block(name + n, group, new Vector3(mid.x, (b0 + b1) * 0.5f, mid.y), yaw, new Vector3(s1 - s0, b1 - b0, 0.05f), p.trim, false);
        };
        if (h.style == 0)
        {
            box("_Mullion", (from + to) * 0.5f - t * 0.4f, (from + to) * 0.5f + t * 0.4f, h.bottom, h.top);
            box("_Transom", from, to, Mathf.Lerp(h.bottom, h.top, 0.62f) - t * 0.4f, Mathf.Lerp(h.bottom, h.top, 0.62f) + t * 0.4f);
        }
        else if (h.style == 1 || h.style == 3)
        {
            for (int c = 1; c < h.cols; c++)
            {
                float s = Mathf.Lerp(from, to, c / (float)h.cols);
                glazing("_Bar", s - bar * 0.5f, s + bar * 0.5f, h.bottom, h.top);
            }

            for (int k = 1; k < h.rows; k++)
            {
                float y = Mathf.Lerp(h.bottom, h.top, k / (float)h.rows);
                glazing("_Bar", from, to, y - bar * 0.5f, y + bar * 0.5f);
            }
        }

        if (h.style == 3)
        {
            // The cut corners: a square turned 45 degrees centred on each corner of the opening fills the corner's
            // triangle (the rest of it is inside the solid wall), faced by a frame bar along the cut.
            float cut = (to - from) * 0.28f;
            float diagonal = cut * 1.4142f;
            foreach ((float s, float y, float turn) in new[] { (from, h.top, 45f), (to, h.top, -45f), (from, h.bottom, -45f), (to, h.bottom, 45f) })
            {
                Vector2 corner = a + dir * s;
                // A triangular prism exactly filling the corner, faced with the wall's own two materials and their
                // world-planar UVs (it was a square turned 45 degrees in the facade's cream, the wall's full thickness:
                // its faces fought the panelling at the same depth and showed as pale triangles round the window,
                // Gustavo 22/10), a hair thinner than the wall so no face shares a plane with it.
                CornerWedge(name + "_Cut", group, a, dir, yaw, s, y, s == from ? 1f : -1f, y == h.top ? -1f : 1f, cut, thickness - 0.006f, left ?? p.wall, right ?? p.wall);
                _ = corner;
                float sx = s + (s == from ? cut * 0.5f : -cut * 0.5f);
                float sy = y + (y == h.top ? -cut * 0.5f : cut * 0.5f);
                Vector2 mid = a + dir * sx;
                GameObject edge = Block(name + "_CutFrame", group, new Vector3(mid.x, sy, mid.y), yaw, new Vector3(diagonal, t, thickness + 0.06f), p.trim, false);
                edge.transform.rotation = manor.rotation * Quaternion.Euler(0f, yaw, turn);
            }
        }

        GameObject pane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        pane.name = name + "_Glass";
        Object.DestroyImmediate(pane.GetComponent<Collider>());
        pane.transform.SetParent(group, false);
        Vector2 c0 = a + dir * ((from + to) * 0.5f);
        pane.transform.position = manor.TransformPoint(new Vector3(c0.x, (h.bottom + h.top) * 0.5f, c0.y));
        pane.transform.rotation = manor.rotation * Quaternion.Euler(0f, yaw, 0f);
        pane.transform.localScale = new Vector3(to - from, h.top - h.bottom, 1f);
        MeshRenderer r = pane.GetComponent<MeshRenderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        if (h.style == 2 && p.stained != null)
        {
            // One pane: the stained glass shader draws both faces (the same design from the street and from the hall).
            r.sharedMaterial = p.stained;
            pane.name = name + "_StainedGlass";
            return;
        }

        r.sharedMaterial = p.glass;
        // Glass both ways (a quad only faces one side).
        GameObject back = Object.Instantiate(pane, group);
        back.name = name + "_GlassBack";
        back.transform.rotation = pane.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
    }

    // A horizontal slab over a polygon (any simple outline), top and bottom faces planar-mapped, sides along the edge.
    private static GameObject Slab(string name, Transform parent, Vector2[] outline, float y0, float y1, Material top, Material bottom, Material side, bool collider)
    {
        List<Vector2> poly = new List<Vector2>(outline);
        if (SignedArea(poly) < 0f)
        {
            poly.Reverse();
        }

        List<int> tri = Triangulate(poly);
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tTop = new List<int>(), tBottom = new List<int>(), tSide = new List<int>();
        float tt = Tile(top), tb = Tile(bottom), ts = Tile(side);
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            verts.Add(new Vector3(poly[i].x, y1, poly[i].y));
            norms.Add(Vector3.up);
            uvs.Add(poly[i] * K / tt);
        }

        for (int i = 0; i < n; i++)
        {
            verts.Add(new Vector3(poly[i].x, y0, poly[i].y));
            norms.Add(Vector3.down);
            uvs.Add(poly[i] * K / tb);
        }

        for (int i = 0; i < tri.Count; i += 3)
        {
            // Counter-clockwise seen from above: Unity's front faces are clockwise from the viewer.
            tTop.AddRange(new[] { tri[i], tri[i + 2], tri[i + 1] });
            tBottom.AddRange(new[] { n + tri[i], n + tri[i + 1], n + tri[i + 2] });
        }

        float run = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % n];
            float len = Vector2.Distance(a, b);
            Vector2 d = (b - a) / Mathf.Max(len, 0.0001f);
            Vector3 normal = new Vector3(d.y, 0f, -d.x);
            int s = verts.Count;
            verts.Add(new Vector3(a.x, y0, a.y));
            verts.Add(new Vector3(b.x, y0, b.y));
            verts.Add(new Vector3(b.x, y1, b.y));
            verts.Add(new Vector3(a.x, y1, a.y));
            for (int k = 0; k < 4; k++)
            {
                norms.Add(normal);
            }

            uvs.Add(new Vector2(run * K, y0) / ts);
            uvs.Add(new Vector2((run + len) * K, y0) / ts);
            uvs.Add(new Vector2((run + len) * K, y1) / ts);
            uvs.Add(new Vector2(run * K, y1) / ts);
            run += len;
            tSide.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(tTop, 0);
        mesh.SetTriangles(tBottom, 1);
        mesh.SetTriangles(tSide, 2);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = manor.position;
        go.transform.rotation = manor.rotation;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { top, bottom, side };
        if (collider)
        {
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        Static(go);
        return go;
    }

    private static float SignedArea(List<Vector2> poly)
    {
        float a = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 p0 = poly[i], p1 = poly[(i + 1) % poly.Count];
            a += p0.x * p1.y - p1.x * p0.y;
        }

        return a * 0.5f;
    }

    // Ear clipping for a simple counter-clockwise polygon.
    private static List<int> Triangulate(List<Vector2> poly)
    {
        List<int> result = new List<int>();
        List<int> idx = new List<int>();
        for (int i = 0; i < poly.Count; i++)
        {
            idx.Add(i);
        }

        int guard = 0;
        while (idx.Count > 3 && guard++ < 5000)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int i0 = idx[(i - 1 + idx.Count) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                if (Cross(b - a, c - b) <= 1e-6f)
                {
                    continue;
                }

                bool inside = false;
                foreach (int j in idx)
                {
                    if (j == i0 || j == i1 || j == i2)
                    {
                        continue;
                    }

                    if (InTriangle(poly[j], a, b, c))
                    {
                        inside = true;
                        break;
                    }
                }

                if (inside)
                {
                    continue;
                }

                result.AddRange(new[] { i0, i1, i2 });
                idx.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
            {
                // Degenerate (collinear) leftovers: drop a middle point.
                idx.RemoveAt(0);
            }
        }

        if (idx.Count == 3)
        {
            result.AddRange(new[] { idx[0], idx[1], idx[2] });
        }

        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool InTriangle(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, q - a), d2 = Cross(c - b, q - b), d3 = Cross(a - c, q - c);
        return d1 > 0f && d2 > 0f && d3 > 0f;
    }

    private static float Tile(Material m)
    {
        float t = p.tiling != null && m != null ? p.tiling(m) : 2f;
        return t > 0f ? t : 2f;
    }

    private static void Static(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
    }
}
