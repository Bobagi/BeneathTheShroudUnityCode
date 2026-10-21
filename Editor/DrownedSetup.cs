using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The Drowned (o Afogado), the small creature that can be killed. Model from Meshy, reduced to 10 thousand
// triangles by the game-3d-assets skill and rigged in Blender with a Mixamo-named skeleton
// (<skill>/scripts/rig_humanoid.py; Mixamo's own auto-rigger failed on this mesh), so it imports as a Unity
// Humanoid and the Mixamo zombie animations (Art/Animations/Drowned, "Scary Zombie Pack") retarget onto it.
// Horror > Testes > Afogado na cena aberta sets up the import, the material and an Animator Controller with
// every clip, and puts one in the open scene playing them in turn (AnimationShowcase).
public static class DrownedSetup
{
    private const string Folder = ProjectPaths.CreatureModels + "/Drowned";
    private const string ModelPath = Folder + "/Drowned.fbx";
    private const string AnimFolder = ProjectPaths.Art + "/Animations/Drowned";
    private const string ControllerPath = AnimFolder + "/Drowned.controller";
    private const string MaterialPath = Folder + "/Drowned.mat";

    // Clips that repeat (the rest play once: attacks, death, scream).
    // Lying or on all fours the Drowned's body (barnacles and all) is thicker than the X Bot the clips were made on:
    // thighs and shins sank 5 to 13 cm in these clips (measured 02/10). Unity's heightOffset is inverted: negative lifts.
    private const float ProneLift = -0.07f;
    private static readonly string[] Prone = { "zombie_crawl", "running_crawl", "zombie_biting_2" };

    private static readonly string[] Looping = { "zombie_walk", "zombie_run", "zombie_idle", "zombie_crawl", "running_crawl", "zombie_neck_bite", "zombie_biting", "creep_walk" };

    [MenuItem("Horror/Testes/Afogado na cena aberta")]
    public static void PlaceInOpenScene()
    {
        AnimatorController controller = Import();
        GameObject existing = GameObject.Find("Drowned_Showcase");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject drowned = (GameObject)PrefabUtility.InstantiatePrefab(model);
        // Unpacked: as a model prefab instance its overrides (name, controller, place) were lost on the next reimport.
        PrefabUtility.UnpackPrefabInstance(drowned, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        drowned.name = "Drowned_Showcase";
        // Facing the player of the test scene (who starts at z -1.5 looking along +Z): the model's face is its +Z.
        drowned.transform.SetPositionAndRotation(new Vector3(-1.2f, 0f, 2.4f), Quaternion.Euler(0f, 180f, 0f));
        Animator animator = drowned.GetComponent<Animator>() ?? drowned.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        // Always: a culled animator keeps a stale pose, which made the first test captures wrong.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        foreach (SkinnedMeshRenderer r in drowned.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.sharedMaterial = material;
            r.updateWhenOffscreen = false;
        }
        // Rigid hands with long claws: the wrists bend up after the animation when the claw tips would go into the floor.
        drowned.AddComponent<HandGroundContact>();
        AnimationShowcase showcase = drowned.AddComponent<AnimationShowcase>();
        SerializedObject so = new SerializedObject(showcase);
        SerializedProperty states = so.FindProperty("states");
        AnimationClip[] clips = controller.animationClips;
        states.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            states.GetArrayElementAtIndex(i).stringValue = clips[i].name;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Selection.activeGameObject = drowned;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(drowned.scene);
    }

