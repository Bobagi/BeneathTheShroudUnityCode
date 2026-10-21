using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// HUD assets: the period typewriter font as a TMP font asset and hotbar icons rendered from the
// item prefabs with a temporary camera (transparent background).
public static class HudAssetBuilder
{
    private const string FontPath = ProjectPaths.Fonts + "/SpecialElite-Regular.ttf";
    private const string FontAssetPath = ProjectPaths.Fonts + "/SpecialElite SDF.asset";
    private const string IconFolder = ProjectPaths.Icons;

    [MenuItem("Horror/Prototype/Build HUD Assets")]
    public static void BuildAll()
    {
        TMP_FontAsset font = BuildFont();
        RenderIcon(ProjectPaths.ItemPrefabs + "/Lantern.prefab", ProjectPaths.ItemPrefabs + "/LanternDefinition.asset", new Vector3(20f, -35f, 0f));
        RenderIcon(ProjectPaths.ItemPrefabs + "/Revolver.prefab", ProjectPaths.ItemPrefabs + "/RevolverDefinition.asset", new Vector3(10f, -90f, 0f));

        InventoryHud hud = Object.FindAnyObjectByType<InventoryHud>();
        if (hud != null && font != null)
        {
            PrototypeItemBuilder.SetField(hud, "font", font);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Prototype] HUD assets built.");
    }

    private static TMP_FontAsset BuildFont()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
        {
            return existing;
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (source == null)
        {
            Debug.LogWarning("[Prototype] Font not found at " + FontPath);
            return null;
        }

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        font.name = "SpecialElite SDF";
        AssetDatabase.CreateAsset(font, FontAssetPath);
        font.material.name = "SpecialElite SDF Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        font.atlasTexture.name = "SpecialElite SDF Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
        // Pre-bake Portuguese glyphs so accents render even before they are first requested.
        font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?/·[]()-+ÁÀÂÃÉÊÍÓÔÕÚÇáàâãéêíóôõúç");
        EditorUtility.SetDirty(font);
        return font;
    }

    private static void RenderIcon(string prefabPath, string definitionPath, Vector3 euler)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(definitionPath);
        if (prefab == null || definition == null)
        {
            return;
        }

        BattlefieldTerrainBuilder.EnsureFolder(IconFolder);
        Vector3 origin = new Vector3(0f, -2000f, 0f);
        GameObject instance = (GameObject)Object.Instantiate(prefab, origin, Quaternion.Euler(euler));
        foreach (Light light in instance.GetComponentsInChildren<Light>())
        {
            light.enabled = false;
        }

        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>())
        {
            particles.gameObject.SetActive(false);
        }

        Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (renderer is ParticleSystemRenderer)
            {
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        GameObject cameraObject = new GameObject("IconCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f;
        camera.transform.position = bounds.center - Vector3.forward * 5f;
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 20f;
        camera.enabled = false;
        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData data =
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;

        GameObject keyObject = new GameObject("IconKeyLight");
        Light key = keyObject.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 2.2f;
        key.color = new Color(1f, 0.92f, 0.8f);
        keyObject.transform.rotation = Quaternion.Euler(30f, -30f, 0f);

        const int size = 256;
        RenderTexture target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        string path = $"{IconFolder}/{prefab.name}_Icon.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(instance);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(keyObject);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        definition.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        EditorUtility.SetDirty(definition);
    }
}
