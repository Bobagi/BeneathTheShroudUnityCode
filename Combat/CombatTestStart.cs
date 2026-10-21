using UnityEngine;

// Test scene only: the player starts with the dive torch found, its batteries in and the beam on, clipped to the
// suit's chest, so the revolver can be held in both hands from the first frame.
public sealed class CombatTestStart : MonoBehaviour
{
    [SerializeField]
    private ChestTorchMount mount;

    [SerializeField]
    private DiveGear gear;

    private void Start()
    {
        if (gear != null)
        {
            gear.GiveTorch();
            gear.GiveBatteries();
            gear.SendMessage("SetTorch", true, SendMessageOptions.DontRequireReceiver);
        }

        if (mount != null)
        {
            mount.Mount();
        }
    }
}