    public static AnimatorController Import()
    {
        ModelImporter model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        model.animationType = ModelImporterAnimationType.Human;
        model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.importAnimation = false;
        // HandGroundContact reads the skin's vertices at run time (the claw tips): the mesh must stay readable.
        model.isReadable = true;
        model.SaveAndReimport();

        Texture(Folder + "/Drowned_BaseColor.png", TextureImporterType.Default, true);
        Texture(Folder + "/Drowned_Normal.png", TextureImporterType.NormalMap, false);
        Texture(Folder + "/Drowned_MetallicSmoothness.png", TextureImporterType.Default, false);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Drowned_BaseColor.png"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Drowned_Normal.png"));
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Drowned_MetallicSmoothness.png"));
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetFloat("_Smoothness", 1f);
        EditorUtility.SetDirty(material);
        List<AnimationClip> clips = new List<AnimationClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnimFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter anim = (ModelImporter)AssetImporter.GetAtPath(path);
            string clipName = System.IO.Path.GetFileNameWithoutExtension(path).Split('@').Last();
            anim.animationType = ModelImporterAnimationType.Human;
            // The clip files carry no skin, so their own "rest pose" is the first frame of the motion (a hunched zombie):
            // an avatar made from them is calibrated wrong and every retargeted pose comes out off. The avatar is copied
            // from the pack's skinned X Bot, which has the true T-pose and the same skeleton.
            anim.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            anim.sourceAvatar = SourceAvatar();
            anim.importAnimation = true;
            anim.materialImportMode = ModelImporterMaterialImportMode.None;
            ModelImporterClipAnimation[] defaults = anim.defaultClipAnimations;
            foreach (ModelImporterClipAnimation c in defaults)
            {
                c.name = clipName;
                c.loopTime = Looping.Contains(clipName);
                c.loopPose = c.loopTime;
                // Root settings validated 02/10 against the original clips sampled on the X Bot without retargeting (lowest
                // skin point per frame): height AND rotation baked into the pose, height measured from the feet, horizontal
                // travel left as root motion. Baking the travel made the body float and sink; leaving the rotation out
                // dropped the whole-body pitch (the running crawl went flat with the legs in the air). With this set every
                // clip matches the original within 2 to 3 cm. The showcase drops the travel (walks in place); the creature's
                // AI should use applyRootMotion. Re-measure after any change (ground.py / sheets in the scratch tools).
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = false;
                c.heightFromFeet = true;
                c.lockRootPositionXZ = false;
                c.keepOriginalPositionXZ = true;
                c.heightOffset = Prone.Contains(clipName) ? ProneLift : 0f;
            }

            anim.clipAnimations = defaults;
            anim.SaveAndReimport();
            clips.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")));
        }

        // Reuse the controller asset (same GUID) so scenes that point at it keep working after a reimport.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        // No IK pass: the claw fix bends the wrists in LateUpdate (humanoid hand IK is full-body and bent the crawls).
        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = false;
        controller.layers = layers;
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState old in machine.states)
        {
            machine.RemoveState(old.state);
        }

        foreach (AnimationClip clip in clips.OrderBy(c => c.name))
        {
            machine.AddState(clip.name).motion = clip;
        }

        AssetDatabase.SaveAssets();
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        Debug.Log("[Drowned] avatar " + (avatar != null ? (avatar.isValid + " human " + avatar.isHuman) : "missing") + ", clips " + clips.Count);
        return controller;
    }

    // The Mixamo pack's own skinned character (T-pose): the avatar every clip of the pack is read with.
    private const string SourceAvatarModel = ProjectPaths.CharacterModels + "/XBotReference/XBot.fbx";

    private static Avatar SourceAvatar()
    {
        ModelImporter source = (ModelImporter)AssetImporter.GetAtPath(SourceAvatarModel);
        if (source.animationType != ModelImporterAnimationType.Human)
        {
            source.animationType = ModelImporterAnimationType.Human;
            source.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            source.importAnimation = false;
            source.SaveAndReimport();
        }

        return AssetDatabase.LoadAllAssetsAtPath(SourceAvatarModel).OfType<Avatar>().First();
    }

    private static void Texture(string path, TextureImporterType type, bool srgb)
    {
        TextureImporter t = (TextureImporter)AssetImporter.GetAtPath(path);
        t.textureType = type;
        t.sRGBTexture = srgb;
        t.maxTextureSize = 2048;
        t.SaveAndReimport();
    }
}
