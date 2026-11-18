using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The six hand clips of the demo's finale (GDD "O fim da demo"; DemoFinale plays them by name when they exist):
// Hide_Crouch, Revolver_OpenSlow, Revolver_LoadOne, Revolver_LoadFast, Revolver_CloseSlow, Revolver_RiseAim, in
// Art/Animations/FirstPerson/Final. Gustavo animates them in Blender (17/10: "the simplest way to edit"), so this
// makes each one that is missing as a copy of the Revolver_Hold pose with the clip's own length, which
// FirstPersonBlender then turns into an Action of the right length in the .blend; the import brings the edited
// Action back into the same .anim and remakes the Animator (states with the same names).
public static class FirstPersonFinalClips
{
    // Name, seconds. The timing of the scene itself is DemoFinale's and GameTuning's: these are the poses' lengths.
    public static readonly (string name, float seconds)[] Clips =
    {
        ("Hide_Crouch", 1.4f),
        ("Revolver_OpenSlow", 1.2f),
        ("Revolver_LoadOne", 1.0f),
        ("Revolver_LoadFast", 0.7f),
        ("Revolver_CloseSlow", 0.5f),
        ("Revolver_RiseAim", 0.4f),
    };

    public static string PathOf(string name) => FirstPersonRig.FinalFolder + "/" + name + ".anim";

    [MenuItem("Horror/Animação/Final da demo: preparar os seis clipes e abrir no Blender")]
    public static void PrepareAndOpen()
    {
        int made = EnsurePlaceholders();
        FirstPersonRig.BuildController();
        // The .blend only knows the clips it was built from: a new clip means a rebuild (the current file is kept in
        // backup/ first, so nothing edited is lost).
        bool rebuild = made > 0 || !File.Exists(FirstPersonBlender.BlendPath) || !FirstPersonBlender.BlendHasActions(Clips.Select(c => c.name));
        if (rebuild)
        {
            FirstPersonBlender.BackupBlend("antes_dos_clipes_do_final");
            if (!FirstPersonBlender.RebuildBlend())
            {
                return;
            }
        }

        FirstPersonBlender.OpenInBlender();
        Debug.Log("[FirstPerson] Final da demo: " + made + " clipe(s) novo(s), .blend " + (rebuild ? "refeito" : "mantido") + ". No Blender: escolha a Action (Hide_Crouch, Revolver_OpenSlow...), mova a caixa amarela (arma) e os anéis azuis (mãos), grave, e volte com \"importar os clipes do Blender\". Guia: Docs/Animacao/FINAL_NO_BLENDER.md");
    }

    // Makes the missing clips as copies of the hold pose at their length. Returns how many were made.
    public static int EnsurePlaceholders()
    {
        Directory.CreateDirectory(FirstPersonRig.FinalFolder);
        AnimationClip hold = AssetDatabase.LoadAssetAtPath<AnimationClip>(FirstPersonRig.RevolverHoldClip);
        if (hold == null)
        {
            Debug.LogError("[FirstPerson] falta " + FirstPersonRig.RevolverHoldClip);
            return 0;
        }

        int made = 0;
        foreach ((string name, float seconds) in Clips)
        {
            string path = PathOf(name);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            {
                continue;
            }

            AnimationClip clip = new AnimationClip { frameRate = hold.frameRate, name = name };
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(hold))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(hold, binding);
                if (curve == null || curve.length == 0)
                {
                    continue;
                }

                // The pose held from 0 to the clip's length: the first key's value at both ends.
                float value = curve.keys[0].value;
                AnimationCurve held = new AnimationCurve(new Keyframe(0f, value), new Keyframe(seconds, value));
                AnimationUtility.SetEditorCurve(clip, binding, held);
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(hold);
            settings.loopTime = false;
            settings.startTime = 0f;
            settings.stopTime = seconds;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            AssetDatabase.CreateAsset(clip, path);
            made++;
        }

        if (made > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[FirstPerson] " + made + " clipe(s) do final criados em " + FirstPersonRig.FinalFolder + " (pose da empunhadura, no comprimento de cada um)");
        }

        return made;
    }
}
