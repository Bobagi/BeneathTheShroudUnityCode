using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Adds the player's brightness setting (in stops) on top of each volume's authored exposure. Every
// screen shows darkness differently, so horror games ship this calibration (Frictional's advice).
// Runs after DreadDirector, which swaps the house volume to a runtime copy of its profile.
[DefaultExecutionOrder(200)]
public sealed class BrightnessApplier : MonoBehaviour
{
    [SerializeField]
    private Volume[] volumes;

    private ColorAdjustments[] grades;
    private float[] baseExposure;

    private void Start()
    {
        grades = new ColorAdjustments[volumes.Length];
        baseExposure = new float[volumes.Length];
        for (int i = 0; i < volumes.Length; i++)
        {
            // volume.profile returns (and creates, if needed) a runtime copy: the asset stays untouched.
            if (volumes[i] != null && volumes[i].profile.TryGet(out ColorAdjustments grade))
            {
                grades[i] = grade;
                baseExposure[i] = grade.postExposure.value;
            }
        }

        GameSettings.Changed += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= Apply;
    }

    private void Apply()
    {
        for (int i = 0; i < grades.Length; i++)
        {
            if (grades[i] != null)
            {
                grades[i].postExposure.Override(baseExposure[i] + GameSettings.Brightness);
            }
        }
    }
}
