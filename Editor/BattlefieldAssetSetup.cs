using UnityEditor;
using UnityEngine;

// One-shot asset setup for the battlefield: texture import types, terrain layers, URP materials
// and material remaps on the Blender-made FBX models. Re-runnable.
public static class BattlefieldAssetSetup
{
    private const string TextureRoot = ProjectPaths.Textures;
    private const string MaterialFolder = ProjectPaths.EnvironmentMaterials;
    private const string LayerFolder = ProjectPaths.TerrainLayers;
    private const string ModelFolder = ProjectPaths.Models;

    private struct LayerSpec
    {
        public string layer;
        public string texture;
        public float tile;
        public Color tint;
    }

    private struct MaterialSpec
    {
        public string material;
        public string texture;
        public Color tint;
        public float tile;
        public float metallic;
    }

    [MenuItem("Horror/Battlefield/0 Setup Assets")]
    public static void Setup()
    {
        ConfigureTextures();
        CreateTerrainLayers();
        CreateMaterials();
        RemapModelMaterials();
        ConfigureCreatureImport();
        AssetDatabase.SaveAssets();
        Debug.Log("[Battlefield] Assets set up.");
    }

    private static void ConfigureTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool changed = false;
            if (path.EndsWith("_Normal.jpg") && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }

            bool linear = path.EndsWith("_Mask.png") || path.EndsWith("_MetallicSmooth.png");
            if (linear && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                changed = true;
            }

            if (importer.maxTextureSize != 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }

            if (importer.anisoLevel != 4)
            {
                importer.anisoLevel = 4;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static Texture2D Tex(string set, string suffix)
    {
        string extension = suffix == "Mask" || suffix == "MetallicSmooth" ? ".png" : ".jpg";
        return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureRoot}/{set}/{set}_{suffix}{extension}");
    }

    private static void CreateTerrainLayers()
    {
        BattlefieldTerrainBuilder.EnsureFolder(LayerFolder);
        LayerSpec[] specs =
        {
            new LayerSpec { layer = "Mud", texture = "Mud", tile = 5f, tint = new Color(0.66f, 0.64f, 0.62f) },
            new LayerSpec { layer = "Dirt", texture = "Dirt", tile = 6f, tint = new Color(0.5f, 0.48f, 0.46f) },
            new LayerSpec { layer = "Churned", texture = "Churned", tile = 7f, tint = new Color(0.64f, 0.62f, 0.6f) },
            new LayerSpec { layer = "Gravel", texture = "Gravel", tile = 4f, tint = new Color(0.66f, 0.65f, 0.63f) },
        };

        foreach (LayerSpec spec in specs)
        {
            string path = $"{LayerFolder}/{spec.layer}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = Tex(spec.texture, "Albedo");
            layer.normalMapTexture = Tex(spec.texture, "Normal");
            layer.maskMapTexture = Tex(spec.texture, "Mask");
            layer.tileSize = new Vector2(spec.tile, spec.tile);
            layer.normalScale = 1f;
            layer.smoothness = 1f;
            layer.metallic = 0f;
            // Darken and desaturate the photo-scanned soils toward a cold, wet battlefield palette.
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(spec.tint.r, spec.tint.g, spec.tint.b, 1f);
            EditorUtility.SetDirty(layer);
        }
    }

    public static Material GetMaterial(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
    }

