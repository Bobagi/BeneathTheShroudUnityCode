using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

// Horror > Testes > Mãos gravadas no jogador: plays a hand take recorded from a first person phone video
// (D:\Projetos\hand-mocap) on the test scene player: the baked clip Art/Animations/FirstPerson/Tomadas/<take>.anim on
// the first person rig's layer "Mãos gravadas" (FirstPersonTakes; right hand as recorded, left mirrored), with the
// reference video on a screen ahead of the start, on the clip's clock. H switches it and J goes to the next take of
// the same video (RecordedHandsTest). The test scene generator calls it.
public static class HandMocapStage
{
    private const string Reference = ProjectPaths.FirstPersonTakes + "/Reference";
    public const string DefaultTakeName = FirstPersonTakes.DefaultTake;

    // Ahead of the test scene's start (z -1.5, looking along +Z), a little to the right, facing it.
    private static readonly Vector3 ScreenPlace = new Vector3(1.4f, 1.75f, 2.6f);

    [MenuItem("Horror/Testes/Mãos gravadas no jogador")]
    public static void PlaceInOpenScene()
    {
        PlaceInOpenScene(DefaultTakeName);
    }

    public static void PlaceInOpenScene(string take)
    {
        FirstPersonController controller = Object.FindAnyObjectByType<FirstPersonController>();
        if (!File.Exists(FirstPersonTakes.CapturePath(take)) || controller == null)
        {
            Debug.LogWarning("[HandMocap] Falta a captura " + FirstPersonTakes.CapturePath(take) + " (rode o processar.py) ou o jogador na cena.");
            return;
        }

        FirstPersonArms arms = controller.CameraPivot.GetComponentInChildren<FirstPersonArms>(true);
        if (arms == null)
        {
            Debug.LogWarning("[HandMocap] O jogador da cena não tem o rig dos braços (FirstPersonArms).");
            return;
        }

        Directory.CreateDirectory(Reference);
        AssetDatabase.Refresh();
        foreach (string name in new[] { "HandMocap_Showcase", "HandMocap_Screen" })
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        GameObject screen = new GameObject("HandMocap_Screen");
        Vector3 toStart = new Vector3(0f, ScreenPlace.y, -1.5f) - ScreenPlace;
        screen.transform.SetPositionAndRotation(ScreenPlace, Quaternion.LookRotation(toStart, Vector3.up));

        VideoClip clip = VideoClip(take);
        int width = clip != null ? (int)clip.width : 540;
        int height = clip != null ? (int)clip.height : 960;
        string texturePath = Reference + "/HandMocapVideo.renderTexture";
        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
        if (texture == null || texture.width != width || texture.height != height)
        {
            AssetDatabase.DeleteAsset(texturePath);
            texture = new RenderTexture(width, height, 0);
            AssetDatabase.CreateAsset(texture, texturePath);
        }

        float tall = 1.4f;
        Quad(screen.transform, new Vector2(tall * width / height, tall), texture);
        TextMeshPro label = Label(screen.transform, new Vector3(0f, tall / 2f + 0.17f, 0f), "Vídeo: " + take);

        VideoPlayer video = screen.AddComponent<VideoPlayer>();
        video.clip = clip;
        video.playOnAwake = false;
        video.isLooping = true;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = texture;

        // The hands are the baked clip on the rig's Animator (FirstPersonTakes); this only switches it (H).
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(FirstPersonTakes.ClipPath(take)) == null)
        {
            FirstPersonTakes.Bake(take);
        }

        RecordedHandsTest old = Object.FindAnyObjectByType<RecordedHandsTest>();
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        RecordedHandsTest test = new GameObject("RecordedHandsTest").AddComponent<RecordedHandsTest>();
        test.transform.SetParent(screen.transform, false);
        SerializedObject so = new SerializedObject(test);
        so.FindProperty("arms").objectReferenceValue = arms;
        string[] takes = TakesOf(take);
        SerializedProperty states = so.FindProperty("states");
        states.arraySize = takes.Length;
        for (int i = 0; i < takes.Length; i++)
        {
            states.GetArrayElementAtIndex(i).stringValue = takes[i];
        }

        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("video").objectReferenceValue = video;
        // The test scene starts with the gun in the hands (Gustavo, 08/10): H turns the recorded hands on.
        so.FindProperty("playOnStart").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Erick's own hands (the suit's gloves are rubber mitts).
        PlayerBody body = controller.GetComponentInChildren<PlayerBody>(true);
        BodySkins skins = body != null ? body.GetComponent<BodySkins>() : null;
        if (skins != null)
        {
            skins.Apply("Erick");
            // The body is a model prefab instance: the change only sticks as a recorded property override.
            SerializedObject skinState = new SerializedObject(skins);
            skinState.FindProperty("current").intValue = skins.Current;
            skinState.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.activeGameObject = screen;
        EditorSceneManager.MarkSceneDirty(screen.scene);
    }

    // The take's preview video (H.264) copied into the project, so the scene does not depend on D:\Projetos.
    private static VideoClip VideoClip(string take)
    {
        string target = Reference + "/" + take + ".mp4";
        string source = FirstPersonTakes.CaptureFolder + "/" + take + "/" + take + "_preview.mp4";
        if (File.Exists(source) && (!File.Exists(target) || File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(target)))
        {
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target);
        }

        return AssetDatabase.LoadAssetAtPath<VideoClip>(target);
    }

    private static void Quad(Transform parent, Vector2 size, Texture texture)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Picture";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        // A quad shows its face along -Z: turned around, it faces the same way as the screen.
        quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        string path = Reference + "/HandMocapVideo.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", texture);
        EditorUtility.SetDirty(material);
        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // The take and the other fitted takes of the same video (<video>_fit_ambos, _fit_rtm, _fit_mp) that have a clip,
    // the chosen take first.
    private static string[] TakesOf(string take)
    {
        int fit = take.IndexOf("_fit_", System.StringComparison.Ordinal);
        string video = fit < 0 ? take : take.Substring(0, fit);
        List<string> takes = new List<string>();
        if (Directory.Exists(ProjectPaths.FirstPersonTakes))
        {
            foreach (string file in Directory.GetFiles(ProjectPaths.FirstPersonTakes, video + "*.anim"))
            {
                takes.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        takes.Sort((a, b) => a == take ? -1 : b == take ? 1 : string.CompareOrdinal(a, b));
        return takes.ToArray();
    }

    private static TextMeshPro Label(Transform parent, Vector3 position, string text)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(parent, false);
        label.transform.localPosition = position;
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        tmp.text = text;
        tmp.fontSize = 0.55f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 0.3f);
        tmp.color = Color.white;
        return tmp;
    }
}
