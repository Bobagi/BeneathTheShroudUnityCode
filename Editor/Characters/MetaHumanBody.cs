using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The materials of a MetaHuman body made by SourceAssets/Tools/metahuman_body.py (07/10): "Body" (the skin) and "Head",
// which is the MetaHuman face export with one submesh per part: Face, EyeL, EyeR, Teeth, Eyelashes. The maps sit beside
// the FBX with the names metahuman_textures.py writes (<Name>Body_Skin_*, <Name>Body_Head_*, _Eyes_, _Teeth_,
// _Eyelashes_). Used by the Flooded House player (first person: the head only casts shadows) and by the arrival
// cutscene (the head is seen). Returns the skin material, which the cut body of an outfit (MetaHumanOutfit) shares.
public static class MetaHumanBody
{
    public static Material Apply(GameObject bodyObject, string folder, string name, bool firstPerson)
    {
        string stem = folder + "/" + name + "Body_";
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material skin = Lit(name + "Skin", lit, stem + "Skin_BaseColor.png", stem + "Normal.png", stem + "Skin_MetallicSmoothness.png", 0.8f);
        Material face = Lit(name + "Head", lit, stem + "Head_BaseColor.png", stem + "Head_Normal.png", stem + "Head_MetallicSmoothness.png", 0.6f);
        // Wet: a sharp highlight is what makes an eye read as alive in the dark.
        Material eye = Lit(name + "Eyes", lit, stem + "Eyes_BaseColor.png", null, null, 0.92f);
        Material teeth = Lit(name + "Teeth", lit, stem + "Teeth_BaseColor.png", stem + "Teeth_Normal.png", null, 0.7f);
        // The lashes map's alpha is the coverage: imported as transparency so the mips do not mix the empty
        // pixels into the strands.
        if (AssetImporter.GetAtPath(stem + "Eyelashes_BaseColor.png") is TextureImporter lashImporter && !lashImporter.alphaIsTransparency)
        {
            lashImporter.alphaIsTransparency = true;
            lashImporter.SaveAndReimport();
        }
        Material lashes = Lit(name + "Eyelashes", lit, stem + "Eyelashes_BaseColor.png", null, null, 0.3f);
        lashes.SetFloat("_AlphaClip", 1f);
        lashes.SetFloat("_Cutoff", 0.4f);
        lashes.EnableKeyword("_ALPHATEST_ON");
        lashes.SetFloat("_Cull", (float)CullMode.Off);
        EditorUtility.SetDirty(lashes);

        foreach (SkinnedMeshRenderer r in bodyObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r.name == "Head")
            {
                // One submesh per part, in the order metahuman_body.py writes the slots (the FBX imports with no
                // materials, so the slot names are not there to read).
                string[] order = { "Face", "EyeL", "EyeR", "Teeth", "Eyelashes" };
                Material[] slots = new Material[r.sharedMesh.subMeshCount];
                for (int i = 0; i < slots.Length; i++)
                {
                    string part = i < order.Length ? order[i] : "Face";
                    if (r.sharedMaterials.Length > i && r.sharedMaterials[i] != null && System.Array.IndexOf(order, r.sharedMaterials[i].name) >= 0)
                    {
                        part = r.sharedMaterials[i].name;
                    }
                    switch (part)
                    {
                        case "EyeL":
                        case "EyeR":
                            slots[i] = eye;
                            break;
                        case "Teeth":
                            slots[i] = teeth;
                            break;
                        case "Eyelashes":
                            slots[i] = lashes;
                            break;
                        default:
                            slots[i] = face;
                            break;
                    }
                }
                r.sharedMaterials = slots;
                // In first person the camera sits inside the head: it only casts shadows (the outside view of the
                // test scene draws it while it renders).
                r.shadowCastingMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
            else if (r.name == "Body")
            {
                r.sharedMaterial = skin;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            r.updateWhenOffscreen = true;
        }
        return skin;
    }

    private static Material Lit(string name, Shader lit, string baseMap, string normalMap, string metallicMap, float smoothness)
    {
        Material m = MetaHumanOutfit.MaterialAsset(name, lit);
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
        m.SetColor("_BaseColor", Color.white);
        Texture2D normal = null;
        if (normalMap != null)
        {
            MetaHumanOutfit.EnsureTextureImport(normalMap, true);
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMap);
        }
        m.SetTexture("_BumpMap", normal);
        Keyword(m, "_NORMALMAP", normal != null);
        Texture2D metal = null;
        if (metallicMap != null)
        {
            MetaHumanOutfit.EnsureTextureImport(metallicMap, false);
            metal = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicMap);
        }
        m.SetTexture("_MetallicGlossMap", metal);
        Keyword(m, "_METALLICSPECGLOSSMAP", metal != null);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Cull", (float)CullMode.Back);
        // With a map, URP multiplies its alpha (1 - roughness) by this: the skin's wet parts (lids, nostrils, lips)
        // at full strength burned white under a strong light.
        m.SetFloat("_Smoothness", smoothness);
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap) == null)
        {
            Debug.LogWarning("[MetaHumanBody] Sem textura " + baseMap + " (rode metahuman_textures.py body).");
        }
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
}
