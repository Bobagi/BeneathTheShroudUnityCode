using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

// Whole-body clips recorded from video (09/10): D:\Projetos\hand-mocap body_fit.py writes <take>.bodymocap.json (bone
// turns per frame against a canonical standing pose), BodyMocapImporter (package com.bobagi.handmocap) bakes it into a
// Humanoid clip on the player's body, and this puts the clips on the player's Animator:
//   Art/Animations/Body/Repouso.anim       the standing pose measured front and side (one frame)
//   Art/Animations/Body/IdleGravado.anim   the idle filmed from the front, looped
//   Art/Animations/Body/Tosse.anim         the cough (left hand to the mouth: the right one holds the torch)
//   layer "Idle gravado" (whole body but the fingers, IK pass, weight 0): state "Idle gravado" and "Repouso";
//        PlayerBody turns it on with UseRecordedIdle while standing still (the pack idle stays the default)
//   layer "Corpo gravado" (spine and head, IK pass): "Empty" and "Tosse", back to "Empty" at the end;
//        PlayerBody.Cough() plays it together with the rig's Cough clip (the hand to the mouth, FirstPersonArms)
// The fingers are not in the clips (the capture has none): the layers' masks leave them to the clip underneath.
public static class RecordedBody
{
    private const string CaptureFolder = "D:/Projetos/hand-mocap/output/body";
    private const string ModelPath = ProjectPaths.CharacterModels + "/PlayerBody/PlayerBody.fbx";
    private const string Reference = ProjectPaths.BodyAnimations + "/Reference";
    // Same as the game's body (FloodedHousePlayerBody.BodyScale).
    private const float BodyScale = 0.965f;
    public const string IdleState = "Idle gravado";
    public const string RestState = "Repouso";

    // capture name, clip name
    private static readonly (string take, string clip)[] Takes =
    {
        ("repouso", "Repouso"),
        ("fullbody-front", "IdleGravado"),
        ("tosse", "Tosse"),
    };

    [MenuItem("Horror/Animação/Assar corpo gravado (repouso, idle e tosse)")]
    public static void BakeAll()
    {
        if (!AssetDatabase.IsValidFolder(ProjectPaths.BodyAnimations))
        {
            AssetDatabase.CreateFolder(ProjectPaths.Art + "/Animations", "Body");
        }
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        foreach ((string take, string clip) in Takes)
        {
            BodyMocapImporter.Result r = BodyMocapImporter.Bake(CaptureFolder + "/" + take + ".bodymocap.json", model,
                ProjectPaths.BodyAnimations + "/" + clip + ".anim", BodyScale);
            Debug.Log(r != null ? "[BodyMocap] " + r.report : "[BodyMocap] " + take + ": falhou (ver aviso acima).");
        }
        AddLayers(AssetDatabase.LoadAssetAtPath<AnimatorController>(ProjectPaths.PlayerAnimations + "/PlayerBody.controller"));
    }

    private static AnimationClip ClipOf(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(ProjectPaths.BodyAnimations + "/" + name + ".anim");
    }

