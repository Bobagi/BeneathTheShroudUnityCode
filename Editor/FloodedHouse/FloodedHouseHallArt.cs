using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The Luna mansion as the GDD's concept art paints it (Dayse, "Planta da Casa e Imagens", 13/10): the cream facade
// with its ochre plinth and moss, dark slate roofs, the stained glass by the front door, the gilded pediment; in the
// hall a floor of wide brown planks, dark vertical boards over a golden foliage paper, the frosted teal double door to
// the courtyard with brass H pulls (creatures' shadows show through it), three paintings (a wide sunset over snowy peaks
// above the door, a red field and a bowl of flowers each side), a low dark sideboard and a small carved table; red
// walls in the rooms either side.
// Surfaces: ambientCG sets Dayse listed in "Modelos 3D - Sob o Manto" (Planks037A, Planks023A, PaintedPlaster018 for the
// facade, Ground047 for the moss), historic wallpapers from Cooper Hewitt (CC0, cut to one repeat), the stained glass and
// the tracery drawn for this house (SourceAssets/Tools/house_art_textures.py), paintings from museum open access (CC0).
public static partial class FloodedHouseBuilder
{
    private static Material frostedGlass, stainedGlass, hallBrass;

    // The concept art's surfaces on a palette (the game's house and the opening's: both call it).
    public static LunaManor.Palette WithHouseArt(LunaManor.Palette palette)
    {
        palette.wall = Surface("CreamFacade") ?? palette.wall;
        palette.plinth = Surface("OchrePlinth");
        palette.hallFloor = Surface("HallPlanks");
        palette.boards = Surface("WallBoards");
        palette.goldPaper = Surface("WallpaperGold");
        palette.redPaper = Surface("WallpaperRed");
        palette.greenPaper = Surface("WallpaperGreen");
        palette.slate = Surface("SlateRoof");
        palette.stained = StainedGlassMaterial();
        palette.gilt = GiltMaterial();
        return palette;
    }

