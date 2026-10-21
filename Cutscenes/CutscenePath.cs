using UnityEngine;

// A smooth path through a few points (Catmull-Rom), walked at an even speed: Evaluate(0.5) is halfway along
// its length, not halfway through its points. Cutscene cues move boats, people and cameras along it
// (CutsceneCue, kind Move). Points are in world space; the scene builder places them.
public sealed class CutscenePath : MonoBehaviour
{
    public Vector3[] points = new Vector3[0];

    private const int SamplesPerSegment = 24;

    private float[] lengths;
    private float total;

    public float Length
    {
        get
        {
            Measure();
            return total;
        }
    }

    // Position at fraction t of the length.
    public Vector3 Evaluate(float t)
    {
        Measure();
        if (points.Length == 0)
        {
            return transform.position;
        }

        if (points.Length == 1 || total <= 0f)
        {
            return points[0];
        }

        float wanted = Mathf.Clamp01(t) * total;
        int last = lengths.Length - 1;
        int lo = 0;
        int hi = last;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (lengths[mid] < wanted)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        int i = Mathf.Max(1, lo);
        float span = lengths[i] - lengths[i - 1];
        float k = span > 1e-5f ? (wanted - lengths[i - 1]) / span : 0f;
        return Vector3.Lerp(Sample((i - 1) / (float)SamplesPerSegment), Sample(i / (float)SamplesPerSegment), k);
    }

    // Direction of travel at fraction t.
    public Vector3 Tangent(float t)
    {
        float step = 0.5f / Mathf.Max(1f, Length);
        Vector3 d = Evaluate(Mathf.Min(1f, t + step)) - Evaluate(Mathf.Max(0f, t - step));
        return d.sqrMagnitude > 1e-8f ? d.normalized : transform.forward;
    }

    private void OnValidate()
    {
        lengths = null;
    }

    private void Measure()
    {
        int segments = Mathf.Max(0, points.Length - 1);
        int count = segments * SamplesPerSegment + 1;
        if (lengths != null && lengths.Length == count)
        {
            return;
        }

        lengths = new float[Mathf.Max(1, count)];
        total = 0f;
        Vector3 previous = Sample(0f);
        for (int s = 1; s < count; s++)
        {
            Vector3 p = Sample(s / (float)SamplesPerSegment);
            total += Vector3.Distance(previous, p);
            lengths[s] = total;
            previous = p;
        }
    }

    // u runs from 0 to the number of segments.
    private Vector3 Sample(float u)
    {
        int segments = points.Length - 1;
        int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, segments - 1);
        float t = u - i;
        Vector3 p0 = points[Mathf.Max(0, i - 1)];
        Vector3 p1 = points[i];
        Vector3 p2 = points[i + 1];
        Vector3 p3 = points[Mathf.Min(segments, i + 2)];
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        if (points.Length < 2)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.6f, 0.1f);
        Vector3 previous = Evaluate(0f);
        for (int i = 1; i <= 60; i++)
        {
            Vector3 p = Evaluate(i / 60f);
            Gizmos.DrawLine(previous, p);
            previous = p;
        }
    }
}
