using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Turns a Humanoid animation pack clip (MoCap Online, Mixamo...) into a first person rig clip: the clip is sampled
// on the PlayerBody of the open scene (Unity's Humanoid retargeting puts the pack's motion on this MetaHuman, fingers
// included), and both arms (clavicle to fingertips) are written frame by frame as quaternion curves into the rig
// clip. Any rotation curve the rig clip already had on those bones is removed first (a clip with m_LocalRotation
// curves ignores localEulerAnglesRaw ones written on top, 10/10). Other curves (the reload's cylinder, the
// speedloader, the events) are kept.
public static class PackArmsToRig
{
    public const string BodyName = "PlayerBody";
    public const float Fps = 30f;
    public const string PackFolder = "Assets/_Project/Art/Animations/Packs/MocapOnline_Pistol/";

    // The game's revolver clips from the pack (10/10, Gustavo: "apague tudo e refaça do zero usando esse pacote").
    public static readonly (string pack, string clip, bool loop)[] Revolver =
    {
        ("W1_Stand_Aim_Idle_IPC", FirstPersonRig.RevolverHoldClip, true),
        ("W1_Stand_Aim_Idle_IPC", FirstPersonRig.RevolverAimClip, true),
        ("W1_Jog_Aim_F_Loop_IPC", FirstPersonRig.RevolverSprintClip, true),
        ("W1_Stand_Fire_Single", FirstPersonRig.RevolverFireClip, false),
    };

    // Where the gun's root (the grip's palm point) sits in the eye's space for each clip (metres: right, up, ahead).
    // The pack's shooter holds the gun 24 cm right of and 26 cm below the eye (a third person stance, measured on
    // MotusMan): the pose of the arms around the gun is the pack's, where the gun sits on screen is ours, as in any
    // shooter. The bore is always parallel to the eye's forward. Aim: the sights (6 cm above the root) on the eye line.
    public static readonly Dictionary<string, Vector3> GunInEye = new Dictionary<string, Vector3>
    {
        { FirstPersonRig.RevolverHoldClip, new Vector3(0.09f, -0.17f, 0.30f) },
        { FirstPersonRig.RevolverAimClip, new Vector3(0.0f, -0.09f, 0.46f) },
        { FirstPersonRig.RevolverSprintClip, new Vector3(0.05f, -0.16f, 0.30f) },
        { FirstPersonRig.RevolverFireClip, new Vector3(0.0f, -0.09f, 0.46f) },
    };

    // The pack's shooter blades the torso and leans: relative to the chest (the rig's root) the gun points some
    // 40 degrees off to the side and the hands sit off-centre, so with the root fixed in the eye's space the gun
    // aimed at the sky's corner (10/10, seen in Play). The arms are therefore carried over RELATIVE TO THE HEAD (the
    // camera): per frame, M = H_idle * inverse(H_pack), with H the head's pose in the chest's space (H_idle from the
    // body's own idle, which is how the rig's root was measured in the eye's space), applied to the arm set in root
    // space: the clavicles turn and the upper arms move with them, so the arm set pivots on the chest as a whole.
    public static bool HeadRelative = true;

    // The camera in the head bone's space (centimetres), measured in Play on the Erick body with the game running
    // (10/10): deriving it from the idle clip put the gun a quarter of the screen low.
    public static readonly Vector3 EyeInHead = new Vector3(-13.5f, -10.5f, 0.2f);

    private struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    // A hand's frame from its joints (the same physical frame on any skeleton): origin at the wrist, X down the
    // palm to the middle knuckle, Y across the knuckles (index to pinky), Z out of the back of the hand.
    private static void HandFrame(Transform wrist, Transform middle, Transform index, Transform pinky, out Vector3 origin, out Quaternion rotation, out float size)
    {
        Vector3 x = (middle.position - wrist.position).normalized;
        Vector3 y = pinky.position - index.position;
        y = (y - x * Vector3.Dot(y, x)).normalized;
        Vector3 z = Vector3.Cross(x, y).normalized;
        rotation = Quaternion.LookRotation(z, y);
        origin = wrist.position;
        size = Vector3.Distance(middle.position, wrist.position);
    }

