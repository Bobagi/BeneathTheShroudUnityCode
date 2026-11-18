using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The player's revolver: a Colt Python .357 (swing-out cylinder, all six cases out at once, a speedloader), by Erai
// (Sketchfab, CC BY 4.0; Assets/_Project/Art/Items/Python357/LICENSE.txt). It replaced the Reichsrevolver M1879 on 08/10
// (Gustavo: that one was too hard to reload: a loading gate, a rod and one round at a time). Made from the pack by
// SourceAssets/Tools/export_python357.py (turned to face +Z, palm point at the origin, real size) and python357_textures.py.
// This script sets the importer (Generic rig, the author's swing of the cylinder cut into Open and Close, whose crane
// curves the first person reload clip carries), the URP materials and the prefab. Menu: Horror > Combate > Preparar o
// revólver Python.
public static class Python357Setup
{
    public const string Folder = ProjectPaths.Art + "/Items/Python357";
    public const string ModelPath = Folder + "/Python357.fbx";
    public const string ReloadModelPath = Folder + "/Python357_Reload.fbx";
    public const string PrefabPath = Folder + "/Python357.prefab";
    // The old Animator controller of the prefab (Idle/Open/Close), removed on 10/10: the rig's reload clip keys the crane.
    private const string ControllerPath = Folder + "/Python357.controller";

    // The author's animation (frames of the Blender file): the cylinder swings out in 1 to 30 and in again in 120 to 155.
    private const int OpenFirst = 1;
    private const int OpenLast = 30;
    private const int CloseFirst = 120;
    private const int CloseLast = 155;
    public const float ClipSpeed = 1.5f;

    [MenuItem("Horror/Combate/Preparar o revólver Python")]
    public static void Prepare()
    {
        Texture("Python357", true);
        Texture("Python357_Rounds", true);
        Texture("Python357_SpeedLoader", true);
        ConfigureModel();
        ConfigureReloadModel();
        Material revolver = MakeMaterial("Python357");
        Material rounds = MakeMaterial("Python357_Rounds");
        MakeMaterial("Python357_SpeedLoader");
        MakePrefab(revolver, rounds);
        AssetDatabase.DeleteAsset(ControllerPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Python357] prepared: " + PrefabPath);
    }

    private static void Texture(string name, bool metallic)
    {
        TextureImporter normal = AssetImporter.GetAtPath(Folder + "/Textures/" + name + "_Normal.png") as TextureImporter;
        if (normal != null && normal.textureType != TextureImporterType.NormalMap)
        {
            normal.textureType = TextureImporterType.NormalMap;
            normal.SaveAndReimport();
        }

        TextureImporter ms = AssetImporter.GetAtPath(Folder + "/Textures/" + name + "_MetallicSmoothness.png") as TextureImporter;
        if (ms != null && metallic && ms.sRGBTexture)
        {
            ms.sRGBTexture = false;
            ms.SaveAndReimport();
        }
    }

    private static void ConfigureModel()
    {
        ModelImporter model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        model.animationType = ModelImporterAnimationType.Generic;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.optimizeGameObjects = false;
        model.importAnimation = true;
        model.animationCompression = ModelImporterAnimationCompression.Off;
        ModelImporterClipAnimation[] defaults = model.defaultClipAnimations;
        List<ModelImporterClipAnimation> clips = new List<ModelImporterClipAnimation>();
        foreach ((string name, int first, int last) in new[] { ("Open", OpenFirst, OpenLast), ("Close", CloseFirst, CloseLast) })
        {
            // A new clip each time: ModelImporterClipAnimation is a class, and reusing defaults[0] for both made the
            // second overwrite the first (two "Close" clips and an Open state with no motion: the cylinder never opened).
            ModelImporterClipAnimation clip = new ModelImporterClipAnimation
            {
                takeName = defaults[0].takeName,
            };
            clip.name = name;
            clip.firstFrame = first;
            clip.lastFrame = last;
            clip.loopTime = false;
            clips.Add(clip);
        }

        model.clipAnimations = clips.ToArray();
        model.SaveAndReimport();
    }

    private static void ConfigureReloadModel()
    {
        ModelImporter model = AssetImporter.GetAtPath(ReloadModelPath) as ModelImporter;
        if (model == null)
        {
            return;
        }

        model.animationType = ModelImporterAnimationType.Generic;
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importNormals = ModelImporterNormals.Import;
        model.importTangents = ModelImporterTangents.CalculateMikk;
        model.optimizeGameObjects = false;
        model.importAnimation = false;
        model.SaveAndReimport();
    }

    private static Material MakeMaterial(string name)
    {
        string path = Folder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }

        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + "_BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + "_Normal.png"));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + name + "_MetallicSmoothness.png"));
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Smoothness", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // The model's own clips (Open, Close) are read by the first person reload clip's crane track; the prefab carries no
    // Animator (the rig's clip moves the crane).
    private static void MakePrefab(Material revolver, Material rounds)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = smr.name.StartsWith("Bullet") ? rounds : revolver;
            smr.updateWhenOffscreen = true;
        }

        Animator animator = instance.GetComponent<Animator>();
        if (animator != null)
        {
            Object.DestroyImmediate(animator);
        }

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
    }
}
