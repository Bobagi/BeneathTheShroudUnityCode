using UnityEngine;

// A hull riding the storm sea: the same Gerstner sum the StormSea shader displaces the water with (Displace in
// StormSea.shader: swell toward the island, fading near its shore, plus two fixed wind chops), evaluated on the CPU
// at the bow, the stern and both sides, so the boat rises, pitches and rolls with the waves on screen. Sits on a
// child of the boat (the parent is moved along its course by the cutscene); only this child's height and tilt
// change, with some inertia, and the bow lifts a little with speed.
// The wave numbers are read from the sea's material, so whatever SeaWaves gave it, this follows.
// Matching what is drawn (03/10, "the boat does not follow the water"): the clock is the shader's (_Time.y is
// Time.time, not the level's time), and Gerstner waves also move the water sideways, so the height at a point is
// that of the grid point displaced onto it (found by stepping back by the sideways offset twice). Response is quick
// (a slow one left the boat a third of a second behind each crest).
public sealed class WaveRider : MonoBehaviour
{
    [SerializeField]
    private Renderer sea;

    [SerializeField]
    private float length = 4.2f;

    [SerializeField]
    private float beam = 1.6f;

    // A light open boat floats high: about 0.18 m of its 0.3 m keel in the water and ~0.45 m of side above it.
    // At +0.18 the waterline was 16 cm under the gunwale and the stern went under when the bow lifted (03/10).
    [Tooltip("How deep the hull sits: the waterline below the hull's origin (negative = the origin is above the water).")]
    [SerializeField]
    private float draft = -0.12f;

    [Tooltip("Degrees the bow lifts per metre per second of speed.")]
    [SerializeField]
    private float planing = 1.2f;

    [SerializeField]
    private float response = 9f;

    private static readonly int IslandCenterId = Shader.PropertyToID("_IslandCenter");
    private static readonly int ShoreRadiusId = Shader.PropertyToID("_ShoreRadius");
    private static readonly int OpenSeaRadiusId = Shader.PropertyToID("_OpenSeaRadius");
    private static readonly int SwellHeightId = Shader.PropertyToID("_SwellHeight");
    private static readonly int SwellLengthId = Shader.PropertyToID("_SwellLength");
    private static readonly int ChopHeightId = Shader.PropertyToID("_ChopHeight");
    private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
    private static readonly int WaveCId = Shader.PropertyToID("_WaveC");
    private static readonly int WaveDId = Shader.PropertyToID("_WaveD");
    private static readonly int ShoreChopId = Shader.PropertyToID("_ShoreChop");

    private Vector2 islandCenter;
    private float shoreRadius;
    private float openSeaRadius;
    private float swellHeight;
    private float swellLength;
    private float chopHeight;
    private float waveSpeed;
    private Vector4 waveC;
    private Vector4 waveD;
    private float shoreChop = 0.35f;
    private Vector3 lastParent;
    private float height;
    private float pitch;
    private float roll;

    private void OnEnable()
    {
        GameTuning.Changed += ReadSea;
        ReadSea();
        lastParent = transform.parent != null ? transform.parent.position : transform.position;
    }

    private void OnDisable()
    {
        GameTuning.Changed -= ReadSea;
    }

    private void ReadSea()
    {
        Material m = sea != null ? sea.sharedMaterial : null;
        if (m == null)
        {
            return;
        }

        Vector4 c = m.GetVector(IslandCenterId);
        islandCenter = new Vector2(c.x, c.y);
        shoreRadius = m.GetFloat(ShoreRadiusId);
        openSeaRadius = m.GetFloat(OpenSeaRadiusId);
        swellHeight = m.GetFloat(SwellHeightId);
        swellLength = Mathf.Max(1f, m.GetFloat(SwellLengthId));
        chopHeight = m.GetFloat(ChopHeightId);
        waveSpeed = m.GetFloat(WaveSpeedId);
        waveC = m.GetVector(WaveCId);
        waveD = m.GetVector(WaveDId);
        shoreChop = m.HasProperty(ShoreChopId) ? m.GetFloat(ShoreChopId) : 0.35f;
    }

