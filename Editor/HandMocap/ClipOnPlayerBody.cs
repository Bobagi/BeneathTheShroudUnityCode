using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

// Opening a clip of the player goes straight to the object that plays it in the Animation window (Gustavo, 09/10:
// "abrir direto no objeto que tem essa animação"). The Animation window only previews a clip on a selected Animator
// whose controller uses it, so double-clicking such a clip (or "Ver no jogador" in its right-click menu) selects, in
// the open scene, the first person arms rig (FirstPersonArms: the hands, the torch, the revolver, the takes) or the
// PlayerBody (locomotion, the cough's spine), opens the window on that clip with the preview on, and frames it in the
// Scene view. With the rig selected the body's arms follow the scrubbed clip (ArmsFromRig runs in the editor). Clips
// neither controller uses open as before.
public static class ClipOnPlayerBody
{
    [OnOpenAsset(1)]
    private static bool OnOpen(EntityId id, int line)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GetAssetPath(id));
        return clip != null && Show(clip, false);
    }

    [MenuItem("Assets/Ver no jogador (Animation)", false, 1000)]
    private static void Menu()
    {
        Show(Selection.activeObject as AnimationClip, true);
    }

    [MenuItem("Assets/Ver no jogador (Animation)", true)]
    private static bool MenuValid()
    {
        return Selection.activeObject is AnimationClip;
    }

    private static bool Show(AnimationClip clip, bool warn)
    {
        Animator animator = null;
        FirstPersonArms arms = Object.FindObjectsByType<FirstPersonArms>(FindObjectsInactive.Include)
            .FirstOrDefault(a => Uses(a.GetComponent<Animator>(), clip));
        if (arms != null)
        {
            animator = arms.GetComponent<Animator>();
        }
        else
        {
            PlayerBody body = Object.FindObjectsByType<PlayerBody>(FindObjectsInactive.Include)
                .FirstOrDefault(b => Uses(b.GetComponent<Animator>(), clip));
            animator = body != null ? body.GetComponent<Animator>() : null;
        }

        if (animator == null)
        {
            if (warn)
            {
                Debug.LogWarning("[Clip] \"" + clip.name + "\" não está no Animator dos braços nem no do corpo do jogador da cena aberta (abra a SuitTest ou a Flooded_House).");
            }

            return false;
        }

        Selection.activeGameObject = animator.gameObject;
        EditorGUIUtility.PingObject(animator.gameObject);
        AnimationWindow window = EditorWindow.GetWindow<AnimationWindow>();
        window.Show();
        window.Focus();
        window.animationClip = clip;
        window.previewing = true;
        window.time = 0f;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }

        return true;
    }

    private static bool Uses(Animator animator, AnimationClip clip)
    {
        return animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Contains(clip);
    }
}
