using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds the first person arms rig and its Animator (FirstPersonArms explains the structure):
//   Prefabs/Player/FirstPersonArms.prefab          the rig: the arm bones of PlayerBody.fbx (bind pose) under a root
//                                                  placed where the body's chest sits relative to the eye, plus the
//                                                  sockets on the hands (WeaponSocket, TorchSocket, LeftSocket)
//   Art/Animations/FirstPerson/FirstPersonArms.controller   layers "Mãos", "Ação" (left arm mask), "Mãos gravadas"
//   Art/Animations/FirstPerson/Masks/LeftArm.mask
// The clips are the files in Art/Animations/FirstPerson (and Tomadas/ for the takes): the controller is rebuilt from
// whatever is there, so a new take or action needs no code. Everything here keeps its GUID when rebuilt.
public static class FirstPersonRig
{
    public const string Folder = ProjectPaths.FirstPersonAnimations;
    public const string PrefabPath = ProjectPaths.PlayerPrefabs + "/FirstPersonArms.prefab";
    public const string ControllerPath = Folder + "/FirstPersonArms.controller";
    public const string LeftArmMaskPath = Folder + "/Masks/LeftArm.mask";
    public const string ModelPath = ProjectPaths.CharacterModels + "/PlayerBody/PlayerBody.fbx";
    public const string IdleClip = Folder + "/Arms_Idle.anim";
    public const string TorchHoldClip = Folder + "/Lanterna/Torch_Hold.anim";
    public const string TorchFocusClip = Folder + "/Lanterna/Torch_Focus.anim";
    public const string RevolverHoldClip = Folder + "/Revolver/Revolver_Hold.anim";
    public const string RevolverAimClip = Folder + "/Revolver/Revolver_Aim.anim";
    public const string RevolverReloadClip = Folder + "/Revolver/Revolver_Reload.anim";
    public const string RevolverSprintClip = Folder + "/Revolver/Revolver_Sprint.anim";
    // Optional: a shot clip made in Blender (the kick itself stays procedural; this is the hands' own reaction).
    public const string RevolverFireClip = Folder + "/Revolver/Revolver_Fire.anim";
    public const string CoughClip = Folder + "/Acoes/Cough.anim";
    // The demo's finale (GDD 09/10, "O fim da demo"): clips Gustavo makes in Blender, one state each on the hands
    // layer, entered by name from code (DemoFinale, FirstPersonArms.PlayHands); missing files are simply skipped.
    public const string FinalFolder = Folder + "/Final";
    public static readonly string[] FinalStates = { "Hide_Crouch", "Revolver_OpenSlow", "Revolver_LoadOne", "Revolver_LoadFast", "Revolver_CloseSlow", "Revolver_RiseAim" };

    // The bones of one arm, from the shoulder out (the body's names, with _l / _r).
    public static readonly string[] Chain = { "clavicle", "upperarm", "lowerarm", "hand" };
    public static readonly string[] Fingers = { "thumb", "index", "middle", "ring", "pinky" };

    public static IEnumerable<string> BoneNames(bool left)
    {
        string side = left ? "_l" : "_r";
        foreach (string c in Chain)
        {
            yield return c + side;
        }

        foreach (string f in Fingers)
        {
            for (int s = 1; s <= 3; s++)
            {
                yield return f + "_0" + s + side;
            }
        }
    }

    public static string Side(string bone)
    {
        return bone.EndsWith("_l") ? "_l" : "_r";
    }

    // The clip path of a bone: clavicle_r/upperarm_r/lowerarm_r/hand_r/index_01_r/...
    public static string PathOf(string bone)
    {
        string side = Side(bone);
        string stem = bone.Substring(0, bone.Length - 2);
        List<string> parts = new List<string>();
        int chainIndex = System.Array.IndexOf(Chain, stem);
        if (chainIndex >= 0)
        {
            for (int i = 0; i <= chainIndex; i++)
            {
                parts.Add(Chain[i] + side);
            }

            return string.Join("/", parts);
        }

        foreach (string c in Chain)
        {
            parts.Add(c + side);
        }

        string finger = stem.Substring(0, stem.Length - 3);
        int segment = int.Parse(stem.Substring(stem.Length - 1));
        for (int s = 1; s <= segment; s++)
        {
            parts.Add(finger + "_0" + s + side);
        }

        return string.Join("/", parts);
    }

