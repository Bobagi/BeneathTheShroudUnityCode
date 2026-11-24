using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Horror > Testes > Combate na cena aberta: in a scene with the game's player (the suit test scene), hangs the Colt
// Python on the first person rig (both hands on it in the rig's clips, torch on the chest) and puts three Drowned in
// the open, so shooting, hit reactions and the ragdoll can be tried. The suit test scene generator calls Arm() too.
public static class CombatTestSetup
{
    // The Colt Python .357 (Art/Items/Python357, made by Python357Setup) is 28 cm long, palm point of the grip at its origin,
    // barrel along +Z. A little over real size gives a grip a hand fits around and a gun that reads on screen. The
    // Revolver_Hold grip (the MoCap Online pistol hand, gun fitted to it on the skin, 10/10) was made at 1.2: changing
    // this means refitting the weapon socket (SourceAssets/Tools/revolver_grip_fit_blender.py + GunSocketFromTargets).
    private const float ModelScale = 1.2f;
    private const string RevolverPrefab = Python357Setup.PrefabPath;
    private const string RoundMeshPath = Python357Setup.Folder + "/Python357_Round.asset";

    // Where the three stand (feet), facing the player's start.
    private static readonly Vector3[] Spots = { new Vector3(-5.5f, 0f, 5f), new Vector3(5.5f, 0f, 7f), new Vector3(0.5f, 0f, 11f) };

    [MenuItem("Horror/Testes/Combate na cena aberta")]
    public static void Open()
    {
        Arm(Object.FindAnyObjectByType<FirstPersonController>());
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // The gun for the game (the house, 17/10): the Colt Python on the rig with its chest mount, six rounds and no
    // spare (GDD: the demo gives no magnum ammunition), no CombatTestStart (the story gives the torch) and no enemies.
    public static Revolver ArmStory(FirstPersonController controller)
    {
        PlayerBody body = controller.GetComponentInChildren<PlayerBody>(true);
        FirstPersonArms arms = controller.CameraPivot.GetComponentInChildren<FirstPersonArms>(true);
        ViewModelMotion motion = controller.CameraPivot.GetComponentInChildren<ViewModelMotion>(true);
        if (arms == null || body == null)
        {
            Debug.LogError("[Combat] the player has no first person arms rig or body: the revolver was not built");
            return null;
        }

        Revolver existing = controller.CameraPivot.GetComponentInChildren<Revolver>(true);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        Transform oldLoader = arms.LeftSocket.Find("Speedloader");
        if (oldLoader != null)
        {
            Object.DestroyImmediate(oldLoader.gameObject);
        }

        Revolver revolver = BuildRevolver(arms, motion, controller, body);
        SerializedObject so = new SerializedObject(revolver);
        so.FindProperty("spare").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        // Holstered until the inventory draws it (Inventory.Equip turns the object on).
        revolver.gameObject.SetActive(false);

        TorchHold torch = controller.CameraPivot.GetComponentInChildren<TorchHold>(true);
        ChestTorchMount mount = controller.GetComponent<ChestTorchMount>() ?? controller.gameObject.AddComponent<ChestTorchMount>();
        SerializedObject m = new SerializedObject(mount);
        m.FindProperty("controller").objectReferenceValue = controller;
        m.FindProperty("body").objectReferenceValue = body.GetComponent<Animator>();
        m.FindProperty("torch").objectReferenceValue = torch;
        m.ApplyModifiedPropertiesWithoutUndo();
        return revolver;
    }

    // enemies false: only the gun (the suit test scene is a shooting range for now, 13/10).
    public static void Arm(FirstPersonController controller, bool enemies = true)
    {
        if (controller == null)
        {
            Debug.LogWarning("[Combat] no player in the open scene");
            return;
        }

        foreach (GameObject old in Object.FindObjectsByType<DrownedBrain>().Select(b => b.gameObject).ToArray())
        {
            Object.DestroyImmediate(old);
        }

        if (enemies)
        {
            GameObject prefab = DrownedEnemyBuilder.Build();
            Transform group = GameObject.Find("Enemies")?.transform ?? new GameObject("Enemies").transform;
            Vector3 player = controller.transform.position;
            foreach (Vector3 spot in Spots)
            {
                GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                Vector3 face = player - spot;
                face.y = 0f;
                enemy.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(face));
            }
        }

        PlayerBody body = controller.GetComponentInChildren<PlayerBody>(true) ?? Object.FindAnyObjectByType<PlayerBody>();
        FirstPersonArms arms = controller.CameraPivot.GetComponentInChildren<FirstPersonArms>(true);
        ViewModelMotion motion = controller.CameraPivot.GetComponentInChildren<ViewModelMotion>(true);
        if (arms == null)
        {
            Debug.LogWarning("[Combat] the player has no first person arms rig (FirstPersonArms): regenerate the player");
            return;
        }

        Revolver existing = controller.CameraPivot.GetComponentInChildren<Revolver>(true);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        Transform oldLoader = arms.LeftSocket.Find("Speedloader");
        if (oldLoader != null)
        {
            Object.DestroyImmediate(oldLoader.gameObject);
        }

        Revolver revolver = BuildRevolver(arms, motion, controller, body);
        SerializedObject so = new SerializedObject(revolver);
        so.FindProperty("spare").intValue = 60;
        so.ApplyModifiedPropertiesWithoutUndo();

        TorchHold torch = controller.CameraPivot.GetComponentInChildren<TorchHold>(true);
        ChestTorchMount mount = controller.GetComponent<ChestTorchMount>() ?? controller.gameObject.AddComponent<ChestTorchMount>();
        SerializedObject m = new SerializedObject(mount);
        m.FindProperty("controller").objectReferenceValue = controller;
        m.FindProperty("body").objectReferenceValue = body.GetComponent<Animator>();
        m.FindProperty("torch").objectReferenceValue = torch;
        m.ApplyModifiedPropertiesWithoutUndo();
        CombatTestStart start = controller.GetComponent<CombatTestStart>() ?? controller.gameObject.AddComponent<CombatTestStart>();
        SerializedObject s = new SerializedObject(start);
        s.FindProperty("mount").objectReferenceValue = mount;
        s.FindProperty("gear").objectReferenceValue = Object.FindAnyObjectByType<DiveGear>();
        s.ApplyModifiedPropertiesWithoutUndo();
    }

