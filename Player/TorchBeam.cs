using UnityEngine;

// The visible beam right out of the lens (see Art/Shaders/TorchBeam.shader): a cone mesh built once on the
// torch light, on while the torch is on, stronger in water (suspended silt) than in air (dust), following
// the light's colour, opening and current output (DiveGear dims it near walls). Numbers in GameTuning.
[RequireComponent(typeof(Light))]
public sealed class TorchBeam : MonoBehaviour
{
    [SerializeField]
    private Material beamMaterial;

    private const int Segments = 28;
    private const float LensRadius = 0.03f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int LengthId = Shader.PropertyToID("_Length");
    private static readonly int TanId = Shader.PropertyToID("_Tan");

    private Light source;
    private MeshRenderer beamRenderer;
    private MaterialPropertyBlock block;
    private Mesh mesh;
    private float builtLength = -1f;
    private float builtTan = -1f;
    private float strength;

    private void Awake()
    {
        source = GetComponent<Light>();
        GameObject beam = new GameObject("Beam");
        beam.transform.SetParent(transform, false);
        beam.AddComponent<MeshFilter>().sharedMesh = mesh = new Mesh { name = "TorchBeamCone" };
        beamRenderer = beam.AddComponent<MeshRenderer>();
        beamRenderer.sharedMaterial = beamMaterial;
        beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beamRenderer.receiveShadows = false;
        block = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    private void LateUpdate()
    {
        GameTuning tuning = GameTuning.Instance;
        bool on = source.enabled && beamMaterial != null;
        if (beamRenderer.enabled != on)
        {
            beamRenderer.enabled = on;
        }

        if (!on)
        {
            return;
        }

        // Matches the lit spot the beam lands on (the outer cone), its soft edge fading before the spill.
        float tan = Mathf.Tan(source.spotAngle * 0.5f * Mathf.Deg2Rad) * 0.72f;
        if (!Mathf.Approximately(tan, builtTan) || !Mathf.Approximately(tuning.torchBeamLength, builtLength))
        {
            BuildCone(tuning.torchBeamLength, tan);
        }

        bool underwater = WaterVolume.IsUnderwater(transform.position);
        float target = (underwater ? tuning.torchBeamWater : tuning.torchBeamAir) * source.intensity / Mathf.Max(0.01f, tuning.torchIntensity);
        strength = Mathf.MoveTowards(strength, target, Time.deltaTime * 3f);
        block.SetColor(ColorId, source.color);
        block.SetFloat(StrengthId, strength);
        block.SetFloat(LengthId, builtLength);
        block.SetFloat(TanId, builtTan);
        beamRenderer.SetPropertyBlock(block);
    }

    // A closed truncated cone: as wide as the lens at the glass, opening towards +Z, capped at both ends so
    // every pixel of the beam has a back face to march from.
    private void BuildCone(float length, float tan)
    {
        builtLength = length;
        builtTan = tan;
        float near = LensRadius;
        float far = LensRadius + length * tan;
        Vector3[] vertices = new Vector3[Segments * 2 + 2];
        int[] triangles = new int[Segments * 12];
        vertices[Segments * 2] = Vector3.zero;
        vertices[Segments * 2 + 1] = new Vector3(0f, 0f, length);
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            vertices[i] = dir * near;
            vertices[Segments + i] = dir * far + Vector3.forward * length;
            int n = (i + 1) % Segments;
            int t = i * 12;
            // Side.
            triangles[t] = i; triangles[t + 1] = Segments + i; triangles[t + 2] = Segments + n;
            triangles[t + 3] = i; triangles[t + 4] = Segments + n; triangles[t + 5] = n;
            // Lens cap and far cap.
            triangles[t + 6] = Segments * 2; triangles[t + 7] = i; triangles[t + 8] = n;
            triangles[t + 9] = Segments * 2 + 1; triangles[t + 10] = Segments + n; triangles[t + 11] = Segments + i;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }
}
