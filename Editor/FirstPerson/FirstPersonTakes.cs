using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Hands recorded from a first person phone video (D:\Projetos\hand-mocap: <take>_right.handmocap.json, 21 points per
// frame in the camera's space and the joint angles of the fitted hand model) baked into a fixed clip of the first
// person arms rig: Art/Animations/FirstPerson/Tomadas/<take>.anim, a state of the same name on the layer "Mãos
// gravadas". The right hand is the recording; the left plays it mirrored. The camera of the video is the rig's parent
// (the game's camera), so the clip is in the eye's space like every other first person clip.
// Arms: each wrist goes where the phone saw it (within 92% of the arm's reach) with the hand's seen turn; the arm is
// solved once here, at bake time (two bones, the elbow one forearm behind the wrist along the hand), and the result is
// keyed; nothing is solved in the game. Fingers: every joint turns only the way the joint can (base: spread about Y,
// then bend about Z from the straight finger in the palm's plane; middle and tip: Z only; thumb: the first bone swings
// to the seen direction, the other two are hinges), on the rig's own finger bones.
public static class FirstPersonTakes
{
    public const string DefaultTake = "gustavo_virar_apertar_fit_ambos";
    public const string CaptureFolder = "D:/Projetos/hand-mocap/output";

    private const float PhoneAhead = 0.10f;
    private const float Spread = 0.09f;
    private const float MaxReach = 0.92f;
    private const float SmoothHand = 2f;
    private const float SmoothFingers = 1.5f;
    private static readonly Vector2 BaseBend = new Vector2(-5f, 90f);
    private static readonly Vector2 MiddleBend = new Vector2(0f, 110f);
    private static readonly Vector2 TipBend = new Vector2(0f, 75f);
    private const float MaxSpread = 15f;
    private static readonly Vector2 ThumbMiddleBend = new Vector2(-10f, 55f);
    private static readonly Vector2 ThumbTipBend = new Vector2(-10f, 80f);
    private const float ThumbSwing = 55f;

    [System.Serializable]
    private class Capture
    {
        public float fps;
        public int frameCount;
        public int[] valid;
        public float[] camera;
        public float[] angles;
    }

    [MenuItem("Horror/Animação/Primeira pessoa: assar a tomada padrão das mãos gravadas")]
    private static void BakeMenu()
    {
        Selection.activeObject = Bake(DefaultTake);
    }

    public static string CapturePath(string take)
    {
        return CaptureFolder + "/" + take + "/" + take + "_right.handmocap.json";
    }

    public static string ClipPath(string take)
    {
        return ProjectPaths.FirstPersonTakes + "/" + take + ".anim";
    }

    public static AnimationClip Bake(string take)
    {
        string json = CapturePath(take);
        if (!File.Exists(json))
        {
            Debug.LogWarning("[FirstPerson] Falta " + json + ": rode o processar.py do hand-mocap.");
            return null;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonRig.PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[FirstPerson] Falta o rig " + FirstPersonRig.PrefabPath + " (Horror > Animação > Primeira pessoa).");
            return null;
        }

        Capture capture = JsonUtility.FromJson<Capture>(File.ReadAllText(json));
        int n = capture.frameCount;
        if (capture.camera == null || capture.camera.Length < n * 63 || capture.angles == null || capture.angles.Length < n * 20)
        {
            Debug.LogWarning("[FirstPerson] " + json + " não tem os pontos na câmera e os ângulos das juntas (capture_fit.py).");
            return null;
        }

        Transform eye = new GameObject("Eye").transform;
        eye.gameObject.hideFlags = HideFlags.HideAndDontSave;
        GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.hideFlags = HideFlags.HideAndDontSave;
        rig.transform.SetParent(eye, false);
        try
        {
            Hand right = new Hand(rig.transform, false, capture);
            Hand left = new Hand(rig.transform, true, capture);
            ClipRecorder recorder = new ClipRecorder(capture.fps);
            for (int frame = 0; frame < n; frame++)
            {
                float t = frame / capture.fps;
                right.SolveArm(eye, frame);
                left.SolveArm(eye, frame);
                right.PoseFingers(frame);
                left.PoseFingers(frame);
                right.Record(recorder, t);
                left.Record(recorder, t);
                recorder.Active(FirstPersonRig.SocketPath(FirstPersonArms.LeftSocketName) + "/Speedloader", t, false);
            }

            AnimationClip clip = recorder.Write(ClipPath(take), true);
            FirstPersonRig.BuildController();
            Debug.Log("[FirstPerson] " + ClipPath(take) + ": " + n + " quadros a " + capture.fps.ToString("F1") + " fps, estado \"" + take + "\" na camada \"" + FirstPersonArms.TakesLayer + "\".");
            return clip;
        }
        finally
        {
            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(eye.gameObject);
        }
    }

    // One arm of the rig: the capture turned into this hand's targets (mirrored for the left), the finger joints' own
    // axes measured on the bones in the bind pose, and the pose of every frame.
    private sealed class Hand
    {
        private readonly bool left;
        private readonly Transform upper, lower, hand;
        private readonly int frames;
        private readonly Vector3[] wrist;
        private readonly Quaternion[] turn;
        private readonly Vector3[] thumbDirection;
        private readonly float[][] angles;
        private readonly Quaternion palmInHand;
        private readonly Vector3 fingersInHand;
        private readonly Transform[][] bones = new Transform[5][];
        private readonly Quaternion[] straightBase = new Quaternion[5];
        private readonly Quaternion thumbBind;
        private readonly float[] bendSign = new float[5];
        private readonly float[] spreadSign = new float[5];

        public Hand(Transform rig, bool left, Capture capture)
        {
            this.left = left;
            frames = capture.frameCount;
            string side = left ? "_l" : "_r";
            upper = rig.Find(FirstPersonRig.PathOf("upperarm" + side));
            lower = rig.Find(FirstPersonRig.PathOf("lowerarm" + side));
            hand = rig.Find(FirstPersonRig.PathOf("hand" + side));
            for (int f = 0; f < 5; f++)
            {
                bones[f] = new[] { HandBones.Find(hand, left, f, 0), HandBones.Find(hand, left, f, 1), HandBones.Find(hand, left, f, 2) };
            }

            Quaternion palm = Palm(hand.position, bones[1][0].position, bones[2][0].position, bones[4][0].position);
            palmInHand = Quaternion.Inverse(hand.rotation) * palm;
            fingersInHand = hand.InverseTransformDirection(bones[2][0].position - hand.position).normalized;
            thumbBind = bones[0][0].localRotation;
            MeasureJoints(palm * Vector3.up);

            // The capture: OpenCV camera axes (y down) to Unity's (y up); the left hand is the same motion mirrored.
            wrist = new Vector3[frames];
            turn = new Quaternion[frames];
            thumbDirection = new Vector3[frames];
            Vector3[] p = new Vector3[21];
            int last = -1;
            for (int frame = 0; frame < frames; frame++)
            {
                if (capture.valid == null || capture.valid[frame] != 0)
                {
                    for (int k = 0; k < 21; k++)
                    {
                        int i = (frame * 21 + k) * 3;
                        float x = capture.camera[i];
                        p[k] = new Vector3(left ? -x : x, -capture.camera[i + 1], capture.camera[i + 2]);
                    }

                    Quaternion seen = Palm(p[0], p[5], p[9], p[17]);
                    wrist[frame] = p[0];
                    turn[frame] = seen * Quaternion.Inverse(palmInHand);
                    thumbDirection[frame] = Quaternion.Inverse(seen) * (p[2] - p[1]).normalized;
                    last = frame;
                }
                else if (last >= 0)
                {
                    wrist[frame] = wrist[last];
                    turn[frame] = turn[last];
                    thumbDirection[frame] = thumbDirection[last];
                }
            }

            int first = System.Array.FindIndex(turn, q => q.w != 0f || q.x != 0f || q.y != 0f || q.z != 0f);
            for (int frame = 0; frame < first; frame++)
            {
                wrist[frame] = wrist[first];
                turn[frame] = turn[first];
                thumbDirection[frame] = thumbDirection[first];
            }

            wrist = Smooth(wrist, SmoothHand, false);
            thumbDirection = Smooth(thumbDirection, SmoothHand, true);
            turn = Smooth(turn, SmoothHand);

            angles = new float[20][];
            for (int a = 0; a < 20; a++)
            {
                float[] raw = new float[frames];
                for (int frame = 0; frame < frames; frame++)
                {
                    raw[frame] = capture.angles[frame * 20 + a];
                }

                angles[a] = Smooth(raw, SmoothFingers);
            }
        }

        private void MeasureJoints(Vector3 palmNormal)
        {
            Vector3 thumbSide = (bones[1][0].position - bones[4][0].position).normalized;
            for (int f = 0; f < 5; f++)
            {
                float curl = Mathf.DeltaAngle(0f, bones[f][1].localEulerAngles.z) + Mathf.DeltaAngle(0f, bones[f][2].localEulerAngles.z);
                bendSign[f] = curl > 0f ? 1f : -1f;
                if (f == 0)
                {
                    continue;
                }

                Transform b = bones[f][0];
                Quaternion bind = b.localRotation;
                Vector3 axis = bones[f][1].localPosition.normalized;
                Vector3 line = Vector3.ProjectOnPlane(b.position - hand.position, palmNormal).normalized;
                float flat = 0f, fan = 0f;
                for (int pass = 0; pass < 3; pass++)
                {
                    flat = Best(-60f, 60f, a => Mathf.Abs(Vector3.Dot(b.parent.rotation * (bind * Quaternion.Euler(0f, fan, a)) * axis, palmNormal)));
                    fan = Best(-40f, 40f, a => Vector3.Angle(Vector3.ProjectOnPlane(b.parent.rotation * (bind * Quaternion.Euler(0f, a, flat)) * axis, palmNormal), line));
                }

                straightBase[f] = bind * Quaternion.Euler(0f, fan, flat);
                Vector3 before = b.parent.rotation * straightBase[f] * axis;
                Vector3 after = b.parent.rotation * (straightBase[f] * Quaternion.Euler(0f, 1f, 0f)) * axis;
                spreadSign[f] = Vector3.Dot(after - before, thumbSide) > 0f ? 1f : -1f;
            }
        }

        private static float Best(float from, float to, System.Func<float, float> cost)
        {
            float best = from, lowest = float.MaxValue;
            for (float a = from; a <= to; a += 0.25f)
            {
                float c = cost(a);
                if (c < lowest)
                {
                    lowest = c;
                    best = a;
                }
            }

            return best;
        }

        public void SolveArm(Transform eye, int frame)
        {
            float forearm = Vector3.Distance(lower.position, hand.position);
            Vector3 place = wrist[frame] + Vector3.forward * PhoneAhead;
            place.x += left ? -Spread : Spread;
            Vector3 target = eye.TransformPoint(place);
            float reach = Vector3.Distance(upper.position, lower.position) + forearm;
            Vector3 fromShoulder = target - upper.position;
            if (fromShoulder.magnitude > reach * MaxReach)
            {
                target = upper.position + fromShoulder.normalized * reach * MaxReach;
            }

            Quaternion handTurn = eye.rotation * turn[frame];
            Vector3 pole = target - (handTurn * fingersInHand) * forearm - eye.up * 0.05f;
            SolveWithPole(upper, lower, hand, target, pole);
            hand.rotation = handTurn;
        }

        public void PoseFingers(int frame)
        {
            for (int f = 1; f < 5; f++)
            {
                float bend = Mathf.Clamp(angles[f * 4 + 1][frame], BaseBend.x, BaseBend.y);
                float middle = Mathf.Clamp(angles[f * 4 + 2][frame], MiddleBend.x, MiddleBend.y);
                float tip = Mathf.Clamp(angles[f * 4 + 3][frame], TipBend.x, TipBend.y);
                float spread = Mathf.Clamp(angles[f * 4][frame], -MaxSpread, MaxSpread);
                bones[f][0].localRotation = straightBase[f] * Quaternion.Euler(0f, spreadSign[f] * spread, bendSign[f] * bend);
                bones[f][1].localRotation = Quaternion.Euler(0f, 0f, bendSign[f] * middle);
                bones[f][2].localRotation = Quaternion.Euler(0f, 0f, bendSign[f] * tip);
            }

            Transform first = bones[0][0];
            first.localRotation = thumbBind;
            Vector3 current = bones[0][1].position - first.position;
            Vector3 wanted = hand.rotation * palmInHand * thumbDirection[frame];
            Quaternion swing = Quaternion.FromToRotation(current, wanted);
            swing = Quaternion.RotateTowards(Quaternion.identity, swing, ThumbSwing);
            first.localRotation = Quaternion.Inverse(first.parent.rotation) * swing * first.rotation;
            bones[0][1].localRotation = Quaternion.Euler(0f, 0f, bendSign[0] * Mathf.Clamp(angles[2][frame], ThumbMiddleBend.x, ThumbMiddleBend.y));
            bones[0][2].localRotation = Quaternion.Euler(0f, 0f, bendSign[0] * Mathf.Clamp(angles[3][frame], ThumbTipBend.x, ThumbTipBend.y));
        }

        public void Record(ClipRecorder recorder, float t)
        {
            string side = left ? "_l" : "_r";
            recorder.Pose(FirstPersonRig.PathOf("upperarm" + side), t, upper.localPosition, upper.localRotation, true);
            recorder.Pose(FirstPersonRig.PathOf("lowerarm" + side), t, lower.localPosition, lower.localRotation, false);
            recorder.Pose(FirstPersonRig.PathOf("hand" + side), t, hand.localPosition, hand.localRotation, false);
            for (int f = 0; f < 5; f++)
            {
                for (int s = 0; s < 3; s++)
                {
                    Transform b = bones[f][s];
                    recorder.Pose(FirstPersonRig.PathOf(b.name), t, b.localPosition, b.localRotation, false);
                }
            }
        }
    }

    private static float[] Kernel(float sigma)
    {
        int half = Mathf.CeilToInt(sigma * 3f);
        float[] kernel = new float[half * 2 + 1];
        for (int i = -half; i <= half; i++)
        {
            kernel[i + half] = Mathf.Exp(-0.5f * i * i / (sigma * sigma));
        }

        return kernel;
    }

    private static float[] Smooth(float[] values, float sigma)
    {
        float[] kernel = Kernel(sigma);
        int half = kernel.Length / 2, n = values.Length;
        float[] result = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f, weight = 0f;
            for (int k = -half; k <= half; k++)
            {
                float w = kernel[k + half];
                sum += values[Mathf.Clamp(i + k, 0, n - 1)] * w;
                weight += w;
            }

            result[i] = sum / weight;
        }

        return result;
    }

    private static Vector3[] Smooth(Vector3[] values, float sigma, bool unit)
    {
        float[] kernel = Kernel(sigma);
        int half = kernel.Length / 2, n = values.Length;
        Vector3[] result = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 sum = Vector3.zero;
            float weight = 0f;
            for (int k = -half; k <= half; k++)
            {
                float w = kernel[k + half];
                sum += values[Mathf.Clamp(i + k, 0, n - 1)] * w;
                weight += w;
            }

            result[i] = unit ? sum.normalized : sum / weight;
        }

        return result;
    }

    private static Quaternion[] Smooth(Quaternion[] values, float sigma)
    {
        float[] kernel = Kernel(sigma);
        int half = kernel.Length / 2, n = values.Length;
        Quaternion[] result = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            Vector4 sum = Vector4.zero;
            for (int k = -half; k <= half; k++)
            {
                Quaternion q = values[Mathf.Clamp(i + k, 0, n - 1)];
                float w = kernel[k + half] * (Quaternion.Dot(q, values[i]) < 0f ? -1f : 1f);
                sum += new Vector4(q.x, q.y, q.z, q.w) * w;
            }

            sum.Normalize();
            result[i] = new Quaternion(sum.x, sum.y, sum.z, sum.w);
        }

        return result;
    }

    // Palm frame: forward along the middle finger's knuckle, up out of the back of the hand for a right hand.
    private static Quaternion Palm(Vector3 wrist, Vector3 index, Vector3 middle, Vector3 little)
    {
        Vector3 forward = (middle - wrist).normalized;
        Vector3 up = Vector3.Cross(forward, index - little).normalized;
        return Quaternion.LookRotation(forward, up);
    }

    // Two bones with the elbow in the plane of the pole.
    public static void SolveWithPole(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 pole)
    {
        Vector3 shoulder = upper.position;
        float a = Vector3.Distance(shoulder, lower.position);
        float b = Vector3.Distance(lower.position, hand.position);
        Vector3 toTarget = target - shoulder;
        float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
        Vector3 dir = toTarget.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(pole - shoulder, dir);
        if (bend.sqrMagnitude < 1e-8f)
        {
            bend = Vector3.ProjectOnPlane(Vector3.down, dir);
        }

        bend.Normalize();
        float along = (a * a - b * b + d * d) / (2f * d);
        float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        Vector3 elbow = shoulder + dir * along + bend * height;
        upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, shoulder + dir * d - lower.position) * lower.rotation;
    }
}
