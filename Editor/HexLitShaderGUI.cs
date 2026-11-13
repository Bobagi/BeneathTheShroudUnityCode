using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

// Inspector of Horror/Lit Hex (Art/Shaders/HexLit): the stock URP Lit inspector (Surface Options, Surface Inputs,
// Detail Inputs, Advanced) plus a "Hex Tiling" foldout. URP's own LitShader GUI is internal and draws only the
// properties it knows, so the hex properties would be invisible under it; this replicates it with the public pieces
// (BaseShaderGUI, LitGUI) and re-implements the small internal LitDetailGUI. HexTileMaterials switches the house's
// materials to the shader from a menu.
public class HexLitShaderGUI : BaseShaderGUI
{
    private static readonly string[] workflowModeNames = Enum.GetNames(typeof(LitGUI.WorkflowMode));

    // A foldout bit of this GUI, after the four of BaseShaderGUI.Expandable (1, 2, 4, 8).
    private const uint HexFoldout = 1u << 5;

    private static class HexStyles
    {
        public static readonly GUIContent detailInputs = EditorGUIUtility.TrTextContent("Detail Inputs",
            "These settings define the surface details by tiling and overlaying additional maps on the surface.");
        public static readonly GUIContent detailMask = EditorGUIUtility.TrTextContent("Mask",
            "Select a mask for the Detail map. The mask uses the alpha channel of the selected texture. The Tiling and Offset settings have no effect on the mask.");
        public static readonly GUIContent detailAlbedoMap = EditorGUIUtility.TrTextContent("Base Map",
            "Select the surface detail texture. The alpha of your texture determines surface hue and intensity.");
        public static readonly GUIContent detailNormalMap = EditorGUIUtility.TrTextContent("Normal Map",
            "Designates a Normal Map to create the illusion of bumps and dents in the details of this Material's surface.");
        public static readonly GUIContent detailAlbedoMapScaleInfo = EditorGUIUtility.TrTextContent(
            "Setting the scaling factor to a value other than 1 results in a less performant shader variant.");

        public static readonly GUIContent hexInputs = EditorGUIUtility.TrTextContent("Hex Tiling",
            "Mikkelsen 2022: every map is sampled three times, once per hex tile around the pixel, each hex at its own random offset and rotation, and blended; a tiling texture never shows its repeat. Costs three samples per map instead of one. Not for the detail maps.");
        public static readonly GUIContent hexTile = EditorGUIUtility.TrTextContent("Enable", "Hex tiling on the base, normal, metallic/specular, occlusion and emission maps.");
        public static readonly GUIContent hexScale = EditorGUIUtility.TrTextContent("Hex Scale",
            "UV units one hex spans flat to flat (1 = one texture tile). The house surfaces tile every 1.5 to 3 m: 0.5 to 1 works.");
        public static readonly GUIContent hexContrast = EditorGUIUtility.TrTextContent("Border Contrast",
            "0.5 keeps the reference blend; higher sharpens the borders between hexes, lower softens them.");
        public static readonly GUIContent hexRotation = EditorGUIUtility.TrTextContent("Rotation (radians)",
            "Largest random rotation per hex. 0 keeps tiles, planks and wallpaper motifs aligned; pi allows any angle.");
    }

    private LitGUI.LitProperties litProperties;
    private MaterialProperty detailMask, detailAlbedoMapScale, detailAlbedoMap, detailNormalMapScale, detailNormalMap;
    private MaterialProperty hexTile, hexScale, hexContrast, hexRotation;

    public override void FillAdditionalFoldouts(MaterialHeaderScopeList materialScopesList)
    {
        materialScopesList.RegisterHeaderScope(HexStyles.detailInputs, Expandable.Details, _ => DrawDetailArea());
        materialScopesList.RegisterHeaderScope(HexStyles.hexInputs, HexFoldout, _ => DrawHexArea());
    }

    public override void FindProperties(MaterialProperty[] properties)
    {
        base.FindProperties(properties);
        litProperties = new LitGUI.LitProperties(properties);
        detailMask = FindProperty("_DetailMask", properties, false);
        detailAlbedoMapScale = FindProperty("_DetailAlbedoMapScale", properties, false);
        detailAlbedoMap = FindProperty("_DetailAlbedoMap", properties, false);
        detailNormalMapScale = FindProperty("_DetailNormalMapScale", properties, false);
        detailNormalMap = FindProperty("_DetailNormalMap", properties, false);
        hexTile = FindProperty("_HexTile", properties, false);
        hexScale = FindProperty("_HexScale", properties, false);
        hexContrast = FindProperty("_HexContrast", properties, false);
        hexRotation = FindProperty("_HexRotation", properties, false);
    }

    public override void ValidateMaterial(Material material)
    {
        SetMaterialKeywords(material, LitGUI.SetMaterialKeywords, SetHexLitKeywords);
    }

