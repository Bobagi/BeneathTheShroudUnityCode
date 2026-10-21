using UnityEngine;

[DisallowMultipleComponent]
public sealed class GlobalSlowMotionTimeController : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)]
    private float globalTimeScaleSliderValue = 0f;

    [SerializeField, Min(0f)]
    private float physicsFixedDeltaTimeAtNormalSpeed = 0.02f;

    private float lastAppliedGlobalTimeScaleSliderValue = -1f;

    private void OnEnable()
    {
        ApplyGlobalTimeScaleFromInspector();
    }

    private void OnDisable()
    {
        ApplyGlobalTimeScale(1f);
    }

    private void OnValidate()
    {
        ApplyGlobalTimeScaleFromInspector();
    }

    private void Update()
    {
        ApplyGlobalTimeScaleFromInspector();
    }

    private void ApplyGlobalTimeScaleFromInspector()
    {
        if (Mathf.Approximately(lastAppliedGlobalTimeScaleSliderValue, globalTimeScaleSliderValue))
        {
            return;
        }

        lastAppliedGlobalTimeScaleSliderValue = globalTimeScaleSliderValue;
        ApplyGlobalTimeScale(globalTimeScaleSliderValue);
    }

    private void ApplyGlobalTimeScale(float globalTimeScale)
    {
        Time.timeScale = Mathf.Clamp01(globalTimeScale);
        Time.fixedDeltaTime = physicsFixedDeltaTimeAtNormalSpeed * Time.timeScale;
    }
}
