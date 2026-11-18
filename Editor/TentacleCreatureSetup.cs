using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The tentacled creatures: the loose tentacle (3 m, seen through windows) and the colossal octopus (12 m across,
// seven arms: that is what the Meshy model came with). Both were reduced and rigged outside the project by the
// game-3d-assets skill (optimize_model.py, then rig_tentacles.py) into SourceAssets/BlenderExports/Creatures.
// Horror > Testes > Tentáculo e polvo na cena aberta: copies them into Art/Models/Creature, sets the import up
// (checklist of the skill: no imported materials, the FBX normals, Mikk tangents, normal map and linear
// metallic/smoothness), builds Prefabs/Creatures/Tentacle.prefab and Octopus.prefab and places both in the open
// scene. Prefab layout: an empty root whose +Y is the side the arms curl toward (the octopus' mantle side, the
// tentacle's back) with the model under it turned to match, so placing one is just a position and a yaw;
// TentacleRig with one chain per arm; a capsule per bone fitted to the skin it carries (TentacleHitbox), the
// mantle's on its own bones, all under one kinematic body.
public static class TentacleCreatureSetup
{
    private const string SourceRoot = "SourceAssets/BlenderExports/Creatures";

    [MenuItem("Horror/Testes/Tentáculo e polvo na cena aberta")]
    public static void PlaceInOpenScene()
    {
        GameObject tentacle = Build("Tentacle");
        GameObject octopus = Build("Octopus");
        foreach (string old in new[] { "Tentacle_Test", "Octopus_Test", "Octopus_TestGround" })
        {
            GameObject found = GameObject.Find(old);
            if (found != null)
            {
                Object.DestroyImmediate(found);
            }
        }

        // The tentacle stands out of the ground in the open; the octopus (12 m across) lies flat beyond the wall.
        GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(tentacle);
        t.name = "Tentacle_Test";
        t.transform.SetPositionAndRotation(new Vector3(-3.2f, 0f, 3.8f), Quaternion.Euler(-90f, 30f, 0f));
        // The octopus needs ground out to z 34: its own piece only when the scene's floor stops short of it (the
        // test floor is 120 m since 07/10; two floors at the same height flickered against each other).
        GameObject floor = GameObject.Find("Floor");
        Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorRenderer == null || floorRenderer.bounds.max.z < 34f)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Octopus_TestGround";
            ground.transform.SetPositionAndRotation(new Vector3(0f, -0.1f, 23f), Quaternion.identity);
            ground.transform.localScale = new Vector3(24f, 0.2f, 22f);
            if (floorRenderer != null)
            {
                ground.GetComponent<Renderer>().sharedMaterial = floorRenderer.sharedMaterial;
            }
        }

        GameObject o = (GameObject)PrefabUtility.InstantiatePrefab(octopus);
        o.name = "Octopus_Test";
        o.transform.SetPositionAndRotation(new Vector3(0f, 0f, 23f), Quaternion.Euler(0f, 180f, 0f));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.scene);
    }

    public static GameObject Build(string name)
    {
        string folder = ProjectPaths.CreatureModels + "/" + name;
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(ProjectPaths.CreatureModels, name);
        }

        string modelPath = folder + "/" + name + ".fbx";
        Copy(SourceRoot + "/" + name + "/Rigged/" + name + ".fbx", modelPath);
        foreach (string map in new[] { "BaseColor", "Normal", "MetallicSmoothness" })
        {
            Copy(SourceRoot + "/" + name + "/" + name + "_" + map + ".png", folder + "/" + name + "_" + map + ".png");
        }

        AssetDatabase.Refresh();
        ModelImporter model = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        model.animationType = ModelImporterAnimationType.Generic;
        model.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        model.importAnimation = false;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.SaveAndReimport();
        Texture(folder + "/" + name + "_Normal.png", TextureImporterType.NormalMap, true);
        Texture(folder + "/" + name + "_MetallicSmoothness.png", TextureImporterType.Default, false);
        Texture(folder + "/" + name + "_BaseColor.png", TextureImporterType.Default, true);
        Material material = Material(folder, name);

        GameObject root = new GameObject(name);
        GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        body.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(body.GetComponent<Animator>());
        SkinnedMeshRenderer skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
        skin.sharedMaterial = material;
        skin.updateWhenOffscreen = true;

        List<TentacleRig.Chain> chains = FindChains(body.transform);
        Vector3 up = name == "Octopus" ? PlaneNormal(body.transform, chains, skin) : TentacleBack(chains);
        // Model turned so the curl side is the root's +Y, and (tentacle) the arm runs along the root's +Z.
        Vector3 forward = name == "Octopus" ? Vector3.ProjectOnPlane(Vector3.forward, up) : (chains[0].bones.Last().position - chains[0].bones[0].position);
        if (forward.sqrMagnitude < 1e-4f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        }

        Quaternion modelToRoot = Quaternion.Inverse(Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, up), up));
        body.transform.localRotation = modelToRoot * body.transform.localRotation;
        body.transform.localPosition = modelToRoot * body.transform.localPosition;
        if (name == "Tentacle")
        {
            // Base at the root.
            body.transform.localPosition -= root.transform.InverseTransformPoint(chains[0].bones[0].position);
        }
        else
        {
            // Mouth over the root, the arm tips on the root's floor. Measured on the model: the arms run down from
            // the body (bases ~1.9 m up) to the tips, and the end of the mantle sac hangs 0.4 m below the tips;
            // resting the lowest point of the whole skin left the arms in the air, so the arms rest and the end of
            // the sac sinks into the ground (or the water).
            Vector3 mouth = body.GetComponentsInChildren<Transform>().First(t => t.name == "Root").position;
            Mesh rest = new Mesh();
            skin.BakeMesh(rest, true);
            Vector3[] restVerts = rest.vertices;
            BoneWeight[] restWeights = skin.sharedMesh.boneWeights;
            float lowest = Enumerable.Range(0, restVerts.Length)
                .Where(i => skin.bones[restWeights[i].boneIndex0].name.StartsWith("Arm"))
                .Select(i => skin.transform.TransformPoint(restVerts[i]).y)
                .OrderBy(y => y).ElementAt(restVerts.Length / 200);
            body.transform.localPosition -= new Vector3(mouth.x, lowest, mouth.z);
        }

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        AudioSource voice = root.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 4f;
        voice.maxDistance = 80f;
        voice.rolloffMode = AudioRolloffMode.Logarithmic;
        TentacleRig rig = root.AddComponent<TentacleRig>();
        AudioClip[] roars = new[] { "roar_01.ogg", "roar_02.ogg", "roar_03.ogg", "creature_roar_1.wav", "creature_roar_2.wav" }
            .Select(f => AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Creature/" + f)).Where(c => c != null).ToArray();
        rig.Configure(chains.ToArray(), Vector3.up, voice, roars, CombatVfxMaterials.WoundFluid);
        Colliders(skin, chains, rig, body.transform);

        string prefabPath = ProjectPaths.Root + "/Prefabs/Creatures/" + name + ".prefab";
        int tris = skin.sharedMesh.triangles.Length / 3;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[Tentacles] {name}: {chains.Count} chains, {chains.Sum(c => c.bones.Length)} bones, {tris} triangles");
        return prefab;
    }

    private static void Copy(string from, string to)
    {
        if (!File.Exists(from))
        {
            throw new FileNotFoundException("Rigged source missing (run the game-3d-assets skill first)", from);
        }

        File.Copy(from, to, true);
    }

    private static void Texture(string path, TextureImporterType type, bool srgb)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = type;
        importer.sRGBTexture = srgb;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static Material Material(string folder, string name)
    {
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_BaseColor.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_Normal.png"));
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_MetallicSmoothness.png"));
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Smoothness", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // Chains by the rig's bone names: Tentacle_00.. for the loose tentacle, Arm{k}_00.. for each octopus arm.
    private static List<TentacleRig.Chain> FindChains(Transform model)
    {
        // By name (the mesh and the FBX root share the creature's name; bones are unique).
        Dictionary<string, Transform> bones = model.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
        List<TentacleRig.Chain> chains = new List<TentacleRig.Chain>();
        foreach (string prefix in new[] { "Tentacle_" }.Concat(Enumerable.Range(0, 16).Select(k => "Arm" + k + "_")))
        {
            List<Transform> chain = new List<Transform>();
            for (int i = 0; bones.TryGetValue(prefix + i.ToString("00"), out Transform b); i++)
            {
                chain.Add(b);
            }

            if (chain.Count > 0)
            {
                chains.Add(new TentacleRig.Chain { name = prefix.TrimEnd('_'), bones = chain.ToArray() });
            }
        }

        return chains;
    }

    // The plane the octopus' arms lie in: the two most different arm directions; its normal points to the side the
    // mantle bulges (vertices near the centre, on average).
    private static Vector3 PlaneNormal(Transform model, List<TentacleRig.Chain> chains, SkinnedMeshRenderer skin)
    {
        Transform root = model.GetComponentsInChildren<Transform>().First(t => t.name == "Root");
        Vector3 centre = root.position;
        Vector3[] dirs = chains.Select(c => (c.bones.Last().position - centre).normalized).ToArray();
        Vector3 best = Vector3.up;
        float bestArea = 0f;
        for (int i = 0; i < dirs.Length; i++)
        {
            for (int j = i + 1; j < dirs.Length; j++)
            {
                Vector3 n = Vector3.Cross(dirs[i], dirs[j]);
                if (n.magnitude > bestArea)
                {
                    bestArea = n.magnitude;
                    best = n.normalized;
                }
            }
        }

        Mesh baked = new Mesh();
        skin.BakeMesh(baked, true);
        float reach = chains.Max(c => (c.bones[0].position - centre).magnitude);
        float side = baked.vertices.Select(v => skin.transform.TransformPoint(v) - centre)
            .Where(d => d.magnitude < reach).Sum(d => Vector3.Dot(d, best));
        return side >= 0f ? best : -best;
    }

    // The loose tentacle curls toward its back (the side away from the suckers, which Meshy put underneath: world
    // down of the model as generated).
    private static Vector3 TentacleBack(List<TentacleRig.Chain> chains)
    {
        Vector3 along = (chains[0].bones.Last().position - chains[0].bones[0].position).normalized;
        return Vector3.ProjectOnPlane(Vector3.up, along).normalized;
    }

    // A capsule per bone, fitted to the skin bound to that bone (radius: 80th percentile of the distance from the
    // bone's segment); the mantle bones too. Each gets the hitbox that tells the rig which arm was shot.
    private static void Colliders(SkinnedMeshRenderer skin, List<TentacleRig.Chain> chains, TentacleRig rig, Transform model)
    {
        Mesh baked = new Mesh();
        skin.BakeMesh(baked, true);
        Vector3[] world = baked.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
        BoneWeight[] weights = skin.sharedMesh.boneWeights;
        Transform[] skinBones = skin.bones;
        Dictionary<Transform, List<Vector3>> owned = new Dictionary<Transform, List<Vector3>>();
        for (int i = 0; i < world.Length; i++)
        {
            Transform b = skinBones[weights[i].boneIndex0];
            if (!owned.TryGetValue(b, out List<Vector3> list))
            {
                owned[b] = list = new List<Vector3>();
            }

            list.Add(world[i]);
        }

        for (int c = 0; c < chains.Count; c++)
        {
            Transform[] bones = chains[c].bones;
            for (int i = 0; i < bones.Length; i++)
            {
                Vector3 end = i + 1 < bones.Length ? bones[i + 1].position : bones[i].position + (bones[i].position - bones[i - 1].position);
                Capsule(bones[i], end, owned, rig, c, i);
            }
        }

        foreach (string mantle in new[] { "Root", "Mantle_1", "Mantle_2" })
        {
            Transform b = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == mantle);
            if (b == null || !owned.ContainsKey(b))
            {
                continue;
            }

            Transform next = b.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Mantle"));
            Vector3 end = next != null ? next.position : owned[b].Aggregate(Vector3.zero, (s, p) => s + p) / owned[b].Count;
            Capsule(b, end, owned, rig, -1, 0);
        }
    }

    private static void Capsule(Transform bone, Vector3 end, Dictionary<Transform, List<Vector3>> owned, TentacleRig rig, int chain, int index)
    {
        Vector3 a = bone.position;
        Vector3 segment = end - a;
        float length = segment.magnitude;
        if (length < 1e-4f)
        {
            return;
        }

        float radius = 0.05f;
        if (owned.TryGetValue(bone, out List<Vector3> pts) && pts.Count >= 6)
        {
            List<float> d = pts.Select(p =>
            {
                float t = Mathf.Clamp01(Vector3.Dot(p - a, segment) / (length * length));
                return (p - (a + segment * t)).magnitude;
            }).OrderBy(x => x).ToList();
            radius = d[(int)(d.Count * 0.8f)];
        }

        CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        Vector3 local = bone.InverseTransformVector(segment);
        int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
        float scale = bone.lossyScale[axis];
        capsule.direction = axis;
        capsule.center = bone.InverseTransformPoint(a + segment * 0.5f);
        capsule.radius = radius / Mathf.Max(1e-4f, Mathf.Max(bone.lossyScale.x, bone.lossyScale.z));
        capsule.height = (length + radius * 2f) / Mathf.Max(1e-4f, scale);
        TentacleHitbox hitbox = bone.gameObject.AddComponent<TentacleHitbox>();
        hitbox.rig = rig;
        hitbox.chain = chain;
        hitbox.bone = index;
    }
}
