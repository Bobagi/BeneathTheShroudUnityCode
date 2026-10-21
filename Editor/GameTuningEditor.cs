using UnityEditor;
using UnityEngine;

// Horror > Ajustes do jogo: selects (and creates, the first time) the GameTuning asset.
public static class GameTuningMenu
{
    public const string AssetPath = ProjectPaths.Root + "/Settings/Resources/" + GameTuning.ResourceName + ".asset";

    [MenuItem("Horror/Ajustes do jogo %#t")]
    public static void Open()
    {
        Selection.activeObject = Ensure();
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    public static GameTuning Ensure()
    {
        GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(AssetPath);
        if (tuning == null)
        {
            string folder = System.IO.Path.GetDirectoryName(AssetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), "Resources");
            }

            tuning = ScriptableObject.CreateInstance<GameTuning>();
            AssetDatabase.CreateAsset(tuning, AssetPath);
            AssetDatabase.SaveAssets();
        }

        return tuning;
    }
}

// The [GameTuning] object in Play Mode shows the asset's values right in its Inspector.
[CustomEditor(typeof(GameTuningHost))]
public sealed class GameTuningHostEditor : Editor
{
    private Editor inner;

    public override void OnInspectorGUI()
    {
        GameTuning tuning = ((GameTuningHost)target).Tuning;
        if (tuning == null)
        {
            EditorGUILayout.HelpBox("Sem asset GameTuning. Use Horror > Ajustes do jogo.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox("Estes valores ficam no asset " + GameTuningMenu.AssetPath +
            ": o que mudar aqui durante o Play continua depois de parar, e vale para todas as cenas.", MessageType.Info);
        CreateCachedEditor(tuning, null, ref inner);
        inner.OnInspectorGUI();
    }
}
