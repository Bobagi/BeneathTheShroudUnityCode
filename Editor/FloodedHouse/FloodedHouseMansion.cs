using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Story pieces shared by the mansion's scenes (FloodedHouseMansionStory places them): materials, fungus, recorders,
// pickups, readables and photos. The rooms are Dayse's floor plan since 11/10 (LunaManor, placed by FloodedHouseManor).
public static partial class FloodedHouseBuilder
{
    private static Material kitchenTile, greenPaint, cloth, screenFrame, lanternGlassOff, fungusBlue, fungusPink, fungusGreen, moldPatch, sporeHaze, wormMat, rockGlow;
    private static readonly List<Light> gridLights = new List<Light>();
    private static readonly List<Renderer> gridGlows = new List<Renderer>();
    // Fittings whose shade glows while the mains are on (LitFixture): renderer, its off material and its lit one.
    private static readonly List<Renderer> gridFixtures = new List<Renderer>();
    private static readonly List<Material> gridFixturesOff = new List<Material>();
    private static readonly List<Material> gridFixturesOn = new List<Material>();

    private struct MansionStory
    {
        public Door dehumidifierDoor, generatorDoor, basementDoor;
        public Transform basementDoorHinge;
    }

    private static MansionStory mansion;

    // ---------------------------------------------------------------- materials

