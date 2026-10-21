using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// The inventory in the test scene (Gustavo, 06/10): the backpack on the player (Inventory + InventoryHudPanel), the
// item definitions (Prefabs/Items/Inventory, made here when missing) and a few pickups on the floor near the start,
// as placeholder shapes with a label (no free models good enough yet: rounds, a medkit, a bandage, batteries, a
// filter). Also called by the suit test generator.
public static class InventoryTestSetup
{
    private const string Folder = ProjectPaths.ItemPrefabs + "/Inventory";

    private struct Spec
    {
        public string id, name, description;
        public InventoryItem.Kind kind;
        public float amount;
        public int stack;
        public Color colour;
        public PrimitiveType shape;
        public Vector3 size;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { id = "ammo_revolver", name = "Balas de revólver", description = "6 balas. Usar passa para a reserva da arma.", kind = InventoryItem.Kind.Ammo, amount = 6f, stack = 5, colour = new Color(0.78f, 0.6f, 0.25f), shape = PrimitiveType.Cube, size = new Vector3(0.12f, 0.06f, 0.08f) },
        new Spec { id = "medkit", name = "Kit médico", description = "Cura toda a vida.", kind = InventoryItem.Kind.Heal, amount = 1f, stack = 3, colour = new Color(0.85f, 0.2f, 0.2f), shape = PrimitiveType.Cube, size = new Vector3(0.22f, 0.1f, 0.16f) },
        new Spec { id = "bandage", name = "Atadura", description = "Cura um terço da vida.", kind = InventoryItem.Kind.Heal, amount = 0.34f, stack = 5, colour = new Color(0.9f, 0.88f, 0.8f), shape = PrimitiveType.Cylinder, size = new Vector3(0.08f, 0.03f, 0.08f) },
        new Spec { id = "batteries", name = "Pilhas", description = "Pilhas para a lanterna.", kind = InventoryItem.Kind.Battery, amount = 1f, stack = 4, colour = new Color(0.2f, 0.45f, 0.8f), shape = PrimitiveType.Capsule, size = new Vector3(0.04f, 0.05f, 0.04f) },
        new Spec { id = "filter", name = "Filtro da máscara", description = "Um filtro novo para o respirador.", kind = InventoryItem.Kind.Filter, amount = 1f, stack = 4, colour = new Color(0.35f, 0.6f, 0.35f), shape = PrimitiveType.Cylinder, size = new Vector3(0.1f, 0.03f, 0.1f) },
    };

