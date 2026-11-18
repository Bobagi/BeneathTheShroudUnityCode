using UnityEngine;

// Feeds the sea's wave numbers from GameTuning into the StormSea material, in the editor too, so the
// swell can be tuned while looking at it. The waves themselves are computed on the GPU.
// A scene whose island is not the house's (the arrival cutscene: a whole island 90 m across) overrides only the
// island's shape, the centre and where the swell dies at the shore; its own material copy keeps the house's sea
// untouched. Wave heights, lengths and speed stay in GameTuning for every scene.
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public sealed class SeaWaves : MonoBehaviour
{
    private static readonly int IslandCenter = Shader.PropertyToID("_IslandCenter");
    private static readonly int ShoreRadius = Shader.PropertyToID("_ShoreRadius");
    private static readonly int OpenSeaRadius = Shader.PropertyToID("_OpenSeaRadius");
    private static readonly int SwellHeight = Shader.PropertyToID("_SwellHeight");
    private static readonly int SwellLength = Shader.PropertyToID("_SwellLength");
    private static readonly int ChopHeight = Shader.PropertyToID("_ChopHeight");
    private static readonly int WaveSpeed = Shader.PropertyToID("_WaveSpeed");

    [SerializeField]
    private bool overrideIsland;

    [SerializeField]
    private Vector2 islandCenter;

    [SerializeField]
    private float shoreRadius = 40f;

    [SerializeField]
    private float openSeaRadius = 120f;

    private void OnEnable()
    {
        GameTuning.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameTuning.Changed -= Apply;
    }

    public void OverrideIsland(Vector2 centre, float shore, float open)
    {
        overrideIsland = true;
        islandCenter = centre;
        shoreRadius = shore;
        openSeaRadius = open;
        Apply();
    }

    private void Apply()
    {
        Material material = GetComponent<MeshRenderer>().sharedMaterial;
        if (material == null)
        {
            return;
        }

        GameTuning tuning = GameTuning.Instance;
        Vector2 centre = overrideIsland ? islandCenter : tuning.islandCenter;
        float shore = overrideIsland ? shoreRadius : tuning.shoreRadius;
        float open = overrideIsland ? openSeaRadius : tuning.openSeaRadius;
        material.SetVector(IslandCenter, new Vector4(centre.x, centre.y, 0f, 0f));
        material.SetFloat(ShoreRadius, shore);
        material.SetFloat(OpenSeaRadius, Mathf.Max(shore + 1f, open));
        material.SetFloat(SwellHeight, tuning.swellHeight);
        material.SetFloat(SwellLength, Mathf.Max(1f, tuning.swellLength));
        material.SetFloat(ChopHeight, tuning.chopHeight);
        material.SetFloat(WaveSpeed, tuning.waveSpeed);
    }
}
