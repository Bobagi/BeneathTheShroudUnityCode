using System.Collections.Generic;
using UnityEngine;

// The jacket's spring bones on a body that does not have them (04/10). ErickOutfit.fbx carries bones the game's
// skeleton lacks (jacket_hem_*, from SourceAssets/Tools/metahuman_outfit.py); the builders that re-point the outfit
// onto PlayerBody's bones by name create them first, under the same parent and at the same rest pose, and then hang
// Player/ClothSprings on the body with capsules round the thighs and the hips.
public static class OutfitSprings
{
    public const string HemPrefix = "jacket_hem_";

    // Adds to `bones` (and to the body's hierarchy) every bone of the source outfit that the body has not got.
    public static void AddMissingBones(GameObject source, Dictionary<string, Transform> bones)
    {
        foreach (Transform t in source.GetComponentsInChildren<Transform>(true))
        {
            if (bones.ContainsKey(t.name) || t.parent == null || !bones.TryGetValue(t.parent.name, out Transform parent))
            {
                continue;
            }

            if (!t.name.StartsWith(HemPrefix))
            {
                continue;
            }

            Transform bone = new GameObject(t.name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = t.localPosition;
            bone.localRotation = t.localRotation;
            bone.localScale = t.localScale;
            bones[t.name] = bone;
        }
    }

    // ClothSprings on `body` for the hem chains, if the body has them.
    public static void Attach(GameObject body, Dictionary<string, Transform> bones)
    {
        List<Transform> roots = new List<Transform>();
        foreach (KeyValuePair<string, Transform> pair in bones)
        {
            if (pair.Key.StartsWith(HemPrefix) && pair.Key.EndsWith("_1"))
            {
                roots.Add(pair.Value);
            }
        }

        if (roots.Count == 0)
        {
            return;
        }

        roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        // Capsules the size of the CLOTHES the hem hangs over, not of the skin (measured on ErickOutfit.fbx, 07/10:
        // trousers and T-shirt 0.16 to 0.17 m from the pelvis axis, the coat's hem 0.18; the thigh in trousers about
        // 0.11 at the top). With 0.12 round the hips the hem was pushed 5 cm into the trousers when walking.
        List<ClothSprings.Capsule> capsules = new List<ClothSprings.Capsule>();
        foreach (string side in new[] { "l", "r" })
        {
            if (bones.TryGetValue("thigh_" + side, out Transform thigh) && bones.TryGetValue("calf_" + side, out Transform calf))
            {
                capsules.Add(new ClothSprings.Capsule { start = thigh, end = calf, radius = ClothSprings.ThighRadius });
            }
        }

        if (bones.TryGetValue("pelvis", out Transform pelvis) && bones.TryGetValue("spine_03", out Transform spine))
        {
            capsules.Add(new ClothSprings.Capsule { start = pelvis, end = spine, radius = ClothSprings.HipsRadius });
        }

        ClothSprings springs = body.GetComponent<ClothSprings>() ?? body.AddComponent<ClothSprings>();
        springs.Configure(roots.ToArray(), capsules.ToArray());
    }
}
