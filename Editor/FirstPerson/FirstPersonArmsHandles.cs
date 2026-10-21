using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Editing the first person rig in the Scene view (Gustavo, 10/10: "não apareceu os ossos para eu ajeitar"). While the
// selection is inside a FirstPersonArms hierarchy, every bone is drawn as a clickable joint (a click selects it, so it
// can be rotated with the usual tools and keyed in the Animation window) and the player's body cannot be picked: a
// click on the hand's skin used to select the body's mesh, the Animation window followed that selection, the rig's
// preview stopped and the gun went back to its rest place ("o revólver sai"). Picking comes back when the selection
// leaves the rig.
[InitializeOnLoad]
public static class FirstPersonArmsHandles
{
    private static GameObject pickingDisabledOn;

    static FirstPersonArmsHandles()
    {
        SceneView.duringSceneGui += OnSceneGui;
        Selection.selectionChanged += OnSelectionChanged;
    }

    private static FirstPersonArms RigOf(Transform t)
    {
        while (t != null)
        {
            FirstPersonArms arms = t.GetComponent<FirstPersonArms>();
            if (arms != null)
            {
                return arms;
            }

            t = t.parent;
        }

        return null;
    }

    private static void OnSelectionChanged()
    {
        FirstPersonArms rig = Selection.activeTransform != null ? RigOf(Selection.activeTransform) : null;
        PlayerBody body = rig != null ? Object.FindAnyObjectByType<PlayerBody>(FindObjectsInactive.Include) : null;
        GameObject wanted = body != null ? body.gameObject : null;
        if (wanted == pickingDisabledOn)
        {
            return;
        }

        if (pickingDisabledOn != null)
        {
            SceneVisibilityManager.instance.EnablePicking(pickingDisabledOn, true);
        }

        pickingDisabledOn = wanted;
        if (wanted != null)
        {
            SceneVisibilityManager.instance.DisablePicking(wanted, true);
        }
    }

    private static void OnSceneGui(SceneView view)
    {
        FirstPersonArms rig = Selection.activeTransform != null ? RigOf(Selection.activeTransform) : null;
        if (rig == null)
        {
            return;
        }

        Transform selected = Selection.activeTransform;
        HashSet<string> names = new HashSet<string>();
        foreach (string n in FirstPersonRig.BoneNames(false))
        {
            names.Add(n);
        }

        foreach (string n in FirstPersonRig.BoneNames(true))
        {
            names.Add(n);
        }

        foreach (Transform bone in rig.GetComponentsInChildren<Transform>(true))
        {
            if (!names.Contains(bone.name))
            {
                continue;
            }

            bool finger = !bone.name.StartsWith("clavicle") && !bone.name.Contains("arm") && !bone.name.StartsWith("hand");
            float size = finger ? 0.007f : 0.014f;
            if (bone.parent != null && names.Contains(bone.parent.name))
            {
                Handles.color = new Color(1f, 0.75f, 0.2f, 0.9f);
                Handles.DrawAAPolyLine(finger ? 2f : 4f, bone.parent.position, bone.position);
            }

            Handles.color = bone == selected ? new Color(0.3f, 1f, 0.4f, 1f) : new Color(1f, 0.6f, 0.1f, 0.9f);
            if (Handles.Button(bone.position, Quaternion.identity, size, size * 1.4f, Handles.SphereHandleCap))
            {
                Selection.activeGameObject = bone.gameObject;
            }
        }

        Handles.BeginGUI();
        GUI.Label(new Rect(10f, 10f, 520f, 40f),
            "Rig dos braços: clique num osso (esfera) para selecionar; E gira. O corpo do jogador não é selecionável enquanto o rig estiver selecionado.",
            EditorStyles.helpBox);
        Handles.EndGUI();
    }
}
