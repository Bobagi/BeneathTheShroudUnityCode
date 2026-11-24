using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The revolver's first person clips made from ONE grip (13/10, Gustavo: "quando eu atiro ou miro, a arma e as mãos
// vão pra frente sem motivo... refaça todas as animações para ficarem apresentáveis em primeira pessoa").
//
// How (the way view model animators work: the gun is animated in the camera's space and the arms follow it, solved
// once in the DCC and baked to bones): every clip starts from Revolver_Hold at its first frame, which keeps the hands,
// the fingers and where the gun sits in the right hand exactly as Gustavo left them. Per frame a target says where the
// gun's SIGHT LINE goes in the eye's space; the right arm is solved to carry the gun there (two bones, elbow kept on
// the side it bends in the hold, the forearm taking half of the wrist's twist so the skin does not wring) and the left
// hand either stays where it holds the gun in the hold (support grip), goes somewhere in the eye's space, or carries
// the speedloader onto the open cylinder. The result is written as editable Euler curves (ClipRecorder): open it in
// the Animation window and adjust by hand afterwards; nothing solves in the game.
//
//   Revolver_Aim     sights on the eye line, rear sight AimEyeRelief ahead of the eye, the gun level.
//   Revolver_Sprint  gun low in the middle, muzzle down and to the left, canted (the arms pump by ViewModelMotion).
//   Revolver_Reload  swing out, muzzle up and eject, muzzle down, speedloader, close (ReloadKeys, 3 s).
// Revolver_Hold is not touched. The shot has no clip: the kick is procedural (ViewModelMotion.Kick, Revolver flip),
// so a shot from the hip stays at the hip and an aimed one stays aimed.
public static class ViewModelPoses
{
    public const float Fps = 30f;

    // The Colt Python's sights in the gun holder's space (metres; the holder is the Revolver object on the weapon
    // socket, barrel along +Z), measured on the mesh 13/10: top of the rear sight blade and of the front sight ramp.
    public static readonly Vector3 RearSight = new Vector3(0f, 0.1147f, 0.048f);
    public static readonly Vector3 FrontSight = new Vector3(0f, 0.1168f, 0.262f);

    // Aim: the rear sight this far ahead of the eye on its forward line (the front sight lines up behind it).
    public static float AimEyeRelief = 0.33f;
    public static float AimSightDrop = 0.004f;

    // Sprint: the rear sight's place in the eye (metres) and the gun's turn (pitch down, yaw left, roll).
    public static Vector3 SprintPosition = new Vector3(0.03f, -0.11f, 0.28f);
    public static Vector3 SprintEuler = new Vector3(30f, -30f, 28f);

    private const string Path = FirstPersonRig.Folder + "/Revolver/";
    private const string ExtractorPath = "/Python357/Python357_Rig/Revolver/Extractor";
    private const string CylinderName = "Cylinder";

    // ------------------------------------------------------------------ the rig as found in the open scene

    private sealed class Rig
    {
        public FirstPersonArms arms;
        public Transform eye;
        public Transform viewModel;
        public Transform gun;
        public Transform extractor;
        public Transform cylinder;
        public Transform loader;
        public Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        public List<string> boneNames;
        public string gunPath;
        public string extractorPath;
        public string loaderPath;
        public Quaternion extractorClosed;
        public Quaternion extractorOpen;
        public Transform[] chamberBones;
        // The hand's turn relative to its forearm in the hold, per side: only twist added on top of it is shared.
        public Dictionary<string, Quaternion> holdWrist = new Dictionary<string, Quaternion>();
        public float worstTime;
        public float lastReach;
        public float worstReach;
    }

