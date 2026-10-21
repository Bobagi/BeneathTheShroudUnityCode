using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Clothes from MetaHuman Creator on a game body, for any character (06/10, Gustavo: "fácil de criar NPCs com MetaHuman
// e de trocar as roupas"). The outfit FBX comes from SourceAssets/Tools/metahuman_outfit.py
// and holds, on the body's own skeleton: "Outfit" (one material per garment, named <Name>_<Part>), "<Name>Body" (the
// body minus what the clothes cover, with a band of skin inside every cuff and collar) and the groom cards ("Hair",
// "Beard", "Eyebrows"...). Every renderer is re-pointed at the body's bones by name and gets its material from the
// textures beside the FBX:
//   <Name>_<Part>_BaseColor / _Normal / _MetallicSmoothness / _Occlusion   a garment (metahuman_textures.py outfit)
//   <Name>_<Groom>_BaseColor                                               a groom (alpha = coverage, clipped)
// The head is the body's own ("Head": the MetaHuman face with its eyes, MetaHumanBody), added to the returned list so
// the skin keeps it. Returns the renderers: one BodySkins skin. Swapping clothes = another outfit folder, another
// skin.
public static class MetaHumanOutfit
{
    // bodyObject: an instance of the character's body FBX (its Animator and bones). folder: where <Name>Outfit.fbx and
    // its textures are. skin: the material of the bare body, used by the cut body and head. firstPerson: head and
    // grooms are drawn only into shadows (the camera sits inside the head).
    public static Renderer[] Attach(GameObject bodyObject, string folder, string name, Material skin, bool firstPerson)
    {
        string fbx = folder + "/" + name + "Outfit.fbx";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (asset == null)
        {
            Debug.LogWarning("[MetaHumanOutfit] Falta " + fbx + ": a roupa ficou de fora.");
            return new Renderer[0];
        }

        Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        foreach (Transform t in bodyObject.GetComponentsInChildren<Transform>(true))
        {
            bones[t.name] = t;
        }
        SkinnedMeshRenderer bare = bodyObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Body");
        Transform rootBone = bare != null ? bare.rootBone : bodyObject.GetComponentInChildren<SkinnedMeshRenderer>(true).rootBone;

        GameObject source = Object.Instantiate(asset);
        // Spring bones the outfit adds (a jacket's hem): made on the body first, sprung by Player/ClothSprings.
        OutfitSprings.AddMissingBones(source, bones);
        OutfitSprings.Attach(bodyObject, bones);
        List<Renderer> renderers = new List<Renderer>();
        foreach (SkinnedMeshRenderer from in source.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Transform[] mapped = new Transform[from.bones.Length];
            bool complete = true;
            for (int i = 0; i < mapped.Length; i++)
            {
                if (!bones.TryGetValue(from.bones[i].name, out mapped[i]))
                {
                    Debug.LogError("[MetaHumanOutfit] Osso da roupa que o corpo não tem: " + from.bones[i].name + " (" + from.name + ")");
                    complete = false;
                }
            }
            if (!complete)
            {
                continue;
            }

            Transform old = bodyObject.transform.Find(from.name);
            GameObject part = old != null ? old.gameObject : new GameObject(from.name);
            part.transform.SetParent(bodyObject.transform, false);
            part.transform.localPosition = from.transform.localPosition;
            part.transform.localRotation = from.transform.localRotation;
            part.transform.localScale = from.transform.localScale;
            SkinnedMeshRenderer r = part.GetComponent<SkinnedMeshRenderer>();
            if (r == null)
            {
                r = part.AddComponent<SkinnedMeshRenderer>();
            }
            r.sharedMesh = from.sharedMesh;
            r.bones = mapped;
            r.rootBone = rootBone;
            r.localBounds = from.localBounds;

            bool groom = false;
            if (from.name == "Outfit")
            {
                Material[] slots = new Material[from.sharedMesh.subMeshCount];
                for (int i = 0; i < slots.Length; i++)
                {
                    string materialName = from.sharedMaterials.Length > i && from.sharedMaterials[i] != null ? from.sharedMaterials[i].name : name + "_Garment" + i;
                    slots[i] = Garment(folder, materialName);
                }
                r.sharedMaterials = slots;
            }
            else if (from.name == name + "Body")
            {
                r.sharedMaterial = skin;
            }
            else
            {
                r.sharedMaterial = Groom(folder, name + "_" + from.name);
                groom = true;
            }

            r.shadowCastingMode = firstPerson && groom ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            r.updateWhenOffscreen = true;
            renderers.Add(r);
        }
        Object.DestroyImmediate(source);

        // The body's own head (face, eyes, teeth, lashes) belongs to this skin too.
        SkinnedMeshRenderer head = bodyObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Head");
        if (head != null)
        {
            renderers.Add(head);
        }
        return renderers.ToArray();
    }

    // A garment's material from the four Unity maps beside the FBX (missing maps are left empty).
    private static Material Garment(string folder, string materialName)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        string map = folder + "/" + materialName;
        EnsureTextureImport(map + "_Normal.png", true);
        EnsureTextureImport(map + "_MetallicSmoothness.png", false);
        EnsureTextureImport(map + "_Occlusion.png", false);
        Material m = MaterialAsset(materialName, lit);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(map + "_BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(map + "_Normal.png");
        m.SetTexture("_BumpMap", normal);
        Keyword(m, "_NORMALMAP", normal != null);
        Texture2D metal = AssetDatabase.LoadAssetAtPath<Texture2D>(map + "_MetallicSmoothness.png");
        m.SetTexture("_MetallicGlossMap", metal);
        Keyword(m, "_METALLICSPECGLOSSMAP", metal != null);
        m.SetFloat("_Smoothness", metal != null ? 1f : 0.3f);
        m.SetFloat("_Metallic", 0f);
        Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(map + "_Occlusion.png");
        m.SetTexture("_OcclusionMap", occlusion);
        Keyword(m, "_OCCLUSIONMAP", occlusion != null);
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(map + "_BaseColor.png") == null)
        {
            Debug.LogWarning("[MetaHumanOutfit] Sem textura " + map + "_BaseColor.png (rode metahuman_textures.py): a peça fica branca.");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Hair, beard, eyebrows: cards with the coverage in the alpha, clipped, two-sided.
    private static Material Groom(string folder, string materialName)
    {
        Material m = MaterialAsset(materialName, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + materialName + "_BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.35f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.35f);
        m.SetTexture("_MetallicGlossMap", null);
        m.DisableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void Keyword(Material m, string keyword, bool on)
    {
        if (on)
        {
            m.EnableKeyword(keyword);
        }
        else
        {
            m.DisableKeyword(keyword);
        }
    }

    public static Material MaterialAsset(string name, Shader shader)
    {
        string path = ProjectPaths.FloodedHouseMaterials + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.enableInstancing = true;
        return material;
    }

    public static void EnsureTextureImport(string path, bool normal)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }
        bool changed = false;
        if (normal && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            changed = true;
        }
        if (!normal && importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }
}
