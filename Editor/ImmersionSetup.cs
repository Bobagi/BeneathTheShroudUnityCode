using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts Immersion on the player's camera (the player builder calls Add; the menu does it in the open scene) with the
// photographed blood (Quixel, made brighter for the screen: Art/VFX/Textures/BloodScreen) for the blood thrown on the screen by a hard blow.
public static class ImmersionSetup
{
    [MenuItem("Horror/Imersão/Ligar efeitos de imersão na cena aberta")]
    public static void ApplyToOpenScene()
    {
        foreach (FirstPersonController controller in Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Include))
        {
            Add(controller.CameraPivot.gameObject, controller, controller.CameraPivot.GetComponent<UnderwaterView>());
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static Immersion Add(GameObject camera, FirstPersonController controller, UnderwaterView view)
    {
        Immersion immersion = camera.GetComponent<Immersion>() ?? camera.AddComponent<Immersion>();
        SerializedObject so = new SerializedObject(immersion);
        so.FindProperty("controller").objectReferenceValue = controller;
        so.FindProperty("view").objectReferenceValue = view;
        SerializedProperty blood = so.FindProperty("bloodScreen");
        // Brighter, fuller-red versions of the Quixel spatter and stain made for the screen (quixel_blood.py).
        string[] names = { "BloodScreen_01", "BloodScreen_02" };
        blood.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            string path = ProjectPaths.Vfx + "/Textures/BloodScreen/" + names[i] + ".png";
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            blood.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(immersion);
        return immersion;
    }
}
