using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The house closes itself (Dayse on the build of 25/11, WhatsApp of 26/11: "as portas estão quase todas abertas, não
// tem muita graça explorar pq vc pode ir pra qualquer lugar... usar os fungos e as coisas do próprio cenário para ir
// bloqueando a passagem, aos poucos"; "os fungos podem ser maiores e mais espaçados pela casa, bloqueando entradas").
//   Growth barriers: the doorways of the rooms off the demo's path are grown shut with her own models, and each
//   kind gives way to something the story hands the player, so the house opens in three steps:
//     the hook (after the laundry)        the bedrooms and the bathroom of the upper west and north wings (blue fungus)
//     the suit (from the attic)           the library with the nest, and the vestibule out to the road (silk, blue)
//     the basement key (generator room)   the pantry, the china room and the power room (the yellow, damp fungus)
//   The rest of the doors that the plan's trim left swung open now start shut (unlocked): a door is something to open.
//   The leaves in a grown doorway stay open, held by the growth.
// What stays free from the start is the demo's own path: hall, dining and living rooms, kitchen, courtyard, the
// stairs, the galleries, the laundry behind its key.
public static partial class FloodedHouseBuilder
{
    private enum Growth { Blue, Yellow, Silk }

    private static void GrowthBarriers(Transform parent)
    {
        Transform g = Group("GrowthBarriers", parent);
        AudioClip tear = AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Creature/growth_tear.wav");
        // (name, plan point of the doorway's middle, the wall runs along x, floor, width and height of the opening in
        // metres of the plan, growth, item that clears it, message while it does not)
        (string name, Vector2 at, bool alongX, float floor, float width, float height, Growth kind, string item, string message)[] doors =
        {
            ("Solteiro1", new Vector2(-10f, 11.6f), false, UF, 1f, 2.1f, Growth.Blue, "gancho", "msg.growth_hook"),
            ("Solteiro2", new Vector2(-10f, 16.8f), false, UF, 1f, 2.1f, Growth.Blue, "gancho", "msg.growth_hook"),
            ("Banheiro1", new Vector2(-8.8f, 21.6f), true, UF, 1f, 2.1f, Growth.Blue, "gancho", "msg.growth_hook"),
            ("Casal1", new Vector2(-5f, 21.6f), true, UF, 1f, 2.1f, Growth.Blue, "gancho", "msg.growth_hook"),
            ("BibliotecaEstar", new Vector2(11.6f, 6.3f), false, 0f, 1f, 2.2f, Growth.Silk, "traje", "msg.growth_suit"),
            ("BibliotecaGaleria", new Vector2(10.1f, 10.4f), false, 0f, 1f, 2.2f, Growth.Silk, "traje", "msg.growth_suit"),
            ("Vestibulo", new Vector2(0.1f, 21.4f), true, 0f, LunaManor.BackDoorWidth, 2.5f, Growth.Blue, "traje", "msg.growth_suit"),
            ("Despensa", new Vector2(-11.9f, 16f), true, 0f, 1f, 2.1f, Growth.Yellow, "chave_porao", "msg.growth_damp"),
            ("Loucas", new Vector2(-7f, 21.4f), true, 0f, 1f, 2.1f, Growth.Yellow, "chave_porao", "msg.growth_damp"),
            ("Forca", new Vector2(7.5f, 21.4f), true, 0f, 1f, 2.1f, Growth.Yellow, "chave_porao", "msg.growth_damp"),
        };
        List<Vector3> grown = new List<Vector3>();
        foreach ((string name, Vector2 at, bool alongX, float floor, float width, float height, Growth kind, string item, string message) in doors)
        {
            if ((kind == Growth.Blue && !BlueFungusReady()) || (kind == Growth.Yellow && !HasModel("FungusYellowColony")) || (kind == Growth.Silk && !HasModel("SilkWallPatch")))
            {
                continue;
            }

            Vector3 foot = M(at.x, floor, at.y);
            GrowthBarrier(g, name, foot, alongX ? Vector3.forward : Vector3.right, width * PlanScale, height, kind, item, message, tear);
            grown.Add(foot);
        }

        Debug.Log("[FloodedHouse] growth barriers: " + grown.Count + "; doors shut: " + ShutFreeDoors(grown));
    }

    // One doorway grown shut: `foot` the middle of its threshold, `normal` the way through it.
    private static void GrowthBarrier(Transform parent, string name, Vector3 foot, Vector3 normal, float width, float height, Growth kind, string item, string message, AudioClip tear)
    {
        GameObject barrier = new GameObject("Growth_" + name);
        barrier.transform.SetParent(parent, false);
        barrier.transform.SetPositionAndRotation(foot, Quaternion.LookRotation(normal, Vector3.up));
        BoxCollider stop = barrier.AddComponent<BoxCollider>();
        stop.center = new Vector3(0f, height * 0.5f, 0f);
        stop.size = new Vector3(width + 0.3f, height, 0.5f);

        Transform growth = new GameObject("Growth").transform;
        growth.SetParent(barrier.transform, false);
        Vector3 side = Vector3.Cross(Vector3.up, normal).normalized;

        // What fills the opening behind the fungus: a dark wet skin from jamb to jamb.
        Material skin = Material("GrowthMembrane", Shader.Find("Universal Render Pipeline/Lit"));
        skin.SetColor("_BaseColor", kind == Growth.Silk ? new Color(0.09f, 0.02f, 0.025f) : kind == Growth.Yellow ? new Color(0.06f, 0.055f, 0.02f) : new Color(0.015f, 0.03f, 0.045f));
        skin.SetFloat("_Smoothness", 0.55f);
        EditorUtility.SetDirty(skin);
        Material skinOf = Material("GrowthMembrane" + kind, skin.shader);
        skinOf.CopyPropertiesFromMaterial(skin);
        EditorUtility.SetDirty(skinOf);
        GameObject membrane = Primitive(PrimitiveType.Cube, "Membrane", growth, foot + Vector3.up * (height * 0.5f), barrier.transform.rotation, new Vector3(width + 0.16f, height + 0.06f, 0.07f), skinOf);
        membrane.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        foreach (float face in new[] { 1f, -1f })
        {
            Vector3 outward = normal * face;
            Vector3 plane = foot + outward * 0.035f;
            switch (kind)
            {
                case Growth.Silk:
                {
                    // Her wall patch stretched over the whole opening and past the frame, then a smaller one across it.
                    foreach ((float scale, float up, float turn) in new[] { (1.18f, 0.5f, 0f), (0.7f, 0.62f, 70f) })
                    {
                        GameObject patch = ModelProp("SilkWallPatch", growth, plane, Quaternion.identity, false, false);
                        Bounds pb = RendererBounds(patch);
                        float wide = Mathf.Max(pb.size.x, pb.size.z), tall = pb.size.y;
                        float fit = Mathf.Max((width * scale) / Mathf.Max(wide, 0.01f), (height * scale) / Mathf.Max(tall, 0.01f));
                        patch.transform.localScale *= fit;
                        Seat(patch, plane + Vector3.up * (height * up), outward, Vector3.back, Vector3.up, Quaternion.AngleAxis(turn + Random.Range(-12f, 12f), outward) * Vector3.up, 0.01f);
                        NoCollidersNoShadows(patch, false);
                        DrySilk(patch);
                    }

                    break;
                }

                case Growth.Yellow:
                {
                    Material plaque = DayseFungusMaterial("FungusYellowColony", false);
                    Material bulbs = DayseFungusMaterial("FungusYellowCluster", false);
                    foreach ((float up, float off, float size) in new[] { (0.25f, -0.12f, 1.6f), (0.55f, 0.14f, 1.5f), (0.84f, -0.06f, 1.55f) })
                    {
                        GameObject colony = ModelProp("FungusYellowColony", growth, plane, Quaternion.identity, false, false);
                        colony.transform.localScale *= size * width / 1.25f / 0.9f;
                        Seat(colony, plane + Vector3.up * (height * up) + side * (off * width), outward, Vector3.back, Vector3.up, Quaternion.AngleAxis(Random.Range(-20f, 20f), outward) * Vector3.up, 0.05f);
                        NoCollidersNoShadows(colony, false);
                        foreach (Renderer r in colony.GetComponentsInChildren<Renderer>())
                        {
                            r.sharedMaterial = plaque;
                        }
                    }

                    if (HasModel("FungusYellowCluster"))
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            Vector3 p = plane + side * Random.Range(-0.6f, 0.6f) * width + Vector3.up * Random.Range(0.15f, 0.95f) * height;
                            GameObject cluster = ModelProp("FungusYellowCluster", growth, p, Quaternion.identity, false, false);
                            cluster.transform.localScale *= Random.Range(1.2f, 2f);
                            Seat(cluster, p, outward, Vector3.back, Vector3.up, Vector3.up, 0.02f);
                            NoCollidersNoShadows(cluster, false);
                            foreach (Renderer r in cluster.GetComponentsInChildren<Renderer>())
                            {
                                r.sharedMaterial = bulbs;
                            }
                        }
                    }

                    break;
                }

                default:
                {
                    Material plaque = DayseFungusMaterial("FungusBlueColony", true);
                    Material stalks = DayseFungusMaterial("FungusBlueCluster", true);
                    foreach ((float up, float off, float size) in new[] { (0.24f, 0.12f, 1.65f), (0.54f, -0.14f, 1.5f), (0.84f, 0.06f, 1.6f) })
                    {
                        GameObject colony = ModelProp("FungusBlueColony", growth, plane, Quaternion.identity, false, false);
                        colony.transform.localScale *= size * width / 1.25f / 0.9f;
                        Seat(colony, plane + Vector3.up * (height * up) + side * (off * width), outward, Vector3.back, Vector3.up, Quaternion.AngleAxis(Random.Range(-25f, 25f), outward) * Vector3.up, 0.06f);
                        NoCollidersNoShadows(colony, false);
                        foreach (Renderer r in colony.GetComponentsInChildren<Renderer>())
                        {
                            r.sharedMaterial = plaque;
                        }
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 p = plane + outward * 0.1f + side * Random.Range(-0.5f, 0.5f) * width + Vector3.up * Random.Range(0.12f, 0.92f) * height;
                        PlaceFungus(growth, BlueCluster, 0, p, outward, Random.Range(1.5f, 2.4f), Random.Range(0f, 360f), stalks);
                    }

                    break;
                }
            }
        }

        // The pieces were placed in the world; as children of `growth` they shrivel together towards the threshold.
        GrowthBarrier component = barrier.AddComponent<GrowthBarrier>();
        component.Configure(item, message, "prompt.clear_growth", growth, stop, tear);
    }

    // Every leaf the plan's trim left swung open, away from the grown doorways, starts shut (unlocked, E opens it).
    private static int ShutFreeDoors(List<Vector3> grown)
    {
        int shut = 0;
        foreach (Door door in root.GetComponentsInChildren<Door>(true))
        {
            SerializedObject so = new SerializedObject(door);
            SerializedProperty startOpen = so.FindProperty("startOpen");
            if (!startOpen.boolValue)
            {
                continue;
            }

            bool held = false;
            foreach (Vector3 at in grown)
            {
                Vector3 d = door.transform.position - at;
                held |= Mathf.Abs(d.y) < 1.5f && new Vector2(d.x, d.z).magnitude < 1.7f;
            }

            if (held)
            {
                continue;
            }

            door.transform.localRotation *= Quaternion.Euler(0f, -so.FindProperty("openAngle").floatValue, 0f);
            startOpen.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            shut++;
        }

        return shut;
    }
}
