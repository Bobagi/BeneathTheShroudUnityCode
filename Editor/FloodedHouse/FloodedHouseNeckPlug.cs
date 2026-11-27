using UnityEditor;
using UnityEngine;

// The inside of the collar (26/11, Gustavo: looking down "vejo o corpo como se estivesse atrás dele, tudo clipado"):
// the first person body has no head drawn (shadow only) and no skin under the clothes, so the view straight down went
// through the collar into the hollow torso and showed the floor. A flat dark lid just under the collar, carried by the
// upper spine: inside the shirt it is simply dark. Together with the eye that moves forward as the head bows
// (GameTuning.eyeForward / lookDownEyeForward) and the body's own share of the dark vision light.
public static partial class FloodedHouseBuilder
{
    private const string NeckPlugName = "NeckPlug";

    [MenuItem("Horror/Flooded House/Corpo: tampa da gola")]
    private static void NeckPlugMenu()
    {
        PlayerBody body = Object.FindAnyObjectByType<PlayerBody>();
        if (body != null)
        {
            NeckPlug(body.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(body.gameObject.scene);
        }
    }

    internal static GameObject NeckPlug(GameObject bodyObject)
    {
        Animator animator = bodyObject.GetComponent<Animator>();
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        }

        if (neck == null)
        {
            neck = head.parent;
        }

        Transform old = chest.Find(NeckPlugName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject plug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plug.name = NeckPlugName;
        Object.DestroyImmediate(plug.GetComponent<Collider>());
        plug.transform.SetParent(chest, true);
        // 6 cm under the neck joint and 4 cm back from it (measured on the Erick body: the collar's ring), lying flat.
        Vector3 at = bodyObject.transform.InverseTransformPoint(neck.position) + new Vector3(0f, -0.06f, -0.04f);
        plug.transform.SetPositionAndRotation(bodyObject.transform.TransformPoint(at), bodyObject.transform.rotation);
        plug.transform.localScale = Vector3.one;
        Vector3 lossy = plug.transform.lossyScale;
        plug.transform.localScale = new Vector3(0.22f / lossy.x, 0.002f / lossy.y, 0.17f / lossy.z);

        string path = ProjectPaths.FloodedHouseMaterials + "/NeckPlug.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(0.012f, 0.01f, 0.01f));
            AssetDatabase.CreateAsset(material, path);
        }

        MeshRenderer renderer = plug.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.renderingLayerMask = 2;
        return plug;
    }
}
