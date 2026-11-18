using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Puts the "LensRain" full screen pass (Art/Shaders/LensRain.shader) on every URP renderer, after post-processing like
// the gas mask's. It draws nothing unless a camera with LensRain.cs is rendering (the main menu's).
public static class LensRainSetup
{
    public static void Ensure()
    {
        string path = ProjectPaths.Shaders + "/LensRain.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Horror/LensRain"));
            AssetDatabase.CreateAsset(material, path);
        }

        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            if (QualitySettings.GetRenderPipelineAssetAt(level) is not UniversalRenderPipelineAsset pipeline)
            {
                continue;
            }

            SerializedProperty list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData renderer)
                {
                    Add(renderer, material);
                }
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static void Add(UniversalRendererData renderer, Material material)
    {
        FullScreenPassRendererFeature pass = (FullScreenPassRendererFeature)renderer.rendererFeatures.Find(f => f is FullScreenPassRendererFeature && f.name == "LensRain");
        if (pass == null)
        {
            pass = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            pass.name = "LensRain";
            AssetDatabase.AddObjectToAsset(pass, renderer);
            renderer.rendererFeatures.Add(pass);
            SerializedObject data = new SerializedObject(renderer);
            SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pass, out string _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        pass.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        pass.fetchColorBuffer = true;
        pass.requirements = ScriptableRenderPassInput.Color;
        pass.passMaterial = material;
        pass.passIndex = 0;
        pass.SetActive(true);
        EditorUtility.SetDirty(pass);
        EditorUtility.SetDirty(renderer);
    }
}