    // A surface set at its real scale (LunaManor.SurfaceTiles; non-square tiles scale v by width / height). Null when the
    // set's textures are not in the project.
    private static Material Surface(string set)
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>($"{ProjectPaths.Textures}/{set}/{set}_Albedo.jpg") == null)
        {
            return null;
        }

        Material m = Textured(set, LunaManor.SurfaceTiles[set], Color.white, 0.1f);
        float v = LunaManor.SurfaceHeights.TryGetValue(set, out float height) ? LunaManor.SurfaceTiles[set] / height : 1f;
        m.SetTextureScale("_BaseMap", new Vector2(1f, v));
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Texture2D ArtTexture(string folder, string file, bool normal, bool linear)
    {
        string path = $"{ProjectPaths.Textures}/{folder}/{file}";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool dirty = false;
            if (normal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                dirty = true;
            }

            if (linear && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                dirty = true;
            }

            if (!normal && !linear && !importer.alphaIsTransparency && file.EndsWith(".png"))
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material StainedGlassMaterial()
    {
        Shader shader = Shader.Find("Horror/StainedGlass");
        Texture2D albedo = ArtTexture("StainedGlass", "StainedGlass_Albedo.png", false, false);
        if (shader == null || albedo == null)
        {
            return null;
        }

        stainedGlass = Material("StainedGlass", shader);
        stainedGlass.SetTexture("_BaseMap", albedo);
        stainedGlass.SetTexture("_BumpMap", ArtTexture("StainedGlass", "StainedGlass_Normal.png", true, false));
        stainedGlass.SetFloat("_Backlight", GameTuning.Instance.stainedGlassGlow);
        stainedGlass.SetFloat("_FlashGain", GameTuning.Instance.stainedGlassFlash);
        // Cathedral glass is nearly opaque to the eye: the colour is what reads, the sky behind only lightens it.
        stainedGlass.SetFloat("_GlassOpacity", 0.92f);
        stainedGlass.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(stainedGlass);
        return stainedGlass;
    }

    // The pediment's gold: URP Lit cut out by the tracery's alpha, metal and smoothness from its map.
    private static Material GiltMaterial()
    {
        Texture2D albedo = ArtTexture("PedimentOrnament", "PedimentOrnament_Albedo.png", false, false);
        if (albedo == null)
        {
            return null;
        }

        Material m = Material("PedimentGilt", Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", ArtTexture("PedimentOrnament", "PedimentOrnament_Normal.png", true, false));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", ArtTexture("PedimentOrnament", "PedimentOrnament_MetallicSmooth.png", false, true));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material FrostedGlassMaterial()
    {
        Shader shader = Shader.Find("Horror/FrostedGlass");
        if (shader == null)
        {
            return glass;
        }

        Material m = Material("FrostedGlass", shader);
        m.SetColor("_Tint", new Color(0.36f, 0.66f, 0.6f));
        m.SetFloat("_BlurPerMetre", GameTuning.Instance.frostedBlurPerMetre);
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- the hall

    private static void DressHallArt(Transform parent)
    {
        Transform h = Group("HallArt", parent);
        frostedGlass = FrostedGlassMaterial();
        hallBrass = Plain("HallBrass", new Color(0.72f, 0.55f, 0.24f), 0.62f, 1f);
        stainedGlass = stainedGlass != null ? stainedGlass : StainedGlassMaterial();
        CourtyardDoors(h);
        StainedGlassLights(h);

        // Paintings (concept art): the wide sunset over snowy peaks above the door, the red field on its left and the
        // bowl of flowers on its right, over the rail on the boards. The north wall's flat parts run x 2.4 to 4 each side
        // (the door's half-round porch between), its face into the hall at z 8.75; the door's facet at z 9.703.
        Painting("HallLandscape", h, M(0f, 2.93f, 9.703f), Vector3.back, 0.56f, 2.1f);
        Painting("HallRedField", h, M(-3.2f, 2.45f, 8.75f), Vector3.back, 0.92f, 0f);
        Painting("HallFlowers", h, M(3.2f, 2.45f, 8.75f), Vector3.back, 0.92f, 0f);

        // The furniture the art draws against that wall: a low dark sideboard on the left (Poly Haven's gothic commode),
        // the carved console on the right (Poly Haven; its gothic_coffee_table is too deep for this wall: living room).
        GameObject sideboard = Against(h, "Commode", V2(-3.2f, 8.9f), Vector2.down, 0f);
        GameObject console = Against(h, "Console", V2(3.2f, 8.9f), Vector2.down, 0f);
        On(h, "Candlestick", sideboard, -0.3f);
        On(h, "BrassVase", console, 0.25f);

        HousePaintings(h);
    }

    // The rest of the house's paintings (museum open access, CC0; what a wealthy family hangs: still lifes in the dining
    // room, the sea and portraits in the living room, a moonlit sea and a river in the library, the jellyfish plate the
    // marine biologist kept, storms along the galleries, landscapes over the beds). Wall points are on the wall's room
    // face (interior partitions 0.1 m off their line, outer walls 0.15 m); height = the canvas, centres at eye level.
    private static void HousePaintings(Transform parent)
    {
        Transform p = Group("Paintings", parent);
        // Dining room.
        Painting("StillLifeFruit", p, M(-11.5f, 1.8f, 4.1f), Vector3.right, 0.75f, 0f);
        Painting("StillLifeGame", p, M(-5.7f, 1.95f, 8.75f), Vector3.back, 0.62f, 0f);
        // Living room.
        Painting("SeaStorm1", p, M(7.8f, 1.85f, 8.75f), Vector3.back, 0.9f, 0f);
        Painting("PortraitWoman", p, M(11.5f, 1.8f, 3.9f), Vector3.left, 0.95f, 0f);
        Painting("PortraitMan", p, M(4.1f, 1.8f, 7.6f), Vector3.right, 0.85f, 0f);
        // Alex's own portrait among the family's (GDD "O que buscar": for the walls of the house), on the east wall between
        // the library door (z 6.3) and the corner; the picture is a generated photograph in sepia (18/10, ComfyUI).
        Painting("AlexPortrait", p, M(11.5f, 1.75f, 7.9f), Vector3.left, 0.7f, 0f);
        // Library.
        Painting("NightNorthCape", p, M(13.6f, 1.75f, 13.3f), Vector3.back, 0.75f, 0f);
        Painting("LandscapeRiver", p, M(10.25f, 1.8f, 12.2f), Vector3.right, 0.6f, 0f);
        Painting("HaeckelJellyfish", p, M(19.75f, 1.7f, 8.4f), Vector3.left, 0.8f, 0f);
        // Upper galleries.
        Painting("SeaStorm2", p, M(-9.9f, UF + 1.75f, 14.2f), Vector3.right, 0.8f, 0f);
        Painting("SeaStorm3", p, M(10.3f, UF + 1.75f, 18.6f), Vector3.left, 0.75f, 0f);
        // Master bedrooms, over the beds' heads.
        Painting("LandscapeForest", p, M(-7.4f, UF + 1.95f, 23.1f), Vector3.right, 0.8f, 0f);
        Painting("TropicalRioBay", p, M(7.8f, UF + 1.85f, 23.1f), Vector3.left, 0.55f, 0f);
    }

    // The frosted double door into the courtyard (the hall's half-round porch, its middle facet 2.4 m wide at z 9.853):
    // two leaves of teal glass in thin brass frames, an H pull on each meeting stile, both sides. The two leaves are
    // linked (Door.Link): E on either swings both, outward into the gallery, and E again closes them (Gustavo, 22/10).
    // They open 90 degrees, not more: the porch's side facets angle away right behind the jambs, and at 100 degrees
    // the leaves cut through them.
    private static void CourtyardDoors(Transform parent)
    {
        const float facetZ = 9.853f, half = 1.08f, height = 2.43f, frame = 0.05f, thick = 0.045f;
        // The leaves hang on the courtyard face of the 30 cm wall (a door is hinged at a wall face, not in the middle
        // of the jamb): centred in the wall, the open leaf's first 15 cm stood inside the jamb ("atravessa a parede").
        const float leafZ = facetZ + 0.15f - thick * 0.5f - 0.01f;
        AudioClip creak = DoorSound("glass_door_open") ?? DoorSound("door_creak_2");
        Door left = null, right = null;
        foreach (float side in new[] { -1f, 1f })
        {
            Transform pivot = Group(side < 0f ? "CourtyardDoor_L" : "CourtyardDoor_R", parent);
            pivot.position = M(side * half, 0f, leafZ + thick * 0.5f);
            float outer = side * half, inner = side * 0.006f;
            float x0 = Mathf.Min(outer, inner), x1 = Mathf.Max(outer, inner);
            Vector3 A(float x, float y, float dz) => M(x, y, leafZ + dz);
            // The glass (its collider is what the E finds), then the brass frame round it.
            Box("Glass", pivot, A(x0 + frame, frame, -0.008f), A(x1 - frame, height - frame, 0.008f), frostedGlass, true);
            Box("FrameBottom", pivot, A(x0, 0.01f, -thick * 0.5f), A(x1, 0.01f + frame * 2f, thick * 0.5f), hallBrass, false);
            Box("FrameTop", pivot, A(x0, height - frame, -thick * 0.5f), A(x1, height, thick * 0.5f), hallBrass, false);
            Box("FrameOuter", pivot, A(side < 0f ? x0 : x1 - frame, 0.01f, -thick * 0.5f), A(side < 0f ? x0 + frame : x1, height, thick * 0.5f), hallBrass, false);
            Box("FrameInner", pivot, A(side < 0f ? x1 - frame : x0, 0.01f, -thick * 0.5f), A(side < 0f ? x1 : x0 + frame, height, thick * 0.5f), hallBrass, false);
            // The H pull, a little in from the meeting edge, on both faces.
            float px = side * 0.11f;
            foreach (float face in new[] { -1f, 1f })
            {
                float zf = face * (thick * 0.5f + 0.035f);
                foreach (float bar in new[] { -0.035f, 0.035f })
                {
                    Box("PullBar", pivot, A(px + bar - 0.011f, 0.86f, zf - 0.011f), A(px + bar + 0.011f, 1.34f, zf + 0.011f), hallBrass, false);
                }

                Box("PullCross", pivot, A(px - 0.035f, 1.09f, zf - 0.008f), A(px + 0.035f, 1.13f, zf + 0.008f), hallBrass, false);
                Box("PullPost", pivot, A(px - 0.01f, 1.09f, Mathf.Min(0f, zf) - 0.002f), A(px + 0.01f, 1.13f, Mathf.Max(0f, zf) + 0.002f), hallBrass, false);
            }

            Door door = pivot.gameObject.AddComponent<Door>();
            door.Configure(Door.Lock.None, null, null, null, null);
            // Into the courtyard (+z): the left leaf turns one way, the right the other.
            SetField(door, "openAngle", side < 0f ? -90f : 90f);
            DoorAudio(door, pivot.gameObject, creak, creak);
            if (side < 0f)
            {
                left = door;
            }
            else
            {
                right = door;
            }
        }

        if (left != null && right != null)
        {
            Door.Link(left, right);
        }
    }

    // The colour the stained glass throws: a spot outside each window (the night and the lightning), the glass as its
    // cookie, shadowed by the wall round the window so only the window's shape comes in; and in the haze.
    private static void StainedGlassLights(Transform parent)
    {
        Texture2D cookie = ArtTexture("StainedGlass", "StainedGlass_Cookie.png", false, false);
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(cookie)) is TextureImporter importer && (importer.wrapMode != TextureWrapMode.Clamp || importer.mipmapEnabled))
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        foreach (float side in new[] { -1f, 1f })
        {
            // The window on the hall's chamfer (centre at x 3.2, z 0.95); the chamfer faces out south-west / south-east.
            Vector2 window = new Vector2(side * 3.2f, 0.95f);
            Vector2 outward = new Vector2(side * 0.766f, -0.645f);
            Vector3 glassCentre = M(window, 1.7f);
            Vector3 at = glassCentre + new Vector3(outward.x, 0f, outward.y) * 2.6f + Vector3.up * 1.4f;
            Vector3 aim = M(window - outward * 2.6f, 0f);
            Light spot = PointOrSpot(side < 0f ? "StainedGlassLight_W" : "StainedGlassLight_E", parent, at, LightType.Spot, new Color(0.85f, 0.9f, 1f), GameTuning.Instance.stainedGlassLight, 9f, true);
            spot.transform.rotation = Quaternion.LookRotation(aim - at);
            spot.spotAngle = 48f;
            spot.innerSpotAngle = 30f;
            spot.cookie = cookie;
            Volumetric(spot, GameTuning.Instance.stainedGlassFog, 0.4f);
            spot.gameObject.AddComponent<StainedGlassLight>().Configure(stainedGlass, frostedGlass);

            // The rain on the glass, heard from inside by the window (an outside sound: AmbienceSource sets its level
            // and dulls it under the roof).
            AudioClip rainOnGlass = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Ambience/rain_window_loop.wav");
            if (rainOnGlass != null)
            {
                GameObject rain = new GameObject("RainOnStainedGlass");
                rain.transform.SetParent(parent, false);
                rain.transform.position = glassCentre - new Vector3(outward.x, 0f, outward.y) * 0.3f;
                AudioSource source = rain.AddComponent<AudioSource>();
                source.clip = rainOnGlass;
                source.loop = true;
                source.playOnAwake = true;
                source.spatialBlend = 1f;
                source.minDistance = 1f;
                source.maxDistance = 7f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.time = side < 0f ? 0f : 13f;
                rain.AddComponent<AmbienceSource>().SetKind(AmbienceSource.Kind.Rain);
            }
        }
    }

    // A framed painting on a wall: canvas (the image, varnished) in a dark moulded frame with a thin gilt lip. `facing`
    // is the wall's normal into the room (one of the four axes); width 0 = from the image's aspect at `height`. The
    // images: Art/Textures/Paintings (SourceAssets/Tools/paintings_prepare.py; museum open access, CC0).
    private static GameObject Painting(string image, Transform parent, Vector3 wallPoint, Vector3 facing, float height, float width)
    {
        Texture2D picture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ProjectPaths.Textures}/Paintings/{image}.jpg");
        if (picture == null)
        {
            return null;
        }

        if (width <= 0f)
        {
            width = height * picture.width / picture.height;
        }

        hallBrass = hallBrass != null ? hallBrass : Plain("HallBrass", new Color(0.72f, 0.55f, 0.24f), 0.62f, 1f);
        Transform p = Group("Painting_" + image, parent);
        Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
        float border = 0.06f + 0.035f * height;
        Vector3 centre = wallPoint + facing * 0.03f;
        Material canvas = Material("Painting_" + image, Shader.Find("Universal Render Pipeline/Lit"));
        canvas.SetTexture("_BaseMap", picture);
        canvas.SetColor("_BaseColor", Color.white);
        canvas.SetFloat("_Smoothness", 0.32f);
        Texture2D weave = ArtTexture("Paintings", "CanvasWeave_Normal.png", true, false);
        if (weave != null)
        {
            canvas.SetTexture("_BumpMap", weave);
            canvas.SetFloat("_BumpScale", 0.5f);
            canvas.EnableKeyword("_NORMALMAP");
        }

        EditorUtility.SetDirty(canvas);
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Canvas";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(p, false);
        quad.transform.SetPositionAndRotation(centre + facing * 0.012f, Quaternion.LookRotation(-facing));
        quad.transform.localScale = new Vector3(width, height, 1f);
        quad.GetComponent<MeshRenderer>().sharedMaterial = canvas;

        // The frame: four bars (world-axis boxes: the walls here run along x or z), and the gilt lip inside them.
        Material wood = doorPaint;
        System.Action<string, float, float, float, float, float, float, Material> bar = (n, l, r, b, t, depth0, depth1, m) =>
        {
            Vector3 c0 = centre + right * l + Vector3.up * b + facing * depth0;
            Vector3 c1 = centre + right * r + Vector3.up * t + facing * depth1;
            Box(n, p, Vector3.Min(c0, c1), Vector3.Max(c0, c1), m, false);
        };
        float hw = width * 0.5f, hh = height * 0.5f;
        bar("FrameL", -hw - border, -hw, -hh - border, hh + border, -0.03f, 0.05f, wood);
        bar("FrameR", hw, hw + border, -hh - border, hh + border, -0.03f, 0.05f, wood);
        bar("FrameB", -hw, hw, -hh - border, -hh, -0.03f, 0.05f, wood);
        bar("FrameT", -hw, hw, hh, hh + border, -0.03f, 0.05f, wood);
        float lip = 0.012f;
        bar("LipL", -hw - lip, -hw, -hh - lip, hh + lip, 0f, 0.03f, hallBrass);
        bar("LipR", hw, hw + lip, -hh - lip, hh + lip, 0f, 0.03f, hallBrass);
        bar("LipB", -hw, hw, -hh - lip, -hh, 0f, 0.03f, hallBrass);
        bar("LipT", -hw, hw, hh, hh + lip, 0f, 0.03f, hallBrass);
        foreach (MeshRenderer r in p.GetComponentsInChildren<MeshRenderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        return p.gameObject;
    }
}
