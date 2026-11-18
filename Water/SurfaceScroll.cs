using UnityEngine;

// Slides a water surface's ripple maps so the water looks alive. Uses a property block, so the
// shared material asset is never modified in Play Mode.
[RequireComponent(typeof(Renderer))]
public sealed class SurfaceScroll : MonoBehaviour
{
    [SerializeField]
    private Vector2 speed = new Vector2(0.015f, 0.01f);

    [SerializeField]
    private float tiling = 1f;

    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private Renderer target;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        Vector2 offset = speed * Time.time;
        target.GetPropertyBlock(block);
        block.SetVector(BaseMapStId, new Vector4(tiling, tiling, offset.x, offset.y));
        target.SetPropertyBlock(block);
    }
}
