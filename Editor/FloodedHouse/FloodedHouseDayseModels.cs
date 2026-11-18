using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// The models Dayse generated in Meshy on 14 and 15/11 (GDD, "Modelos da Dayse no Meshy"; Docs/Notas/
// modelos-dayse-meshy-2026-11.md), reduced by SourceAssets/Tools/dayse_meshy_batch.sh and placed where the GDD's table
// says each one goes:
//   The red fungus's nest (library, north-west corner): the eggs are now her shell (Horror/EggShell: translucent, the
//   liquid and its bubbles made by the shader, "Líquido só no shader") with her embryo hanging inside, held by her
//   ceiling mounts (two single ones with a big egg, the triple one with three small eggs), over her ground nest, her
//   wall patches on the two walls of the corner. The generated silk ribbons stay between them.
//   The creature of the egg (her adult, the green arachnid with mushrooms): rigged and animated in Blender
//   (shroom_crab_rig.py), a prefab with ShroomCrabBrain, lying closed by the nest it guards.
//   "A criatura disfarçada de fruta" (Gustavo, 15/11): a fruit basket on the kitchen counter (Poly Haven fruit, CC0)
//   with a closed young beside it (her embryo model, belly down: a green mango) that opens and runs (FruitMimic).
//   The blue fungus: her colony plaque and her cluster where the colonies of glowing powder were (pantry, china room,
//   second bathroom, north gallery), glowing on the bright parts of their own texture (fungus_emission.py).
//   The yellow fungus (the GDD says "a definir"; a Claude proposal, registered there): the damp of the laundry and of
//   the passage beside it, on walls only (it drips).
//   The heart palm: in place of the PulsePalm generated from the same drawing on 13/10.
//   The mushroom goblet (still with Dayse on 16/11): the garden uses it in place of the GiantMushroom as soon as
//   Props/MushroomGoblet exists, its bowl filled by Horror/Liquid (boiling).
// Glow and opacity come from GameTuning through TunedGrowths.
public static partial class FloodedHouseBuilder
{
    public const string ShroomCrabPrefabPath = ProjectPaths.Root + "/Prefabs/Creatures/ShroomCrab.prefab";
    private const string CrabFolder = ProjectPaths.CreatureModels + "/ShroomCrab";
    private const string CrabControllerPath = ProjectPaths.Art + "/Animations/ShroomCrab/ShroomCrab.controller";
    private static readonly string[] CrabLoops = { "Idle", "Walk", "Curled" };
    private static readonly string[] CrabStates = { "Curled", "Unfold", "Idle", "Walk", "Attack", "Hit", "Death" };

    private static readonly List<Material> eggShellMaterials = new List<Material>();
    private static readonly List<Material> blueFungusMaterials = new List<Material>();
    private static readonly List<Material> yellowFungusMaterials = new List<Material>();
    private static readonly List<Material> reedBudMaterials = new List<Material>();

    private static bool HasModel(string name) =>
        AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/" + name + "/" + name + ".fbx") != null;

    // ---------------------------------------------------------------- the creature of the egg: import, clips, prefab

