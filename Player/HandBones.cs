using UnityEngine;

// Finger bones of a character. On the player's body (a MetaHuman) the fingers are not in the Humanoid avatar
// (Editor/PlayerAvatarFingers explains why), so they are found by name under the hand: thumb_01_r ... pinky_03_l.
// A character whose avatar does map its fingers (packs, the X Bot) answers through the Animator as before.
public static class HandBones
{
    // Thumb to little finger, the order of HumanBodyBones.
    public static readonly string[] Names = { "thumb", "index", "middle", "ring", "pinky" };

    // finger 0..4 (thumb to little), segment 0..2 (from the palm out).
    public static Transform Find(Animator animator, bool left, int finger, int segment)
    {
        HumanBodyBones first = left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
        Transform mapped = animator.GetBoneTransform(first + finger * 3 + segment);
        if (mapped != null)
        {
            return mapped;
        }
        return Find(animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand), left, finger, segment);
    }

    public static Transform Find(Transform hand, bool left, int finger, int segment)
    {
        if (hand == null)
        {
            return null;
        }
        string side = left ? "_l" : "_r";
        Transform bone = hand.Find(Names[finger] + "_01" + side);
        for (int s = 1; s <= segment && bone != null; s++)
        {
            bone = bone.Find(Names[finger] + "_0" + (s + 1) + side);
        }
        return bone;
    }

    // Which way a finger's joints turn about their hinge (local Z on the MetaHuman) to close toward the palm: the
    // bind pose curls every finger a little about Z alone, so its sign is the bend's. Read it before anything poses
    // the hand.
    public static float BendSign(Transform middleJoint)
    {
        return middleJoint != null && Mathf.DeltaAngle(0f, middleJoint.localEulerAngles.z) > 0f ? 1f : -1f;
    }
}