    // Called by the player's Animator builder too, so a rebuild keeps the layers.
    public static void AddLayers(AnimatorController controller)
    {
        if (controller == null || ClipOf("Tosse") == null)
        {
            return;
        }
        // Spine and head only: the arm of an action (the cough's left hand) is the first person rig's clip, in the
        // eye's space (Art/Animations/FirstPerson/Acoes), so it stays on the screen whatever the view does.
        AvatarMask action = Mask("BodyAction.mask", part => part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head);
        AvatarMask idle = Mask("BodyIdle.mask", part => part != AvatarMaskBodyPart.LeftFingers && part != AvatarMaskBodyPart.RightFingers
            && part != AvatarMaskBodyPart.LeftHandIK && part != AvatarMaskBodyPart.RightHandIK
            && part != AvatarMaskBodyPart.LeftFootIK && part != AvatarMaskBodyPart.RightFootIK);

        List<AnimatorControllerLayer> layers = controller.layers.Where(l => l.name != PlayerBody.RecordedIdleLayer && l.name != PlayerBody.BodyClipLayer).ToList();

        AnimatorStateMachine idleMachine = Machine(controller, PlayerBody.RecordedIdleLayer);
        AnimatorState idleState = idleMachine.AddState(IdleState, new Vector3(250f, 0f, 0f));
        idleState.motion = ClipOf("IdleGravado");
        AnimatorState rest = idleMachine.AddState(RestState, new Vector3(250f, 80f, 0f));
        rest.motion = ClipOf("Repouso");
        idleMachine.defaultState = idleState;
        // Right after the base layer: the recorded hands and the actions still play over it.
        layers.Insert(1, new AnimatorControllerLayer
        {
            name = PlayerBody.RecordedIdleLayer,
            stateMachine = idleMachine,
            avatarMask = idle,
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 0f,
            iKPass = true,
        });

        AnimatorStateMachine actionMachine = Machine(controller, PlayerBody.BodyClipLayer);
        AnimatorState empty = actionMachine.AddState(PlayerBody.BodyClipEmpty, new Vector3(250f, 0f, 0f));
        actionMachine.defaultState = empty;
        AnimatorState cough = actionMachine.AddState(PlayerBody.CoughState, new Vector3(250f, 80f, 0f));
        cough.motion = ClipOf("Tosse");
        AnimatorStateTransition back = cough.AddTransition(empty);
        back.hasExitTime = true;
        back.exitTime = 0.94f;
        back.duration = 0.2f;
        back.hasFixedDuration = true;
        layers.Add(new AnimatorControllerLayer
        {
            name = PlayerBody.BodyClipLayer,
            stateMachine = actionMachine,
            avatarMask = action,
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 1f,
            iKPass = true,
        });
        controller.layers = layers.ToArray();
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static AnimatorStateMachine Machine(AnimatorController controller, string name)
    {
        AnimatorStateMachine machine = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(machine, controller);
        return machine;
    }

    private static AvatarMask Mask(string file, System.Func<AvatarMaskBodyPart, bool> active)
    {
        string path = ProjectPaths.BodyAnimations + "/" + file;
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, path);
        }
        for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
        {
            mask.SetHumanoidBodyPartActive(part, active(part));
        }
        EditorUtility.SetDirty(mask);
        return mask;
    }

    // ---------------------------------------------------------------- test scene

    // Left of the test scene's start (the hand video is on the right), facing it.
    private static readonly Vector3 ScreenPlace = new Vector3(-1.6f, 1.6f, 2.6f);

    [MenuItem("Horror/Testes/Corpo gravado no jogador (tosse e idle)")]
    public static void PlaceInOpenScene()
    {
        FirstPersonController controller = Object.FindAnyObjectByType<FirstPersonController>();
        if (controller == null)
        {
            Debug.LogWarning("[BodyMocap] Falta o jogador na cena.");
            return;
        }
        if (ClipOf("Tosse") == null)
        {
            BakeAll();
        }
        if (!AssetDatabase.IsValidFolder(Reference))
        {
            AssetDatabase.CreateFolder(ProjectPaths.BodyAnimations, "Reference");
        }
        GameObject existing = GameObject.Find("BodyMocap_Screen");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject screen = new GameObject("BodyMocap_Screen");
        Vector3 toStart = new Vector3(0f, ScreenPlace.y, -1.5f) - ScreenPlace;
        screen.transform.SetPositionAndRotation(ScreenPlace, Quaternion.LookRotation(new Vector3(toStart.x, 0f, toStart.z), Vector3.up));

        VideoClip cough = Video("tosse");
        VideoClip idle = Video("fullbody-front");
        int width = cough != null ? (int)cough.width : 1078;
        int height = cough != null ? (int)cough.height : 850;
        string texturePath = Reference + "/BodyMocapVideo.renderTexture";
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
        if (texture == null || texture.width != width || texture.height != height)
        {
            AssetDatabase.DeleteAsset(texturePath);
            texture = new RenderTexture(width, height, 0);
            AssetDatabase.CreateAsset(texture, texturePath);
        }
        float tall = 1.1f;
        Quad(screen.transform, new Vector2(tall * width / height, tall), texture);
        TextMeshPro label = Label(screen.transform, new Vector3(0f, tall / 2f + 0.15f, 0f));

        VideoPlayer video = screen.AddComponent<VideoPlayer>();
        video.clip = cough;
        video.playOnAwake = false;
        video.isLooping = false;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = texture;

        RecordedBodyTest test = new GameObject("RecordedBodyTest").AddComponent<RecordedBodyTest>();
        test.transform.SetParent(screen.transform, false);
        SerializedObject so = new SerializedObject(test);
        so.FindProperty("body").objectReferenceValue = controller.GetComponentInChildren<PlayerBody>(true);
        so.FindProperty("video").objectReferenceValue = video;
        so.FindProperty("coughVideo").objectReferenceValue = cough;
        so.FindProperty("idleVideo").objectReferenceValue = idle;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = screen;
        EditorSceneManager.MarkSceneDirty(screen.scene);
    }

    // The fit's preview (video with the model drawn on it, and the 3D skeleton from the front and the side), copied
    // into the project so the scene does not depend on D:\Projetos.
    private static VideoClip Video(string take)
    {
        string target = Reference + "/" + take + "_preview.mp4";
        string source = CaptureFolder + "/" + take + "_preview.mp4";
        if (File.Exists(source) && (!File.Exists(target) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(target)))
        {
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target);
        }
        return AssetDatabase.LoadAssetAtPath<VideoClip>(target);
    }

    private static void Quad(Transform parent, Vector2 size, Texture texture)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Picture";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        // A quad shows its face along -Z: turned around, it faces the same way as the screen.
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        string path = Reference + "/BodyMocapVideo.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static TextMeshPro Label(Transform parent, Vector3 position)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = position;
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        tmp.text = "Corpo gravado: K tosse, I idle";
        tmp.fontSize = 0.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 0.3f);
        tmp.color = Color.white;
        return tmp;
    }
}