    private static Pose HeadInChest(Transform chest, Transform head)
    {
        return new Pose { position = chest.InverseTransformPoint(head.position), rotation = Quaternion.Inverse(chest.rotation) * head.rotation };
    }

    // The body's idle clip: the PlayerBody controller's clip named Idle (or any idle).
    private static AnimationClip BodyIdle(Animator animator)
    {
        RuntimeAnimatorController c = animator.runtimeAnimatorController;
        if (c == null)
        {
            return null;
        }

        AnimationClip exact = c.animationClips.FirstOrDefault(k => k.name == "Idle");
        return exact != null ? exact : c.animationClips.FirstOrDefault(k => k.name.ToLower().Contains("idle"));
    }

    // Reports where the hold's bore points in the eye's space (0 degrees = down the eye's forward).
    [MenuItem("Horror/Animação/Primeira pessoa: medir a mira do revólver do pack")]
    public static void MeasureAimMenu()
    {
        Debug.Log("[PackArms] cano a " + MeasureAim().ToString("F1") + " graus da frente do olho");
    }

    public static float MeasureAim()
    {
        FirstPersonArms arms = Object.FindAnyObjectByType<FirstPersonArms>(FindObjectsInactive.Include);
        AnimationClip hold = AssetDatabase.LoadAssetAtPath<AnimationClip>(FirstPersonRig.RevolverHoldClip);
        Transform socket = arms != null ? arms.transform.Find(FirstPersonRig.SocketPath(FirstPersonArms.WeaponSocketName)) : null;
        if (hold == null || socket == null || socket.childCount == 0)
        {
            return -1f;
        }

        hold.SampleAnimation(arms.gameObject, 0f);
        Vector3 bore = arms.transform.InverseTransformDirection(socket.GetChild(0).forward).normalized;
        Vector3 eyeForward = (Quaternion.Inverse(FirstPersonRig.RootInEyeRotation) * Vector3.forward).normalized;
        return Vector3.Angle(bore, eyeForward);
    }

    [MenuItem("Horror/Animação/Primeira pessoa: revólver inteiro a partir do pack (MoCap Online)")]
    public static void BakeRevolverMenu()
    {
        foreach ((string pack, string clip, bool loop) in Revolver)
        {
            AnimationClip source = LoadPackClip(pack);
            if (source == null)
            {
                Debug.LogError("[PackArms] falta " + PackFolder + pack + ".fbx (importe o pack como Humanoid).");
                continue;
            }

            BakeClip(source, clip, loop);
        }

        FirstPersonRig.BuildController();
    }

    public static AnimationClip LoadPackClip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(PackFolder + name + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
    }

    public static AnimationClip BakeClip(AnimationClip source, string targetPath, bool loop)
    {
        GameObject body = GameObject.Find(BodyName);
        Animator animator = body != null ? body.GetComponent<Animator>() : null;
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("[PackArms] o " + BodyName + " com Animator Humanoid precisa estar na cena aberta (SuitTest).");
            return null;
        }

        AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
        bool created = target == null;
        if (created)
        {
            target = new AnimationClip { frameRate = Fps };
        }

        List<string> bones = FirstPersonRig.BoneNames(false).Concat(FirstPersonRig.BoneNames(true)).ToList();
        Transform[] all = body.GetComponentsInChildren<Transform>(true);
        Dictionary<string, Transform> bodyBone = bones.ToDictionary(b => b, b => all.FirstOrDefault(t => t.name == b));

