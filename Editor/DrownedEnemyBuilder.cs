using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The Drowned as an enemy: Assets/_Project/Prefabs/Creatures/Drowned.prefab (Horror > Testes > Criar prefab do
// Afogado; DrownedSetup also places these in the test scene).
//   Ragdoll: Unity's own ragdoll builder (internal UnityEditor.RagdollBuilder, driven by reflection) on the
//   humanoid bones; its colliders are sized for an ordinary person, so each one is then fitted to the skin bound to
//   its bone (capsule radius = 80th percentile of the vertices' distance from the bone's axis, box = the
//   vertices' extent), or shots would hit the air beside the Drowned's thin limbs.
//   The builder makes no body for the feet (they ride the shins): lying on its back the pointed toes went 13 cm
//   into the floor, so each foot gets a box fitted to the foot and toe skin, on the foot bone with no Rigidbody of
//   its own (it joins the shin's body; a shot to it finds the shin's hitbox up the hierarchy).
//   Every collider gets a CreatureHitbox (head, torso, arm, leg) pointing at its body.
//   Brain, vitals, hit reactions, claw contact and its voice.
public static class DrownedEnemyBuilder
{
    public const string PrefabPath = ProjectPaths.Root + "/Prefabs/Creatures/Drowned.prefab";
    private const string ModelPath = ProjectPaths.CreatureModels + "/Drowned/Drowned.fbx";
    private const string MaterialPath = ProjectPaths.CreatureModels + "/Drowned/Drowned.mat";
    private const string ControllerPath = ProjectPaths.Art + "/Animations/Drowned/Drowned.controller";
    private const float TotalMass = 55f;

    [MenuItem("Horror/Testes/Criar prefab do Afogado")]
    public static GameObject Build()
    {
        DrownedSetup.Import();
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        root.name = "Drowned";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Animator animator = root.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        SkinnedMeshRenderer skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
        skin.sharedMaterial = material;
        skin.updateWhenOffscreen = true;

        BuildRagdoll(animator);
        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>();
        FitColliders(skin, bodies);
        AddFeet(animator, skin);
        float massSum = bodies.Sum(b => b.mass);
        foreach (Rigidbody b in bodies)
        {
            b.mass *= TotalMass / massSum;
        }

        AudioSource voice = root.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 1.5f;
        voice.maxDistance = 25f;
        voice.rolloffMode = AudioRolloffMode.Logarithmic;

        HitReactor reactor = root.AddComponent<HitReactor>();
        root.AddComponent<HandGroundContact>();
        Ragdoll ragdoll = root.AddComponent<Ragdoll>();
        ragdoll.Configure(animator, bodies);
        CreatureVitals vitals = root.AddComponent<CreatureVitals>();
        vitals.Configure(ragdoll, reactor, voice, CombatVfxMaterials.WoundFluid);
        DrownedBrain brain = root.AddComponent<DrownedBrain>();
        brain.Configure(vitals, voice);

        foreach (Rigidbody b in bodies)
        {
            CreatureHitbox hitbox = b.gameObject.AddComponent<CreatureHitbox>();
            hitbox.owner = vitals;
            hitbox.body = b;
            hitbox.region = RegionOf(animator, b.transform);
        }

        string folder = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(ProjectPaths.Root + "/Prefabs", "Creatures");
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[Drowned] prefab with " + bodies.Length + " ragdoll bodies");
        return prefab;
    }

    private static void BuildRagdoll(Animator animator)
    {
        // Unity 6.5: the builder lives in UnityEditor.PhysicsModule and is a plain class (the window wraps it).
        System.Type type = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name == "UnityEditor.PhysicsModule")
            .Select(a => a.GetType("UnityEditor.RagdollBuilder"))
            .First(t => t != null);
        object builder = System.Activator.CreateInstance(type, true);
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        void Set(string field, HumanBodyBones bone)
        {
            type.GetField(field, Any).SetValue(builder, animator.GetBoneTransform(bone));
        }

        type.GetField("animator", Any)?.SetValue(builder, animator);
        Set("pelvis", HumanBodyBones.Hips);
        Set("leftHips", HumanBodyBones.LeftUpperLeg);
        Set("leftKnee", HumanBodyBones.LeftLowerLeg);
        Set("leftFoot", HumanBodyBones.LeftFoot);
        Set("rightHips", HumanBodyBones.RightUpperLeg);
        Set("rightKnee", HumanBodyBones.RightLowerLeg);
        Set("rightFoot", HumanBodyBones.RightFoot);
        Set("leftArm", HumanBodyBones.LeftUpperArm);
        Set("leftElbow", HumanBodyBones.LeftLowerArm);
        Set("rightArm", HumanBodyBones.RightUpperArm);
        Set("rightElbow", HumanBodyBones.RightLowerArm);
        Set("middleSpine", HumanBodyBones.Chest);
        Set("head", HumanBodyBones.Head);
        type.GetField("totalMass", Any).SetValue(builder, TotalMass);
        type.GetField("strength", Any).SetValue(builder, 0f);
        type.GetMethod("CheckConsistency", Any)?.Invoke(builder, null);
        type.GetMethod("OnWizardCreate", Any).Invoke(builder, null);
    }

    private static BodyRegion RegionOf(Animator animator, Transform bone)
    {
        if (bone == animator.GetBoneTransform(HumanBodyBones.Head))
        {
            return BodyRegion.Head;
        }

        foreach (HumanBodyBones b in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm })
        {
            if (bone == animator.GetBoneTransform(b))
            {
                return BodyRegion.Arm;
            }
        }

        foreach (HumanBodyBones b in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
        {
            if (bone == animator.GetBoneTransform(b))
            {
                return BodyRegion.Leg;
            }
        }

        return BodyRegion.Torso;
    }

    // Each body's collider fitted to the skin bound to its bone and to the bones it carries without a body of its
    // own (the hand rides the forearm, the toes the foot, the neck the head...).
    private static void FitColliders(SkinnedMeshRenderer skin, Rigidbody[] bodies)
    {
        Mesh mesh = skin.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        BoneWeight[] weights = mesh.boneWeights;
        Transform[] bones = skin.bones;
        HashSet<Transform> owners = new HashSet<Transform>(bodies.Select(b => b.transform));
        // Bake the rest pose into world space once (vertex world positions at bind).
        Mesh baked = new Mesh();
        skin.BakeMesh(baked, true);
        Vector3[] world = baked.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray();
        Dictionary<Transform, List<Vector3>> points = bodies.ToDictionary(b => b.transform, b => new List<Vector3>());
        // Only the vertices bound to the bone itself, for a limb's thickness (the foot hanging off the shin, or the
        // hand off the forearm, would make it fat).
        Dictionary<Transform, List<Vector3>> own = bodies.ToDictionary(b => b.transform, b => new List<Vector3>());
        for (int i = 0; i < vertices.Length; i++)
        {
            Transform bone = bones[weights[i].boneIndex0];
            if (own.ContainsKey(bone))
            {
                own[bone].Add(world[i]);
            }

            while (bone != null && !owners.Contains(bone))
            {
                bone = bone.parent;
            }

            if (bone != null)
            {
                points[bone].Add(world[i]);
            }
        }

        foreach (Rigidbody body in bodies)
        {
            List<Vector3> pts = points[body.transform];
            if (pts.Count < 8)
            {
                continue;
            }

            List<Vector3> local = pts.Select(p => body.transform.InverseTransformPoint(p)).ToList();
            Collider collider = body.GetComponent<Collider>();
            if (collider is CapsuleCollider capsule)
            {
                int axis = capsule.direction;
                List<Vector3> core = own[body.transform].Count >= 8 ? own[body.transform].Select(p => body.transform.InverseTransformPoint(p)).ToList() : local;
                List<float> radial = core.Select(p =>
                {
                    Vector3 d = p - capsule.center;
                    d[axis] = 0f;
                    return d.magnitude;
                }).OrderBy(r => r).ToList();
                float radius = radial[(int)(radial.Count * 0.8f)];
                // Height: the skin's extent along the axis, from the joint to the end of the part.
                float min = local.Min(p => p[axis]);
                float max = local.Max(p => p[axis]);
                capsule.radius = Mathf.Max(0.02f, radius);
                capsule.height = Mathf.Max(capsule.radius * 2f, max - min);
                Vector3 c = capsule.center;
                c[axis] = (min + max) * 0.5f;
                capsule.center = c;
            }
            else if (collider is BoxCollider box)
            {
                Vector3 lo = new Vector3(Percentile(local, 0, 0.05f), Percentile(local, 1, 0.05f), Percentile(local, 2, 0.05f));
                Vector3 hi = new Vector3(Percentile(local, 0, 0.95f), Percentile(local, 1, 0.95f), Percentile(local, 2, 0.95f));
                box.center = (lo + hi) * 0.5f;
                box.size = hi - lo;
            }
            else if (collider is SphereCollider sphere)
            {
                Vector3 centre = local.Aggregate(Vector3.zero, (a, p) => a + p) / local.Count;
                List<float> radial = local.Select(p => (p - centre).magnitude).OrderBy(r => r).ToList();
                sphere.center = centre;
                sphere.radius = radial[(int)(radial.Count * 0.85f)];
            }
        }
    }

    private static void AddFeet(Animator animator, SkinnedMeshRenderer skin)
    {
        Mesh baked = new Mesh();
        skin.BakeMesh(baked, true);
        Vector3[] vertices = baked.vertices;
        BoneWeight[] weights = skin.sharedMesh.boneWeights;
        Transform[] bones = skin.bones;
        foreach (HumanBodyBones footBone in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
        {
            Transform foot = animator.GetBoneTransform(footBone);
            List<Vector3> local = new List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (bones[weights[i].boneIndex0].IsChildOf(foot))
                {
                    local.Add(foot.InverseTransformPoint(skin.transform.TransformPoint(vertices[i])));
                }
            }

            if (local.Count < 8)
            {
                continue;
            }

            Vector3 lo = new Vector3(Percentile(local, 0, 0.05f), Percentile(local, 1, 0.05f), Percentile(local, 2, 0.05f));
            Vector3 hi = new Vector3(Percentile(local, 0, 0.95f), Percentile(local, 1, 0.95f), Percentile(local, 2, 0.95f));
            BoxCollider box = foot.gameObject.AddComponent<BoxCollider>();
            box.center = (lo + hi) * 0.5f;
            box.size = hi - lo;
        }
    }

    private static float Percentile(List<Vector3> pts, int axis, float q)
    {
        List<float> v = pts.Select(p => p[axis]).OrderBy(x => x).ToList();
        return v[Mathf.Clamp((int)(v.Count * q), 0, v.Count - 1)];
    }
}
