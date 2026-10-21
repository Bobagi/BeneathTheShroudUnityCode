using UnityEditor;
using UnityEngine;

// Runs every battlefield builder in order and puts the player and the starting items back in the
// spawn bay of the fire trench.
public static class BattlefieldRebuild
{
    public static readonly Vector2 PlayerSpawn = new Vector2(-1f, 3f);

    [MenuItem("Horror/Battlefield/Rebuild Everything")]
    public static void RebuildAll()
    {
        BattlefieldAssetSetup.Setup();
        BattlefieldTerrainBuilder.BuildTerrains();
        BattlefieldTerrainBuilder.PaintTerrains();
        BattlefieldDressingBuilder.Build();
        BattlefieldAtmosphereBuilder.Build();
        PlaceSpawn();
        DynamicGI.UpdateEnvironment();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Battlefield] Rebuilt everything.");
    }

    [MenuItem("Horror/Battlefield/Place Spawn")]
    public static void PlaceSpawn()
    {
        GameObject player = GameObject.Find("PlayerArmature");
        player.transform.position = new Vector3(PlayerSpawn.x, BattlefieldLayout.InnerHeight(PlayerSpawn.x, PlayerSpawn.y) + 0.05f, PlayerSpawn.y);
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        GameObject crate = GameObject.Find("SpawnCrate");
        // The crate stands on the fire-step, so the items sit at a comfortable reaching height.
        float crateFloor = BattlefieldLayout.InnerHeight(2.2f, 3.0f) + BattlefieldLayout.FireStepHeight;
        if (crate != null)
        {
            crate.transform.SetPositionAndRotation(new Vector3(2.2f, crateFloor, 3.46f), Quaternion.identity);
        }

        GameObject lantern = GameObject.Find("Lantern");
        if (lantern != null)
        {
            lantern.transform.SetPositionAndRotation(new Vector3(1.85f, crateFloor + 0.47f, 3.46f), Quaternion.identity);
        }

        GameObject revolver = GameObject.Find("Revolver");
        if (revolver != null)
        {
            revolver.transform.SetPositionAndRotation(new Vector3(2.5f, crateFloor + 0.475f, 3.44f), Quaternion.Euler(0f, 200f, 90f));
        }

        Unity.Cinemachine.CinemachineThirdPersonFollow follow =
            GameObject.Find("PlayerFollowCamera").GetComponent<Unity.Cinemachine.CinemachineThirdPersonFollow>();
        follow.VerticalArmLength = 0.45f;
        follow.ShoulderOffset = new Vector3(1f, 0.25f, 0f);
        EditorUtility.SetDirty(follow);
        EditorUtility.SetDirty(player);
    }
}