    private static void CreateMansionMaterials()
    {
        kitchenTile = Textured("LabFloor", 0.9f, new Color(0.8f, 0.78f, 0.72f), 0.45f);
        greenPaint = Plain("LockerGreen", new Color(0.12f, 0.3f, 0.18f), 0.45f, 0.5f);
        greenPaint.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Textures + "/PaintedMetalRust/PaintedMetalRust_Albedo.jpg"));
        greenPaint.SetColor("_BaseColor", new Color(0.35f, 0.75f, 0.45f));
        EditorUtility.SetDirty(greenPaint);
        cloth = Plain("SuitCloth", new Color(0.42f, 0.4f, 0.28f), 0.15f, 0f);
        screenFrame = Plain("PanelFrame", new Color(0.08f, 0.08f, 0.08f), 0.5f, 0.4f);
        screenFrame.EnableKeyword("_EMISSION");
        screenFrame.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.05f) * 2f);
        screenFrame.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        lanternGlassOff = Transparent("LanternGlassOff", new Color(0.55f, 0.5f, 0.42f, 0.25f), 0.85f, null);
        // Emission 1.2 to 1.5 of a saturated colour: the caps are the light source and glow with a small halo; at
        // 2.2 to 2.4 exposure and ACES pushed them to white, and at 1 they barely read as glowing.
        fungusBlue = Glow("FungusBlue", new Color(0.12f, 0.22f, 0.3f), new Color(0.1f, 0.45f, 1f) * 1.5f);
        fungusPink = Glow("FungusPink", new Color(0.3f, 0.14f, 0.24f), new Color(1f, 0.2f, 0.7f) * 1.4f);
        fungusGreen = Glow("FungusGreen", new Color(0.16f, 0.26f, 0.12f), new Color(0.3f, 1f, 0.38f) * 1.2f);
        moldPatch = MoldPatchMaterial();
        wormMat = Plain("Worm", new Color(0.18f, 0.08f, 0.07f), 0.7f, 0f);
        rockGlow = Glow("LuminaRock", new Color(0.1f, 0.2f, 0.25f), new Color(0.3f, 0.95f, 1f) * 4f);
    }

    private static Material Glow(string name, Color albedo, Color emission)
    {
        Material material = Plain(name, albedo, 0.65f, 0f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Glowing mould on the walls: blotches of a noise texture, cut out and emissive.
    private static Material MoldPatchMaterial()
    {
        string folder = ProjectPaths.Textures + "/Fungus";
        string path = folder + "/MoldPatch.png";
        if (!System.IO.File.Exists(path))
        {
            BattlefieldTerrainBuilder.EnsureFolder(folder);
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float edge = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2.1f);
                    float n = Mathf.PerlinNoise(u * 9f, v * 9f) * 0.6f + Mathf.PerlinNoise(u * 27f + 3f, v * 27f) * 0.4f;
                    float spots = Mathf.Clamp01((n * edge * 2.4f - 0.55f) * 4f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, spots));
                }
            }

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        Material material = Material("MoldPatch", Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        material.SetColor("_BaseColor", new Color(0.05f, 0.07f, 0.08f));
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.4f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        // Faint: the clumps carry the glow; the film only shows the mould spreading around them.
        material.SetColor("_EmissionColor", new Color(0.08f, 0.3f, 0.5f) * 0.45f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        TileSizes[material] = 1f;
        return material;
    }

    // ---------------------------------------------------------------- fungus

    // A clump of glowing mushrooms on a wall or the floor. The caps shine; around them a soft tinted halo is painted
    // on the surface (Horror/SurfaceGlow), with no light at all: see the shader for why. Colours by kind: blue,
    // pink, green (Dayse: "azul, rosa e verde").
    private static Transform FungusCluster(string name, Transform parent, Vector3 at, Vector3 outward, int kind, float scale)
    {
        Material[] mats = { fungusBlue, fungusPink, fungusGreen };
        Color[] colors = { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.35f, 0.8f), new Color(0.4f, 1f, 0.5f) };
        Transform cluster = Group(name, parent);
        cluster.position = at;
        cluster.rotation = Quaternion.FromToRotation(Vector3.up, outward);
        int count = Random.Range(6, 11);
        for (int i = 0; i < count; i++)
        {
            Vector2 spot = Random.insideUnitCircle * 0.28f * scale;
            float h = Random.Range(0.03f, 0.13f) * scale;
            Vector3 local = new Vector3(spot.x, 0f, spot.y);
            GameObject stem = Primitive(PrimitiveType.Cylinder, "Stem", cluster, cluster.TransformPoint(local + Vector3.up * h * 0.5f), cluster.rotation, new Vector3(0.012f, h * 0.5f, 0.012f) * scale, mats[kind]);
            stem.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            float cap = Random.Range(0.035f, 0.075f) * scale;
            GameObject head = Primitive(PrimitiveType.Sphere, "Cap", cluster, cluster.TransformPoint(local + Vector3.up * h), cluster.rotation, new Vector3(cap, cap * 0.45f, cap), mats[kind]);
            head.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // A glowing film on the surface around the clump.
        GameObject film = GameObject.CreatePrimitive(PrimitiveType.Quad);
        film.name = "Film";
        Object.DestroyImmediate(film.GetComponent<Collider>());
        film.transform.SetParent(cluster, false);
        film.transform.localPosition = Vector3.up * 0.004f;
        film.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        film.transform.localScale = Vector3.one * 0.6f * scale;
        MeshRenderer filmRenderer = film.GetComponent<MeshRenderer>();
        filmRenderer.sharedMaterial = moldPatch;
        filmRenderer.shadowCastingMode = ShadowCastingMode.Off;

        // The halo on the surface: about a metre across, its strength a fraction of the caps' glow.
        Material[] halos = { FungusHalo("FungusHaloBlue", colors[0]), FungusHalo("FungusHaloPink", colors[1]), FungusHalo("FungusHaloGreen", colors[2]) };
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        halo.name = "Halo";
        Object.DestroyImmediate(halo.GetComponent<Collider>());
        halo.transform.SetParent(cluster, false);
        halo.transform.localPosition = Vector3.up * 0.006f;
        halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        halo.transform.localScale = Vector3.one * 1.1f * scale;
        MeshRenderer haloRenderer = halo.GetComponent<MeshRenderer>();
        haloRenderer.sharedMaterial = halos[kind];
        haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
        haloRenderer.receiveShadows = false;
        haloRenderer.lightProbeUsage = LightProbeUsage.Off;
        haloRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        return cluster;
    }

    // ---------------------------------------------------------------- story

    private static Material FungusHalo(string name, Color colour)
    {
        Material material = Material(name, Shader.Find("Horror/SurfaceGlow"));
        material.SetColor("_Color", colour * 0.22f);
        material.SetFloat("_Falloff", 2.4f);
        material.SetFloat("_Pulse", 0.3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Recorder MakeRecorder(string name, Transform parent, Vector3 at, float yaw, string id, string title, string clipName, AudioClip commentClip, string comment)
    {
        Transform rec = Group(name, parent);
        rec.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        // Poly Haven cassette player (game-3d-assets: 4.8k to 3k triangles; at 2k the window panel smeared),
        // laid on its back with the controls up. The red LED is dim: it should catch the eye, not light the room.
        GameObject player = ModelProp("CassettePlayer", rec, at, rec.rotation * Quaternion.Euler(-90f, 0f, 0f), false);
        // Its pivot is at one end (the base of the standing model): centre it on the spot.
        Bounds size = RendererBounds(player);
        player.transform.position += new Vector3(at.x - size.center.x, 0f, at.z - size.center.z);
        float top = RendererBounds(player).max.y;
        Vector3 ledAt = new Vector3(at.x, top + 0.0012f, at.z) + rec.rotation * new Vector3(0.025f, 0f, -0.07f);
        Material ledMat = Material("RecorderLed", Shader.Find("Universal Render Pipeline/Unlit"));
        GameObject dot = Primitive(PrimitiveType.Sphere, "LedDot", rec, ledAt, Quaternion.identity, Vector3.one * 0.005f, ledMat);
        dot.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        BoxCollider reach = rec.gameObject.AddComponent<BoxCollider>();
        reach.isTrigger = true;
        reach.center = new Vector3(0f, 0.04f, 0f);
        reach.size = new Vector3(0.3f, 0.12f, 0.24f);
        AudioSource voice = rec.gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0.7f;
        voice.minDistance = 2f;
        voice.maxDistance = 18f;
        voice.playOnAwake = false;
        Recorder recorder = rec.gameObject.AddComponent<Recorder>();
        recorder.SetPrompt("prompt.play_recorder");
        recorder.Configure(id, title, Voice(clipName), VoiceLines(clipName), commentClip, comment);
        SetField(recorder, "indicatorDot", dot.GetComponent<MeshRenderer>());
        SetField(recorder, "source", voice);
        // Recorded tape deck keys (Gustavo's second batch, 01/10): PLAY with the motor starting, STOP at the end.
        SetField(recorder, "buttonClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/cassette_play.wav"));
        SetField(recorder, "stopClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/cassette_stop.wav"));
        return recorder;
    }

    private static AudioClip Voice(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Voice/" + name + ".wav");
    }

    // Subtitle timing of a recording, from SourceAssets/Voice/timings.json (voice_process.py). Each line's text is the
    // Loc key voice.<name>.<n> (the table holds Dayse's Portuguese and the translations); the json text is kept only
    // when the table has no such key, so a new recording still shows its subtitle until it is added.
    private static Recorder.Line[] VoiceLines(string name)
    {
        string path = System.IO.Path.Combine(Application.dataPath, "../SourceAssets/Voice/timings.json");
        if (!System.IO.File.Exists(path))
        {
            return new Recorder.Line[0];
        }

        string json = System.IO.File.ReadAllText(path);
        int start = json.IndexOf("\"" + name + "\"", System.StringComparison.Ordinal);
        if (start < 0)
        {
            return new Recorder.Line[0];
        }

        int open = json.IndexOf('[', start);
        int close = json.IndexOf(']', open);
        List<Recorder.Line> lines = new List<Recorder.Line>();
        System.Text.RegularExpressions.Regex pattern = new System.Text.RegularExpressions.Regex(
            @"""text"":\s*""(.*?)"",\s*""start"":\s*([0-9.]+),\s*""end"":\s*([0-9.]+)",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        foreach (System.Text.RegularExpressions.Match match in pattern.Matches(json.Substring(open, close - open)))
        {
            string key = "voice." + name + "." + (lines.Count + 1);
            if (!Loc.Has(key))
            {
                Debug.LogWarning("[Loc] no line " + key + " in ui.txt: the subtitle stays in Portuguese");
            }

            lines.Add(new Recorder.Line
            {
                text = Loc.Has(key) ? key : match.Groups[1].Value,
                start = float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                end = float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
            });
        }

        return lines.ToArray();
    }

    // Something small to pick up. It shimmers (Horror/ItemGlow on a shell over its own mesh) so it can be found
    // in the dark; it used to carry a small point light, which lit the wall behind it more than the item
    // (Gustavo, 01/10: "tire essa luz e coloque um shader... um brilho no contorno, como item colecionável").
    private static StoryPickup Pickup(string name, Transform parent, Vector3 at, GameObject[] visuals, string item, string prompt, string message, Vector3 reachSize)
    {
        Transform pivot = Group(name, parent);
        pivot.position = at;
        foreach (GameObject visual in visuals)
        {
            visual.transform.SetParent(pivot, true);
        }

        BoxCollider reach = pivot.gameObject.AddComponent<BoxCollider>();
        reach.isTrigger = true;
        reach.center = Vector3.up * reachSize.y * 0.5f;
        reach.size = reachSize;
        foreach (GameObject visual in visuals)
        {
            GlowShells(visual);
        }

        StoryPickup pickup = pivot.gameObject.AddComponent<StoryPickup>();
        pickup.SetPrompt(prompt);
        pickup.Configure(item, message);
        SetField(pickup, "visuals", visuals);
        SetField(pickup, "pickupClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Pickup_Metal.ogg"));
        return pickup;
    }

    // The item's meshes drawn again with the glow material, as children, so they hide with the item. One slot
    // per submesh: a Poly Haven prop with several materials glows all over, not only on its last part.
    private static void GlowShells(GameObject visual)
    {
        Material glow = Material("ItemGlow", Shader.Find("Horror/ItemGlow"));
        foreach (MeshRenderer source in visual.GetComponentsInChildren<MeshRenderer>())
        {
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            GameObject shell = new GameObject("GlowShell");
            shell.transform.SetParent(source.transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer renderer = shell.AddComponent<MeshRenderer>();
            Material[] slots = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = glow;
            }

            renderer.sharedMaterials = slots;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }

    private static ReadableNote Readable(string name, Transform parent, Vector3 at, float yaw, string id, string title, PlayerItems.EntryKind kind, string body, string signature, Texture photo = null)
    {
        Transform pivot = Group(name, parent);
        pivot.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        // A picture (photo print, or the folded newspaper page in portrait) is drawn on the sheet itself.
        bool picture = photo != null;
        bool portrait = picture && photo.height > photo.width;
        Vector3 half = portrait ? new Vector3(0.08f, 0.002f, 0.11f) : picture ? new Vector3(0.09f, 0.002f, 0.065f) : new Vector3(0.11f, 0.002f, 0.15f);
        GameObject sheet = Box("Sheet", pivot, at - half, at + half, paper, false);
        sheet.transform.RotateAround(at, Vector3.up, yaw);
        if (picture)
        {
            Material photoMat = Material("Photo_" + id, Shader.Find("Universal Render Pipeline/Lit"));
            photoMat.SetTexture("_BaseMap", photo);
            photoMat.SetFloat("_Smoothness", 0.5f);
            EditorUtility.SetDirty(photoMat);
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(pivot, false);
            face.transform.position = at + Vector3.up * 0.0035f;
            face.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            face.transform.localScale = portrait ? new Vector3(0.15f, 0.21f, 1f) : new Vector3(0.165f, 0.115f, 1f);
            face.GetComponent<MeshRenderer>().sharedMaterial = photoMat;
        }

        BoxCollider reach = pivot.gameObject.AddComponent<BoxCollider>();
        reach.isTrigger = true;
        reach.center = Vector3.up * 0.04f;
        reach.size = new Vector3(0.36f, 0.1f, 0.4f);
        ReadableNote note = pivot.gameObject.AddComponent<ReadableNote>();
        note.SetPrompt(kind == PlayerItems.EntryKind.Photo ? "prompt.photo" : kind == PlayerItems.EntryKind.Clipping ? "prompt.clipping"
            : kind == PlayerItems.EntryKind.Notice ? "prompt.notice" : "prompt.note");
        note.SetText(body, signature);
        note.SetArchive(id, title, kind, photo);
        return note;
    }

    // The photographs of the story (Art/Textures/Story/photo_<id>.png). Since 18/10 they are real pictures generated with
    // ComfyUI (scratchpad art pipeline, Juggernaut XL + Qwen Image Edit from the two reference portraits): whatever file
    // is there is used as is. The sepia placeholder (two figures) is only drawn when the file is missing.
    private static Texture2D PhotoTexture(string id, bool graduation, bool lab)
    {
        string folder = ProjectPaths.Textures + "/Story";
        string path = folder + "/photo_" + id + ".png";
        BattlefieldTerrainBuilder.EnsureFolder(folder);
        if (System.IO.File.Exists(path))
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }
        }

        const int w = 480, h = 320;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGB24, false);
        Random.State saved = Random.state;
        Random.InitState(id.GetHashCode());
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float vignette = 1f - Mathf.Pow(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.25f, 2f);
                float grain = Random.Range(-0.04f, 0.04f);
                float sky = lab ? 0.55f + v * 0.1f : 0.62f + v * 0.18f;
                float tone = Mathf.Clamp01(sky * vignette + grain);
                texture.SetPixel(x, y, new Color(tone, tone * 0.86f, tone * 0.66f));
            }
        }

        // Two figures side by side: heads, shoulders, bodies; caps for the graduation.
        foreach (float cx in new[] { 0.4f, 0.6f })
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    bool head = Vector2.Distance(new Vector2(u * 1.5f, v), new Vector2(cx * 1.5f, 0.66f)) < 0.075f;
                    bool body = v < 0.56f && Mathf.Abs(u - cx) < 0.085f + (0.56f - v) * 0.12f;
                    bool cap = graduation && v > 0.72f && v < 0.76f && Mathf.Abs(u - cx) < 0.07f;
                    if (head || body || cap)
                    {
                        float shade = cap ? 0.08f : head ? 0.32f : lab ? 0.75f : 0.18f;
                        texture.SetPixel(x, y, new Color(shade, shade * 0.88f, shade * 0.7f));
                    }
                }
            }
        }

        Random.state = saved;
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