    public static string SocketPath(string socket)
    {
        return socket == FirstPersonArms.LeftSocketName ? PathOf("hand_l") + "/" + socket : PathOf("hand_r") + "/" + socket;
    }

    // The rig root's pose under the ViewModel pivot: the body's chest (spine_05, the collarbones' parent) relative to
    // the eye, measured on the body standing in its idle with a level view (10/10), and its scale (the FBX is in
    // centimetres: 0.01, times the body's 0.965).
    public static readonly Vector3 RootInEyePosition = new Vector3(-0.0036f, -0.3589f, -0.0940f);
    public static readonly Quaternion RootInEyeRotation = Quaternion.Euler(0.50f, 93.63f, 258.83f);
    public static readonly Vector3 RootScale = Vector3.one * 0.00965f;

    [MenuItem("Horror/Animação/Primeira pessoa: refazer o prefab do rig dos braços")]
    public static void BuildPrefabMenu()
    {
        Selection.activeObject = BuildPrefab(RootInEyePosition, RootInEyeRotation, RootScale);
    }

    // Makes (or remakes, in place) the prefab from the body's FBX. root: the rig root's pose under the ViewModel pivot
    // (the chest relative to the eye, measured on the standing body). The sockets keep what they have.
    public static GameObject BuildPrefab(Vector3 rootPosition, Quaternion rootRotation, Vector3 rootScale)
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject source = Object.Instantiate(fbx);
        Dictionary<string, (Vector3 position, Quaternion rotation)> sockets = ReadSockets();
        try
        {
            Transform[] all = source.GetComponentsInChildren<Transform>(true);
            GameObject root = new GameObject("FirstPersonArms");
            root.transform.localPosition = rootPosition;
            root.transform.localRotation = rootRotation;
            root.transform.localScale = rootScale;
            foreach (string side in new[] { "_l", "_r" })
            {
                Transform clavicle = all.FirstOrDefault(t => t.name == "clavicle" + side);
                if (clavicle == null)
                {
                    Debug.LogError("[FirstPerson] clavicle" + side + " not found in " + ModelPath);
                    continue;
                }

                Vector3 lp = clavicle.localPosition;
                Quaternion lr = clavicle.localRotation;
                Vector3 ls = clavicle.localScale;
                clavicle.SetParent(root.transform, false);
                clavicle.localPosition = lp;
                clavicle.localRotation = lr;
                clavicle.localScale = ls;
                // Only the arm bones: anything else hanging off the chain (nothing on this body) goes.
                foreach (Transform t in clavicle.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (t != null && t != clavicle && !BoneNames(side == "_l").Contains(t.name))
                    {
                        Object.DestroyImmediate(t.gameObject);
                    }
                }

                foreach (Transform t in clavicle.GetComponentsInChildren<Transform>(true))
                {
                    foreach (Component c in t.GetComponents<Component>().Where(c => !(c is Transform)).ToArray())
                    {
                        Object.DestroyImmediate(c);
                    }
                }
            }

            Transform rightHand = root.transform.Find(PathOf("hand_r"));
            Transform leftHand = root.transform.Find(PathOf("hand_l"));
            Transform weapon = Socket(rightHand, FirstPersonArms.WeaponSocketName, sockets);
            Transform torch = Socket(rightHand, FirstPersonArms.TorchSocketName, sockets);
            Transform leftSocket = Socket(leftHand, FirstPersonArms.LeftSocketName, sockets);

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            FirstPersonArms arms = root.AddComponent<FirstPersonArms>();
            SerializedObject so = new SerializedObject(arms);
            so.FindProperty("weaponSocket").objectReferenceValue = weapon;
            so.FindProperty("torchSocket").objectReferenceValue = torch;
            so.FindProperty("leftSocket").objectReferenceValue = leftSocket;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(ProjectPaths.PlayerPrefabs);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(source);
        }
    }

    private static Transform Socket(Transform hand, string name, Dictionary<string, (Vector3 position, Quaternion rotation)> saved)
    {
        Transform socket = new GameObject(name).transform;
        socket.SetParent(hand, false);
        // The rig is in centimetres (the FBX's scale): the socket cancels it, so what hangs on it keeps its own metres.
        Vector3 lossy = hand.lossyScale;
        socket.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
        if (saved.TryGetValue(name, out (Vector3 position, Quaternion rotation) pose))
        {
            socket.localPosition = pose.position;
            socket.localRotation = pose.rotation;
        }

        return socket;
    }

    private static Dictionary<string, (Vector3, Quaternion)> ReadSockets()
    {
        Dictionary<string, (Vector3, Quaternion)> result = new Dictionary<string, (Vector3, Quaternion)>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            return result;
        }

        foreach (string name in new[] { FirstPersonArms.WeaponSocketName, FirstPersonArms.TorchSocketName, FirstPersonArms.LeftSocketName })
        {
            Transform t = prefab.transform.Find(SocketPath(name));
            if (t != null)
            {
                result[name] = (t.localPosition, t.localRotation);
            }
        }

        return result;
    }

    // Writes a socket's pose (in its hand's space) into the prefab.
    public static void SetSocket(string name, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform socket = root.transform.Find(SocketPath(name));
            if (socket == null)
            {
                Debug.LogError("[FirstPerson] socket " + name + " missing in " + PrefabPath);
                return;
            }

            socket.localPosition = localPosition;
            socket.localRotation = localRotation;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- Animator

    [MenuItem("Horror/Animação/Primeira pessoa: refazer o Animator dos braços")]
    public static void BuildControllerMenu()
    {
        Selection.activeObject = BuildController();
    }

    // Idle / torch / revolver on the first layer, the cough on a left arm layer, one state per take on the last.
    // Rebuilt in place from the clips on disk; states keep Write Defaults off (every clip keys the same bones).
    public static bool UseFireClip = false;

    public static AnimatorController BuildController()
    {
        Directory.CreateDirectory(Folder);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (AnimatorControllerParameter p in controller.parameters.ToArray())
        {
            controller.RemoveParameter(p);
        }

        controller.AddParameter(FirstPersonArms.HandsParameter, AnimatorControllerParameterType.Int);
        controller.AddParameter(FirstPersonArms.AimParameter, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FirstPersonArms.ReloadParameter, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(FirstPersonArms.SprintParameter, AnimatorControllerParameterType.Bool);
        controller.AddParameter(FirstPersonArms.FireParameter, AnimatorControllerParameterType.Trigger);

        // Sub-assets of the old layers (state machines) are dropped with them.
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (sub is AnimatorStateMachine || sub is AnimatorState || sub is AnimatorStateTransition || sub is AnimatorTransition || sub is BlendTree)
            {
                Object.DestroyImmediate(sub, true);
            }
        }

        List<AnimatorControllerLayer> layers = new List<AnimatorControllerLayer>();
        layers.Add(HandsLayer(controller));
        layers.Add(ActionLayer(controller));
        layers.Add(TakesLayer(controller));
        controller.layers = layers.ToArray();
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorStateMachine Machine(AnimatorController controller, string name)
    {
        AnimatorStateMachine machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(machine, controller);
        return machine;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, string clipPath, Vector3 at)
    {
        AnimatorState state = machine.AddState(name, at);
        state.motion = clipPath != null ? AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) : null;
        state.writeDefaultValues = false;
        if (clipPath != null && state.motion == null)
        {
            Debug.LogWarning("[FirstPerson] missing clip " + clipPath + " (state " + name + " has no motion)");
        }

        return state;
    }

    private static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float seconds, params (string parameter, AnimatorConditionMode mode, float value)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = seconds;
        foreach ((string parameter, AnimatorConditionMode mode, float value) in conditions)
        {
            t.AddCondition(mode, value, parameter);
        }

        return t;
    }

    private static AnimatorControllerLayer HandsLayer(AnimatorController controller)
    {
        AnimatorStateMachine machine = Machine(controller, FirstPersonArms.HandsLayer);
        AnimatorState empty = State(machine, FirstPersonArms.EmptyState, IdleClip, new Vector3(0f, 0f, 0f));
        AnimatorState torch = State(machine, FirstPersonArms.TorchHoldState, TorchHoldClip, new Vector3(300f, -100f, 0f));
        AnimatorState focus = State(machine, FirstPersonArms.TorchFocusState, TorchFocusClip, new Vector3(600f, -100f, 0f));
        AnimatorState hold = State(machine, FirstPersonArms.RevolverHoldState, RevolverHoldClip, new Vector3(300f, 100f, 0f));
        AnimatorState aim = State(machine, FirstPersonArms.RevolverAimState, RevolverAimClip, new Vector3(600f, 100f, 0f));
        AnimatorState reload = State(machine, FirstPersonArms.RevolverReloadState, RevolverReloadClip, new Vector3(300f, 250f, 0f));
        AnimatorState sprint = State(machine, FirstPersonArms.RevolverSprintState, RevolverSprintClip, new Vector3(600f, 250f, 0f));
        machine.defaultState = empty;

        const string Hands = FirstPersonArms.HandsParameter;
        const string Aim = FirstPersonArms.AimParameter;
        const string Reload = FirstPersonArms.ReloadParameter;
        const string Sprint = FirstPersonArms.SprintParameter;
        // Running with the gun: a pose of its own (the gun low in the middle, the arms pumping with the steps through
        // ViewModelMotion); a reload or the aim end it, as putting the gun away does.
        Go(hold, sprint, 0.3f, (Sprint, AnimatorConditionMode.If, 0f));
        Go(sprint, hold, 0.3f, (Sprint, AnimatorConditionMode.IfNot, 0f));
        Go(sprint, reload, 0.15f, (Reload, AnimatorConditionMode.If, 0f));
        Go(sprint, empty, 0.25f, (Hands, AnimatorConditionMode.Equals, 0f));
        Go(sprint, torch, 0.3f, (Hands, AnimatorConditionMode.Equals, 1f));
        Go(empty, torch, 0.25f, (Hands, AnimatorConditionMode.Equals, 1f));
        Go(empty, hold, 0.3f, (Hands, AnimatorConditionMode.Equals, 2f));
        Go(torch, empty, 0.25f, (Hands, AnimatorConditionMode.Equals, 0f));
        Go(torch, hold, 0.3f, (Hands, AnimatorConditionMode.Equals, 2f));
        Go(hold, empty, 0.25f, (Hands, AnimatorConditionMode.Equals, 0f));
        Go(hold, torch, 0.3f, (Hands, AnimatorConditionMode.Equals, 1f));
        Go(torch, focus, 0.18f, (Aim, AnimatorConditionMode.If, 0f));
        Go(focus, torch, 0.18f, (Aim, AnimatorConditionMode.IfNot, 0f));
        Go(focus, empty, 0.25f, (Hands, AnimatorConditionMode.Equals, 0f));
        Go(focus, hold, 0.3f, (Hands, AnimatorConditionMode.Equals, 2f));
        Go(hold, aim, 0.18f, (Aim, AnimatorConditionMode.If, 0f));
        Go(aim, hold, 0.18f, (Aim, AnimatorConditionMode.IfNot, 0f));
        Go(aim, empty, 0.25f, (Hands, AnimatorConditionMode.Equals, 0f));
        Go(aim, torch, 0.3f, (Hands, AnimatorConditionMode.Equals, 1f));
        Go(hold, reload, 0.06f, (Reload, AnimatorConditionMode.If, 0f));
        Go(aim, reload, 0.06f, (Reload, AnimatorConditionMode.If, 0f));
        AnimatorStateTransition done = reload.AddTransition(hold);
        done.hasExitTime = true;
        done.exitTime = 1f;
        done.hasFixedDuration = true;
        done.duration = 0.1f;
        Go(reload, empty, 0.25f, (Hands, AnimatorConditionMode.Equals, 0f));
        Go(reload, torch, 0.3f, (Hands, AnimatorConditionMode.Equals, 1f));

        // A shot clip (Revolver_Fire.anim) is off since 13/10: a clip of its own swapped the whole pose for the pack's
        // (the gun jumped 16 cm forward on every shot, Gustavo). The kick is procedural over whatever state plays
        // (ViewModelMotion.Kick and the gun's own flip in Revolver), like most shooters' additive recoil.
        if (UseFireClip && AssetDatabase.LoadAssetAtPath<AnimationClip>(RevolverFireClip) != null)
        {
            AnimatorState fire = State(machine, FirstPersonArms.RevolverFireState, RevolverFireClip, new Vector3(900f, 100f, 0f));
            Go(hold, fire, 0.02f, (FirstPersonArms.FireParameter, AnimatorConditionMode.If, 0f));
            Go(aim, fire, 0.02f, (FirstPersonArms.FireParameter, AnimatorConditionMode.If, 0f));
            AnimatorStateTransition fired = fire.AddTransition(hold);
            fired.hasExitTime = true;
            fired.exitTime = 1f;
            fired.hasFixedDuration = true;
            fired.duration = 0.08f;
            Go(fire, aim, 0.08f, (Aim, AnimatorConditionMode.If, 0f)).hasExitTime = true;
            Go(fire, reload, 0.06f, (Reload, AnimatorConditionMode.If, 0f));
        }

        // The finale's clips, when they exist: states with no transitions in (code enters them by name) and one out
        // to the hold when the clip ends (except the one-round loop, which the script chains itself).
        for (int i = 0; i < FinalStates.Length; i++)
        {
            string path = FinalFolder + "/" + FinalStates[i] + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) == null)
            {
                continue;
            }

            AnimatorState final = State(machine, FinalStates[i], path, new Vector3(900f, 250f + 60f * i, 0f));
            Go(final, empty, 0.25f, (Hands, AnimatorConditionMode.Equals, 0f));
            Go(final, torch, 0.3f, (Hands, AnimatorConditionMode.Equals, 1f));
            Go(final, aim, 0.2f, (Aim, AnimatorConditionMode.If, 0f));
            AnimatorStateTransition finalDone = final.AddTransition(hold);
            finalDone.hasExitTime = true;
            finalDone.exitTime = 1f;
            finalDone.hasFixedDuration = true;
            finalDone.duration = 0.15f;
        }

        return new AnimatorControllerLayer
        {
            name = FirstPersonArms.HandsLayer,
            stateMachine = machine,
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 1f,
        };
    }

    private static AnimatorControllerLayer ActionLayer(AnimatorController controller)
    {
        AnimatorStateMachine machine = Machine(controller, FirstPersonArms.ActionLayer);
        AnimatorState empty = State(machine, FirstPersonArms.EmptyState, null, new Vector3(0f, 0f, 0f));
        machine.defaultState = empty;
        AnimatorState cough = State(machine, FirstPersonArms.CoughState, CoughClip, new Vector3(300f, 0f, 0f));
        AnimatorStateTransition back = cough.AddTransition(empty);
        back.hasExitTime = true;
        back.exitTime = 0.95f;
        back.hasFixedDuration = true;
        back.duration = 0.2f;
        return new AnimatorControllerLayer
        {
            name = FirstPersonArms.ActionLayer,
            stateMachine = machine,
            avatarMask = LeftArmMask(),
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 1f,
        };
    }

    private static AnimatorControllerLayer TakesLayer(AnimatorController controller)
    {
        AnimatorStateMachine machine = Machine(controller, FirstPersonArms.TakesLayer);
        AnimatorState empty = State(machine, FirstPersonArms.EmptyState, null, new Vector3(0f, 0f, 0f));
        machine.defaultState = empty;
        int row = 0;
        if (Directory.Exists(ProjectPaths.FirstPersonTakes))
        {
            foreach (string file in Directory.GetFiles(ProjectPaths.FirstPersonTakes, "*.anim").OrderBy(f => f))
            {
                string path = file.Replace('\\', '/');
                State(machine, Path.GetFileNameWithoutExtension(path), path, new Vector3(300f, 80f * row++, 0f));
            }
        }

        return new AnimatorControllerLayer
        {
            name = FirstPersonArms.TakesLayer,
            stateMachine = machine,
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 1f,
        };
    }

    // A Generic rig's mask lists transforms: the left arm's bones (and what hangs on its socket).
    private static AvatarMask LeftArmMask()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LeftArmMaskPath));
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(LeftArmMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, LeftArmMaskPath);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            mask.transformCount = 0;
            mask.AddTransformPath(prefab.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                mask.SetTransformActive(i, path.StartsWith("clavicle_l"));
            }
        }

        EditorUtility.SetDirty(mask);
        return mask;
    }
}