    [MenuItem("Horror/Testes/Inventário na cena aberta")]
    private static void Menu()
    {
        FirstPersonController controller = Object.FindAnyObjectByType<FirstPersonController>();
        if (controller == null)
        {
            Debug.LogWarning("[Inventory] Sem jogador na cena.");
            return;
        }
        Place(controller);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    public static void Place(FirstPersonController controller)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }
        GameObject player = controller.gameObject;
        DiveHud hud = Object.FindAnyObjectByType<DiveHud>();
        FirstPersonArms arms = controller.CameraPivot.GetComponentInChildren<FirstPersonArms>(true);
        Revolver revolver = controller.CameraPivot.GetComponentInChildren<Revolver>(true);
        Transform revolverObject = revolver != null ? revolver.transform : null;

        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory == null)
        {
            inventory = player.AddComponent<Inventory>();
        }
        SerializedObject so = new SerializedObject(inventory);
        so.FindProperty("hud").objectReferenceValue = hud;
        so.FindProperty("gear").objectReferenceValue = player.GetComponent<DiveGear>();
        so.FindProperty("air").objectReferenceValue = player.GetComponent<AirSupply>();
        so.FindProperty("respirator").objectReferenceValue = player.GetComponent<Respirator>();
        so.FindProperty("revolver").objectReferenceValue = revolver;
        so.FindProperty("chestMount").objectReferenceValue = player.GetComponent<ChestTorchMount>();
        so.FindProperty("arms").objectReferenceValue = arms;
        SerializedProperty tools = so.FindProperty("startingTools");
        tools.arraySize = revolverObject != null ? 2 : 1;
        tools.GetArrayElementAtIndex(0).enumValueIndex = (int)Inventory.Hand.Torch;
        if (revolverObject != null)
        {
            tools.GetArrayElementAtIndex(1).enumValueIndex = (int)Inventory.Hand.Revolver;
        }
        // The test scene starts with the gun already in the hands (Gustavo, 08/10); 1 takes the torch.
        so.FindProperty("startingHand").enumValueIndex = (int)(revolverObject != null ? Inventory.Hand.Revolver : Inventory.Hand.Torch);
        so.ApplyModifiedPropertiesWithoutUndo();
        // The inventory decides what is in the hands; the combat start's chest mount would fight it.
        CombatTestStart combatStart = player.GetComponent<CombatTestStart>();
        if (combatStart != null)
        {
            Object.DestroyImmediate(combatStart);
        }

        InventoryHudPanel panel = player.GetComponent<InventoryHudPanel>();
        if (panel == null)
        {
            panel = player.AddComponent<InventoryHudPanel>();
        }
        SerializedObject po = new SerializedObject(panel);
        po.FindProperty("inventory").objectReferenceValue = inventory;
        po.FindProperty("hud").objectReferenceValue = hud;
        po.FindProperty("controller").objectReferenceValue = controller;
        po.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        po.ApplyModifiedPropertiesWithoutUndo();

        GameObject old = GameObject.Find("InventoryTest_Items");
        if (old != null)
        {
            Object.DestroyImmediate(old);
        }
        Transform group = new GameObject("InventoryTest_Items").transform;
        // On the floor to the left of the start (the screen with the video is to the right), one of each, and a
        // second box of rounds to see the stacks add up.
        Vector3[] spots = { new Vector3(-1.2f, 0f, 0.4f), new Vector3(-1.6f, 0f, 0.9f), new Vector3(-1.2f, 0f, 1.4f), new Vector3(-1.7f, 0f, 1.9f), new Vector3(-1.1f, 0f, 2.3f), new Vector3(-0.8f, 0f, 0.9f) };
        int i = 0;
        foreach (Spec spec in Specs)
        {
            InventoryItem item = Definition(spec);
            Pickup(group, item, spec, spots[i++], 1);
        }
        Pickup(group, Definition(Specs[0]), Specs[0], spots[i], 2);
    }

    private static Material Placeholder(string name, Color colour)
    {
        string path = ProjectPaths.FloodedHouseMaterials + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", 0.35f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static InventoryItem Definition(Spec spec)
    {
        string path = Folder + "/" + spec.id + ".asset";
        InventoryItem item = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItem>();
            AssetDatabase.CreateAsset(item, path);
        }
        item.id = spec.id;
        item.displayName = spec.name;
        item.description = spec.description;
        item.kind = spec.kind;
        item.amount = spec.amount;
        item.stackMax = spec.stack;
        item.colour = spec.colour;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void Pickup(Transform group, InventoryItem item, Spec spec, Vector3 at, int count)
    {
        GameObject pickup = new GameObject("Pickup_" + spec.id + (count > 1 ? "_x" + count : ""));
        pickup.transform.SetParent(group, false);
        pickup.transform.position = at;
        GameObject shape = GameObject.CreatePrimitive(spec.shape);
        shape.name = "Shape";
        shape.transform.SetParent(pickup.transform, false);
        shape.transform.localPosition = new Vector3(0f, spec.size.y * 0.5f + 0.005f, 0f);
        shape.transform.localScale = spec.size;
        shape.GetComponent<Renderer>().sharedMaterial = Placeholder("Item_" + spec.id, spec.colour);
        // The collider is on the shape (Interactable reaches its collider); the pickup is the Interactable.
        ItemPickup component = pickup.AddComponent<ItemPickup>();
        component.Configure(item, count);
        TextMeshPro label = new GameObject("Label").AddComponent<TextMeshPro>();
        label.transform.SetParent(pickup.transform, false);
        label.transform.localPosition = new Vector3(0f, spec.size.y + 0.14f, 0f);
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");
        label.text = spec.name + (count > 1 ? " ×" + count : "");
        label.fontSize = 0.6f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = HudFactory.Ink;
        label.rectTransform.sizeDelta = new Vector2(2f, 0.3f);
        label.gameObject.AddComponent<FaceCamera>();
    }
}
