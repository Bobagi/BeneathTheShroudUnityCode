using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// The player's visible body, its animator, the seats and the torch beam (see PlayerBody, Seat, TorchBeam).
// Body: the MetaHuman processed by SourceAssets/Tools/player_body_process.py (game-3d-assets skill), with
// its own body texture (skin and the grey briefs). Erick's clothes (MetaHuman Creator + the Fab Survivor Outfit,
// SourceAssets/Tools/metahuman_outfit.py) are a skin on the same bones and the one drawn by default.
// Animations: Unity Starter Assets (idle, walk, run) and Mixamo (crouch, swim, tread water, sit, climb out),
// retargeted through the humanoid avatar.
public static partial class FloodedHouseBuilder
{
    // The MetaHuman is 1.76 m with the eyes at ~1.68; the camera eye is 1.62.
    private const float BodyScale = 0.965f;

    private static AnimatorController BuildPlayerAnimator()
    {
        string path = ProjectPaths.PlayerAnimations + "/PlayerBody.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Mode", AnimatorControllerParameterType.Int);
        controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Seated", AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter { name = "ClimbSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        // IK pass on: the torch hand, the reaching hand and the look-at are set in PlayerBody.OnAnimatorIK.
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        AnimatorStateMachine machine = layers[0].stateMachine;

        string starter = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Character/Animations/";
        // Thresholds are the clips' own speeds, so the feet do not slide more than they must.
        // Negative speed walks backwards: the same walk played in reverse (PlayerBody faces the view then).
        AnimationClip walk = Clip(starter + "Locomotion--Walk_N.anim.fbx");
        AnimationClip crouchWalk = PlayerClip("CrouchedWalking");
        AnimatorState land = BlendState(controller, machine, "Land", new Vector3(0f, 0f, 0f),
            (walk, -2f, -1f), (Clip(starter + "Stand--Idle.anim.fbx"), 0f, 1f), (walk, 2f, 1f), (Clip(starter + "Locomotion--Run_N.anim.fbx"), 5.3f, 1f));
        AnimatorState crouch = BlendState(controller, machine, "Crouch", new Vector3(0f, 80f, 0f),
            (crouchWalk, -1.4f, -1f), (PlayerClip("CrouchingIdle"), 0f, 1f), (crouchWalk, 1.4f, 1f));
        AnimatorState water = BlendState(controller, machine, "Water", new Vector3(260f, 0f, 0f),
            (PlayerClip("TreadingWater"), 0f, 1f), (PlayerClip("Swimming"), 1.2f, 1f));
        AnimatorState sitDown = machine.AddState("StandToSit", new Vector3(0f, -120f, 0f));
        sitDown.motion = PlayerClip("StandToSit");
        AnimatorState sitting = machine.AddState("Sitting", new Vector3(260f, -120f, 0f));
        sitting.motion = PlayerClip("Sitting");
        AnimatorState standUp = machine.AddState("SitToStand", new Vector3(260f, -220f, 0f));
        standUp.motion = PlayerClip("SitToStand");
        // Climbing out of the water (Mixamo "Climbing To Top"), started from code by PlayerBody.BeginClimb,
        // stretched by ClimbSpeed to GameTuning.climbOutSeconds; root motion lifts the body onto the edge.
        AnimatorState climbOut = machine.AddState("ClimbOut", new Vector3(520f, 0f, 0f));
        climbOut.motion = PlayerClip("ClimbingToTop");
        climbOut.speedParameter = "ClimbSpeed";
        climbOut.speedParameterActive = true;
        machine.defaultState = land;