    private static Rig Find()
    {
        FirstPersonArms arms = UnityEngine.Object.FindAnyObjectByType<FirstPersonArms>(FindObjectsInactive.Include);
        Revolver revolver = UnityEngine.Object.FindAnyObjectByType<Revolver>(FindObjectsInactive.Include);
        if (arms == null || revolver == null)
        {
            Debug.LogError("[ViewModel] abra a SuitTest: precisa do FirstPersonArms com o revólver no encaixe");
            return null;
        }

        Rig rig = new Rig { arms = arms, viewModel = arms.transform.parent };
        rig.eye = rig.viewModel.parent;
        rig.gun = revolver.transform;
        rig.boneNames = FirstPersonRig.BoneNames(false).Concat(FirstPersonRig.BoneNames(true)).ToList();
        foreach (string b in rig.boneNames)
        {
            rig.bones[b] = arms.transform.Find(FirstPersonRig.PathOf(b));
        }

        rig.gunPath = AnimationUtility.CalculateTransformPath(rig.gun, arms.transform);
        rig.extractor = rig.gun.Find(ExtractorPath.TrimStart('/'));
        rig.extractorPath = rig.gunPath + ExtractorPath;
        rig.cylinder = rig.extractor != null ? rig.extractor.Find(CylinderName) : null;
        rig.loader = arms.LeftSocket != null ? arms.LeftSocket.Find("Speedloader") : null;
        rig.loaderPath = rig.loader != null ? AnimationUtility.CalculateTransformPath(rig.loader, arms.transform) : null;
        rig.chamberBones = new Transform[6];
        Transform gunRig = rig.gun.Find("Python357/Python357_Rig");
        for (int i = 0; i < 6 && gunRig != null; i++)
        {
            rig.chamberBones[i] = gunRig.Find("Bullet" + (i + 1));
        }

        // The swing-out from the model's own Open clip (Python357.fbx): first and last frame of the crane bone.
        AnimationClip open = AssetDatabase.LoadAllAssetsAtPath(Python357Setup.Folder + "/Python357.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == "Open");
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Python357Setup.Folder + "/Python357.fbx"));
        temp.hideFlags = HideFlags.HideAndDontSave;
        Transform ex = temp.GetComponentsInChildren<Transform>().First(t => t.name == "Extractor");
        open.SampleAnimation(temp, 0f);
        rig.extractorClosed = ex.localRotation;
        open.SampleAnimation(temp, open.length);
        rig.extractorOpen = ex.localRotation;
        UnityEngine.Object.DestroyImmediate(temp);
        return rig;
    }

    // ------------------------------------------------------------------ menus

    [MenuItem("Horror/Animação/Primeira pessoa: refazer mira, corrida e recarga (a partir do Revolver_Hold)")]
    public static void BuildAllMenu()
    {
        BuildAll();
    }

    public static string BuildAll()
    {
        string log = "";
        log += Bake(Path + "Revolver_Aim.anim", 1f / Fps, true, t => AimTarget(), null, null, t => false, null) + "\n";
        log += Bake(Path + "Revolver_Sprint.anim", 1f / Fps, true, t => (SprintPosition, Quaternion.Euler(SprintEuler)), null, null, t => false, null) + "\n";
        log += BakeReload() + "\n";
        FirstPersonRig.BuildController();
        Debug.Log("[ViewModel]\n" + log);
        return log;
    }

    private static (Vector3, Quaternion) AimTarget()
    {
        return (new Vector3(0f, -AimSightDrop, AimEyeRelief), Quaternion.identity);
    }

    // Another source and held item than the revolver's (the torch): set by BakeTorch around its Bake calls.
    private static string sourceClip;
    private static Transform item;
    private static Frame? itemFrame;
    private static bool solveLeft = true;

    // ------------------------------------------------------------------ the torch

    // Torch_Focus: the torch raised to the right of the eye, the lens TorchFocusPosition ahead, its beam on the point
    // under the crosshair TorchFocusDistance away; from Torch_Hold's grip (the left arm is the body's, not touched).
    public static Vector3 TorchFocusPosition = new Vector3(0.09f, -0.06f, 0.52f);
    public static float TorchFocusDistance = 6f;

    // Torch_Hold: lower right, the whole torch in view, the beam on the crosshair's point (it was so low only the
    // lens showed, and running pushed it off the screen, 13/10).
    public static Vector3 TorchHoldPosition = new Vector3(0.15f, -0.15f, 0.50f);

    [MenuItem("Horror/Animação/Primeira pessoa: refazer a lanterna erguida (a partir do Torch_Hold)")]
    public static void BuildTorchMenu()
    {
        Debug.Log("[ViewModel] " + BakeTorch());
    }

    public static string BakeTorch()
    {
        FirstPersonArms arms = UnityEngine.Object.FindAnyObjectByType<FirstPersonArms>(FindObjectsInactive.Include);
        TorchHold torch = UnityEngine.Object.FindAnyObjectByType<TorchHold>(FindObjectsInactive.Include);
        if (arms == null || torch == null)
        {
            return "falta a lanterna na mão do rig";
        }

        Light beam = (Light)new SerializedObject(torch).FindProperty("beam").objectReferenceValue;
        sourceClip = FirstPersonRig.TorchHoldClip;
        item = torch.transform;
        itemFrame = new Frame { position = Quaternion.Inverse(torch.transform.rotation) * (beam.transform.position - torch.transform.position), rotation = Quaternion.Inverse(torch.transform.rotation) * beam.transform.rotation };
        solveLeft = false;
        try
        {
            Vector3 to = new Vector3(0f, 0f, TorchFocusDistance) - TorchFocusPosition;
            string log = Bake(FirstPersonRig.TorchFocusClip, 1f / Fps, true, t => (TorchFocusPosition, Quaternion.LookRotation(to, Vector3.up)), null, null, t => false, null);
            Vector3 toHold = new Vector3(0f, 0f, TorchFocusDistance) - TorchHoldPosition;
            log += "\n" + Bake(FirstPersonRig.TorchHoldClip, 1f / Fps, true, t => (TorchHoldPosition, Quaternion.LookRotation(toHold, Vector3.up)), null, null, t => false, null);
            return log;
        }
        finally
        {
            sourceClip = null;
            item = null;
            itemFrame = null;
            solveLeft = true;
        }
    }

    // ------------------------------------------------------------------ the reload

    // Where the gun's sight line goes over the reload (seconds, eye space). Between keys: smoothstep.
    private struct GunKey
    {
        public float t;
        public Vector3 position;
        public Vector3 euler;
        public bool hold;   // the hold's own gun pose
        public GunKey(float t, Vector3 p, Vector3 e, bool hold = false) { this.t = t; position = p; euler = e; this.hold = hold; }
    }

    // What the left hand does: on the gun (support grip, optionally slid along the gun), at a point of the eye, or
    // carrying the speedloader to the cylinder (0 = 6 cm behind it, 1 = rounds in).
    private enum Left { Gun, Eye, Loader }

    private struct LeftKey
    {
        public float t;
        public Left kind;
        public Vector3 v;
        public LeftKey(float t, Left kind, Vector3 v) { this.t = t; this.kind = kind; this.v = v; }
    }

    public static float ReloadLength = 3.1f;

    private static readonly GunKey[] ReloadGun =
    {
        new GunKey(0.00f, Vector3.zero, Vector3.zero, true),
        // Turned onto its right side, toward the middle: the left thumb swings the cylinder out.
        new GunKey(0.30f, new Vector3(0.05f, -0.10f, 0.30f), new Vector3(-12f, -8f, -55f)),
        new GunKey(0.58f, new Vector3(0.03f, -0.10f, 0.30f), new Vector3(-8f, -6f, -50f)),
        // Muzzle up, the left palm strikes the ejector rod.
        new GunKey(0.88f, new Vector3(0.07f, -0.12f, 0.17f), new Vector3(-58f, 0f, -20f)),
        new GunKey(1.02f, new Vector3(0.07f, -0.115f, 0.17f), new Vector3(-64f, 0f, -20f)),
        // Muzzle well down in front of the chest, turned onto its right side: the open chambers face up and back,
        // at an angle to the eye, so the left hand brings the speedloader in from above without covering them (with
        // the chambers facing the eye the hand hid the whole gun; muzzle out to the right looked wrong, 13/10).
        new GunKey(1.35f, new Vector3(0.0f, -0.13f, 0.31f), new Vector3(25f, -5f, -45f)),
        new GunKey(2.15f, new Vector3(0.0f, -0.13f, 0.31f), new Vector3(23f, -5f, -45f)),
        // Back on its side, the left hand closes the cylinder, then up to the hold.
        new GunKey(2.45f, new Vector3(0.04f, -0.10f, 0.30f), new Vector3(-6f, -6f, -45f)),
        new GunKey(2.70f, new Vector3(0.05f, -0.10f, 0.30f), new Vector3(-4f, -6f, -40f)),
        new GunKey(3.05f, Vector3.zero, Vector3.zero, true),
    };

    private static readonly LeftKey[] ReloadLeft =
    {
        new LeftKey(0.00f, Left.Gun, Vector3.zero),
        new LeftKey(0.60f, Left.Gun, Vector3.zero),
        // Slides forward along the gun to push the ejector rod.
        new LeftKey(0.85f, Left.Gun, new Vector3(0.0f, 0.01f, 0.07f)),
        new LeftKey(1.00f, Left.Gun, new Vector3(0.0f, 0.0f, 0.10f)),
        // Down to the belt for the speedloader (out of view).
        new LeftKey(1.30f, Left.Eye, new Vector3(-0.12f, -0.50f, 0.12f)),
        new LeftKey(1.40f, Left.Eye, new Vector3(-0.12f, -0.50f, 0.12f)),
        new LeftKey(1.70f, Left.Loader, new Vector3(0f, 0f, 0f)),
        new LeftKey(1.88f, Left.Loader, new Vector3(1f, 0f, 0f)),
        new LeftKey(2.02f, Left.Loader, new Vector3(1f, 0f, 0f)),
        // Away with the empty loader, then back onto the gun to close the cylinder.
        new LeftKey(2.28f, Left.Eye, new Vector3(-0.16f, -0.42f, 0.18f)),
        new LeftKey(2.55f, Left.Gun, new Vector3(0f, 0.02f, 0.03f)),
        new LeftKey(2.75f, Left.Gun, Vector3.zero),
        new LeftKey(3.05f, Left.Gun, Vector3.zero),
    };

    // The crane: closed, swung out, closed again (seconds, 0 to 1).
    private static readonly (float t, float open)[] ReloadCrane =
    {
        (0.00f, 0f), (0.32f, 0f), (0.52f, 1f), (2.48f, 1f), (2.62f, 0f), (3.1f, 0f),
    };

    private static readonly (float t, bool on)[] ReloadLoader = { (0f, false), (1.32f, true), (2.26f, false) };

    private static readonly (float t, string name)[] ReloadEvents =
    {
        (0.34f, "ReloadOpen"), (1.0f, "ReloadEject"), (1.9f, "ReloadRoundsIn"), (2.62f, "ReloadClosed"), (3.0f, "ReloadFinished"),
    };

    private static string BakeReload()
    {
        Func<float, float> crane = t => Track(ReloadCrane.Select(k => (k.t, k.open)).ToArray(), t);
        AnimationEvent[] events = ReloadEvents.Select(e => new AnimationEvent { time = e.t, functionName = e.name }).ToArray();
        string log = Bake(Path + "Revolver_Reload.anim", ReloadLength, false, null, ReloadLeftAt, crane, t => ReloadLoader.Last(k => k.t <= t + 1e-4f).on, events);
        // The camera's key (FirstPersonArms.cameraPitch, applied by CameraTilt): the eye looks down at the open
        // cylinder while the speedloader goes in. The gun's keys above stay where the arms are comfortable.
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path + "Revolver_Reload.anim");
        AnimationCurve pitch = new AnimationCurve(ReloadCamera.Select(k => new Keyframe(k.t, k.v)).ToArray());
        for (int i = 0; i < pitch.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(pitch, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(pitch, i, AnimationUtility.TangentMode.ClampedAuto);
        }

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(FirstPersonArms), "cameraPitch"), pitch);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        return log;
    }

    // Degrees the eye looks down over the reload.
    private static readonly (float t, float v)[] ReloadCamera =
    {
        (0f, 0f), (0.3f, 6f), (0.6f, 6f), (0.95f, 3f), (1.35f, 14f), (2.15f, 14f), (2.45f, 8f), (2.75f, 3f), (3.05f, 0f),
    };

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private static float Track((float t, float v)[] keys, float t)
    {
        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (t <= keys[i + 1].t)
            {
                float u = Smooth(Mathf.InverseLerp(keys[i].t, keys[i + 1].t, t));
                return Mathf.Lerp(keys[i].v, keys[i + 1].v, u);
            }
        }

        return keys[keys.Length - 1].v;
    }

    private static (LeftKey a, LeftKey b, float u) ReloadLeftAt(float t)
    {
        for (int i = 0; i < ReloadLeft.Length - 1; i++)
        {
            if (t <= ReloadLeft[i + 1].t)
            {
                return (ReloadLeft[i], ReloadLeft[i + 1], Smooth(Mathf.InverseLerp(ReloadLeft[i].t, ReloadLeft[i + 1].t, t)));
            }
        }

        LeftKey last = ReloadLeft[ReloadLeft.Length - 1];
        return (last, last, 0f);
    }

    // ------------------------------------------------------------------ baking

    private struct Frame
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    // gunTarget: where the sight line goes (eye space), or null for the reload's keys. left: the reload's left hand
    // keys, or null for the support grip all along. crane: 0..1 swing-out. loaderOn: the speedloader shown.
    private static string Bake(string target, float length, bool loop, Func<float, (Vector3, Quaternion)> gunTarget,
        Func<float, (LeftKey, LeftKey, float)> left, Func<float, float> crane, Func<float, bool> loaderOn, AnimationEvent[] events)
    {
        Rig rig = Find();
        AnimationClip hold = AssetDatabase.LoadAssetAtPath<AnimationClip>(sourceClip ?? FirstPersonRig.RevolverHoldClip);
        if (rig == null || hold == null)
        {
            return "falhou";
        }

        if (item != null)
        {
            rig.gun = item;
            rig.gunPath = AnimationUtility.CalculateTransformPath(item, rig.arms.transform);
        }

        Vector3 vmPosition = rig.viewModel.localPosition;
        Quaternion vmRotation = rig.viewModel.localRotation;
        rig.viewModel.localPosition = Vector3.zero;
        rig.viewModel.localRotation = Quaternion.identity;
        Dictionary<Transform, (Vector3, Quaternion)> saved = rig.arms.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => (t.localPosition, t.localRotation));
        ClipRecorder recorder = new ClipRecorder(Fps);
        float worstRight = 0f, worstLeft = 0f, worstTwist = 0f, worstLeftTime = 0f;
        try
        {
            // The hold's first frame: the grip, the fingers, the gun on the socket.
            hold.SampleAnimation(rig.arms.gameObject, 0f);
            Frame sight = itemFrame ?? SightInHolder();
            Transform hr = rig.bones["hand_r"], hl = rig.bones["hand_l"];
            Pose holderInHand = Relative(hr, rig.gun);
            Pose leftInHolder = Relative(rig.gun, hl);
            Pose holdSightEye = EyePose(rig, rig.gun.position + rig.gun.rotation * sight.position, rig.gun.rotation * sight.rotation);
            Pose loaderInHand = rig.loader != null ? Relative(hl, rig.loader) : default;
            foreach (string side in new[] { "_r", "_l" })
            {
                rig.holdWrist[side] = Quaternion.Inverse(rig.bones["lowerarm" + side].rotation) * rig.bones["hand" + side].rotation;
            }

            Dictionary<string, Quaternion> holdLocal = rig.boneNames.Where(b => rig.bones[b] != null).ToDictionary(b => b, b => rig.bones[b].localRotation);
            Dictionary<string, Vector3> holdPosition = rig.boneNames.Where(b => rig.bones[b] != null).ToDictionary(b => b, b => rig.bones[b].localPosition);
            // Elbow poles: where the elbows are in the hold, in the eye's space (the arms keep bending that way).
            Vector3 poleR = rig.eye.InverseTransformPoint(rig.bones["lowerarm_r"].position + (rig.bones["lowerarm_r"].position - Mid(rig, "_r")) * 2f);
            Vector3 poleL = rig.eye.InverseTransformPoint(rig.bones["lowerarm_l"].position + (rig.bones["lowerarm_l"].position - Mid(rig, "_l")) * 2f);
            Vector3 gunLocalPos = rig.gun.localPosition;
            Quaternion gunLocalRot = rig.gun.localRotation;
            // The chambers in the cylinder's space, from the closed gun (the round bones do not follow the crane).
            Vector3[] chamberInCylinder = rig.cylinder != null ? rig.chamberBones.Select(b => rig.cylinder.InverseTransformPoint(b.position)).ToArray() : null;

            int frames = Mathf.Max(1, Mathf.RoundToInt(length * Fps)) + 1;
            if (loop && length <= 1f / Fps + 1e-4f)
            {
                frames = 2;
            }

            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(length, f / Fps);
                // Back to the hold's pose each frame (only the arms change).
                foreach (string b in holdLocal.Keys)
                {
                    rig.bones[b].localRotation = holdLocal[b];
                    rig.bones[b].localPosition = holdPosition[b];
                }

                rig.gun.localPosition = gunLocalPos;
                rig.gun.localRotation = gunLocalRot;
                float open = crane != null ? crane(t) : 0f;
                if (rig.extractor != null)
                {
                    rig.extractor.localRotation = Quaternion.Slerp(rig.extractorClosed, rig.extractorOpen, open);
                }

                // Where the sight line goes.
                Pose sightEye = gunTarget != null ? ToPose(gunTarget(t)) : ReloadGunAt(t, holdSightEye);
                Vector3 sightWorld = rig.eye.TransformPoint(sightEye.position);
                Quaternion sightWorldRot = rig.eye.rotation * sightEye.rotation;
                Quaternion holderRot = sightWorldRot * Quaternion.Inverse(sight.rotation);
                Vector3 holderPos = sightWorld - holderRot * sight.position;
                Quaternion handRRot = holderRot * Quaternion.Inverse(holderInHand.rotation);
                Vector3 handRPos = holderPos - handRRot * holderInHand.position;
                float missR = Solve(rig, "_r", handRPos, handRRot, rig.eye.TransformPoint(poleR), ref worstTwist);
                if (missR > worstRight)
                {
                    worstRight = missR;
                    rig.worstTime = t;
                    rig.worstReach = rig.lastReach;
                }

                // The left hand.
                Vector3 gunPosNow = rig.gun.position;
                Quaternion gunRotNow = rig.gun.rotation;
                Pose onGun = new Pose(gunPosNow + gunRotNow * leftInHolder.position, gunRotNow * leftInHolder.rotation);
                Pose leftPose = onGun;
                float holding = 0f;
                if (left != null)
                {
                    (LeftKey a, LeftKey b, float u) = left(t);
                    Pose pa = LeftPose(rig, a, onGun, gunPosNow, gunRotNow, leftInHolder, loaderInHand, chamberInCylinder);
                    Pose pb = LeftPose(rig, b, onGun, gunPosNow, gunRotNow, leftInHolder, loaderInHand, chamberInCylinder);
                    leftPose = new Pose(Vector3.Lerp(pa.position, pb.position, u), Quaternion.Slerp(pa.rotation, pb.rotation, u));
                    // How much the hand holds the speedloader (its fingers close around it).
                    holding = Mathf.Max(a.kind == Left.Loader || a.kind == Left.Eye ? 1f - u : 0f, b.kind == Left.Loader || b.kind == Left.Eye ? u : 0f);
                    holding *= loaderOn != null && loaderOn(t) ? 1f : 0f;
                }

                float missL = solveLeft ? Solve(rig, "_l", leftPose.position, leftPose.rotation, rig.eye.TransformPoint(poleL), ref worstTwist) : 0f;
                if (holding > 0.001f && rig.loader != null)
                {
                    CurlAround(rig, rig.loader.position, holding);
                }
                if (missL > worstLeft)
                {
                    worstLeft = missL;
                    worstLeftTime = t;
                }

                foreach (string b in rig.boneNames)
                {
                    Transform bone = rig.bones[b];
                    if (bone != null)
                    {
                        recorder.Pose(FirstPersonRig.PathOf(b), t, bone.localPosition, bone.localRotation, b.StartsWith("upperarm"));
                    }
                }

                recorder.Pose(rig.gunPath, t, gunLocalPos, gunLocalRot, true);
                if (rig.extractor != null && crane != null)
                {
                    recorder.Pose(rig.extractorPath, t, rig.extractor.localPosition, rig.extractor.localRotation, false);
                }

                if (rig.loaderPath != null)
                {
                    recorder.Active(rig.loaderPath, t, loaderOn != null && loaderOn(t));
                }
            }
        }
        finally
        {
            foreach (KeyValuePair<Transform, (Vector3, Quaternion)> kv in saved)
            {
                if (kv.Key != null)
                {
                    kv.Key.localPosition = kv.Value.Item1;
                    kv.Key.localRotation = kv.Value.Item2;
                }
            }

            rig.viewModel.localPosition = vmPosition;
            rig.viewModel.localRotation = vmRotation;
        }

        recorder.Write(target, loop, events);
        return System.IO.Path.GetFileName(target) + ": alcance pior direita " + (worstRight * 1000f).ToString("F1") + " mm (em " + rig.worstTime.ToString("F2") + " s, distância/alcance " + rig.worstReach.ToString("F2") + "), esquerda "
            + (worstLeft * 1000f).ToString("F1") + " mm (em " + worstLeftTime.ToString("F2") + " s), torção do pulso levada ao antebraço até " + worstTwist.ToString("F0") + " graus";
    }

    private static Pose ToPose((Vector3 p, Quaternion q) v)
    {
        return new Pose(v.p, v.q);
    }

    private static Vector3 Mid(Rig rig, string side)
    {
        return (rig.bones["upperarm" + side].position + rig.bones["hand" + side].position) * 0.5f;
    }

    private static Pose ReloadGunAt(float t, Pose hold)
    {
        Func<GunKey, Pose> pose = k => k.hold ? hold : new Pose(k.position, Quaternion.Euler(k.euler));
        for (int i = 0; i < ReloadGun.Length - 1; i++)
        {
            if (t <= ReloadGun[i + 1].t)
            {
                float u = Smooth(Mathf.InverseLerp(ReloadGun[i].t, ReloadGun[i + 1].t, t));
                Pose a = pose(ReloadGun[i]), b = pose(ReloadGun[i + 1]);
                return new Pose(Vector3.Lerp(a.position, b.position, u), Quaternion.Slerp(a.rotation, b.rotation, u));
            }
        }

        return pose(ReloadGun[ReloadGun.Length - 1]);
    }

    private static Pose LeftPose(Rig rig, LeftKey key, Pose onGun, Vector3 gunPos, Quaternion gunRot, Pose leftInHolder, Pose loaderInHand, Vector3[] chamberInCylinder)
    {
        switch (key.kind)
        {
            case Left.Gun:
                return new Pose(gunPos + gunRot * (leftInHolder.position + key.v), onGun.rotation);
            case Left.Eye:
                return new Pose(rig.eye.TransformPoint(key.v), onGun.rotation);
            default:
                if (rig.cylinder == null || chamberInCylinder == null || rig.loader == null)
                {
                    return onGun;
                }

                // The loader's rounds onto the chambers: rounds point down the loader's -Y... the loader turned so its
                // +Y points out of the back of the cylinder (the cylinder bone's +Y), its round 1 on the nearest chamber.
                Vector3 back = rig.cylinder.up;
                Vector3[] rears = chamberInCylinder.Select(c => rig.cylinder.TransformPoint(c + Vector3.up * 0.030f)).ToArray();
                Vector3 centre = rears.Aggregate(Vector3.zero, (s, p) => s + p) / rears.Length;
                Quaternion upright = Quaternion.FromToRotation(rig.loader.parent.rotation * Vector3.up, back) * rig.loader.parent.rotation;
                Quaternion best = upright;
                float bestError = float.MaxValue;
                for (int deg = 0; deg < 360; deg += 2)
                {
                    Quaternion q = Quaternion.AngleAxis(deg, back) * upright;
                    float error = 0f;
                    for (int r = 1; r <= 6; r++)
                    {
                        Transform round = rig.loader.Find("Round" + r);
                        Vector3 p = centre + q * Vector3.Scale(round.localPosition, rig.loader.lossyScale);
                        error += rears.Min(c => (c - p).sqrMagnitude);
                    }

                    if (error < bestError)
                    {
                        bestError = error;
                        best = q;
                    }
                }

                Vector3 loaderPos = centre + back * Mathf.Lerp(0.035f, 0.004f, key.v.x);
                Quaternion handRot = best * Quaternion.Inverse(loaderInHand.rotation);
                return new Pose(loaderPos - handRot * loaderInHand.position, handRot);
        }
    }

    // The left fingers closed around the speedloader by weight: each joint turned about its own Z (the hinge of this
    // skeleton's fingers), in the direction that brings the fingertip nearer the loader (tested once per finger).
    private static readonly float[] LoaderCurl = { 30f, 50f, 35f };

    private static void CurlAround(Rig rig, Vector3 target, float weight)
    {
        foreach (string finger in new[] { "index", "middle", "ring", "pinky", "thumb" })
        {
            Transform[] joints = new Transform[3];
            for (int j = 0; j < 3; j++)
            {
                joints[j] = rig.bones.TryGetValue(finger + "_0" + (j + 1) + "_l", out Transform b) ? b : null;
            }

            if (joints[0] == null || joints[2] == null)
            {
                continue;
            }

            Quaternion[] rest = joints.Select(b => b != null ? b.localRotation : Quaternion.identity).ToArray();
            float best = float.MaxValue;
            float bestSign = 1f;
            foreach (float sign in new[] { 1f, -1f })
            {
                for (int j = 0; j < 3; j++)
                {
                    if (joints[j] != null)
                    {
                        joints[j].localRotation = rest[j] * Quaternion.Euler(0f, 0f, sign * LoaderCurl[j] * (finger == "thumb" ? 0.5f : 1f));
                    }
                }

                float d = Vector3.Distance(joints[2].position, target);
                if (d < best)
                {
                    best = d;
                    bestSign = sign;
                }
            }

            for (int j = 0; j < 3; j++)
            {
                if (joints[j] != null)
                {
                    joints[j].localRotation = rest[j] * Quaternion.Euler(0f, 0f, bestSign * LoaderCurl[j] * weight * (finger == "thumb" ? 0.5f : 1f));
                }
            }
        }
    }

    // Two bones to the target with the elbow toward the pole, then the hand turned as asked and half of its twist
    // about the forearm handed to the forearm. Returns how far the wrist ended from the target (metres).
    private static float Solve(Rig rig, string side, Vector3 handPos, Quaternion handRot, Vector3 pole, ref float worstTwist)
    {
        Transform upper = rig.bones["upperarm" + side], lower = rig.bones["lowerarm" + side], hand = rig.bones["hand" + side];
        float reach = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
        rig.lastReach = Vector3.Distance(upper.position, handPos) / reach;
        FirstPersonTakes.SolveWithPole(upper, lower, hand, handPos, pole);
        hand.rotation = handRot;
        // Twist of the hand about the forearm's long axis, half of it moved into the forearm.
        Vector3 axis = (hand.position - lower.position).normalized;
        Quaternion relative = Quaternion.Inverse(lower.rotation) * hand.rotation * Quaternion.Inverse(rig.holdWrist[side]);
        Vector3 axisLocal = Quaternion.Inverse(lower.rotation) * axis;
        Vector3 r = new Vector3(relative.x, relative.y, relative.z);
        Vector3 proj = Vector3.Project(r, axisLocal);
        Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, relative.w);
        if (twist.w * twist.w + proj.sqrMagnitude > 1e-8f)
        {
            float mag = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            twist = new Quaternion(twist.x / mag, twist.y / mag, twist.z / mag, twist.w / mag);
            twist.ToAngleAxis(out float angle, out Vector3 _);
            if (angle > 180f)
            {
                angle = 360f - angle;
            }

            worstTwist = Mathf.Max(worstTwist, angle);
            Quaternion half = Quaternion.Slerp(Quaternion.identity, twist, 0.5f);
            Quaternion keep = hand.rotation;
            lower.rotation = lower.rotation * half;
            hand.rotation = keep;
        }

        return Vector3.Distance(hand.position, handPos);
    }

    private static Frame SightInHolder()
    {
        return new Frame { position = RearSight, rotation = Quaternion.LookRotation(FrontSight - RearSight, Vector3.up) };
    }

    private static Pose Relative(Transform parent, Transform child)
    {
        Quaternion inv = Quaternion.Inverse(parent.rotation);
        return new Pose(inv * (child.position - parent.position), inv * child.rotation);
    }

    private static Pose EyePose(Rig rig, Vector3 worldPos, Quaternion worldRot)
    {
        return new Pose(rig.eye.InverseTransformPoint(worldPos), Quaternion.Inverse(rig.eye.rotation) * worldRot);
    }

    // Where the gun's sight line is in the eye's space at the hold's first frame (for reports and tuning).
    public static string ReportHold()
    {
        Rig rig = Find();
        AnimationClip hold = AssetDatabase.LoadAssetAtPath<AnimationClip>(FirstPersonRig.RevolverHoldClip);
        Dictionary<Transform, (Vector3, Quaternion)> saved = rig.arms.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => (t.localPosition, t.localRotation));
        hold.SampleAnimation(rig.arms.gameObject, 0f);
        Frame sight = SightInHolder();
        Pose p = EyePose(rig, rig.gun.position + rig.gun.rotation * sight.position, rig.gun.rotation * sight.rotation);
        foreach (KeyValuePair<Transform, (Vector3, Quaternion)> kv in saved)
        {
            kv.Key.localPosition = kv.Value.Item1;
            kv.Key.localRotation = kv.Value.Item2;
        }

        return "mira (alça) no olho " + (p.position * 100f).ToString("F1") + " cm, giro " + p.rotation.eulerAngles.ToString("F1");
    }
}
