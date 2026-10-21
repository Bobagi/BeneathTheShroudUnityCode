using UnityEditor;

// The hand mocap importer and the animation correction editor live in the package com.bobagi.handmocap
// (D:\Projetos\hand-mocap\unity, repo Bobagi/hand-mocap), so fixes made while using them here improve the tools for
// other projects too. This points the package at this project's folders and model, and keeps the editor where it
// was in the Horror menu (the package's own entries are under Tools > Animação).
[InitializeOnLoad]
public static class HandMocapProjectSetup
{
    static HandMocapProjectSetup()
    {
        HandMocapSettings.OwnRoot = ProjectPaths.Root;
        HandMocapSettings.EditedFolder = ProjectPaths.Art + "/Animations/Editadas";
        HandMocapSettings.HandAnimations = ProjectPaths.FirstPersonTakes;
        HandMocapSettings.BodyAnimations = ProjectPaths.BodyAnimations;
        HandMocapSettings.HumanoidModel = ProjectPaths.CharacterModels + "/PlayerBody/PlayerBody.fbx";
        HandMocapSettings.CaptureFolder = "D:/Projetos/hand-mocap/output";
    }

    [MenuItem("Horror/Animação/Editor de correções")]
    private static void OpenCorrectionEditor()
    {
        AnimationCorrectionWindow.Open();
    }
}
