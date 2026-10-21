using UnityEngine;

// Keeps the sea out of an open boat: hands StormSea.shader the hull's space every frame (_HullMask), and the shader
// skips the water inside the hull below the gunwale, in colour, depth and normals. The waves are displaced on the
// GPU, so a raised freeboard alone never kept the crests from showing through the floor. Runs after WaveRider has
// moved the hull this frame. One boat at a time (a global).
[DefaultExecutionOrder(100)]
public sealed class HullWaterMask : MonoBehaviour
{
    private static readonly int HullMaskId = Shader.PropertyToID("_HullMask");
    private static readonly int HullMaskOnId = Shader.PropertyToID("_HullMaskOn");

    private void OnEnable()
    {
        Shader.SetGlobalFloat(HullMaskOnId, 1f);
        LateUpdate();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(HullMaskOnId, 0f);
    }

    private void LateUpdate()
    {
        Shader.SetGlobalMatrix(HullMaskId, transform.worldToLocalMatrix);
    }
}