        // Remove every rotation curve of those bones (quaternion or euler), then write the new ones.
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(target))
        {
            string bone = b.path.Split('/').Last();
            if (bones.Contains(bone) && (b.propertyName.StartsWith("m_LocalRotation") || b.propertyName.StartsWith("localEulerAngles")))
            {
                AnimationUtility.SetEditorCurve(target, b, null);
            }
        }

        int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * Fps) + 1);
        float[] times = new float[frames];
        Dictionary<string, Quaternion[]> rot = bones.ToDictionary(b => b, b => new Quaternion[frames]);
        Transform chest = all.FirstOrDefault(t => t.name == "spine_05");
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Pose[] headPack = new Pose[frames];
        Vector3[] borePack = new Vector3[frames];
        Vector3[] gunPack = new Vector3[frames];
        // The pack's own skeleton (the FBX imported as Generic, with the MotusMan hierarchy), sampled alongside: the
        // LEFT hand's place on the right hand is read there, in the right hand's joint frame, because Humanoid
        // retargeting keeps rotations, not where the hands meet (our left hand floated above the gun, 10/10).
        GameObject packPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackFolder + source.name + "_generic.fbx");
        AnimationClip packGeneric = packPrefab != null ? AssetDatabase.LoadAllAssetsAtPath(PackFolder + source.name + "_generic.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview")) : null;
        GameObject packInstance = packPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(packPrefab) : null;
        Transform[] packAll = packInstance != null ? packInstance.GetComponentsInChildren<Transform>(true) : new Transform[0];
        System.Func<string, Transform> packBone = n => packAll.FirstOrDefault(t => t.name == n);
        Pose[] leftInRight = new Pose[frames];
        float[] packHandSize = new float[frames];
        bool haveLeft = packInstance != null && packGeneric != null && packBone("RightHand") != null && packBone("LeftHandMiddle1") != null;
        if (packInstance != null)
        {
            packInstance.hideFlags = HideFlags.HideAndDontSave;
            packInstance.transform.position = body.transform.position + Vector3.right * 5f;
        }
        Pose headIdle = new Pose { rotation = Quaternion.identity };
        // The gun's bore in the pack pose: the right hand's turn times the weapon socket's turn in the hand (the
        // revolver's model points +Z under the socket), read on the body in chest space.
        FirstPersonArms sceneArms = Object.FindAnyObjectByType<FirstPersonArms>(FindObjectsInactive.Include);
        Transform sceneSocket = sceneArms != null ? sceneArms.transform.Find(FirstPersonRig.SocketPath(FirstPersonArms.WeaponSocketName)) : null;
        Quaternion socketInHand = sceneSocket != null ? sceneSocket.localRotation : Quaternion.identity;
        Vector3 socketPosInHand = sceneSocket != null ? sceneSocket.localPosition : Vector3.zero;
        AnimationClip idle = BodyIdle(animator);
        AnimationMode.StartAnimationMode();
        try
        {
            if (HeadRelative && idle != null && chest != null && head != null)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(body, idle, 0f);
                AnimationMode.EndSampling();
                headIdle = HeadInChest(chest, head);
            }

            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(f / Fps, source.length);
                times[f] = t;
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(body, source, t);
                AnimationMode.EndSampling();
                foreach (string b in bones)
                {
                    if (bodyBone[b] != null)
                    {
                        rot[b][f] = bodyBone[b].localRotation;
                    }
                }

                if (haveLeft)
                {
                    packGeneric.SampleAnimation(packInstance, t);
                    HandFrame(packBone("RightHand"), packBone("RightHandMiddle1"), packBone("RightHandIndex1"), packBone("RightHandPinky1"), out Vector3 ro, out Quaternion rr, out float rs);
                    HandFrame(packBone("LeftHand"), packBone("LeftHandMiddle1"), packBone("LeftHandIndex1"), packBone("LeftHandPinky1"), out Vector3 lo, out Quaternion lr, out float ls);
                    leftInRight[f] = new Pose { position = Quaternion.Inverse(rr) * (lo - ro), rotation = Quaternion.Inverse(rr) * lr };
                    packHandSize[f] = rs;
                }

                if (chest != null && head != null)
                {
                    headPack[f] = HeadInChest(chest, head);
                    Transform hr = bodyBone["hand_r"];
                    borePack[f] = hr != null ? (Quaternion.Inverse(chest.rotation) * (hr.rotation * (socketInHand * Vector3.forward))).normalized : Vector3.forward;
                    gunPack[f] = hr != null ? chest.InverseTransformPoint(hr.TransformPoint(socketPosInHand)) : Vector3.zero;
                }
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            if (packInstance != null)
            {
                Object.DestroyImmediate(packInstance);
            }
        }

        // Eye-relative: the pack's shooter looks down the gun, so the frame carried over is the EYE point with the
        // BORE as forward (the head bone's tilt is not the gaze: using it put the gun 33 degrees up, 10/10). The eye
        // point in the chest's space comes from the head bone plus the eye's offset in the head, measured on the
        // body's idle against where the rig's root was measured in the eye's space. The target is the rig root's
        // own eye frame. M = Target * inverse(PackFrame), per frame, applied to the arm set about the root.
        Dictionary<string, Vector3[]> upperPos = new Dictionary<string, Vector3[]>();
        if (HeadRelative && chest != null && head != null)
        {
            // Rest offsets from the PREFAB: the scene's rig carries the last sampled pose (an earlier bake's upper arm
            // position compounded into the next one, 58 cm off, 10/10).
            Transform rigRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonRig.PrefabPath).transform;
            Quaternion rootInEye = FirstPersonRig.RootInEyeRotation;
            Vector3 eyeInRoot = (Quaternion.Inverse(rootInEye) * (-FirstPersonRig.RootInEyePosition)) / FirstPersonRig.RootScale.x;
            Quaternion targetRot = Quaternion.LookRotation(Quaternion.Inverse(rootInEye) * Vector3.forward, Quaternion.Inverse(rootInEye) * Vector3.up);
            float worstTurn = 0f;
            float worstShift = 0f;
            foreach (string side in new[] { "_r", "_l" })
            {
                Transform clav = rigRoot.Find(FirstPersonRig.PathOf("clavicle" + side));
                Transform upper = rigRoot.Find(FirstPersonRig.PathOf("upperarm" + side));
                Vector3 clavPos = clav.localPosition;
                Vector3 upperRest = upper.localPosition;
                Quaternion[] cq = rot["clavicle" + side];
                Vector3[] up = new Vector3[frames];
                for (int f = 0; f < frames; f++)
                {
                    Vector3 upPack = headPack[f].rotation * Vector3.up;
                    Quaternion packRot = Quaternion.LookRotation(borePack[f], upPack);
                    Quaternion mRot = targetRot * Quaternion.Inverse(packRot);
                    Vector3 gunTargetEye = GunInEye.ContainsKey(targetPath) ? GunInEye[targetPath] : new Vector3(0.09f, -0.17f, 0.30f);
                    Vector3 gunTargetRoot = eyeInRoot + (Quaternion.Inverse(rootInEye) * gunTargetEye) / FirstPersonRig.RootScale.x;
                    Vector3 mPos = gunTargetRoot - mRot * gunPack[f];
                    Quaternion clavOld = cq[f];
                    Quaternion clavNew = mRot * clavOld;
                    Vector3 shoulderWorld = mRot * (clavPos + clavOld * upperRest) + mPos;
                    up[f] = Quaternion.Inverse(clavNew) * (shoulderWorld - clavPos);
                    cq[f] = clavNew;
                    worstTurn = Mathf.Max(worstTurn, Quaternion.Angle(Quaternion.identity, mRot));
                    worstShift = Mathf.Max(worstShift, (mRot * clavPos + mPos - clavPos).magnitude);
                }

                upperPos["upperarm" + side] = up;
            }

            Debug.Log("[PackArms] " + source.name + ": braços levados ao referencial do olho pela linha do cano (giro até " + worstTurn.ToString("F1") + " graus, ombro deslocado até " + worstShift.ToString("F1") + " cm)");
        }

        // The left hand onto the gun: per frame the scene rig is posed with the frame's rotations, the right hand's
        // joint frame is built, the pack's left hand pose in that frame (scaled by our hand's size over the pack's)
        // gives the left wrist's target, and the left arm is solved ONCE here (two bones, elbow down and out) and
        // written as keys, as FirstPersonTakes does. Nothing is solved in the game.
        if (haveLeft)
        {
            FirstPersonArms sceneRig = Object.FindAnyObjectByType<FirstPersonArms>(FindObjectsInactive.Include);
            Transform rroot = sceneRig.transform;
            System.Func<string, Transform> rb = n => rroot.Find(FirstPersonRig.PathOf(n));
            Transform upperL = rb("upperarm_l"), lowerL = rb("lowerarm_l"), handL = rb("hand_l");
            // The hand bone's turn relative to its own joint frame (constant): measured on the rest pose.
            foreach (string b in bones) { if (rb(b) != null) rb(b).localRotation = rot[b][0]; }
            foreach (KeyValuePair<string, Vector3[]> kv in upperPos) { rb(kv.Key).localPosition = kv.Value[0]; }
            HandFrame(handL, rb("middle_01_l"), rb("index_01_l"), rb("pinky_01_l"), out Vector3 lo0, out Quaternion lr0, out float ls0);
            Quaternion boneInFrameL = Quaternion.Inverse(lr0) * handL.rotation;
            float worstReach = 0f;
            Vector3 poleEye = new Vector3(-0.35f, -0.45f, 0.05f);
            for (int f = 0; f < frames; f++)
            {
                foreach (string b in bones) { if (rb(b) != null) rb(b).localRotation = rot[b][f]; }
                foreach (KeyValuePair<string, Vector3[]> kv in upperPos) { rb(kv.Key).localPosition = kv.Value[f]; }
                HandFrame(rb("hand_r"), rb("middle_01_r"), rb("index_01_r"), rb("pinky_01_r"), out Vector3 ro, out Quaternion rr, out float rs);
                float scale = packHandSize[f] > 1e-6f ? rs / packHandSize[f] : 1f;
                Vector3 wristTarget = ro + rr * (leftInRight[f].position * scale);
                Quaternion targetRot = rr * leftInRight[f].rotation * boneInFrameL;
                Vector3 pole = upperL.position + rroot.TransformDirection(Quaternion.Inverse(FirstPersonRig.RootInEyeRotation) * poleEye);
                FirstPersonTakes.SolveWithPole(upperL, lowerL, handL, wristTarget, pole);
                handL.rotation = targetRot;
                worstReach = Mathf.Max(worstReach, Vector3.Distance(handL.position, wristTarget));
                rot["upperarm_l"][f] = upperL.localRotation;
                rot["lowerarm_l"][f] = lowerL.localRotation;
                rot["hand_l"][f] = handL.localRotation;
            }

            Debug.Log("[PackArms] " + source.name + ": mão esquerda posta na direita como no pack (escala " + (packHandSize[0] > 0 ? (ls0 / packHandSize[0]).ToString("F2") : "?") + "), alcance pior " + (worstReach * 1000f).ToString("F1") + " mm");
        }

        string[] comps = { "x", "y", "z", "w" };
        int written = 0;
        foreach (string b in bones)
        {
            if (bodyBone[b] == null)
            {
                continue;
            }

            Quaternion[] q = rot[b];
            for (int f = 1; f < frames; f++)
            {
                if (Quaternion.Dot(q[f], q[f - 1]) < 0f)
                {
                    q[f] = new Quaternion(-q[f].x, -q[f].y, -q[f].z, -q[f].w);
                }
            }

            string path = FirstPersonRig.PathOf(b);
            for (int c = 0; c < 4; c++)
            {
                Keyframe[] keys = new Keyframe[frames];
                for (int f = 0; f < frames; f++)
                {
                    keys[f] = new Keyframe(times[f], q[f][c]);
                }

                AnimationCurve curve = new AnimationCurve(keys);
                for (int f = 0; f < frames; f++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, f, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, f, AnimationUtility.TangentMode.ClampedAuto);
                }

                AnimationUtility.SetEditorCurve(target, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + comps[c]), curve);
            }

            if (upperPos.ContainsKey(b))
            {
                string[] axes = { "x", "y", "z" };
                for (int a = 0; a < 3; a++)
                {
                    Keyframe[] keys = new Keyframe[frames];
                    for (int f = 0; f < frames; f++)
                    {
                        keys[f] = new Keyframe(times[f], upperPos[b][f][a]);
                    }

                    AnimationUtility.SetEditorCurve(target, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + axes[a]), new AnimationCurve(keys));
                }
            }

            written++;
        }

        target.EnsureQuaternionContinuity();
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(target);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(target, settings);
        if (created)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath));
            AssetDatabase.CreateAsset(target, targetPath);
        }

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        Debug.Log("[PackArms] " + source.name + " -> " + targetPath + ": " + written + " ossos, " + frames + " quadros (" + source.length.ToString("F2") + " s), loop " + loop);
        return target;
    }
}
