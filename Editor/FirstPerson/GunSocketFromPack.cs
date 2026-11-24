using System.Linq;
using UnityEditor;
using UnityEngine;

// Takes the weapon placement from an animation pack that was captured with a real pistol in the hand: the pack's
// skeleton carries a weapon bone (hand_r_wep on MotusMan, MoCap Online) where its pistol sits, so the relation hand to
// gun is measured, not guessed. The pack's clip is sampled twice: on its own Generic skeleton (for the weapon bone)
// and, through Humanoid retargeting, on the PlayerBody (for this MetaHuman's hand, which ends with the same world
// orientation as the pack's hand). The gun's root then goes on the weapon socket at the same place relative to the
// hand, shifted by where this revolver's origin sits on its grip compared with the pack's pistol.
public static class GunSocketFromPack
{
    public const string PackFolder = "Assets/_Project/Art/Animations/Packs/MocapOnline_Pistol/";
    public const string GenericFbx = PackFolder + "W1_Stand_Aim_Idle_IPC_generic.fbx";
    public const string HumanoidFbx = PackFolder + "W1_Stand_Aim_Idle_IPC.fbx";
    public const string PackHand = "RightHand";
    public const string PackWeaponBone = "hand_r_wep";

    // Where this revolver's origin (the Python's: the palm point, at the trigger guard's height, 1.15 cm ahead of the
    // grip's centre, bore +Z, up +Y) sits in the pack pistol's frame (the 1911 on hand_r_wep: bore +Z, up +Y, trigger
    // guard bottom at y -1, grip centre at z -1.5 there), in centimetres.
    public static readonly Vector3 RevolverOriginInPackPistol = new Vector3(0f, -1.0f, -0.35f);

    [MenuItem("Horror/Combate/Socket do revólver pela pegada do pack (MoCap Online)")]
    public static void ApplyMenu()
    {
        Apply(0.5f, true);
    }

    public static string Apply(float time, bool write)
    {
        GameObject genericPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GenericFbx);
        AnimationClip genericClip = AssetDatabase.LoadAllAssetsAtPath(GenericFbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
        AnimationClip humanClip = AssetDatabase.LoadAllAssetsAtPath(HumanoidFbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
        GameObject body = GameObject.Find(PackArmsToRig.BodyName);
        Animator bodyAnimator = body != null ? body.GetComponent<Animator>() : null;
        if (genericPrefab == null || genericClip == null || humanClip == null || bodyAnimator == null || !bodyAnimator.isHuman)
        {
            Debug.LogError("[GunSocket] faltam os FBX do pack (Generic e Humanoid) ou o PlayerBody Humanoid na cena.");
            return null;
        }

        // 1. The pack's own skeleton posed by its clip: the hand and the weapon bone in world space.
        GameObject pack = (GameObject)PrefabUtility.InstantiatePrefab(genericPrefab);
        pack.hideFlags = HideFlags.HideAndDontSave;
        Vector3 packHandPos;
        Quaternion packHandRot;
        Vector3 wepPos;
        Quaternion wepRot;
        try
        {
            pack.transform.position = body.transform.position;
            pack.transform.rotation = body.transform.rotation;
            genericClip.SampleAnimation(pack, time);
            Transform[] all = pack.GetComponentsInChildren<Transform>(true);
            Transform packHand = all.FirstOrDefault(t => t.name == PackHand);
            Transform wep = all.FirstOrDefault(t => t.name == PackWeaponBone);
            if (packHand == null || wep == null)
            {
                Debug.LogError("[GunSocket] o pack não tem " + PackHand + "/" + PackWeaponBone);
                return null;
            }

            packHandPos = packHand.position;
            packHandRot = packHand.rotation;
            wepPos = wep.position;
            wepRot = wep.rotation;
        }
        finally
        {
            Object.DestroyImmediate(pack);
        }

        // 2. This body posed by the same clip through Humanoid retargeting: its hand in world space.
        Transform ourHand = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "hand_r");
        Vector3 ourHandPos;
        Quaternion ourHandRot;
        Vector3 ourHandScale;
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(body, humanClip, time);
            AnimationMode.EndSampling();
            ourHandPos = ourHand.position;
            ourHandRot = ourHand.rotation;
            ourHandScale = ourHand.lossyScale;
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        // 3. The revolver's root in world space as if held by the pack's hand, then relative to our hand: the same
        // offset from the hand joint, in our hand's frame (whose units are centimetres: the bone scale is 0.01).
        Vector3 gunWorld = wepPos + wepRot * (RevolverOriginInPackPistol * 0.01f);
        Vector3 offsetWorld = gunWorld - packHandPos;
        Vector3 local = Quaternion.Inverse(ourHandRot) * offsetWorld;
        local = new Vector3(local.x / ourHandScale.x, local.y / ourHandScale.y, local.z / ourHandScale.z);
        Quaternion localRot = Quaternion.Inverse(ourHandRot) * wepRot;
        float handTurn = Quaternion.Angle(packHandRot, ourHandRot);
        string report = "[GunSocket] socket do revólver pela pegada do pack: pos " + local.ToString("F2") + " cm, rot " + localRot.eulerAngles.ToString("F1") +
                        " (mão do pack e nossa mão com " + handTurn.ToString("F0") + " graus de diferença de orientação no mundo, informativo: os eixos dos ossos diferem)";
        Debug.Log(report);
        if (write)
        {
            FirstPersonRig.SetSocket(FirstPersonArms.WeaponSocketName, local, localRot);
            FirstPersonArms arms = Object.FindAnyObjectByType<FirstPersonArms>(FindObjectsInactive.Include);
            Transform socket = arms != null ? arms.transform.Find(FirstPersonRig.SocketPath(FirstPersonArms.WeaponSocketName)) : null;
            if (socket != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(socket))
                {
                    PrefabUtility.RevertObjectOverride(socket, InteractionMode.AutomatedAction);
                }

                socket.localPosition = local;
                socket.localRotation = localRot;
            }

            Debug.Log("[GunSocket] socket gravado no prefab e na cena.");
        }

        return report;
    }
}
