using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Builds the lantern and revolver as WorldItem prefabs (with their ItemDefinitions) and wires the
// player: hand/belt sockets, inventory, hands IK controller, HUD, camera shake, footsteps.
public static class PrototypeItemBuilder
{
    private const string ItemFolder = ProjectPaths.ItemPrefabs;
    private const string AudioFolder = ProjectPaths.Audio;

    // Measured in Play Mode during the Idle animation: this pose makes the socket level with the
    // world and facing the character's forward while the arm hangs at rest.
    private static readonly Vector3 RightSocketLocalPosition = new Vector3(0.0183f, -0.0467f, -0.002f);
    private static readonly Quaternion RightSocketLocalRotation = new Quaternion(-0.29015f, 0.87255f, -0.21227f, -0.33077f);
    private static readonly Quaternion BeltSocketLocalRotation = Quaternion.Euler(359.46f, 354.64f, 1.47f);

    [MenuItem("Horror/Prototype/Build Items")]
    public static void BuildItems()
    {
        BattlefieldTerrainBuilder.EnsureFolder(ItemFolder);
        BuildLantern();
        BuildRevolver();
        AssetDatabase.SaveAssets();
        Debug.Log("[Prototype] Items built.");
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogWarning("[Prototype] Missing asset " + path);
        }