    private static void CreateMaterials()
    {
        BattlefieldTerrainBuilder.EnsureFolder(MaterialFolder);
        MaterialSpec[] specs =
        {
            new MaterialSpec { material = "Wood", texture = "Wood", tint = new Color(0.72f, 0.66f, 0.58f), tile = 1f },
            new MaterialSpec { material = "Burlap", texture = "Burlap", tint = new Color(0.75f, 0.68f, 0.55f), tile = 1f },
            new MaterialSpec { material = "DeadBark", texture = "DeadBark", tint = new Color(0.55f, 0.5f, 0.46f), tile = 1f },
            new MaterialSpec { material = "RustyWire", texture = "RustyMetal", tint = new Color(0.6f, 0.45f, 0.35f), tile = 1f, metallic = 0.6f },
            new MaterialSpec { material = "Rope", texture = "Burlap", tint = new Color(0.55f, 0.47f, 0.33f), tile = 2f },
            new MaterialSpec { material = "Mud", texture = "CraterMud", tint = new Color(0.8f, 0.78f, 0.75f), tile = 1f },
            new MaterialSpec { material = "CreatureSkin", texture = "DeadBark", tint = new Color(0.2f, 0.19f, 0.2f), tile = 6f },
        };

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (MaterialSpec spec in specs)
        {
            string path = $"{MaterialFolder}/{spec.material}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = lit;
            material.SetTexture("_BaseMap", Tex(spec.texture, "Albedo"));
            material.SetColor("_BaseColor", spec.tint);
            material.SetTexture("_BumpMap", Tex(spec.texture, "Normal"));
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", Tex(spec.texture, "MetallicSmooth"));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", spec.metallic);
            material.SetFloat("_Smoothness", 0.8f);
            material.SetTexture("_OcclusionMap", null);
            material.mainTextureScale = new Vector2(spec.tile, spec.tile);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }
    }

    private static void RemapModelMaterials()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(subAsset is Material embedded))
                {
                    continue;
                }

                string materialName = embedded.name;
                // Blender duplicates get ".001" suffixes; map them to the base material.
                int dot = materialName.IndexOf('.');
                string baseName = dot > 0 ? materialName.Substring(0, dot) : materialName;
                Material target = GetMaterial(baseName);
                if (target != null)
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName), target);
                }
            }

            importer.SaveAndReimport();
        }
    }

    private const string PropFolder = ProjectPaths.Props;

    // Poly Haven FBX files reference .exr textures that are not shipped; rebuild each material from
    // the JPG/PNG set whose file name matches the material (or the only set in the folder).
    [MenuItem("Horror/Battlefield/0b Setup Props")]
    public static void SetupProps()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { PropFolder }))
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            string folder = System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string textureFolder = folder + "/textures";
            string[] diffuseGuids = AssetDatabase.FindAssets("_diff_", new[] { textureFolder });

            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (!(subAsset is Material embedded))
                {
                    continue;
                }

                string diffusePath = null;
                foreach (string diffuseGuid in diffuseGuids)
                {
                    string candidate = AssetDatabase.GUIDToAssetPath(diffuseGuid);
                    string baseName = System.IO.Path.GetFileName(candidate).Split(new[] { "_diff_" }, System.StringSplitOptions.None)[0];
                    if (baseName == embedded.name || baseName.EndsWith("_" + embedded.name) || diffuseGuids.Length == 1)
                    {
                        diffusePath = candidate;
                        break;
                    }
                }

                if (diffusePath == null && diffuseGuids.Length > 0)
                {
                    diffusePath = AssetDatabase.GUIDToAssetPath(diffuseGuids[0]);
                }

                if (diffusePath == null)
                {
                    continue;
                }

                string setBase = diffusePath.Split(new[] { "_diff_" }, System.StringSplitOptions.None)[0];
                string resolution = diffusePath.Contains("_2k") ? "2k" : "1k";
                Texture2D normal = FindTexture(setBase + "_nor_gl_" + resolution);
                if (normal != null)
                {
                    TextureImporter normalImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                    if (normalImporter.textureType != TextureImporterType.NormalMap)
                    {
                        normalImporter.textureType = TextureImporterType.NormalMap;
                        normalImporter.SaveAndReimport();
                    }
                }

                Texture2D metallicSmooth = FindTexture(setBase + "_MS");
                if (metallicSmooth != null)
                {
                    TextureImporter msImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(metallicSmooth));
                    if (msImporter.sRGBTexture)
                    {
                        msImporter.sRGBTexture = false;
                        msImporter.SaveAndReimport();
                    }
                }

                string materialPath = $"{folder}/{embedded.name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath));
                material.SetColor("_BaseColor", Color.white);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }

                if (metallicSmooth != null)
                {
                    material.SetTexture("_MetallicGlossMap", metallicSmooth);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    material.SetFloat("_Smoothness", 1f);
                }

                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            }

            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Battlefield] Props set up.");
    }

    private static Texture2D FindTexture(string pathWithoutExtension)
    {
        foreach (string extension in new[] { ".jpg", ".png" })
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pathWithoutExtension + extension);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    private static void ConfigureCreatureImport()
    {
        string path = ModelFolder + "/Creature_Colossus_v2.fbx";
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            return;
        }

        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.importAnimation = true;
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.wrapMode = WrapMode.Loop;
            clip.loopTime = true;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
}