    private void LateUpdate()
    {
        Transform boat = transform.parent;
        if (boat == null || sea == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        // The material can be changed by SeaWaves after this enabled: read it again now and then.
        if (Time.frameCount % 30 == 0)
        {
            ReadSea();
        }

        float time = Time.time;
        float seaLevel = sea.transform.position.y;
        Vector3 bow = boat.TransformPoint(0f, 0f, length * 0.5f);
        Vector3 stern = boat.TransformPoint(0f, 0f, -length * 0.5f);
        Vector3 port = boat.TransformPoint(-beam * 0.5f, 0f, 0f);
        Vector3 starboard = boat.TransformPoint(beam * 0.5f, 0f, 0f);
        float hBow = Height(bow, time);
        float hStern = Height(stern, time);
        float hPort = Height(port, time);
        float hStarboard = Height(starboard, time);

        float speed = (boat.position - lastParent).magnitude / dt;
        lastParent = boat.position;

        // Lifting the bow with speed turns the hull about its stern, not its middle (about the middle the stern
        // dropped by as much as the bow rose and went under): the origin rises by half the bow's lift.
        float lift = Mathf.Min(speed, 8f) * planing;
        float targetHeight = seaLevel + (hBow + hStern + hPort + hStarboard) * 0.25f - boat.position.y - draft
            + Mathf.Sin(lift * Mathf.Deg2Rad) * length * 0.5f;
        float targetPitch = -Mathf.Atan2(hBow - hStern, length) * Mathf.Rad2Deg - lift;
        // A positive turn about Z lifts the starboard side.
        float targetRoll = Mathf.Atan2(hStarboard - hPort, beam) * Mathf.Rad2Deg;
        float k = 1f - Mathf.Exp(-response * dt);
        height = Mathf.Lerp(height, targetHeight, k);
        pitch = Mathf.Lerp(pitch, targetPitch, k);
        roll = Mathf.Lerp(roll, targetRoll, k);
        transform.localPosition = new Vector3(0f, height, 0f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    // Height of the water above the sea's plane at a world point: the grid point that the waves carry onto it.
    public float Height(Vector3 world, float time)
    {
        Vector2 target = new Vector2(world.x, world.z);
        Vector2 grid = target;
        for (int i = 0; i < 2; i++)
        {
            Vector3 moved = Displace(grid, time);
            grid = target - new Vector2(moved.x, moved.z);
        }

        return Displace(grid, time).y;
    }

    // StormSea.shader, Displace: the offset of the grid point p (x, z sideways, y up).
    public Vector3 Displace(Vector2 p, float time)
    {
        Vector2 toIsland = islandCenter - p;
        float dist = toIsland.magnitude;
        Vector2 inward = toIsland / Mathf.Max(dist, 0.001f);
        float open = SmoothStep(shoreRadius, openSeaRadius, dist);
        float angle = Mathf.Atan2(toIsland.y, toIsland.x);
        float ring = dist + 3f * Mathf.Sin(3f * angle) + 1.6f * Mathf.Sin(7f * angle + 1.3f);
        float swell = swellHeight * 0.5f * open;
        float chop = chopHeight * 0.5f * Mathf.Lerp(shoreChop, 1f, open);
        Vector2 c = new Vector2(waveC.x, waveC.y).normalized;
        Vector2 d = new Vector2(waveD.x, waveD.y).normalized;
        return Wave(inward, swell, swellLength, -ring, time)
            + Wave(inward, swell * 0.45f, swellLength * 0.63f, -ring * 1.07f, time)
            + Wave(c, chop, waveC.w, Vector2.Dot(c, p), time)
            + Wave(d, chop * 0.5f, waveD.w, Vector2.Dot(d, p), time);
    }

    private Vector3 Wave(Vector2 direction, float amplitude, float wavelength, float along, float time)
    {
        if (wavelength <= 0f)
        {
            return Vector3.zero;
        }

        float k = 2f * Mathf.PI / wavelength;
        float c = Mathf.Sqrt(9.8f / k);
        float f = k * (along - c * time * waveSpeed);
        float steepness = Mathf.Min(amplitude * k, 0.85f);
        float a = steepness / k;
        float cos = Mathf.Cos(f);
        return new Vector3(direction.x * a * cos, a * Mathf.Sin(f), direction.y * a * cos);
    }

    private static float SmoothStep(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a));
        return t * t * (3f - 2f * t);
    }
}