    // The gun on the rig's weapon socket (hand_r/WeaponSocket, measured so the grip sits in the right palm), its
    // speedloader on the left socket (shown by the reload clip). Where the hands are on it is the rig's clips.
    private static Revolver BuildRevolver(FirstPersonArms arms, ViewModelMotion motion, FirstPersonController controller, PlayerBody body)
    {
        GameObject holder = new GameObject("Revolver");
        arms.Attach(holder.transform, arms.WeaponSocket);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RevolverPrefab), holder.transform);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (Component c in model.GetComponents<Component>().Where(c => !(c is Transform)).ToArray())
        {
            Object.DestroyImmediate(c);
        }

        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * ModelScale;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer skinned)
            {
                skinned.updateWhenOffscreen = true;
                // The six rounds of the rig stay on the speedloader (they do not follow the cylinder in the game).
                skinned.enabled = !skinned.name.StartsWith("Bullet");
            }
        }

        // The gun's own skin, at rest: the top of the frame and the bore (in the model's units, the scale applies on top).
        SkinnedMeshRenderer gunSkin = model.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == "Revolver");
        Mesh baked = new Mesh();
        gunSkin.BakeMesh(baked, true);
        Vector3[] pts = baked.vertices.Select(v => model.transform.InverseTransformPoint(gunSkin.transform.TransformPoint(v))).ToArray();
        // The exported model's +Z is not the bore: its barrel runs about 10 degrees down from it (09/10, measured: the
        // rib falls 15 mm over 9 cm). The barrel's line is measured on the mesh and the model turned to put it on +Z, so
        // the holder's +Z (the socket's, keyed in the clips) is where the bullets fly.
        Vector3 boreDirection = BoreDirection(pts);
        model.transform.localRotation = Quaternion.FromToRotation(boreDirection, Vector3.forward);
        float front = pts.Max(p => p.z);
        float bore = pts.Where(p => p.z > front - 0.012f).Average(p => p.y);
        float muzzleZ = front + 0.01f;
        bore += boreDirection.y / boreDirection.z * (muzzleZ - front);
        Debug.Log($"[Combat] revolver front {front:F3}, bore height at the muzzle {bore:F3}, barrel {Vector3.Angle(boreDirection, Vector3.forward):F1} degrees off the model's +Z");

        Transform muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(model.transform, false);
        muzzle.localPosition = new Vector3(0f, bore, muzzleZ);
        muzzle.localRotation = Quaternion.LookRotation(boreDirection, Vector3.up);
        Light flash = new GameObject("MuzzleFlash").AddComponent<Light>();
        flash.transform.SetParent(muzzle, false);
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.72f, 0.4f);
        flash.intensity = 9f;
        flash.range = 7f;
        flash.shadows = LightShadows.None;
        ParticleSystem smoke = BuildSmoke(muzzle);

        AudioSource source = holder.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        Revolver revolver = holder.AddComponent<Revolver>();
        SerializedObject so = new SerializedObject(revolver);
        so.FindProperty("controller").objectReferenceValue = controller;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("arms").objectReferenceValue = arms;
        so.FindProperty("motion").objectReferenceValue = motion;
        so.FindProperty("model").objectReferenceValue = model.transform;
        so.FindProperty("muzzle").objectReferenceValue = muzzle;
        Transform cylinderBone = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Cylinder");
        so.FindProperty("cylinder").objectReferenceValue = cylinderBone;
        // The cylinder's real axis: the middle of the vertices skinned to its bone (six-fold symmetric, so the middle is
        // on the axis), in the bone's space. The bone itself sits 3 mm off it.
        int cylinderIndex = System.Array.IndexOf(gunSkin.bones, cylinderBone);
        if (cylinderIndex >= 0)
        {
            BoneWeight[] weights = gunSkin.sharedMesh.boneWeights;
            Vector3[] bakedVertices = baked.vertices;
            Vector3[] onCylinder = Enumerable.Range(0, bakedVertices.Length)
                .Where(i => weights[i].boneIndex0 == cylinderIndex && weights[i].weight0 > 0.9f)
                .Select(i => cylinderBone.InverseTransformPoint(gunSkin.transform.TransformPoint(bakedVertices[i])))
                .ToArray();
            Vector3 middle = onCylinder.Aggregate(Vector3.zero, (sum, v) => sum + v) / Mathf.Max(1, onCylinder.Length);
            so.FindProperty("cylinderPivot").vector3Value = middle;
            Debug.Log($"[Combat] cylinder axis {new Vector2(middle.x, middle.z).magnitude * 1000f:F1} mm from its bone");
        }

        so.FindProperty("hammer").objectReferenceValue = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hammer");
        so.FindProperty("flash").objectReferenceValue = flash;
        so.FindProperty("flashSmoke").objectReferenceValue = smoke;
        so.FindProperty("source").objectReferenceValue = source;
        so.FindProperty("impactMaterial").objectReferenceValue = CombatVfxMaterials.BulletDust;
        so.FindProperty("crosshairPrefab").objectReferenceValue = RevolverCrosshairPrefab.Get();
        so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        so.ApplyModifiedPropertiesWithoutUndo();
        // Muzzle star and sparks, decals, blood, smoke and the spent cases.
        CombatEffectsSetup.EnsureDecalFeature();
        CombatEffectsSetup.Apply(revolver);

        // The speedloader in the left hand (the reload clip shows and hides it): a hub and six rounds in the ring of
        // the chambers, on the rig's left socket, hidden by default.
        Renderer[] loaderRounds = BuildSpeedloader(arms.LeftSocket, model, gunSkin, cylinderBone);
        SerializedObject animated = new SerializedObject(revolver);
        SerializedProperty rounds = animated.FindProperty("speedloaderRounds");
        rounds.arraySize = loaderRounds.Length;
        for (int i = 0; i < loaderRounds.Length; i++)
        {
            rounds.GetArrayElementAtIndex(i).objectReferenceValue = loaderRounds[i];
        }

        animated.ApplyModifiedPropertiesWithoutUndo();
        return revolver;
    }

    // Six rounds standing out of a hub along the loader's +Y, on the ring of the chambers (measured on the rig's round
    // bones around the cylinder's axis), the loader's origin at the rounds' bases. Returns the rounds' renderers,
    // Round1..6, in the order of the chambers.
    private static Renderer[] BuildSpeedloader(Transform socket, GameObject model, SkinnedMeshRenderer gunSkin, Transform cylinder)
    {
        Transform loader = new GameObject("Speedloader").transform;
        loader.SetParent(socket, false);
        Transform[] bones = Enumerable.Range(1, 6)
            .Select(i => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Bullet" + i && t.Find("Bullet" + i + "_end") != null))
            .ToArray();
        Material roundsMaterial = AssetDatabase.LoadAssetAtPath<Material>(Python357Setup.Folder + "/Python357_Rounds.mat");
        Material hubMaterial = AssetDatabase.LoadAssetAtPath<Material>(Python357Setup.Folder + "/Python357_SpeedLoader.mat");
        Mesh round = RoundMesh(model, cylinder);

        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(hub.GetComponent<Collider>());
        hub.name = "Hub";
        hub.transform.SetParent(loader, false);
        hub.transform.localPosition = new Vector3(0f, -0.005f, 0f);
        hub.transform.localScale = new Vector3(0.031f, 0.005f, 0.031f);
        Renderer hubRenderer = hub.GetComponent<Renderer>();
        hubRenderer.sharedMaterial = hubMaterial;
        hubRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        List<Renderer> result = new List<Renderer>();
        for (int i = 0; i < 6; i++)
        {
            // Each chamber's place around the cylinder's axis (the bone's Y), in the loader's plane.
            Vector3 p = bones[i] != null && cylinder != null ? cylinder.InverseTransformPoint(bones[i].position) : Vector3.zero;
            Vector2 ring = bones[i] != null ? new Vector2(p.x, p.z) * ModelScale : new Vector2(Mathf.Cos(i * Mathf.PI / 3f), Mathf.Sin(i * Mathf.PI / 3f)) * 0.012f;
            GameObject r = new GameObject("Round" + (i + 1));
            r.transform.SetParent(loader, false);
            r.transform.localPosition = new Vector3(ring.x, 0f, ring.y);
            r.transform.localScale = Vector3.one * ModelScale;
            r.AddComponent<MeshFilter>().sharedMesh = round;
            MeshRenderer mr = r.AddComponent<MeshRenderer>();
            mr.sharedMaterial = roundsMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            result.Add(mr);
        }

        loader.gameObject.SetActive(false);
        return result.ToArray();
    }

    // One of the rig's rounds as a plain mesh: origin at the middle of its base, tip toward +Y.
    private static Mesh RoundMesh(GameObject model, Transform cylinder)
    {
        SkinnedMeshRenderer source = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Bullet1");
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RoundMeshPath);
        if (source == null)
        {
            return mesh;
        }

        Mesh baked = new Mesh();
        source.BakeMesh(baked, true);
        Vector3 front = cylinder != null ? model.transform.InverseTransformDirection(-cylinder.up).normalized : Vector3.forward;
        Vector3[] v = baked.vertices.Select(p => model.transform.InverseTransformPoint(source.transform.TransformPoint(p))).ToArray();
        Vector3[] n = baked.normals.Select(d => model.transform.InverseTransformDirection(source.transform.TransformDirection(d))).ToArray();
        Quaternion toLocal = Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up, front));
        Vector3 middle = v.Aggregate(Vector3.zero, (s, p) => s + p) / v.Length;
        float back = v.Min(p => Vector3.Dot(p - middle, front));
        Vector3 basePoint = middle + front * back;

        bool fresh = mesh == null;
        if (fresh)
        {
            mesh = new Mesh();
        }

        mesh.Clear();
        mesh.name = "Python357_Round";
        mesh.vertices = v.Select(p => toLocal * (p - basePoint)).ToArray();
        mesh.normals = n.Select(d => toLocal * d).ToArray();
        mesh.uv = baked.uv;
        mesh.triangles = baked.triangles;
        mesh.RecalculateBounds();
        if (fresh)
        {
            AssetDatabase.CreateAsset(mesh, RoundMeshPath);
        }
        else
        {
            EditorUtility.SetDirty(mesh);
        }

        return mesh;
    }

    // The barrel's direction in the model's space: the top of the rib along the barrel's front half (slices from half
    // the length to the front sight), a robust slope (the median of the slopes between slices: the rib's vent cuts
    // make single slices low), and the middle of the barrel sideways the same way.
    private static Vector3 BoreDirection(Vector3[] pts)
    {
        float front = pts.Max(p => p.z);
        var slices = Enumerable.Range(0, 8)
            .Select(i => (z0: front * (0.5f + 0.05f * i), z1: front * (0.55f + 0.05f * i)))
            .Select(s => pts.Where(p => p.z >= s.z0 && p.z < s.z1).ToArray())
            .Where(s => s.Length > 0)
            .Select(s => new Vector3((s.Min(p => p.x) + s.Max(p => p.x)) * 0.5f, s.Max(p => p.y), s.Average(p => p.z)))
            .ToArray();
        var up = new List<float>();
        var side = new List<float>();
        for (int i = 0; i < slices.Length; i++)
        {
            for (int j = i + 1; j < slices.Length; j++)
            {
                float dz = slices[j].z - slices[i].z;
                up.Add((slices[j].y - slices[i].y) / dz);
                side.Add((slices[j].x - slices[i].x) / dz);
            }
        }

        up.Sort();
        side.Sort();
        return new Vector3(side[side.Count / 2], up[up.Count / 2], 1f).normalized;
    }

    private static ParticleSystem BuildSmoke(Transform muzzle)
    {
        GameObject go = new GameObject("MuzzleSmoke");
        go.transform.SetParent(muzzle, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new Color(0.75f, 0.74f, 0.72f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.005f;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = CombatVfxMaterials.GunSmoke;
        return ps;
    }
}