    // Ground speed of the Walk clip at scale 1, from the rig's report (falls back to the value of 16/11).
    private static float CrabClipWalkSpeed()
    {
        string report = System.IO.Path.Combine(Application.dataPath, "../SourceAssets/BlenderExports/Rigged/ShroomCrab/ShroomCrab_rig.json");
        if (System.IO.File.Exists(report))
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(report), "\"walk_speed_mps\":\\s*([0-9.]+)");
            if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float speed))
            {
                return speed;
            }
        }

        return 0.51f;
    }

    [MenuItem("Horror/Testes/Criar prefab da criatura do ovo")]
    public static GameObject BuildShroomCrabPrefab()
    {
        string modelPath = CrabFolder + "/ShroomCrab.fbx";
        ModelImporter model = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (model == null)
        {
            Debug.LogWarning("[FloodedHouse] Missing " + modelPath + " (run SourceAssets/Tools/shroom_crab_rig.py).");
            return null;
        }

        if (model.animationType != ModelImporterAnimationType.Generic || model.materialImportMode != ModelImporterMaterialImportMode.None
            || model.importAnimation || model.importTangents != ModelImporterTangents.CalculateMikk)
        {
            model.animationType = ModelImporterAnimationType.Generic;
            model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            model.importAnimation = false;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.importNormals = ModelImporterNormals.Import;
            model.importTangents = ModelImporterTangents.CalculateMikk;
            model.SaveAndReimport();
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        foreach (string state in CrabStates)
        {
            string path = CrabFolder + "/ShroomCrab@" + state + ".fbx";
            ModelImporter anim = AssetImporter.GetAtPath(path) as ModelImporter;
            if (anim == null)
            {
                Debug.LogWarning("[FloodedHouse] Missing clip " + path);
                continue;
            }

            anim.animationType = ModelImporterAnimationType.Generic;
            anim.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            anim.sourceAvatar = avatar;
            anim.importAnimation = true;
            anim.materialImportMode = ModelImporterMaterialImportMode.None;
            // Every frame is keyed in Blender: no resampling error wanted on the feet.
            anim.animationCompression = ModelImporterAnimationCompression.Off;
            ModelImporterClipAnimation[] takes = anim.defaultClipAnimations;
            foreach (ModelImporterClipAnimation take in takes)
            {
                take.name = state;
                take.loopTime = CrabLoops.Contains(state);
                take.loopPose = false;
            }

            anim.clipAnimations = takes;
            anim.SaveAndReimport();
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip != null)
            {
                clips[state] = clip;
            }
        }

        // The controller: one state per clip and no transitions (the brain picks the state); Walk's speed is a parameter
        // so the feet follow the ground speed. The asset is reused so the prefab keeps pointing at it.
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CrabControllerPath));
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CrabControllerPath);
        if (controller == null)
        {
            AssetDatabase.Refresh();
            controller = AnimatorController.CreateAnimatorControllerAtPath(CrabControllerPath);
        }

        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            controller.RemoveParameter(p);
        }

        controller.AddParameter(new AnimatorControllerParameter { name = "WalkSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState s in machine.states)
        {
            machine.RemoveState(s.state);
        }

        for (int i = 0; i < CrabStates.Length; i++)
        {
            if (!clips.TryGetValue(CrabStates[i], out AnimationClip clip))
            {
                continue;
            }

            AnimatorState s = machine.AddState(CrabStates[i], new Vector3(260f, 60f * i, 0f));
            s.motion = clip;
            s.writeDefaultValues = false;
            if (CrabStates[i] == "Walk")
            {
                s.speedParameterActive = true;
                s.speedParameter = "WalkSpeed";
            }

            if (i == 0)
            {
                machine.defaultState = s;
            }
        }

        EditorUtility.SetDirty(controller);

        // The skin: the textures baked by the optimiser beside the static model (Props/ShroomCrab).
        string textures = ProjectPaths.PropModels + "/ShroomCrab";
        EnsureModelImport(textures, "ShroomCrab");
        Material skinMaterial = ModelPropMaterial(textures, "ShroomCrab");
        // A wet shell, not a dusty piece of furniture.
        skinMaterial.SetFloat("_Smoothness", 0.8f);
        EditorUtility.SetDirty(skinMaterial);

        GameObject root = new GameObject("ShroomCrab");
        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
        body.name = "Model";
        // Facing +z: the front pair of legs ahead of the body.
        Transform front = FindDeep(body.transform, "Leg_L1_a");
        Transform bodyBone = FindDeep(body.transform, "Body");
        if (front != null && bodyBone != null && front.position.z < bodyBone.position.z)
        {
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * body.transform.localRotation;
        }

        Animator animator = body.GetComponent<Animator>();
        if (animator == null)
        {
            animator = body.AddComponent<Animator>();
        }

        animator.avatar = avatar;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        Bounds skinBounds = new Bounds(body.transform.position, Vector3.zero);
        foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.sharedMaterial = skinMaterial;
            // The legs reach past the rest pose when it rears and when it dies on its side.
            Bounds local = skin.localBounds;
            local.Expand(0.5f);
            skin.localBounds = local;
            skinBounds.Encapsulate(skin.bounds);
        }

        // One sphere on the body bone: what a bullet hits and what keeps the player out of it.
        float radius = 0.36f;
        if (bodyBone != null)
        {
            SphereCollider shell = bodyBone.gameObject.AddComponent<SphereCollider>();
            shell.radius = radius / Mathf.Max(bodyBone.lossyScale.x, 0.001f);
        }

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        AudioSource voice = root.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 1.2f;
        voice.maxDistance = 20f;
        voice.rolloffMode = AudioRolloffMode.Logarithmic;
        GameObject stepsObject = new GameObject("Steps");
        stepsObject.transform.SetParent(root.transform, false);
        AudioSource steps = stepsObject.AddComponent<AudioSource>();
        steps.clip = SoundLibrary.Instance != null ? SoundLibrary.Instance.creatureCrawlLoop : null;
        steps.loop = true;
        steps.playOnAwake = false;
        steps.volume = 0f;
        steps.pitch = 1.5f;
        steps.spatialBlend = 1f;
        steps.minDistance = 1f;
        steps.maxDistance = 12f;
        steps.rolloffMode = AudioRolloffMode.Linear;

        ShroomCrabBrain brain = root.AddComponent<ShroomCrabBrain>();
        brain.Configure(animator, bodyBone, voice, steps, CombatVfxMaterials.WoundFluid, CrabClipWalkSpeed());

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ShroomCrabPrefabPath));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ShroomCrabPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[FloodedHouse] ShroomCrab prefab: " + clips.Count + " clips, walk clip " + CrabClipWalkSpeed() + " m/s.");
        return prefab;
    }

    // The prefab, rebuilt when it is missing or older than the rigged model or its clips.
    private static GameObject ShroomCrabPrefab()
    {
        string folder = System.IO.Path.Combine(Application.dataPath, "..", CrabFolder);
        string prefabFile = System.IO.Path.Combine(Application.dataPath, "..", ShroomCrabPrefabPath);
        if (!System.IO.Directory.Exists(folder))
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(ShroomCrabPrefabPath);
        }

        bool stale = !System.IO.File.Exists(prefabFile);
        if (!stale)
        {
            System.DateTime built = System.IO.File.GetLastWriteTimeUtc(prefabFile);
            foreach (string file in System.IO.Directory.GetFiles(folder, "*.fbx"))
            {
                stale |= System.IO.File.GetLastWriteTimeUtc(file) > built;
            }
        }

        return stale ? BuildShroomCrabPrefab() : AssetDatabase.LoadAssetAtPath<GameObject>(ShroomCrabPrefabPath);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- materials

    private static Material EggShellMaterial()
    {
        string folder = ProjectPaths.PropModels + "/EggShell";
        EnsureModelImport(folder, "EggShell");
        Material m = Material("EggShellDayse", Shader.Find("Horror/EggShell"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/EggShell_BaseColor.png"));
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/EggShell_Normal.png"));
        // The drawing's egg: deep amber skin, veins of dried blood, the liquid darker toward the base.
        // Darker than the bake: at full tint the shell burned out to white under the torch (16/11, measured in Play).
        m.SetColor("_Tint", new Color(0.68f, 0.48f, 0.24f));
        m.SetColor("_Fluid", new Color(0.5f, 0.26f, 0.05f));
        m.SetColor("_FluidDeep", new Color(0.2f, 0.07f, 0.02f));
        m.SetFloat("_FluidLevel", 0.72f);
        m.SetVector("_VeinEdge", new Vector4(0.2f, 0.5f, 0f, 0f));
        m.SetFloat("_Glow", GameTuning.Instance.eggGlow);
        m.SetFloat("_Opacity", GameTuning.Instance.eggOpacity);
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        if (!eggShellMaterials.Contains(m))
        {
            eggShellMaterials.Add(m);
        }

        return m;
    }

    // A model of Dayse's fungus: its baked maps and, as light, the bright parts of its own colour (name_Emission.png,
    // SourceAssets/Tools/fungus_emission.py); the strength comes from GameTuning through TunedGrowths.
    private static Material DayseFungusMaterial(string name, bool blue)
    {
        string folder = ProjectPaths.PropModels + "/" + name;
        EnsureModelImport(folder, name);
        Material m = ModelPropMaterial(folder, name);
        m.SetFloat("_Smoothness", 0.7f);
        Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_Emission.png");
        if (emission != null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", emission);
            m.SetColor("_EmissionColor", blue ? new Color(0.35f, 0.75f, 1f) * GameTuning.Instance.blueFungusGlow : new Color(0.9f, 1f, 0.35f) * GameTuning.Instance.yellowFungusGlow);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(m);
        List<Material> list = blue ? blueFungusMaterials : yellowFungusMaterials;
        if (!list.Contains(m))
        {
            list.Add(m);
        }

        return m;
    }

    private static Material LiquidMaterial(string name, Color body, Color surface, float boil, float glow)
    {
        Material m = Material(name, Shader.Find("Horror/Liquid"));
        m.SetColor("_Color", body);
        m.SetColor("_SurfaceColor", surface);
        m.SetFloat("_Boil", boil);
        m.SetFloat("_Glow", glow);
        m.SetVector("_Wave", new Vector4(boil > 0f ? 0.006f : 0.0015f, 14f, boil > 0f ? 2.2f : 0.8f, 0f));
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- placing helpers

    // `m` (as ModelProp left it, identity yaw) turned so its local world direction `attach` lies against the surface
    // whose normal is `outward` and its `top` points as close to `upHint` as the surface allows, then moved so its
    // attach-most point touches `at`, sunk `sink` metres into the surface.
    private static void Seat(GameObject m, Vector3 at, Vector3 outward, Vector3 attach, Vector3 top, Vector3 upHint, float sink)
    {
        outward.Normalize();
        Vector3 targetTop = Vector3.ProjectOnPlane(upHint, outward);
        if (targetTop.sqrMagnitude < 0.001f)
        {
            targetTop = Vector3.ProjectOnPlane(Vector3.forward, outward);
        }

        Quaternion from = Quaternion.LookRotation(attach, top);
        Quaternion to = Quaternion.LookRotation(-outward, targetTop.normalized);
        m.transform.rotation = to * Quaternion.Inverse(from) * m.transform.rotation;
        Bounds b = RendererBounds(m);
        float reach = Vector3.Dot(b.center - at, -outward) + Mathf.Abs(Vector3.Dot(b.extents, Abs(outward)));
        Vector3 lateral = Vector3.ProjectOnPlane(b.center - at, outward);
        m.transform.position += outward * (reach - sink) - lateral;
    }

    // Dayse's silk pieces come out of the bake a clean bright red; under the torch they went pale pink. The colour of
    // drying blood, like the generated ribbons beside them: the same texture at 62%.
    private static void DrySilk(GameObject m)
    {
        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            Material mat = r.sharedMaterial;
            if (mat != null && mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", new Color(0.62f, 0.56f, 0.56f));
                mat.SetFloat("_Smoothness", 0.7f);
                EditorUtility.SetDirty(mat);
            }
        }
    }

    private static void NoCollidersNoShadows(GameObject m, bool castShadows)
    {
        foreach (Collider c in m.GetComponentsInChildren<Collider>())
        {
            Object.DestroyImmediate(c);
        }

        foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }
    }

    // The surface of the house behind `outward` near `at`: the point of the real wall, floor or ceiling there (a plan
    // coordinate is the wall's line, and the face stands 15 to 19 cm off it).
    private static Vector3 OnHouseSurface(Vector3 at, Vector3 outward)
    {
        Physics.SyncTransforms();
        return WallFace(at + outward * 0.9f, -outward, 1.6f, out Vector3 hit) ? hit : at;
    }

    // The lowest point of each hanging strand of a ceiling mount: the vertices below its canopy grouped in `count`
    // clusters on the horizontal plane (the sockets where the eggs hang).
    private static Vector3[] MountSockets(GameObject mount, int count)
    {
        List<Vector3> points = new List<Vector3>();
        Bounds b = RendererBounds(mount);
        foreach (MeshFilter mf in mount.GetComponentsInChildren<MeshFilter>())
        {
            foreach (Vector3 v in mf.sharedMesh.vertices)
            {
                Vector3 w = mf.transform.TransformPoint(v);
                if (w.y < b.max.y - b.size.y * 0.3f)
                {
                    points.Add(w);
                }
            }
        }

        if (points.Count == 0)
        {
            return new[] { new Vector3(b.center.x, b.min.y, b.center.z) };
        }

        // Seeds: the lowest point, then the farthest from the seeds so far (on the plane).
        List<Vector3> seeds = new List<Vector3> { points.OrderBy(p => p.y).First() };
        while (seeds.Count < count)
        {
            seeds.Add(points.OrderByDescending(p => seeds.Min(s => (new Vector2(p.x, p.z) - new Vector2(s.x, s.z)).sqrMagnitude)).First());
        }

        Vector3[] lowest = new Vector3[count];
        for (int pass = 0; pass < 4; pass++)
        {
            Vector3[] sum = new Vector3[count];
            int[] n = new int[count];
            for (int i = 0; i < count; i++)
            {
                lowest[i] = new Vector3(0f, float.PositiveInfinity, 0f);
            }

            foreach (Vector3 p in points)
            {
                int best = 0;
                float bestD = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    float d = (new Vector2(p.x, p.z) - new Vector2(seeds[i].x, seeds[i].z)).sqrMagnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }

                sum[best] += p;
                n[best]++;
                if (p.y < lowest[best].y)
                {
                    lowest[best] = p;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (n[i] > 0)
                {
                    seeds[i] = sum[i] / n[i];
                }
            }
        }

        return lowest;
    }

    // ---------------------------------------------------------------- the red fungus's nest

    private static bool NestModelsReady() =>
        HasModel("EggShell") && HasModel("EggEmbryo") && HasModel("EggMountSingle") && HasModel("EggMountTriple") && HasModel("SilkNest") && HasModel("SilkWallPatch");

    // Where the mounts hang, as offsets from the corner along its two walls (plan units): the two single mounts with a
    // big egg each, and the triple cluster.
    private static readonly Vector2[] NestHangs = { new Vector2(1.55f, 0.7f), new Vector2(0.75f, 1.7f), new Vector2(0.75f, 0.7f) };

    // Whether a silk ribbon from `top` to `bottom` passes (seen from above) within reach of one of the hanging eggs.
    private static bool CrossesNest(Vector3 top, Vector3 bottom, Vector2 corner, Vector2 into)
    {
        Vector2 a = new Vector2(top.x, top.z), b = new Vector2(bottom.x, bottom.z);
        foreach (Vector2 hang in NestHangs)
        {
            Vector3 w = M(corner + new Vector2(into.x * hang.x, into.y * hang.y), 0f);
            Vector2 p = new Vector2(w.x, w.z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            if ((a + ab * t - p).magnitude < 0.75f)
            {
                return true;
            }
        }

        return false;
    }

    // One egg hanging with its stem tip at `top`, `height` tall: Dayse's shell, her embryo inside it, the corrosive drip.
    private static GameObject HangingEgg(Transform parent, Vector3 top, float height, bool puddle, float floorY)
    {
        GameObject eggRoot = new GameObject("Egg");
        eggRoot.transform.SetParent(parent, false);
        eggRoot.transform.position = top;
        GameObject shell = ModelProp("EggShell", eggRoot.transform, top, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), false, false);
        shell.transform.localScale *= height;
        Bounds b = RendererBounds(shell);
        // The stem's tip into the mount's socket.
        shell.transform.position += new Vector3(top.x - b.center.x, top.y + 0.04f * height - b.max.y, top.z - b.center.z);
        Material shellMaterial = EggShellMaterial();
        foreach (Renderer r in shell.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = shellMaterial;
            // The shell is a lamp, not a lampshade: it throws no shadow on its own inside.
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // A solid thing to walk round and to shoot at.
        b = RendererBounds(shell);
        CapsuleCollider body = eggRoot.AddComponent<CapsuleCollider>();
        body.direction = 1;
        body.radius = Mathf.Min(b.extents.x, b.extents.z) * 0.85f;
        body.height = b.size.y * 0.8f;
        body.center = eggRoot.transform.InverseTransformPoint(b.center - Vector3.up * b.size.y * 0.05f);

        // The embryo: upright in the liquid, half the egg's height, hanging a little off the axis.
        GameObject embryo = ModelProp("EggEmbryo", eggRoot.transform, b.center, Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-8f, 8f)), false, false);
        embryo.name = "Embryo";
        embryo.transform.localScale *= height * 0.5f / 0.55f;
        Bounds e = RendererBounds(embryo);
        Vector3 hang = new Vector3(b.center.x, b.min.y + b.size.y * 0.4f, b.center.z);
        embryo.transform.position += hang - e.center;
        // Seen through the lit liquid it is a dark shape, as in Dayse's render of the assembled egg: its own material,
        // the model's colour at 40%.
        Material inside = Material("EggEmbryoInside", Shader.Find("Universal Render Pipeline/Lit"));
        inside.CopyPropertiesFromMaterial(ModelPropMaterial(ProjectPaths.PropModels + "/EggEmbryo", "EggEmbryo"));
        inside.SetColor("_BaseColor", new Color(0.4f, 0.42f, 0.36f));
        inside.SetFloat("_Smoothness", 0.75f);
        EditorUtility.SetDirty(inside);
        foreach (Renderer r in embryo.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = inside;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        Drip(parent, new Vector3(b.center.x, b.min.y + 0.01f, b.center.z));
        if (puddle)
        {
            GameObject pool = Primitive(PrimitiveType.Cylinder, "Puddle", parent, new Vector3(b.center.x, floorY + 0.004f, b.center.z), Quaternion.identity,
                new Vector3(Random.Range(0.35f, 0.6f), 0.002f, Random.Range(0.3f, 0.5f)), corrosive);
            pool.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        return eggRoot;
    }

    // A mount on the ceiling over the plan point `at`, scaled so its lowest socket ends `socketY` above the floor.
    private static GameObject CeilingMount(Transform parent, string name, Vector2 at, float socketY, float yaw)
    {
        Vector3 ceiling = M(at, GC);
        GameObject mount = ModelProp(name, parent, ceiling, Quaternion.Euler(0f, yaw, 0f), false, false);
        Bounds b = RendererBounds(mount);
        float wanted = ceiling.y - (M(at, 0f).y + socketY);
        mount.transform.localScale *= wanted / Mathf.Max(b.size.y, 0.01f);
        b = RendererBounds(mount);
        // Its canopy 2 cm into the ceiling.
        mount.transform.position += new Vector3(ceiling.x - b.center.x, ceiling.y + 0.02f - b.max.y, ceiling.z - b.center.z);
        NoCollidersNoShadows(mount, true);
        DrySilk(mount);
        return mount;
    }

    // The corner's colony from Dayse's models. `corner` and `into` as in SilkFungus.
    private static void SilkNestSet(Transform s, Vector2 corner, Vector2 into, FirstPersonController player)
    {
        float floorY = M(corner, 0f).y;
        // Two big eggs on single mounts, three small ones on the triple cluster between them and the corner.
        (Vector2 offset, float height, float bottom)[] big = { (NestHangs[0], 1.0f, 0.5f), (NestHangs[1], 0.88f, 0.62f) };
        foreach ((Vector2 offset, float height, float bottom) in big)
        {
            Vector2 at = corner + new Vector2(into.x * offset.x, into.y * offset.y);
            GameObject mount = CeilingMount(s, "EggMountSingle", at, bottom + height, Random.Range(0f, 360f));
            Vector3 socket = MountSockets(mount, 1)[0];
            HangingEgg(s, socket, height, true, floorY);
        }

        GameObject cluster = CeilingMount(s, "EggMountTriple", corner + new Vector2(into.x * NestHangs[2].x, into.y * NestHangs[2].y), 1.5f, Random.Range(0f, 360f));
        foreach (Vector3 socket in MountSockets(cluster, 3))
        {
            HangingEgg(s, socket, Random.Range(0.4f, 0.48f), false, floorY);
        }

        // The nest on the floor under them; only its core is solid (the tendrils round it are flat on the floor).
        GameObject nest = ModelProp("SilkNest", s, M(corner + new Vector2(into.x * 1.05f, into.y * 1.1f), 0f), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), false);
        nest.transform.localScale *= 1.1f;
        DrySilk(nest);
        Bounds nb = RendererBounds(nest);
        nest.transform.position += Vector3.up * (floorY - 0.02f - nb.min.y);
        nb = RendererBounds(nest);
        BoxCollider core = nest.AddComponent<BoxCollider>();
        core.center = nest.transform.InverseTransformPoint(new Vector3(nb.center.x, nb.min.y + nb.size.y * 0.22f, nb.center.z));
        Vector3 coreSize = new Vector3(nb.size.x * 0.4f, nb.size.y * 0.45f, nb.size.z * 0.4f);
        core.size = Abs(nest.transform.InverseTransformVector(coreSize));

        // The infestation on the two walls of the corner.
        (Vector3 at, Vector3 outward, float size)[] patches =
        {
            (M(corner.x, 1.75f, corner.y + into.y * 1.5f), Vector3.right * into.x, 1.05f),
            (M(corner.x + into.x * 1.7f, 1.6f, corner.y), Vector3.forward * into.y, 0.9f),
        };
        foreach ((Vector3 at, Vector3 outward, float size) in patches)
        {
            GameObject patch = ModelProp("SilkWallPatch", s, at, Quaternion.identity, false, false);
            patch.transform.localScale *= size;
            Seat(patch, OnHouseSurface(at, outward), outward, Vector3.back, Vector3.up, Quaternion.AngleAxis(Random.Range(-25f, 25f), outward) * Vector3.up, 0.012f);
            NoCollidersNoShadows(patch, false);
            DrySilk(patch);
        }

        // The adult that guards them, closed on the floor at the edge of the nest, facing the room.
        GameObject prefab = ShroomCrabPrefab();

        if (prefab != null && player != null)
        {
            GameObject crab = (GameObject)PrefabUtility.InstantiatePrefab(prefab, s);
            crab.name = "NestGuard";
            Vector3 at = M(corner + new Vector2(into.x * 2.3f, into.y * 2.2f), 0f);
            Vector3 face = new Vector3(into.x, 0f, into.y);
            crab.transform.SetPositionAndRotation(at, Quaternion.LookRotation(face.normalized, Vector3.up));
            crab.transform.localScale = Vector3.one * 0.78f;
            crab.GetComponent<ShroomCrabBrain>().SetPlayer(player, player.GetComponentInChildren<AirSupply>());
        }
    }

    // ---------------------------------------------------------------- the blue and the yellow fungus

    private static bool BlueFungusReady() => HasModel("FungusBlueColony") && HasModel("FungusBlueCluster");

    // Dayse's blue fungus on a patch of surface: the colony plaque flat on it and two or three clusters growing out of
    // it (PlaceFungus leans the stalked ones out and up on a wall).
    private static void BlueFungusPatch(Transform parent, Vector3 at, Vector3 outward, float size)
    {
        Vector3 surface = OnHouseSurface(at, outward);
        Material plaque = DayseFungusMaterial("FungusBlueColony", true);
        Material stalks = DayseFungusMaterial("FungusBlueCluster", true);
        GameObject colonyModel = ModelProp("FungusBlueColony", parent, surface, Quaternion.identity, false, false);
        colonyModel.name = "ColonyModel";
        // The plaque is 0.9 m and 24 cm thick as baked: no wider than 1.25 m, and a third of its thickness in the wall.
        float scale = Mathf.Min(size, 1.25f) / 0.9f;
        colonyModel.transform.localScale *= scale;
        Vector3 upHint = Mathf.Abs(outward.y) > 0.9f ? Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward : Quaternion.AngleAxis(Random.Range(-30f, 30f), outward) * Vector3.up;
        Seat(colonyModel, surface, outward, Vector3.back, Vector3.up, upHint, 0.08f * scale);
        NoCollidersNoShadows(colonyModel, false);
        foreach (Renderer r in colonyModel.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterial = plaque;
        }

        Vector3 side = Vector3.Cross(outward, Mathf.Abs(outward.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
        Vector3 side2 = Vector3.Cross(outward, side);
        int tufts = Random.Range(2, 4);
        for (int i = 0; i < tufts; i++)
        {
            Vector2 spot = Random.insideUnitCircle.normalized * Random.Range(0.3f, 0.5f) * scale;
            PlaceFungus(parent, BlueCluster, 0, surface + side * spot.x + side2 * spot.y, outward, Random.Range(0.7f, 1.15f), Random.Range(0f, 360f), stalks);
        }
    }

    // Her cluster as baked: 42 cm tall, its foot down.
    private static readonly FungusModel BlueCluster = new FungusModel { name = "FungusBlueCluster", attach = Vector3.down, top = Vector3.forward, glow = 0f, size = 1f };

    // The yellow fungus (a proposal: the GDD leaves its place open): the damp of the laundry and of the passage south of
    // it, on walls, the plaque with its drips hanging down and the clusters of bulbs round it.
    private static void YellowFungus(Transform parent)
    {
        if (!HasModel("FungusYellowColony") || !HasModel("FungusYellowCluster"))
        {
            return;
        }

        Transform y = Group("YellowFungus", parent);
        Material plaque = DayseFungusMaterial("FungusYellowColony", false);
        Material bulbs = DayseFungusMaterial("FungusYellowCluster", false);
        Rect laundry = LunaManor.Lavanderia, passage = LunaManor.Passagem;
        // Where it grows is measured, not typed: stretches of bare flat wall (no door, window, slab or furniture in
        // front) found by FreeWallSpots; three in the laundry, two in the passage.
        List<(Vector3 at, Vector3 outward, float size)> spots = new List<(Vector3, Vector3, float)>();
        spots.AddRange(FreeWallSpots(laundry, 0f, 3, 0.9f));
        spots.AddRange(FreeWallSpots(passage, 0f, 2, 0.8f));
        foreach ((Vector3 at, Vector3 outward, float size) in spots)
        {
            Vector3 surface = at;
            GameObject colonyModel = ModelProp("FungusYellowColony", y, surface, Quaternion.identity, false, false);
            colonyModel.transform.localScale *= size / 0.9f;
            Seat(colonyModel, surface, outward, Vector3.back, Vector3.up, Vector3.up, 0.07f * size);
            NoCollidersNoShadows(colonyModel, false);
            foreach (Renderer r in colonyModel.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = plaque;
            }

            Vector3 side = Vector3.Cross(outward, Vector3.up).normalized;
            int count = Random.Range(1, 4);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = surface + side * Random.Range(-0.55f, 0.55f) * size + Vector3.up * Random.Range(-0.5f, 0.35f) * size;
                GameObject cluster = ModelProp("FungusYellowCluster", y, p, Quaternion.identity, false, false);
                cluster.transform.localScale *= Random.Range(0.6f, 1f) * size;
                Seat(cluster, OnHouseSurface(p, outward), outward, Vector3.back, Vector3.up, Vector3.up, 0.02f);
                NoCollidersNoShadows(cluster, false);
                foreach (Renderer r in cluster.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = bulbs;
                }
            }
        }
    }

    // Up to `want` places on the walls of `room` (floor at `floorY`) where a plaque `size` metres across can grow: the
    // wall is sampled every 40 cm at two heights; a place is good when the wall's face is flat across the plaque (its
    // centre and four corners at the same depth as most of that wall, so no doorway, window or leaning slab) and
    // nothing stands within 35 cm in front of it. Taken in a shuffled order, at least 1.6 m apart.
    private static List<(Vector3 at, Vector3 outward, float size)> FreeWallSpots(Rect room, float floorY, int want, float size)
    {
        Physics.SyncTransforms();
        List<(Vector3, Vector3, float)> good = new List<(Vector3, Vector3, float)>();
        (Vector2 from, Vector2 to, Vector3 outward)[] walls =
        {
            (new Vector2(room.xMin, room.yMin), new Vector2(room.xMin, room.yMax), Vector3.right),
            (new Vector2(room.xMax, room.yMin), new Vector2(room.xMax, room.yMax), Vector3.left),
            (new Vector2(room.xMin, room.yMin), new Vector2(room.xMax, room.yMin), Vector3.forward),
            (new Vector2(room.xMin, room.yMax), new Vector2(room.xMax, room.yMax), Vector3.back),
        };
        float half = size * 0.5f;
        foreach ((Vector2 from, Vector2 to, Vector3 outward) in walls)
        {
            Vector3 along = (M(to, 0f) - M(from, 0f)).normalized;
            float length = (M(to, 0f) - M(from, 0f)).magnitude;
            List<(Vector3 point, float depth)> hits = new List<(Vector3, float)>();
            foreach (float height in new[] { 1.55f, 2.15f })
            {
                for (float d = half + 0.5f; d < length - half - 0.5f; d += 0.4f)
                {
                    Vector3 origin = M(from, floorY) + along * d + Vector3.up * height + outward * 1.1f;
                    if (WallFace(origin, -outward, 1.6f, out Vector3 hit))
                    {
                        hits.Add((hit, Vector3.Dot(hit - origin, -outward)));
                    }
                }
            }

            if (hits.Count < 3)
            {
                continue;
            }

            float median = hits.Select(h => h.depth).OrderBy(v => v).ElementAt(hits.Count / 2);
            foreach ((Vector3 point, float depth) in hits)
            {
                if (Mathf.Abs(depth - median) > 0.03f)
                {
                    continue;
                }

                bool flat = true;
                foreach (Vector2 corner in new[] { new Vector2(-half, -half), new Vector2(half, -half), new Vector2(-half, half), new Vector2(half, half) })
                {
                    Vector3 origin = point + along * corner.x + Vector3.up * corner.y + outward * 1.1f;
                    flat &= WallFace(origin, -outward, 1.6f, out Vector3 c) && Mathf.Abs(Vector3.Dot(c - point, outward)) < 0.03f;
                }

                Vector3 halfBox = Abs(along) * half + Vector3.up * half + Abs(outward) * 0.15f;
                bool clear = !Physics.CheckBox(point + outward * 0.2f, halfBox, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (flat && clear)
                {
                    good.Add((point, outward, size));
                }
            }
        }

        List<(Vector3 at, Vector3 outward, float size)> chosen = new List<(Vector3, Vector3, float)>();
        foreach ((Vector3 at, Vector3 outward, float s) in good.OrderBy(_ => Random.value))
        {
            if (chosen.Count >= want)
            {
                break;
            }

            if (chosen.All(c => (c.at - at).magnitude > 1.6f))
            {
                chosen.Add((at, outward, s * Random.Range(0.85f, 1.1f)));
            }
        }

        if (chosen.Count < want)
        {
            Debug.LogWarning("[FloodedHouse] FreeWallSpots: " + chosen.Count + " of " + want + " free places in " + room);
        }

        return chosen;
    }

    // ---------------------------------------------------------------- the fruit that is not fruit

    // On the kitchen counter (the units along the north wall, FloodedHouseFurniture): a basket of old fruit and, beside
    // it, the closed young. Its way out: off the counter's edge, across the floor, under the fridge.
    private static void FruitMimicScene(Transform parent, FirstPersonController player)
    {
        if (!HasModel("EggEmbryo") || player == null || AssetDatabase.LoadAssetAtPath<GameObject>(ShroomCrabPrefabPath) == null)
        {
            return;
        }

        Transform k = Group("KitchenFruit", parent);
        Physics.SyncTransforms();
        // The third base unit from the sink: 1 m wide, its front 60 cm off the wall.
        Vector3 unit = M(V2(-16.2f, 16f), 0f) + Vector3.back * (WallHalf * PlanScale) + new Vector3(3f, 0f, -0.31f);
        if (!Physics.Raycast(unit + Vector3.up * 1.6f, Vector3.down, out RaycastHit top, 1.5f, ~0, QueryTriggerInteraction.Ignore) || top.point.y < unit.y + 0.5f)
        {
            Debug.LogWarning("[FloodedHouse] FruitMimic: no counter under " + unit);
            return;
        }

        Vector3 counter = top.point;
        Vector3 basketAt = counter + new Vector3(-0.18f, 0f, 0.03f);
        if (HasModel("WickerBasket"))
        {
            GameObject basket = ModelProp("WickerBasket", k, basketAt, Quaternion.Euler(0f, 12f, 0f), false);
            Bounds bb = RendererBounds(basket);
            basket.transform.position += Vector3.up * (counter.y + 0.001f - bb.min.y);
        }

        // Old fruit in the basket (Poly Haven, CC0): each is baked 20 cm on its longest side; real sizes here.
        (string name, float size, Vector2 offset, float yaw)[] fruit =
        {
            ("FruitAvocado", 0.11f, new Vector2(-0.07f, 0.03f), 30f), ("FruitApple", 0.08f, new Vector2(0.06f, 0.05f), 110f),
            ("FruitPomegranate", 0.1f, new Vector2(0.02f, -0.05f), 200f), ("FruitLime", 0.055f, new Vector2(-0.1f, -0.06f), 300f),
            ("FruitLime", 0.05f, new Vector2(0.11f, -0.03f), 45f),
        };
        foreach ((string name, float size, Vector2 offset, float yaw) in fruit)
        {
            if (!HasModel(name))
            {
                continue;
            }

            Vector3 at = basketAt + new Vector3(offset.x, 0.02f, offset.y);
            GameObject f = ModelProp(name, k, at, Quaternion.Euler(0f, yaw, 0f), false);
            f.transform.localScale *= size / 0.2f;
            Bounds fb = RendererBounds(f);
            f.transform.position += new Vector3(at.x - fb.center.x, counter.y + 0.022f - fb.min.y, at.z - fb.center.z);
            // Going off: darker and duller than fresh fruit.
            foreach (Renderer r in f.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial.SetColor("_BaseColor", new Color(0.62f, 0.56f, 0.48f));
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // The young, closed: Dayse's embryo belly down (its legs hidden under it), 21 cm long, beside the basket.
        Vector3 spot = counter + new Vector3(0.24f, 0f, -0.05f);
        GameObject mimicRoot = new GameObject("FruitMimic");
        mimicRoot.transform.SetParent(k, false);
        mimicRoot.transform.position = spot;
        GameObject closed = ModelProp("EggEmbryo", mimicRoot.transform, spot, Quaternion.identity, false, false);
        closed.name = "Closed";
        const float length = 0.21f;
        closed.transform.localScale *= length / 0.55f;
        // The belly (the model's front, +z once ModelProp has applied the file's root) down, the head along the counter.
        closed.transform.rotation = Quaternion.Euler(0f, 70f, 0f) * Quaternion.Euler(90f, 0f, 0f) * closed.transform.rotation;
        Bounds cb = RendererBounds(closed);
        closed.transform.position += new Vector3(spot.x - cb.center.x, counter.y + 0.001f - cb.min.y, spot.z - cb.center.z);
        cb = RendererBounds(closed);
        BoxCollider shape = closed.AddComponent<BoxCollider>();
        shape.center = closed.transform.InverseTransformPoint(cb.center);
        shape.size = Abs(closed.transform.InverseTransformVector(cb.size));

        // The one that runs: the adult's rig at the young's size, without the adult's brain.
        GameObject runner = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ShroomCrabPrefabPath), mimicRoot.transform);
        PrefabUtility.UnpackPrefabInstance(runner, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        runner.name = "Runner";
        Object.DestroyImmediate(runner.GetComponent<ShroomCrabBrain>());
        Object.DestroyImmediate(runner.GetComponent<Rigidbody>());
        Transform stepsChild = runner.transform.Find("Steps");
        if (stepsChild != null)
        {
            Object.DestroyImmediate(stepsChild.gameObject);
        }

        // The adult's body is 0.8 m long at scale 1.
        runner.transform.localScale = Vector3.one * (length / 0.8f);
        runner.transform.SetPositionAndRotation(new Vector3(spot.x, counter.y, spot.z), Quaternion.Euler(0f, 180f, 0f));
        Animator young = runner.GetComponentInChildren<Animator>();
        young.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        AudioSource voice = runner.GetComponent<AudioSource>();

        // Off the counter's front edge, down, across the floor and under the fridge on the west wall.
        float floorY = M(V2(-16f, 14f), 0f).y;
        Vector3 edge = new Vector3(spot.x - 0.1f, counter.y, spot.z - 0.33f);
        Vector3 landing = new Vector3(edge.x - 0.1f, floorY, edge.z - 0.4f);
        Vector3 mid = M(V2(-17.6f, 12.6f), 0f);
        Vector3 hole = M(V2(-19.15f, 9.8f), 0f);
        FruitMimic mimic = mimicRoot.AddComponent<FruitMimic>();
        // Hidden until it stirs (FruitMimic shows it): in the editor one sees what the player sees first.
        runner.SetActive(false);
        mimic.Configure(closed, runner, young, player, voice, new[] { edge, landing, new Vector3(mid.x, floorY, mid.z), new Vector3(hole.x, floorY, hole.z) }, CrabClipWalkSpeed());
    }

    // ---------------------------------------------------------------- liquid in the containers

    // A volume of liquid inside an open container standing upright: a cylinder fitted inside its bounds (inset by the
    // wall's thickness), filled to `fill` of its height (Horror/Liquid cuts it at the level and draws the surface).
    private static GameObject LiquidIn(GameObject container, float fill, float inset, Material liquid)
    {
        Bounds b = RendererBounds(container);
        GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        volume.name = "Liquid";
        Object.DestroyImmediate(volume.GetComponent<Collider>());
        volume.transform.SetParent(container.transform, true);
        float bottom = b.min.y + b.size.y * 0.08f;
        float height = b.size.y * 0.9f;
        // Unity's cylinder is 2 high and 1 wide, its origin in the middle: the level is measured from that origin.
        volume.transform.position = new Vector3(b.center.x, bottom + height * 0.5f, b.center.z);
        volume.transform.rotation = Quaternion.identity;
        SetWorldScale(volume.transform, new Vector3(b.size.x * (1f - inset), height * 0.5f, b.size.z * (1f - inset)));
        MeshRenderer r = volume.GetComponent<MeshRenderer>();
        Material instance = Material(liquid.name + "_" + Mathf.RoundToInt(fill * 100f), liquid.shader);
        instance.CopyPropertiesFromMaterial(liquid);
        instance.SetFloat("_FillLevel", height * (fill - 0.5f));
        instance.renderQueue = liquid.renderQueue;
        EditorUtility.SetDirty(instance);
        r.sharedMaterial = instance;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return volume;
    }

    // A model with several open bowls (Dayse's mushroom goblet, 17/11: a big cup and a small tilted one on two stems)
    // gets one liquid per bowl. Each bowl is given in the model's own space as measured on its mesh (the lowest point
    // of the rim, the rim's centre and inner radius, and the floor): guessing them from the vertices failed, the cups
    // overlap seen from above and their steep walls barely face up. A bowl is round, not a bucket: the volume is a
    // flattened sphere centred on the rim whose lower half sits inside the cup, so its walls never poke through the shell.
    private static List<GameObject> LiquidInBowls(GameObject container, (Vector3 rim, float radius, float floorY)[] bowls, float fill, float inset, Material liquid)
    {
        var made = new List<GameObject>();
        Transform t = container.transform;
        // The measures are in the upright model (the FBX root's axis conversion already applied); ModelProp puts the
        // placement's yaw on top of that conversion, so it is taken back out here.
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(container);
        Quaternion yaw = t.rotation * Quaternion.Inverse(source != null ? source.transform.localRotation : Quaternion.identity);
        float s = source != null ? t.lossyScale.y / Mathf.Max(Mathf.Abs(source.transform.lossyScale.y), 1e-4f) : 1f;
        for (int k = 0; k < bowls.Length; k++)
        {
            (Vector3 rim, float radius, float floorY) = bowls[k];
            float depth = (rim.y - floorY) * s;
            float r = radius * s;
            Vector3 centre = t.position + yaw * (rim * s);
            GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            volume.name = "Liquid" + (k + 1);
            Object.DestroyImmediate(volume.GetComponent<Collider>());
            volume.transform.SetParent(t, true);
            volume.transform.position = centre;
            volume.transform.rotation = Quaternion.identity;
            // A sphere of diameter 1: twice the radius across, twice the depth down (only the lower half is kept).
            SetWorldScale(volume.transform, new Vector3(r * 2f * (1f - inset), depth * 2f * (1f - inset * 0.5f), r * 2f * (1f - inset)));
            MeshRenderer mr = volume.GetComponent<MeshRenderer>();
            Material instance = Material(liquid.name + "_Bowl" + Mathf.RoundToInt(fill * 100f), liquid.shader);
            instance.CopyPropertiesFromMaterial(liquid);
            // The level in metres over the rim (negative: below it), the same in every bowl.
            instance.SetFloat("_FillLevel", -depth * (1f - fill));
            instance.renderQueue = liquid.renderQueue;
            EditorUtility.SetDirty(instance);
            mr.sharedMaterial = instance;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            made.Add(volume);
        }

        return made;
    }
    private static void SetWorldScale(Transform t, Vector3 world)
    {
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(world.x / Mathf.Max(parent.x, 1e-4f), world.y / Mathf.Max(parent.y, 1e-4f), world.z / Mathf.Max(parent.z, 1e-4f));
    }

    // The buckets of the house hold the water of the leaks they stand under (the model is an empty bucket).
    private static void FillBuckets()
    {
        Material water = LiquidMaterial("LiquidLeakWater", new Color(0.05f, 0.09f, 0.08f), new Color(0.16f, 0.24f, 0.22f), 0f, 0f);
        int filled = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Bucket2" && t.Find("Liquid") == null)
            {
                LiquidIn(t.gameObject, 0.55f + 0.2f * (filled % 2), 0.16f, water);
                filled++;
            }
        }

        Debug.Log("[FloodedHouse] liquid in " + filled + " buckets.");
    }

    // The component that carries the tuned glows to the materials made above.
    private static void TuneGrowths(Transform flora)
    {
        TunedGrowths tuned = flora.gameObject.AddComponent<TunedGrowths>();
        tuned.Configure(eggShellMaterials.ToArray(), blueFungusMaterials.ToArray(), yellowFungusMaterials.ToArray(), reedBudMaterials.ToArray());
    }
}