        return asset;
    }

    private static AudioClip Clip(string relative) => Load<AudioClip>($"{AudioFolder}/{relative}");

    private static ItemDefinition Definition(string name)
    {
        string path = $"{ItemFolder}/{name}.asset";
        ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        return definition;
    }

    public static void SetField(Object target, string field, object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[Prototype] {target.GetType().Name}.{field} not found");
            return;
        }

        switch (value)
        {
            case Object reference:
                property.objectReferenceValue = reference;
                break;
            case float number:
                property.floatValue = number;
                break;
            case int integer:
                property.intValue = integer;
                break;
            case bool flag:
                property.boolValue = flag;
                break;
            case Vector3 vector:
                property.vector3Value = vector;
                break;
            case Object[] references:
                property.arraySize = references.Length;
                for (int i = 0; i < references.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = references[i];
                }

                break;
            case null:
                property.objectReferenceValue = null;
                break;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void StripLegacy(GameObject model)
    {
        // The previous pickup system's components and joints must not ride along.
        foreach (Joint joint in model.GetComponentsInChildren<Joint>(true))
        {
            Object.DestroyImmediate(joint);
        }

        foreach (MonoBehaviour behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.DestroyImmediate(behaviour);
        }

        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.DestroyImmediate(body);
        }

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }

        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
        {
            Object.DestroyImmediate(animator);
        }
    }

    // Exact bounds from mesh vertices, in the root's space. Renderer.bounds is not reliable here:
    // a SkinnedMeshRenderer reports its authored (often stale) local bounds.
    private static Bounds LocalBounds(Transform root)
    {
        bool hasPoint = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null;
            Matrix4x4 toRoot;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh();
                skinned.BakeMesh(mesh, true);
                toRoot = root.worldToLocalMatrix * Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            }
            else
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                mesh = filter != null ? filter.sharedMesh : null;
                toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            }

            if (mesh == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 point = toRoot.MultiplyPoint3x4(vertex);
                if (!hasPoint)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasPoint = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return bounds;
    }

    private static Transform Child(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = localPosition;
        return child;
    }

    private static AudioSource AddAudio(GameObject go, float spatial, float minDistance, float maxDistance)
    {
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatial;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
        return source;
    }

    private static void BuildLantern()
    {
        ItemDefinition definition = Definition("LanternDefinition");
        definition.displayName = "Lamparina";
        definition.kind = ItemKind.Lantern;
        definition.heldPositionOffset = new Vector3(0f, -0.01f, 0f);
        definition.heldRotationEuler = Vector3.zero;
        definition.visibleWhenStowed = true;
        definition.stowedPositionOffset = new Vector3(-0.2f, -0.02f, 0.05f);
        definition.stowedRotationEuler = Vector3.zero;
        definition.aimForwardLocal = Vector3.forward;
        definition.aimUpLocal = Vector3.up;
        definition.raisedReach = 0.5f;
        definition.raisedHeightOffset = 0.12f;
        definition.keepUprightWhenRaised = true;
        definition.zoomCameraWhenRaised = false;
        definition.showReticleWhenRaised = false;
        definition.pickupSound = Clip("Handling/Lantern_Rattle.ogg");
        definition.dropSound = Clip("Handling/Drop_Metal.ogg");
        definition.equipSound = Clip("Handling/Lantern_Rattle.ogg");
        EditorUtility.SetDirty(definition);

        GameObject root = new GameObject("Lantern");
        GameObject source = Load<GameObject>(ProjectPaths.Items + "/OldLantern/lantern.prefab");
        GameObject model = (GameObject)Object.Instantiate(source);
        model.name = "Model";
        StripLegacy(model);
        model.transform.SetParent(root.transform, false);
        model.transform.localScale = Vector3.one * 1.5f;

        // Stand the lantern on its base: move the model so the lowest point sits at y = 0.
        Bounds bounds = LocalBounds(root.transform);
        model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        bounds = LocalBounds(root.transform);

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 1.4f;
        body.linearDamping = 0.1f;
        body.angularDamping = 0.5f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // The bail's top is where fingers close; the lantern hangs straight below it.
        Transform grip = Child(root.transform, "Grip", new Vector3(0f, bounds.max.y - 0.012f, 0f));

        Light flame = root.GetComponentInChildren<Light>(true);
        if (flame == null)
        {
            flame = Child(root.transform, "Flame", new Vector3(0f, bounds.size.y * 0.35f, 0f)).gameObject.AddComponent<Light>();
        }

        flame.gameObject.name = "Flame";
        flame.type = LightType.Point;
        flame.color = new Color(1f, 0.62f, 0.3f);
        flame.range = 13f;
        flame.intensity = 6f;
        flame.shadows = LightShadows.Soft;
        flame.shadowNearPlane = 0.12f;
        flame.enabled = true;

        AudioSource audio = AddAudio(root, 1f, 1f, 15f);
        AudioSource burn = AddAudio(flame.gameObject, 1f, 0.5f, 6f);
        burn.clip = Clip("Ambience/fire-1_0.ogg");
        burn.loop = true;
        burn.volume = 0.25f;

        Renderer glass = null;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == "glass")
            {
                glass = renderer;
            }
        }

        WorldItem item = root.AddComponent<WorldItem>();
        SetField(item, "definition", definition);
        SetField(item, "grip", grip);

        LanternItem lantern = root.AddComponent<LanternItem>();
        SetField(lantern, "flameLight", flame);
        SetField(lantern, "emissiveRenderers", glass != null ? new Object[] { glass } : new Object[0]);
        SetField(lantern, "audioSource", audio);
        SetField(lantern, "toggleClip", Clip("Weapons/Revolver_Cock.wav"));
        SetField(lantern, "burnLoop", burn);

        PrefabUtility.SaveAsPrefabAsset(root, $"{ItemFolder}/Lantern.prefab");
        Object.DestroyImmediate(root);
    }

    private static void BuildRevolver()
    {
        ItemDefinition definition = Definition("RevolverDefinition");
        definition.displayName = "Revólver M1879";
        definition.kind = ItemKind.Revolver;
        definition.heldPositionOffset = Vector3.zero;
        definition.heldRotationEuler = new Vector3(30f, 0f, 0f);
        definition.visibleWhenStowed = true;
        definition.stowedPositionOffset = new Vector3(0.21f, -0.06f, 0.03f);
        definition.stowedRotationEuler = new Vector3(80f, 0f, 0f);
        definition.aimForwardLocal = Vector3.forward;
        definition.aimUpLocal = Vector3.up;
        definition.raisedReach = 0.58f;
        definition.raisedHeightOffset = -0.03f;
        definition.keepUprightWhenRaised = false;
        definition.zoomCameraWhenRaised = true;
        definition.showReticleWhenRaised = true;
        definition.pickupSound = Clip("Handling/Pickup_Metal.ogg");
        definition.dropSound = Clip("Handling/Drop_Metal.ogg");
        definition.equipSound = Clip("Weapons/Revolver_Cock.wav");
        EditorUtility.SetDirty(definition);

        GameObject root = new GameObject("Revolver");
        GameObject source = Load<GameObject>(Python357Setup.PrefabPath);
        GameObject model = (GameObject)Object.Instantiate(source);
        model.name = "Model";
        StripLegacy(model);
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        Bounds bounds = LocalBounds(root.transform);
        model.transform.localPosition = -bounds.center;
        bounds = LocalBounds(root.transform);

        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = bounds.size;
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 1.0f;
        body.angularDamping = 0.4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Barrel points +Z; the handle hangs down and back behind the cylinder.
        Transform grip = Child(root.transform, "Grip", new Vector3(0f, bounds.min.y + 0.075f, bounds.min.z + 0.05f));
        Transform muzzle = Child(root.transform, "Muzzle", new Vector3(0f, bounds.max.y - 0.018f, bounds.max.z));

        GameObject flashPrefab = Load<GameObject>(ProjectPaths.Vfx + "/MuzzleFlash.prefab");
        ParticleSystem flash = null;
        if (flashPrefab != null)
        {
            GameObject flashInstance = (GameObject)PrefabUtility.InstantiatePrefab(flashPrefab, muzzle);
            flashInstance.transform.localPosition = Vector3.zero;
            flashInstance.transform.localRotation = Quaternion.identity;
            flash = flashInstance.GetComponent<ParticleSystem>();
        }

        Light muzzleLight = Child(muzzle, "MuzzleLight", new Vector3(0f, 0f, 0.05f)).gameObject.AddComponent<Light>();
        muzzleLight.type = LightType.Point;
        muzzleLight.color = new Color(1f, 0.7f, 0.4f);
        muzzleLight.range = 8f;
        muzzleLight.intensity = 8f;
        muzzleLight.shadows = LightShadows.None;
        muzzleLight.enabled = false;

        Transform cylinder = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Cylinder")
            {
                cylinder = t;
            }
        }

        AudioSource audio = AddAudio(root, 0.7f, 2f, 120f);

        WorldItem item = root.AddComponent<WorldItem>();
        SetField(item, "definition", definition);
        SetField(item, "grip", grip);

        RevolverWeapon weapon = root.AddComponent<RevolverWeapon>();
        SetField(weapon, "muzzle", muzzle);
        SetField(weapon, "cylinder", cylinder);
        SetField(weapon, "cylinderSpinAxis", Vector3.up);
        SetField(weapon, "muzzleLight", muzzleLight);
        SetField(weapon, "muzzleFlash", flash);
        SetField(weapon, "impactDustPrefab", Load<GameObject>(ProjectPaths.Vfx + "/ImpactDust.prefab") != null
            ? Load<GameObject>(ProjectPaths.Vfx + "/ImpactDust.prefab").GetComponent<ParticleSystem>()
            : null);
        SetField(weapon, "audioSource", audio);
        SetField(weapon, "shotClips", new Object[] { Clip("Weapons/Revolver_Shot1.wav"), Clip("Weapons/Revolver_Shot2.wav") });
        SetField(weapon, "dryFireClip", Clip("Weapons/Revolver_DryFire.mp3"));
        SetField(weapon, "loadRoundClip", Clip("Weapons/Revolver_LoadRound.wav"));
        SetField(weapon, "cockClip", Clip("Weapons/Revolver_Cock.wav"));

        PrefabUtility.SaveAsPrefabAsset(root, $"{ItemFolder}/Revolver.prefab");
        Object.DestroyImmediate(root);
    }

    // ---------- Player ----------

    [MenuItem("Horror/Prototype/Setup Player")]
    public static void SetupPlayer()
    {
        GameObject player = GameObject.Find("PlayerArmature");
        Transform rightHand = FindDeep(player.transform, "Right_Hand");
        Transform rightUpperArm = FindDeep(player.transform, "Right_UpperArm");
        Transform hips = FindDeep(player.transform, "Hips");

        Transform handSocket = FindDeep(player.transform, "RightHandSocket");
        if (handSocket == null)
        {
            handSocket = new GameObject("RightHandSocket").transform;
        }

        handSocket.SetParent(rightHand, false);
        handSocket.localPosition = RightSocketLocalPosition;
        handSocket.localRotation = RightSocketLocalRotation;

        Transform beltSocket = FindDeep(player.transform, "BeltSocket");
        if (beltSocket == null)
        {
            beltSocket = new GameObject("BeltSocket").transform;
        }

        beltSocket.SetParent(hips, false);
        beltSocket.localPosition = Vector3.zero;
        beltSocket.localRotation = BeltSocketLocalRotation;

        AudioSource handling = player.GetComponent<AudioSource>();
        if (handling == null)
        {
            handling = AddAudio(player, 0.4f, 1f, 20f);
        }

        GameObject followCameraObject = GameObject.Find("PlayerFollowCamera");
        Unity.Cinemachine.CinemachineCamera followCamera =
            followCameraObject.GetComponent<Unity.Cinemachine.CinemachineCamera>();

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        Transform reticle = canvas.transform.Find("reticle");
        Transform oldPrompt = canvas.transform.Find("Text (TMP)");
        if (oldPrompt != null)
        {
            oldPrompt.gameObject.SetActive(false);
        }

        PlayerInventory inventory = GetOrAdd<PlayerInventory>(player);
        PlayerHandsController hands = GetOrAdd<PlayerHandsController>(player);

        SetField(inventory, "rightHandSocket", handSocket);
        SetField(inventory, "beltSocket", beltSocket);
        SetField(inventory, "viewCamera", Camera.main);
        SetField(inventory, "hands", hands);
        SetField(inventory, "handlingAudio", handling);
        SetField(inventory, "defaultPickupSound", Clip("Handling/Pickup_Metal.ogg"));
        SetField(inventory, "defaultDropSound", Clip("Handling/Drop_Metal.ogg"));
        SetField(inventory, "defaultSwitchSound", Clip("Handling/Switch.ogg"));

        TwoBoneIKConstraint rightIk = null;
        foreach (TwoBoneIKConstraint constraint in player.GetComponentsInChildren<TwoBoneIKConstraint>(true))
        {
            if (constraint.data.tip == rightHand)
            {
                rightIk = constraint;
            }

            constraint.weight = 0f;
            EditorUtility.SetDirty(constraint);
        }

        SetField(hands, "rightArmIk", rightIk);
        SetField(hands, "rightShoulder", rightUpperArm);
        SetField(hands, "rightHandBone", rightHand);
        SetField(hands, "rightHandSocket", handSocket);
        SetField(hands, "inventory", inventory);
        SetField(hands, "thirdPersonController", player.GetComponent<StarterAssets.ThirdPersonController>());
        SetField(hands, "starterInputs", player.GetComponent<StarterAssets.StarterAssetsInputs>());
        SetField(hands, "followCamera", followCamera);
        SetField(hands, "aimFieldOfView", 30f);
        SetField(hands, "aimCameraDistance", 2.0f);
        // The old dot reticle is retired: DynamicCrosshair (built by the HUD) replaces it.
        SetField(hands, "reticle", null);
        if (reticle != null)
        {
            reticle.gameObject.SetActive(false);
        }

        HandGripPose grip = GetOrAdd<HandGripPose>(player);
        SetField(grip, "inventory", inventory);
        SetField(grip, "hand", rightHand);
        SetField(grip, "fingerRoots", new Object[]
        {
            FindDeep(player.transform, "Right_IndexProximal"),
            FindDeep(player.transform, "Right_MiddleProximal"),
            FindDeep(player.transform, "Right_RingProximal"),
            FindDeep(player.transform, "Right_PinkyProximal"),
        });
        SetField(grip, "thumbRoot", FindDeep(player.transform, "Right_ThumbIntermediate"));

        CameraShake shake = GetOrAdd<CameraShake>(followCameraObject);
        SetField(shake, "noise", followCameraObject.GetComponent<Unity.Cinemachine.CinemachineBasicMultiChannelPerlin>());

        UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        InventoryHud hud = GetOrAdd<InventoryHud>(canvas.gameObject);
        SetField(hud, "inventory", inventory);
        SetField(hud, "canvas", canvas);

        // Squelchy mud footsteps instead of the concrete defaults.
        StarterAssets.ThirdPersonController controller = player.GetComponent<StarterAssets.ThirdPersonController>();
        string[] steps = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder + "/Footsteps" });
        AudioClip[] footsteps = new AudioClip[steps.Length];
        for (int i = 0; i < steps.Length; i++)
        {
            footsteps[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(steps[i]));
        }

        controller.FootstepAudioClips = footsteps;
        controller.FootstepAudioVolume = 0.45f;
        controller.MoveSpeed = 1.9f;
        controller.SprintSpeed = 4.6f;
        EditorUtility.SetDirty(controller);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("[Prototype] Player set up.");
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    public static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }
}
