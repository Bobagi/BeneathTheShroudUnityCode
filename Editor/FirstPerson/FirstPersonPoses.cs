using System.Linq;
using UnityEditor;
using UnityEngine;

// Makes pose clips of the first person rig from an existing one, moving what the hands hold: the hands keep their
// grip (their pose relative to the weapon socket), the gun goes to a new place in the eye's space, and each arm is
// solved ONCE here (two bones, elbow down and out) and written as keys. Nothing is solved in the game. The result is
// an ordinary 2-key clip, edited afterwards in the Animation window like any other; running this again overwrites it.
public static class FirstPersonPoses
{
    // The gun at a run, in the eye's space (metres: right, up, ahead) and its turn (degrees: muzzle down, to the left,
    // rolled): low in the middle of the screen so the arm is seen pumping with the steps, the way most shooters carry
    // a pistol while sprinting (Gustavo, 10/10, from references).
    // At 0.30 m ahead the screen's bottom edge is 0.20 m below the eye (68 degree lens): the gun sits a third of the way
    // up so the hand and the gun read while pumping.
    public static readonly Vector3 SprintGunPosition = new Vector3(0.03f, -0.12f, 0.30f);
    public static readonly Vector3 SprintGunEuler = new Vector3(35f, -30f, 20f);

    [MenuItem("Horror/Animação/Primeira pessoa: criar a pose de corrida do revólver (sobrescreve)")]
    public static void MakeRevolverSprint()
    {
        AnimationClip clip = FromHold(FirstPersonRig.RevolverHoldClip, FirstPersonRig.RevolverSprintClip, SprintGunPosition, Quaternion.Euler(SprintGunEuler));
        FirstPersonRig.BuildController();
        Selection.activeObject = clip;
    }

    public static AnimationClip FromHold(string holdPath, string outPath, Vector3 gunPosition, Quaternion gunRotation)
    {
        AnimationClip hold = AssetDatabase.LoadAssetAtPath<AnimationClip>(holdPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonRig.PrefabPath);
        if (hold == null || prefab == null)
        {
            Debug.LogError("[FirstPerson] missing " + holdPath + " or " + FirstPersonRig.PrefabPath);
            return null;
        }

        Transform eye = new GameObject("Eye").transform;
        GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.transform.SetParent(eye, false);
        try
        {
            hold.SampleAnimation(rig, 0f);
            Transform socket = rig.transform.Find(FirstPersonRig.SocketPath(FirstPersonArms.WeaponSocketName));
            Transform[] hands = { rig.transform.Find(FirstPersonRig.PathOf("hand_r")), rig.transform.Find(FirstPersonRig.PathOf("hand_l")) };
            Vector3[] inSocket = hands.Select(h => socket.InverseTransformPoint(h.position)).ToArray();
            Quaternion[] turnInSocket = hands.Select(h => Quaternion.Inverse(socket.rotation) * h.rotation).ToArray();

            Vector3 newPosition = eye.TransformPoint(gunPosition);
            Quaternion newRotation = eye.rotation * gunRotation;
            for (int i = 0; i < 2; i++)
            {
                bool left = i == 1;
                string side = left ? "_l" : "_r";
                Transform upper = rig.transform.Find(FirstPersonRig.PathOf("upperarm" + side));
                Transform lower = rig.transform.Find(FirstPersonRig.PathOf("lowerarm" + side));
                Vector3 target = newPosition + newRotation * Vector3.Scale(inSocket[i], socket.lossyScale);
                Vector3 pole = upper.position - eye.up * 0.4f + eye.right * (left ? -0.3f : 0.3f) + eye.forward * 0.05f;
                FirstPersonTakes.SolveWithPole(upper, lower, hands[i], target, pole);
                hands[i].rotation = newRotation * turnInSocket[i];
                Debug.Log("[FirstPerson] " + hands[i].name + " placed " + (Vector3.Distance(hands[i].position, target) * 100f).ToString("F2") + " cm from its target; in the eye's space " + eye.InverseTransformPoint(hands[i].position).ToString("F3"));
            }

            ClipRecorder recorder = new ClipRecorder(30f) { AngleTolerance = 0f, DistanceTolerance = 0f };
            foreach (float t in new[] { 0f, 1f })
            {
                foreach (bool left in new[] { false, true })
                {
                    foreach (string name in FirstPersonRig.BoneNames(left))
                    {
                        if (name.StartsWith("clavicle"))
                        {
                            continue;
                        }

                        Transform bone = rig.transform.Find(FirstPersonRig.PathOf(name));
                        recorder.Pose(FirstPersonRig.PathOf(name), t, bone.localPosition, bone.localRotation, name.StartsWith("upperarm"));
                    }
                }
            }

            AnimationClip clip = recorder.Write(outPath, true);
            // What hangs on the sockets (the crane closed, the speedloader hidden): as the hold has them.
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(hold))
            {
                if (binding.path.Contains("/" + FirstPersonArms.WeaponSocketName) || binding.path.Contains("/" + FirstPersonArms.LeftSocketName))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(hold, binding));
                }
            }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Debug.Log("[FirstPerson] wrote " + outPath + " from " + holdPath);
            return clip;
        }
        finally
        {
            Object.DestroyImmediate(eye.gameObject);
        }
    }
}
