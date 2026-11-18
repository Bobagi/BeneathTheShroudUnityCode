using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

// Combat in the demo (GDD, decisions of Gustavo 01/10: the magnum is in the demo and shows up early, six rounds and no
// spare; the small creatures can be killed; 09/10: the demo ends in the hiding scene).
//   The gun: Erick arrives with the Colt Python holstered (Inventory: 2 draws it, 1 the torch once found, I the
//   backpack; CombatTestSetup.ArmStory hangs it on the first person rig with the torch's chest mount).
//   The Drowned of the house (DrownedEnemyBuilder's prefab, killable, DrownedBrain): where they stand is a Claude
//   proposal from the GDD's "Sugestão de localização de inimigos" (the drawing marks fungus, worms and flora, not
//   the Drowned), registered in the GDD:
//     LaundryDrowned: in the laundry by the broken wall, once the generator room is done (on the way down to the
//       basement: the only room the story must cross again; it came in through the hole the first one broke).
//     PowerRoomDrowned: in the power room off the courtyard gallery, from the blackout on, in the dark among the
//       switchboards; an optional room, with a spare filter and a bandage behind it (the GDD's "exploração
//       recompensada").
//   The finale (DemoFinale): the stair grab and the hiding in the laundry, with its own two Drowned.
// Called at the end of BuildMansionStory.
public static partial class FloodedHouseBuilder
{
    private const string HouseItemsFolder = ProjectPaths.ItemPrefabs + "/Inventory";

    private static void BuildCombat(PlayerRig rig, DiveHud hud, FloodedHouseDirector director, DreadDirector dread, LightningStorm storm, AudioSource houseAudio)
    {
        FirstPersonController controller = rig.controller;
        GameObject player = controller.gameObject;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ProjectPaths.Fonts + "/SpecialElite SDF.asset");

        // ---------------- the gun and the backpack
        Revolver revolver = CombatTestSetup.ArmStory(controller);
        FirstPersonArms arms = controller.CameraPivot.GetComponentInChildren<FirstPersonArms>(true);
        Inventory inventory = player.GetComponent<Inventory>() ?? player.AddComponent<Inventory>();
        SetField(inventory, "hud", hud);
        SetField(inventory, "gear", rig.gear);
        SetField(inventory, "air", rig.air);
        SetField(inventory, "respirator", rig.respirator);
        SetField(inventory, "revolver", revolver);
        SetField(inventory, "chestMount", player.GetComponent<ChestTorchMount>());
        SetField(inventory, "arms", arms);
        SerializedObject inventorySo = new SerializedObject(inventory);
        SerializedProperty startingTools = inventorySo.FindProperty("startingTools");
        startingTools.arraySize = 1;
        startingTools.GetArrayElementAtIndex(0).enumValueIndex = (int)Inventory.Hand.Revolver;
        inventorySo.FindProperty("startingHand").enumValueIndex = (int)Inventory.Hand.None;
        inventorySo.ApplyModifiedPropertiesWithoutUndo();
        InventoryHudPanel panel = player.GetComponent<InventoryHudPanel>() ?? player.AddComponent<InventoryHudPanel>();
        SetField(panel, "inventory", inventory);
        SetField(panel, "hud", hud);
        SetField(panel, "controller", controller);
        SetField(panel, "font", font);

