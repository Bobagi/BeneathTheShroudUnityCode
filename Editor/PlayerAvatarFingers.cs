using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The player's avatar (PlayerBody.fbx, a MetaHuman): the Humanoid mapping Unity makes on its own, fingers included,
// with the T-pose Unity enforces as the reference (where every muscle is 0).
// History: on 05/10 the fingers were taken out of the mapping (Mecanim bends each finger joint about one axis per
// hand, 21 to 36 degrees off this rig's hinges, which twisted the fingers of the recorded hands) and the reference
// was set to the model's bind pose. Both were undone on 07/10: with the A-pose as reference every pack clip
// retargeted with the arms 45 degrees too low (the idle crossed the hands behind the back), and with no finger in
// the mapping no pack clip moved the fingers, so a free hand hung with straight, splayed fingers (Gustavo: "a palma
// virada para a frente"). The pack animations (Starter Assets, Mixamo) now drive the hands exactly as they come.
// The first person arms (FirstPersonArms, a Generic rig of the same bones under the camera) animate the fingers as
// plain transform curves and copy them onto this body after the Animator (ArmsFromRig), so the mapping does not
// touch them.
public static class PlayerAvatarFingers
{
    private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

    [MenuItem("Horror/Flooded House/Corrigir dedos do avatar do jogador")]
    public static void FixMenu()
    {
        Debug.Log("[PlayerAvatar] " + (Fix(ProjectPaths.CharacterModels + "/PlayerBody/PlayerBody.fbx") ? "avatar refeito pela Unity (dedos incluídos, pose em T)" : "avatar já estava certo"));
    }

    // True when it changed (and reimported) the model.
    public static bool Fix(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
        {
            return false;
        }

        HumanDescription description = importer.humanDescription;
        bool mapped = description.human != null && description.human.Length > 0;
        bool fingersMapped = mapped && description.human.Any(h => IsFinger(h.humanName));
        if (mapped && fingersMapped && ReferenceIsTPose(modelPath, description))
        {
            return false;
        }

        // Let Unity map the bones (fingers included) and enforce its T-pose from scratch: an empty description on
        // import does both.
        importer.humanDescription = new HumanDescription();
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();
        Debug.Log("[PlayerAvatar] " + modelPath + ": avatar refeito pela Unity, " + importer.humanDescription.human.Length + " ossos Humanoid (dedos incluídos), referência em T: " + ReferenceIsTPose(modelPath, importer.humanDescription));
        return true;
    }

    private static bool IsFinger(string humanName)
    {
        return Fingers.Any(f => humanName.StartsWith("Left " + f + " ") || humanName.StartsWith("Right " + f + " "));
    }

    // The reference pose is a T-pose when both upper arms point sideways along X (the bind pose of a MetaHuman is an
    // A-pose, 45 degrees down). The pose is composed from the description's local rotations down the model's
    // hierarchy.
    public static bool ReferenceIsTPose(string modelPath, HumanDescription description)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null || description.skeleton == null || description.skeleton.Length == 0)
        {
            return false;
        }
        Dictionary<string, SkeletonBone> bones = new Dictionary<string, SkeletonBone>();
        foreach (SkeletonBone b in description.skeleton)
        {
            bones[b.name] = b;
        }
        Dictionary<string, Transform> transforms = model.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
        foreach (string side in new[] { "l", "r" })
        {
            string upper = "upperarm_" + side;
            string lower = "lowerarm_" + side;
            if (!transforms.ContainsKey(upper) || !bones.ContainsKey(lower))
            {
                return false;
            }
            Quaternion world = Quaternion.identity;
            List<Transform> chain = new List<Transform>();
            for (Transform t = transforms[upper]; t != null && t != model.transform; t = t.parent)
            {
                chain.Add(t);
            }
            chain.Reverse();
            foreach (Transform t in chain)
            {
                world *= bones.TryGetValue(t.name, out SkeletonBone b) ? b.rotation : t.localRotation;
            }
            Vector3 direction = (world * bones[lower].position).normalized;
            if (Mathf.Abs(direction.y) > 0.25f || Mathf.Abs(direction.x) < 0.9f)
            {
                return false;
            }
        }
        return true;
    }
}
