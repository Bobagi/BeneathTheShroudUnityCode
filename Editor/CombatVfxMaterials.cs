using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Particle materials for shooting (Art/VFX/Combat): the gun's smoke, the dust a bullet kicks off a wall, and a
// creature's fluid from a wound. Assets, not materials made at runtime: Shader.Find fails in a player build when no
// asset uses the shader, and a particle material with no texture draws every particle as a square (seen on the
// first muzzle smoke). Simple Lit, alpha blended, soft round Kenney textures, so the smoke stays dark in the dark
// instead of glowing like the Unlit WW1 smoke.
public static class CombatVfxMaterials
{
    private const string Folder = ProjectPaths.Vfx + "/Combat";

    // White: each particle system gives its colour as the start colour (smoke grey, dust brown, the creature's fluid).
    public static Material GunSmoke => Get("GunSmoke", "smoke_04", Color.white);
    public static Material BulletDust => Get("BulletDust", "smoke_07", Color.white);
    public static Material WoundFluid => Get("WoundFluid", "light_01", Color.white);

    private static Material Get(string name, string texture, Color color)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder(ProjectPaths.Vfx, "Combat");
        }

        string path = Folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ProjectPaths.Vfx + "/Textures/" + texture + ".png"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(material);
        return material;
    }
}
