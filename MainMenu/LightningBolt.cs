using UnityEngine;

// A visible bolt for the storm's strikes (LightningStorm only flashes lights): on every strike a new jagged path
// from the cloud base down to the sea, somewhere across `spread` around this object, with a few forks; it shows as
// long as the flash lasts and with its strength (LightningStorm.Flashed), so it flickers with the light. Drawn by
// line renderers with an additive HDR material: the bloom gives the glow. For far skies (hundreds of metres out).
public sealed class LightningBolt : MonoBehaviour
{
    [SerializeField]
    private Material material;

    [SerializeField]
    private Color colour = new Color(0.75f, 0.85f, 1f);

    [SerializeField]
    private float intensity = 14f;

    [SerializeField]
    private float height = 420f;

    [SerializeField]
    private float spread = 220f;

    [SerializeField]
    private float width = 3.2f;

    [SerializeField]
    private int segments = 34;

    [SerializeField]
    private int forks = 4;

    [Tooltip("Share of strikes that show a bolt here (the others are behind the clouds).")]
    [SerializeField]
    [Range(0f, 1f)]
    private float showChance = 0.8f;

    private LineRenderer trunk;
    private LineRenderer[] branches;
    private bool visible;

    private void Awake()
    {
        trunk = Line("Trunk", width);
        branches = new LineRenderer[forks];
        for (int i = 0; i < forks; i++)
        {
            branches[i] = Line("Fork" + i, width * 0.45f);
        }

        SetAlpha(0f);
    }

    private void OnEnable()
    {
        LightningStorm.Struck += OnStruck;
        LightningStorm.Flashed += OnFlashed;
    }

    private void OnDisable()
    {
        LightningStorm.Struck -= OnStruck;
        LightningStorm.Flashed -= OnFlashed;
    }

    private void OnStruck(bool close)
    {
        visible = Random.value < showChance;
        if (!visible)
        {
            return;
        }

        Vector3 ground = transform.position + transform.right * Random.Range(-spread, spread) * 0.5f;
        Vector3 top = ground + Vector3.up * height + transform.right * Random.Range(-0.25f, 0.25f) * height;
        Vector3[] path = Jagged(top, ground, segments, height * 0.035f);
        trunk.positionCount = path.Length;
        trunk.SetPositions(path);
        for (int i = 0; i < branches.Length; i++)
        {
            // A fork leaves the trunk in its upper two thirds and dies out partway down.
            int from = Random.Range(2, Mathf.Max(3, segments * 2 / 3));
            Vector3 start = path[from];
            float length = height * Random.Range(0.12f, 0.35f);
            Vector3 dir = (Vector3.down + transform.right * Random.Range(-1.2f, 1.2f)).normalized;
            Vector3[] fork = Jagged(start, start + dir * length, segments / 3, length * 0.06f);
            branches[i].positionCount = fork.Length;
            branches[i].SetPositions(fork);
            branches[i].widthMultiplier = width * Random.Range(0.25f, 0.5f);
        }
    }

    private void OnFlashed(float amount)
    {
        SetAlpha(visible ? amount : 0f);
    }

    private void SetAlpha(float amount)
    {
        Color c = colour * intensity * amount;
        c.a = amount;
        foreach (LineRenderer line in AllLines())
        {
            line.enabled = amount > 0.001f;
            line.startColor = c;
            line.endColor = c * 0.6f;
        }
    }

    private System.Collections.Generic.IEnumerable<LineRenderer> AllLines()
    {
        yield return trunk;
        foreach (LineRenderer b in branches)
        {
            yield return b;
        }
    }

    // A path from a to b broken into steps, each pushed sideways at random (more in the middle), so it zigzags.
    private Vector3[] Jagged(Vector3 a, Vector3 b, int steps, float jitter)
    {
        steps = Mathf.Max(2, steps);
        Vector3[] points = new Vector3[steps + 1];
        Vector3 side = transform.right;
        Vector3 depth = transform.forward;
        float drift = 0f;
        for (int i = 0; i <= steps; i++)
        {
            float k = i / (float)steps;
            drift += Random.Range(-1f, 1f) * jitter;
            drift *= 0.82f;
            Vector3 p = Vector3.Lerp(a, b, k);
            float taper = Mathf.Sin(k * Mathf.PI) + 0.15f;
            p += side * (drift + Random.Range(-1f, 1f) * jitter * 0.6f) * taper + depth * Random.Range(-1f, 1f) * jitter * 0.3f;
            points[i] = i == 0 ? a : (i == steps ? b : p);
        }

        return points;
    }

    private LineRenderer Line(string name, float lineWidth)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = material;
        line.widthMultiplier = lineWidth;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f));
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        return line;
    }
}