    // The keywords of this shader beyond LitGUI's: the detail maps (as URP's internal LitDetailGUI) and hex tiling.
    // Also used by HexTileMaterials after a shader swap.
    public static void SetHexLitKeywords(Material material)
    {
        if (material.HasProperty("_DetailAlbedoMap") && material.HasProperty("_DetailNormalMap") && material.HasProperty("_DetailAlbedoMapScale"))
        {
            bool isScaled = material.GetFloat("_DetailAlbedoMapScale") != 1.0f;
            bool hasDetailMap = material.GetTexture("_DetailAlbedoMap") || material.GetTexture("_DetailNormalMap");
            CoreUtils.SetKeyword(material, "_DETAIL_MULX2", !isScaled && hasDetailMap);
            CoreUtils.SetKeyword(material, "_DETAIL_SCALED", isScaled && hasDetailMap);
        }

        if (material.HasProperty("_HexTile"))
        {
            CoreUtils.SetKeyword(material, "_HEXTILE_ON", material.GetFloat("_HexTile") > 0.5f);
        }
    }

    public override void DrawSurfaceOptions(Material material)
    {
        EditorGUIUtility.labelWidth = 0f;
        if (litProperties.workflowMode != null)
        {
            DoPopup(LitGUI.Styles.workflowModeText, litProperties.workflowMode, workflowModeNames);
        }

        base.DrawSurfaceOptions(material);
    }

    public override void DrawSurfaceInputs(Material material)
    {
        base.DrawSurfaceInputs(material);
        LitGUI.Inputs(litProperties, materialEditor, material);
        DrawEmissionProperties(material, true);
        DrawTileOffset(materialEditor, baseMapProp);
    }

    public override void DrawAdvancedOptions(Material material)
    {
        if (litProperties.reflections != null && litProperties.highlights != null)
        {
            materialEditor.ShaderProperty(litProperties.highlights, LitGUI.Styles.highlightsText);
            materialEditor.ShaderProperty(litProperties.reflections, LitGUI.Styles.reflectionsText);
        }

        base.DrawAdvancedOptions(material);
    }

    private void DrawDetailArea()
    {
        if (detailMask == null || detailAlbedoMap == null || detailNormalMap == null)
        {
            return;
        }

        materialEditor.TexturePropertySingleLine(HexStyles.detailMask, detailMask);
        materialEditor.TexturePropertySingleLine(HexStyles.detailAlbedoMap, detailAlbedoMap, detailAlbedoMap.textureValue != null ? detailAlbedoMapScale : null);
        if (detailAlbedoMapScale.floatValue != 1.0f)
        {
            EditorGUILayout.HelpBox(HexStyles.detailAlbedoMapScaleInfo.text, MessageType.Info, true);
        }

        materialEditor.TexturePropertySingleLine(HexStyles.detailNormalMap, detailNormalMap, detailNormalMap.textureValue != null ? detailNormalMapScale : null);
        materialEditor.TextureScaleOffsetProperty(detailAlbedoMap);
    }

    private void DrawHexArea()
    {
        if (hexTile == null)
        {
            return;
        }

        materialEditor.ShaderProperty(hexTile, HexStyles.hexTile);
        using (new EditorGUI.DisabledScope(hexTile.floatValue < 0.5f))
        {
            materialEditor.ShaderProperty(hexScale, HexStyles.hexScale);
            materialEditor.ShaderProperty(hexContrast, HexStyles.hexContrast);
            materialEditor.ShaderProperty(hexRotation, HexStyles.hexRotation);
        }
    }

    // As URP's LitShader: carry the emission over from the Standard shader and set up the blend mode.
    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        if (material == null)
        {
            throw new ArgumentNullException("material");
        }

        if (material.HasProperty("_Emission"))
        {
            material.SetColor("_EmissionColor", material.GetColor("_Emission"));
        }

        base.AssignNewShaderToMaterial(material, oldShader, newShader);

        if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
        {
            SetupMaterialBlendMode(material);
            return;
        }

        SurfaceType surfaceType = SurfaceType.Opaque;
        BlendMode blendMode = BlendMode.Alpha;
        if (oldShader.name.Contains("/Transparent/Cutout/"))
        {
            material.SetFloat("_AlphaClip", 1);
        }
        else if (oldShader.name.Contains("/Transparent/"))
        {
            surfaceType = SurfaceType.Transparent;
        }

        material.SetFloat("_Blend", (float)blendMode);
        material.SetFloat("_Surface", (float)surfaceType);
        CoreUtils.SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", surfaceType == SurfaceType.Transparent);
        if (oldShader.name.Equals("Standard (Specular setup)"))
        {
            material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Specular);
            Texture texture = material.GetTexture("_SpecGlossMap");
            if (texture != null)
            {
                material.SetTexture("_MetallicSpecGlossMap", texture);
            }
        }
        else
        {
            material.SetFloat("_WorkflowMode", (float)LitGUI.WorkflowMode.Metallic);
            Texture texture = material.GetTexture("_MetallicGlossMap");
            if (texture != null)
            {
                material.SetTexture("_MetallicSpecGlossMap", texture);
            }
        }
    }
}
