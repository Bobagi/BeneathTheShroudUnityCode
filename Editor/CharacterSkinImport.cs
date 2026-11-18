using UnityEditor;

// Character models (Art/Models/Characters: the player's MetaHuman body, Erick's outfit, the hazmat suit) import with up
// to 8 bone influences per vertex instead of Unity's 4. The MetaHuman hand needs them: 29% of its vertices (palm and
// finger roots) are held by more than 4 bones, and cut to 4 the palm folded on an open hand (04/10). The quality levels
// skin with no limit (PerformanceSettings), so the 8 reach the GPU.
public sealed class CharacterSkinImport : AssetPostprocessor
{
    public const int MaxInfluences = 8;

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(ProjectPaths.CharacterModels + "/"))
        {
            return;
        }
        ModelImporter importer = (ModelImporter)assetImporter;
        if (importer.skinWeights != ModelImporterSkinWeights.Custom || importer.maxBonesPerVertex != MaxInfluences)
        {
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = MaxInfluences;
        }
    }
}