        // ---------------- the Drowned of the house
        Transform enemies = Group("Enemies");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrownedEnemyBuilder.PrefabPath);
        if (prefab == null)
        {
            prefab = DrownedEnemyBuilder.Build();
        }

        DrownedBrain laundry = Drowned(prefab, enemies, "LaundryDrowned", M(13.9f, 0f, 17.3f), M(10.5f, 0f, 20.7f));
        DrownedBrain powerRoom = Drowned(prefab, enemies, "PowerRoomDrowned", M(7.2f, 0f, 23.7f), M(7.5f, 0f, 21.4f));
        laundry.gameObject.SetActive(false);
        powerRoom.gameObject.SetActive(false);

        // What the power room guards: a spare filter and a bandage (the first aid tin, Meshy 10/11, 4 k triangles).
        Transform rewards = Group("PowerRoomRewards", enemies);
        FilterPickup("Filter4", rewards, M(9.6f, 0f, 24.2f), "filtro_4");
        InventoryItem bandage = AssetDatabase.LoadAssetAtPath<InventoryItem>(HouseItemsFolder + "/bandage.asset");
        if (bandage != null)
        {
            HouseItemPickup(rewards, bandage, M(9.2f, 0f, 24.1f), new Color(0.9f, 0.88f, 0.8f), PrimitiveType.Cylinder, new Vector3(0.08f, 0.03f, 0.08f), "Bandage");
        }

        // ---------------- the finale: the stair grab and the hiding in the laundry
        Transform finaleGroup = Group("Finale", enemies);
        DrownedBrain searcher = Drowned(prefab, finaleGroup, "FinaleDrowned", M(15.7f, LunaManor.GardenY, 17.9f), M(13f, 0f, 19f));
        searcher.gameObject.SetActive(false);
        // From the hole, past the stair's enclosure, round the room and back; the third point is two metres from the
        // hiding place with nothing between (if the lightning comes then, it is on him at once).
        Transform entry = Marker("Entry", finaleGroup, M(15.7f, LunaManor.GardenY, 17.9f), 270f);
        Transform[] path =
        {
            Marker("Search1", finaleGroup, M(13.4f, 0f, 17.6f), 0f),
            Marker("Search2", finaleGroup, M(11.2f, 0f, 20.8f), 0f),
            Marker("Search3", finaleGroup, M(12.4f, 0f, 22.6f), 0f),
            Marker("Search4", finaleGroup, M(13.2f, 0f, 20.0f), 0f),
            Marker("Search5", finaleGroup, M(11.0f, 0f, 22.9f), 0f),
            Marker("Search6", finaleGroup, M(12.8f, 0f, 18.8f), 0f),
        };
        // Crouched between the washing machines and the smashed dehumidifier, back to the east wall, facing the room
        // (west-south-west): the machines hide him from the hole, the whole room is in front when he rises.
        Transform hideSpot = Marker("HideSpot", finaleGroup, M(14.35f, 0f, 22.2f), 255f);
        // Alex's box of .357 rounds on the floor in front of the hiding place (model 10/11, 999 triangles): where the
        // loose rounds of the finale come from (DemoFinale.looseRounds), seen when he crouches and opens the cylinder.
        ModelProp("AmmoBox357", finaleGroup, hideSpot.position + hideSpot.forward * 0.42f + hideSpot.right * 0.22f,
            Quaternion.Euler(0f, hideSpot.eulerAngles.y + 25f, 0f), false);

        DrownedBrain grabber = Drowned(prefab, finaleGroup, "GrabDrowned", new Vector3(-5.4f, LandingY, -3.2f), new Vector3(-5.4f, LandingY, 0f));
        grabber.gameObject.SetActive(false);
        // At the foot of the stair, right behind the player's heels two steps up.
        Transform grabStart = Marker("GrabStart", finaleGroup, new Vector3(-5.4f, LandingY, -3.0f), 0f);
        Transform grabWater = Marker("GrabWater", finaleGroup, new Vector3(-4.0f, LandingY - 1.5f, -4.6f), 0f);

        // Red emergency lights of the laundry, off until the finale.
        Light[] emergency =
        {
            PointOrSpot("EmergencyLaundry1", finaleGroup, M(12.6f, GC - 0.3f, 22.6f), LightType.Point, new Color(1f, 0.15f, 0.08f), 0f, 7f),
            PointOrSpot("EmergencyLaundry2", finaleGroup, M(13.2f, GC - 0.3f, 17.8f), LightType.Point, new Color(1f, 0.15f, 0.08f), 0f, 7f),
        };
        foreach (Light l in emergency)
        {
            l.enabled = false;
        }

        AudioSource hum = finaleGroup.gameObject.AddComponent<AudioSource>();
        hum.playOnAwake = false;
        hum.loop = true;
        hum.spatialBlend = 0f;
        hum.clip = ProceduralAudio.ElectricHumLoop();

        DemoFinale finale = finaleGroup.gameObject.AddComponent<DemoFinale>();
        SetField(finale, "player", controller);
        SetField(finale, "air", rig.air);
        SetField(finale, "gear", rig.gear);
        SetField(finale, "hud", hud);
        SetField(finale, "inventory", inventory);
        SetField(finale, "revolver", revolver);
        SetField(finale, "arms", arms);
        SetField(finale, "storm", storm);
        SetField(finale, "dread", dread);
        SetField(finale, "houseAudio", houseAudio);
        SetField(finale, "searcher", searcher);
        SetField(finale, "entry", entry);
        SetField(finale, "searchPath", path);
        SetField(finale, "grabber", grabber);
        SetField(finale, "grabStart", grabStart);
        SetField(finale, "grabWater", grabWater);
        SetField(finale, "hideSpot", hideSpot);
        SetField(finale, "emergencyLights", emergency);
        SetField(finale, "hum", hum);

        SetField(director, "inventory", inventory);
        SetField(director, "revolver", revolver);
        SetField(director, "finale", finale);
        SetField(director, "laundryDrowned", laundry);
        SetField(director, "powerRoomDrowned", powerRoom);
        // Half way up the basement stair (scene coordinates: the stair runs from z -2.24 at the landing to 1.0).
        SetField(director, "stairGrabZone", Zone(new Vector3(-6.2f, LandingY - 0.3f, -1.75f), new Vector3(-4.6f, 0.6f, -0.4f)));
    }

    // One Drowned of the prefab (the link kept), on its feet at `at`, facing `toward`.
    private static DrownedBrain Drowned(GameObject prefab, Transform parent, string name, Vector3 at, Vector3 toward)
    {
        GameObject drowned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        drowned.name = name;
        Vector3 face = toward - at;
        face.y = 0f;
        drowned.transform.SetPositionAndRotation(at, face.sqrMagnitude > 0.001f ? Quaternion.LookRotation(face, Vector3.up) : Quaternion.identity);
        return drowned.GetComponent<DrownedBrain>();
    }

    // A backpack item lying in the house, as a plain shape until a model comes (ItemPickup: E puts it in the backpack).
    // `model` (a Props/<name> FBX) replaces the placeholder shape when it exists.
    private static void HouseItemPickup(Transform parent, InventoryItem item, Vector3 at, Color colour, PrimitiveType shape, Vector3 size, string model = null)
    {
        GameObject pickup = new GameObject("Pickup_" + item.id);
        pickup.transform.SetParent(parent, false);
        pickup.transform.position = at;
        GameObject visual;
        if (model != null && AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PropModels + "/" + model + "/" + model + ".fbx") != null)
        {
            visual = ModelProp(model, pickup.transform, at, Quaternion.Euler(0f, 30f, 0f));
            visual.name = "Shape";
        }
        else
        {
            visual = GameObject.CreatePrimitive(shape);
            visual.name = "Shape";
            visual.transform.SetParent(pickup.transform, false);
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f + 0.005f, 0f);
            visual.transform.localScale = size;
            visual.GetComponent<Renderer>().sharedMaterial = Plain("Item_" + item.id, colour, 0.3f, 0f);
        }

        GlowShells(visual);
        ItemPickup component = pickup.AddComponent<ItemPickup>();
        component.Configure(item, 1);
        SetField(component, "pickupClip", AssetDatabase.LoadAssetAtPath<AudioClip>(ProjectPaths.Audio + "/Handling/Pickup_Metal.ogg"));
    }
}
