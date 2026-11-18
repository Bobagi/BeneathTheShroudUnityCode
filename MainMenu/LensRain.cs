using UnityEngine;
using UnityEngine.Rendering;

// Turns the lens rain (Art/Shaders/LensRain.shader, the "LensRain" full screen pass of the renderer) on for the camera
// it sits on, and only for it: the global is set right before each camera renders, so other cameras and scenes see 0
// and skip the effect.
[RequireComponent(typeof(Camera))]
public sealed class LensRain : MonoBehaviour
{
    private static readonly int RainId = Shader.PropertyToID("_LensRain");
    private static readonly int DensityId = Shader.PropertyToID("_LensRainDensity");

    [SerializeField]
    [Range(0f, 1f)]
    private float amount = 1f;

    [Tooltip("How many drops: 0 none, 1 the most (still few).")]
    [SerializeField]
    [Range(0f, 1f)]
    private float density = 0.6f;

    private Camera view;

    private void Awake()
    {
        view = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += Begin;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Begin;
        Shader.SetGlobalFloat(RainId, 0f);
    }

    private void Begin(ScriptableRenderContext context, Camera camera)
    {
        bool mine = camera == view;
        Shader.SetGlobalFloat(RainId, mine ? amount : 0f);
        Shader.SetGlobalFloat(DensityId, density);
    }
}
