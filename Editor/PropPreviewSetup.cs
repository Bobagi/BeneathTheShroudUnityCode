using TMPro;
using UnityEditor;
using UnityEngine;

// The props generated on 09/10 (Meshy, reduced by the game-3d-assets skill) standing to the right of the start in the
// test scene, each with its name and triangle count, so Gustavo can judge them before they go into the house:
// the hooked pole, the folding attic ladder, the wall security panel, Alex's cracked tablet and the wrecked
// dehumidifier. Also called by the suit test generator.
public static class PropPreviewSetup
{
    private const string GroupName = "PropPreview";

    private struct Spec
    {
        public string name, label;
        public Vector3 at;
        public float yaw;
        public float stand; // height of a plain stand under the prop (0 = on the floor)
    }

    private static readonly Spec[] Specs =
    {
        // A clear row at x 5.5, past the table, the end of the wall (x 4) and the reference video screen.
        new Spec { name = "BoatHook", label = "Vara com gancho", at = new Vector3(5.5f, 0f, 0.4f), yaw = 0f },
        new Spec { name = "AlexTablet", label = "Tablet do Alex", at = new Vector3(5.5f, 0f, 1.7f), yaw = 90f, stand = 0.85f },
        new Spec { name = "SecurityPanel", label = "Painel de segurança", at = new Vector3(5.5f, 0f, 3.0f), yaw = -90f, stand = 1.2f },
        new Spec { name = "Dehumidifier", label = "Desumidificador", at = new Vector3(5.7f, 0f, 4.4f), yaw = -90f },
        new Spec { name = "AtticLadder", label = "Escada do sótão", at = new Vector3(5.8f, 0f, 6.0f), yaw = -90f },
    };

    [MenuItem("Horror/Testes/Props novos na cena aberta")]
    private static void Menu()
    {
        Place();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void Place()
    {
        GameObject old = GameObject.Find(GroupName);
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }
        Transform group = new GameObject(GroupName).transform;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");

        foreach (Spec spec in Specs)
        {
            Vector3 floor = spec.at;
            if (spec.stand > 0f)
            {
                GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stand.name = spec.name + "_Stand";
                stand.transform.SetParent(group, false);
                stand.transform.localScale = new Vector3(0.3f, spec.stand, 0.3f);
                stand.transform.position = spec.at + Vector3.up * spec.stand * 0.5f;
                floor += Vector3.up * spec.stand;
            }

            GameObject prop = FloodedHouseBuilder.ModelProp(spec.name, group, floor, Quaternion.Euler(0f, spec.yaw, 0f));
            int triangles = 0;
            float top = floor.y;
            foreach (MeshFilter filter in prop.GetComponentsInChildren<MeshFilter>())
            {
                triangles += filter.sharedMesh.triangles.Length / 3;
            }
            foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
            {
                top = Mathf.Max(top, r.bounds.max.y);
            }

            TextMeshPro label = new GameObject(spec.name + "_Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(group, false);
            label.transform.position = new Vector3(spec.at.x, top + 0.2f, spec.at.z);
            label.font = font;
            label.text = spec.label + "\n<size=70%>" + triangles.ToString("N0") + " triângulos</size>";
            label.fontSize = 0.7f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = HudFactory.Ink;
            label.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            label.gameObject.AddComponent<FaceCamera>();
        }
    }
}
