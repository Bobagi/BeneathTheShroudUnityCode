using UnityEditor;

// Work light for dark scenes: toggles, in every Scene view at once, the editor's own flat lighting
// (the same as the light-bulb button in the Scene view toolbar), fog and post-processing. Only the
// editor view changes; the game's lights, fog and volumes stay exactly as they are.
public static class SceneViewWorkLight
{
    private const string MenuPath = "Horror/Scene View/Luz de trabalho %#l";

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        bool on = !IsOn();
        foreach (SceneView view in SceneView.sceneViews)
        {
            view.sceneLighting = !on;
            view.sceneViewState.showFog = !on;
            view.sceneViewState.showImageEffects = !on;
            view.Repaint();
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        Menu.SetChecked(MenuPath, IsOn());
        return SceneView.sceneViews.Count > 0;
    }

    private static bool IsOn()
    {
        return SceneView.lastActiveSceneView != null && !SceneView.lastActiveSceneView.sceneLighting;
    }
}