        Transition(land, crouch, 0.25f, ("Crouch", AnimatorConditionMode.If, 0f));
        Transition(crouch, land, 0.25f, ("Crouch", AnimatorConditionMode.IfNot, 0f));
        Transition(land, water, 0.35f, ("Mode", AnimatorConditionMode.Greater, 1f));
        Transition(crouch, water, 0.35f, ("Mode", AnimatorConditionMode.Greater, 1f));
        Transition(water, land, 0.35f, ("Mode", AnimatorConditionMode.Less, 2f));
        Transition(land, sitDown, 0.2f, ("Seated", AnimatorConditionMode.If, 0f));
        Transition(crouch, sitDown, 0.2f, ("Seated", AnimatorConditionMode.If, 0f));
        Transition(sitDown, standUp, 0.2f, ("Seated", AnimatorConditionMode.IfNot, 0f));
        Transition(sitting, standUp, 0.2f, ("Seated", AnimatorConditionMode.IfNot, 0f));
        AnimatorStateTransition sat = sitDown.AddTransition(sitting);
        sat.hasExitTime = true;
        sat.exitTime = 0.97f;
        sat.duration = 0.1f;
        AnimatorStateTransition climbed = climbOut.AddTransition(land);
        climbed.hasExitTime = true;
        climbed.exitTime = 0.96f;
        climbed.duration = 0.2f;
        AnimatorStateTransition stood = standUp.AddTransition(land);
        stood.hasExitTime = true;
        stood.exitTime = 0.94f;
        stood.duration = 0.25f;
        EditorUtility.SetDirty(controller);
        // Whole-body clips from video (the cough, the filmed idle): layers "Idle gravado" and "Corpo gravado". The
        // arms in first person are the FirstPersonArms rig's clips, not this controller's.
        RecordedBody.AddLayers(controller);
        return controller;
    }

    private static AnimatorState BlendState(AnimatorController controller, AnimatorStateMachine machine, string name, Vector3 position,
        params (AnimationClip clip, float threshold, float timeScale)[] children)
    {
        BlendTree tree = new BlendTree { name = name, blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        foreach ((AnimationClip clip, float threshold, float _) in children)
        {
            tree.AddChild(clip, threshold);
        }

        ChildMotion[] motions = tree.children;
        for (int i = 0; i < motions.Length; i++)
        {
            motions[i].timeScale = children[i].timeScale;
        }

        tree.children = motions;

        AnimatorState state = machine.AddState(name, position);
        state.motion = tree;
        return state;
    }

    private static void Transition(AnimatorState from, AnimatorState to, float duration, params (string parameter, AnimatorConditionMode mode, float threshold)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        foreach ((string parameter, AnimatorConditionMode mode, float threshold) in conditions)
        {
            t.AddCondition(mode, threshold, parameter);
        }
    }

    private static AnimationClip PlayerClip(string name)
    {
        return Clip(ProjectPaths.PlayerAnimations + "/" + name + ".fbx");
    }

    private static AnimationClip Clip(string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview"))
            {
                return clip;
            }
        }

        Debug.LogError("[FloodedHouse] Missing animation " + path);
        return null;
    }

    // The body under the player: humanoid, in its own skin texture, head drawn only into shadows, its arms copied
    // from the first person rig while a hand is busy (ArmsFromRig). Returns the PlayerBody so the controller can point
    // at it.
    private static PlayerBody BuildPlayerBody(GameObject player, FirstPersonController controller, FirstPersonArms arms)
    {
        string folder = ProjectPaths.CharacterModels + "/PlayerBody";
        PlayerAvatarFingers.Fix(folder + "/PlayerBody.fbx");
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/PlayerBody.fbx");
        GameObject bodyObject = (GameObject)PrefabUtility.InstantiatePrefab(asset, player.transform);
        bodyObject.name = "PlayerBody";
        bodyObject.transform.localPosition = Vector3.zero;
        bodyObject.transform.localRotation = asset.transform.localRotation;
        bodyObject.transform.localScale = asset.transform.localScale * BodyScale;

        // The skin and the head's parts (face, eyes, teeth, lashes), from the maps beside the FBX; in first person
        // the head only casts shadows (the camera sits inside it).
        Material skin = MetaHumanBody.Apply(bodyObject, folder, "Player", true);

        // Looks on the same skeleton (BodySkins): the bare MetaHuman, Erick in his clothes (drawn by default) and
        // the hazmat suit. The head is the body's own in the first two (the outfit brings no head of its own).
        Renderer[] bare = bodyObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        BodySkins skins = bodyObject.AddComponent<BodySkins>();
        skins.Configure(new[]
        {
            new BodySkins.Skin { name = "MetaHuman", renderers = bare },
            new BodySkins.Skin { name = "Erick", renderers = AddErickOutfit(bodyObject, skin) },
            new BodySkins.Skin { name = "Traje", renderers = AddHazmatSuit(bodyObject), bulky = true },
        }, 1);

        // The body on rendering layer 2 (everything else is on 1): every light reaches it except the DarkVision
        // fill at the eye, which is on layer 1 only (it burned the chin and the raised hands white). The layer has
        // to exist in Tags and Layers, or URP masks the bit away and nothing lights the body (measured 07/10).
        EnsureRenderingLayer(1, "PlayerBody");
        foreach (Renderer r in bodyObject.GetComponentsInChildren<Renderer>(true))
        {
            r.renderingLayerMask = 2;
        }

        Animator animator = bodyObject.GetComponent<Animator>();
        animator.runtimeAnimatorController = BuildPlayerAnimator();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        AudioSource stepAudio = bodyObject.AddComponent<AudioSource>();
        stepAudio.playOnAwake = false;
        stepAudio.spatialBlend = 0.5f;
        FootstepAudio steps = bodyObject.AddComponent<FootstepAudio>();
        SetField(steps, "controller", controller);
        SetField(steps, "source", stepAudio);
        SetField(steps, "wood", FootstepClips("wood"));
        SetField(steps, "concrete", FootstepClips("concrete"));
        SetField(steps, "carpet", FootstepClips("carpet"));

        ArmsFromRig fromRig = bodyObject.GetComponent<ArmsFromRig>() ?? bodyObject.AddComponent<ArmsFromRig>();
        SetField(fromRig, "rig", arms);
        PlayerBody body = bodyObject.AddComponent<PlayerBody>();
        SetField(body, "controller", controller);
        SetField(body, "arms", arms);
        return body;
    }

    // Defines rendering layer `index` in Project Settings > Tags and Layers (m_RenderingLayers) when it is missing.
    private static void EnsureRenderingLayer(int index, string layerName)
    {
        Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (settings == null || settings.Length == 0)
        {
            return;
        }
        SerializedObject tags = new SerializedObject(settings[0]);
        SerializedProperty layers = tags.FindProperty("m_RenderingLayers");
        if (layers == null)
        {
            return;
        }
        if (layers.arraySize <= index)
        {
            layers.arraySize = index + 1;
        }
        SerializedProperty slot = layers.GetArrayElementAtIndex(index);
        if (slot.stringValue != layerName)
        {
            slot.stringValue = layerName;
            tags.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }

    // Erick's clothes (SourceAssets/Tools/metahuman_outfit.py): mounted by the generic MetaHumanOutfit, first person
    // (hair and beard drawn only into shadows, like the head they sit on).
    private static Renderer[] AddErickOutfit(GameObject bodyObject, Material skin)
    {
        return MetaHumanOutfit.Attach(bodyObject, ProjectPaths.CharacterModels + "/ErickOutfit", "Erick", skin, true);
    }

    // The hazmat suit (Meshy, made a reskin by SourceAssets/Tools/hazmat_suit_rig.py): Suit, SuitHead (hood and
    // mask, shadow only like the body's head) and Gloves (the MetaHuman's own hands in rubber), exported on the
    // MetaHuman's skeleton with the same rest pose (measured: 342 bones, 0.002 mm and 0 degrees apart), so each
    // renderer is re-pointed by bone name to this body's bones and keeps its own bind poses.
    private static Renderer[] AddHazmatSuit(GameObject bodyObject)
    {
        string folder = ProjectPaths.CharacterModels + "/HazmatSuit";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/HazmatSuit.fbx");
        if (asset == null)
        {
            Debug.LogWarning("[FloodedHouse] Missing " + folder + "/HazmatSuit.fbx, the suit skin is left out");
            return new Renderer[0];
        }

        EnsureTextureImport(folder + "/HazmatSuit_Normal.png", true);
        EnsureTextureImport(folder + "/HazmatSuit_MetallicSmoothness.png", false);
        // Baked at 4096: the chest is a hand from the eye looking down, and at 2048 its straps and lamp blurred.
        foreach (string map in new[] { "BaseColor", "Normal", "MetallicSmoothness" })
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(folder + "/HazmatSuit_" + map + ".png");
            if (importer != null && importer.maxTextureSize != 4096)
            {
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
        }
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material suit = Material("HazmatSuit", lit);
        suit.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/HazmatSuit_BaseColor.png"));
        suit.SetColor("_BaseColor", Color.white);
        suit.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/HazmatSuit_Normal.png"));
        suit.EnableKeyword("_NORMALMAP");
        suit.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/HazmatSuit_MetallicSmoothness.png"));
        suit.EnableKeyword("_METALLICSPECGLOSSMAP");
        suit.SetFloat("_Smoothness", 1f);
        EditorUtility.SetDirty(suit);
        // Black rubber; no normal map (the skin's would print pores and nails on it).
        Material glove = Material("HazmatGlove", lit);
        glove.SetColor("_BaseColor", new Color(0.035f, 0.037f, 0.035f));
        glove.SetFloat("_Metallic", 0f);
        glove.SetFloat("_Smoothness", 0.55f);
        EditorUtility.SetDirty(glove);

        Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        foreach (Transform t in bodyObject.GetComponentsInChildren<Transform>(true))
        {
            bones[t.name] = t;
        }

        Transform rootBone = bodyObject.GetComponentInChildren<SkinnedMeshRenderer>().rootBone;
        GameObject source = Object.Instantiate(asset);
        // The jacket's spring bones (not on the body's skeleton): made first, sprung (Player/ClothSprings).
        OutfitSprings.AddMissingBones(source, bones);
        OutfitSprings.Attach(bodyObject, bones);
        List<Renderer> renderers = new List<Renderer>();
        foreach (SkinnedMeshRenderer from in source.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Transform[] mapped = new Transform[from.bones.Length];
            for (int i = 0; i < mapped.Length; i++)
            {
                if (!bones.TryGetValue(from.bones[i].name, out mapped[i]))
                {
                    Debug.LogError("[FloodedHouse] Suit bone not on the body: " + from.bones[i].name);
                }
            }

            GameObject part = new GameObject(from.name);
            part.transform.SetParent(bodyObject.transform, false);
            part.transform.localPosition = from.transform.localPosition;
            part.transform.localRotation = from.transform.localRotation;
            part.transform.localScale = from.transform.localScale;
            SkinnedMeshRenderer r = part.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = from.sharedMesh;
            r.bones = mapped;
            r.rootBone = rootBone;
            r.localBounds = from.localBounds;
            r.sharedMaterial = from.name == "Gloves" ? glove : suit;
            r.shadowCastingMode = from.name == "SuitHead" ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            r.updateWhenOffscreen = true;
            renderers.Add(r);
        }

        Object.DestroyImmediate(source);
        return renderers.ToArray();
    }

    // Kenney Impact Sounds (CC0), one folder per floor type under Audio/Footsteps.
    private static Object[] FootstepClips(string surface)
    {
        List<Object> clips = new List<Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectPaths.Audio + "/Footsteps/" + surface }))
        {
            clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        return clips.ToArray();
    }

    private static void EnsureTextureImport(string path, bool normal)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        if (normal && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            changed = true;
        }

        if (!normal && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    // Measures a chair from its own mesh: the seat is the area-weighted centre of the upward faces between
    // 0.3 and 0.75 m, the backrest is whatever rises well above it, and the chair faces away from the backrest.
    private static void MakeSeat(GameObject chair)
    {
        float floor = chair.transform.position.y;
        float seatArea = 0f;
        Vector3 seatSum = Vector3.zero;
        List<Vector3> high = new List<Vector3>();
        foreach (MeshFilter filter in chair.GetComponentsInChildren<MeshFilter>())
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
            {
                continue;
            }

            Mesh mesh = filter.sharedMesh;
            Vector3[] v = mesh.vertices;
            int[] tris = mesh.triangles;
            Matrix4x4 m = filter.transform.localToWorldMatrix;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(v[tris[i]]);
                Vector3 b = m.MultiplyPoint3x4(v[tris[i + 1]]);
                Vector3 c = m.MultiplyPoint3x4(v[tris[i + 2]]);
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float area = cross.magnitude * 0.5f;
                Vector3 centre = (a + b + c) / 3f;
                float h = centre.y - floor;
                if (area > 0f && cross.normalized.y > 0.8f && h > 0.3f && h < 0.75f)
                {
                    seatArea += area;
                    seatSum += centre * area;
                }
            }

            for (int i = 0; i < v.Length; i += 7)
            {
                high.Add(m.MultiplyPoint3x4(v[i]));
            }
        }

        if (seatArea <= 0f)
        {
            Debug.LogWarning("[FloodedHouse] No seat surface found on " + chair.name);
            return;
        }

        Vector3 seat = seatSum / seatArea;
        Vector3 backSum = Vector3.zero;
        int backCount = 0;
        foreach (Vector3 p in high)
        {
            if (p.y > seat.y + 0.3f)
            {
                backSum += p;
                backCount++;
            }
        }

        Vector3 facing = backCount > 0 ? Vector3.ProjectOnPlane(seat - backSum / backCount, Vector3.up) : chair.transform.forward;
        Seat component = chair.AddComponent<Seat>();
        component.SetPrompt("prompt.sit");
        component.Configure(seat, facing.sqrMagnitude > 0.0001f ? facing.normalized : chair.transform.forward);
    }

    private static Material TorchBeamMaterial()
    {
        Material beam = Material("TorchBeam", Shader.Find("Horror/TorchBeam"));
        EditorUtility.SetDirty(beam);
        return beam;
    }
}
