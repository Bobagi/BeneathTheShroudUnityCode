using UnityEditor;
using UnityEngine;

// The kitchen fitted like a kitchen (26/11, Gustavo: "falta móveis, principalmente na cozinha que deveria ser cheia de
// armários, ter uma ilha"; "os armários estão sem fundo"). Until now it was one 5 m run on the north wall in a room of
// 12 x 15 m. Laid out by the usual rules of a fitted kitchen (base units 60 cm deep and 90 high, wall units 45 to 60 cm
// over the worktop, at least 1.05 m of aisle round an island, sink, cooker and fridge within a few steps of each other):
//   north wall: the old run (sink, four units, wall units), the gas stove with an extractor hood over it;
//   south wall: a second run under the wide window, wall units either side of it;
//   the middle of the north half: an island of six units back to back, three bar stools on its south side;
//   east walls: three tall larder cupboards north of the dining room door, two units by the gallery door.
// The west wall stays as it was: the fridge, the wall worms and the hole the crab comes out of.
// Units: the "Old kitchen set" kit (Dayse's list, Sketchfab) through SourceAssets/Tools/kit_pieces.py, now with doors
// and flaps closed (--front-y auto) and the back panel the kit never had (--back): KitchenBase, KitchenSink,
// KitchenWall re-extracted, plus KitchenTall, KitchenBaseWhite, KitchenBaseRed, KitchenHood. BarStool: Poly Haven
// bar_chair_round_01 (CC0).
// `Horror > Flooded House > Cozinha: remontar` rebuilds only this group in the open scene (no house rebuild).
public static partial class FloodedHouseBuilder
{
    private const string KitchenFitName = "KitchenFit";

    [MenuItem("Horror/Flooded House/Cozinha: remontar")]
    private static void KitchenFitMenu()
    {
        GameObject furniture = GameObject.Find("Furniture");
        if (furniture == null)
        {
            Debug.LogWarning("[FloodedHouse] No Furniture group in the scene.");
            return;
        }

        // The re-extracted units have other bounds (flaps shut): the old boxes stood out 30 cm in front of them.
        foreach (Transform child in furniture.transform)
        {
            string n = child.name;
            if (n == "KitchenBase" || n == "KitchenSink" || n == "KitchenWall")
            {
                foreach (BoxCollider box in child.GetComponents<BoxCollider>())
                {
                    Object.DestroyImmediate(box);
                }

                Material material = ModelPropMaterial(ProjectPaths.PropModels + "/" + n, n);
                foreach (Renderer r in child.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = material;
                }

                FitCollider(child.gameObject);
            }
        }

        KitchenFit(furniture.transform);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(furniture.scene);
    }

    private static void KitchenFit(Transform furniture)
    {
        Transform old = furniture.Find(KitchenFitName);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Transform k = Group(KitchenFitName, furniture);
        float step = 1f / PlanScale;

        // ---------------- north wall: the hood over the story's gas stove (plan -17.3)
        Hung(k, "KitchenHood", V2(-17.3f, 16f), Vector2.down, 1.72f);

        // ---------------- south wall, under the wide window (plan x -16.2 to -13.8, sill at 0.9)
        float x = -17.8f;
        GameObject first = null, last = null;
        foreach (string unit in new[] { "KitchenBase", "KitchenBase", "KitchenBaseWhite", "KitchenBaseRed", "KitchenBase", "KitchenBase", "KitchenBase" })
        {
            float width = (unit == "KitchenBase" ? 1f : 0.6f) * step;
            GameObject placed = Against(k, unit, V2(x + width * 0.5f, 3.7f), Vector2.up, 0f);
            first = first ?? placed;
            last = placed;
            x += width;
        }

        Hung(k, "KitchenWall", V2(-17.8f + 0.5f * step, 3.7f), Vector2.up, 1.5f);
        Hung(k, "KitchenWall", V2(-17.8f + 1.5f * step, 3.7f), Vector2.up, 1.5f);
        Hung(k, "KitchenWall", V2(x - 0.5f * step, 3.7f), Vector2.up, 1.5f);
        On(k, "OldPot", first, 0.1f, 30f);
        On(k, "WickerBasket", last, -0.1f, 15f);

        // ---------------- the island: two rows of three units back to back, under the north pendant
        Vector3 centre = M(V2(-15f, 12.9f), 0f);
        GameObject islandMid = null, islandEnd = null;
        for (int i = -1; i <= 1; i++)
        {
            GameObject north = ModelProp("KitchenBase", k, centre + new Vector3(i, 0f, 0.31f), Quaternion.identity);
            GameObject south = ModelProp(i == 0 ? "KitchenSink" : "KitchenBase", k, centre + new Vector3(i, 0f, -0.31f), Quaternion.Euler(0f, 180f, 0f));
            if (i == 0)
            {
                islandMid = north;
            }

            if (i == 1)
            {
                islandEnd = south;
            }
        }

        On(k, "CuttingBoard", islandMid, -0.2f, 20f);
        On(k, "BrassPan", islandMid, 0.25f, 110f);
        On(k, "Cutlery", islandEnd, 0.1f, 70f);
        On(k, "MedievalPlate", islandEnd, -0.2f);
        // Stools at the island's north side (the sink is on the south): 0.75 m to the seat's top rail.
        for (int i = -1; i <= 1; i++)
        {
            GameObject stool = ModelProp("BarStool", k, centre + new Vector3(i * 0.9f + 0.05f * i * i, 0f, 0.62f + 0.38f), Quaternion.Euler(0f, 180f + 25f * i, 0f));
            stool.transform.localScale *= 0.75f;
            stool.transform.position += Vector3.up * (centre.y - RendererBounds(stool).min.y);
            foreach (BoxCollider box in stool.GetComponents<BoxCollider>())
            {
                Object.DestroyImmediate(box);
            }

            FitCollider(stool);
        }

        // ---------------- east walls: larder cupboards north of the dining room door, units by the gallery door
        float tall = 0.6f * step;
        for (int i = 0; i < 3; i++)
        {
            Against(k, "KitchenTall", V2(-11.6f, 7.36f + tall * i), Vector2.left, 0f);
        }

        GameObject side = Against(k, "KitchenBase", V2(-9.7f, 13.9f), Vector2.left, 0f);
        Against(k, "KitchenBase", V2(-9.7f, 13.9f + step), Vector2.left, 0f);
        On(k, "EnamelPot", side, 0f, 40f);
        Model(k, "WoodenCrate2", V2(-18.3f, 6f), 70f, 0f);
    }
}
