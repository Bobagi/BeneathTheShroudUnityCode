using System.Collections.Generic;
using UnityEngine;

// Shape of the battlefield shared by the editor builder and runtime systems: where the trenches
// run, where the shell craters are and the rolling ground height. World origin = middle of the
// British fire trench; +Z points toward no man's land and the enemy.
public static class BattlefieldLayout
{
    // Chosen so the inner terrain's edge lands exactly on vertices of the outer terrain grid
    // (4000 / 512 = 7.8125 m spacing): the two surfaces then meet without a seam.
    public const float InnerSize = 406.25f;
    public const float OuterSize = 4000f;
    public const float TerrainBaseY = -20f;
    public const float InnerHeightRange = 60f;
    public const float OuterHeightRange = 160f;

    public const float TrenchDepth = 1.65f;
    public const float TrenchFloorHalfWidth = 0.75f;
    public const float TrenchTopHalfWidth = 1.45f;
    public const float ParapetHeight = 0.45f;
    public const float FireStepHeight = 0.55f;
    public const float FireStepDepth = 0.55f;

    public sealed class Trench
    {
        public string name;
        public List<Vector2> points = new List<Vector2>();
        public bool hasParapet;
        public float depth = TrenchDepth;
        // Length of a ramp that climbs out of the trench at its last point (0 = dead end).
        public float exitRampLength;
    }

    public struct Crater
    {
        public Vector2 center;
        public float radius;
        public float depth;
    }

    private static List<Trench> trenches;
    private static List<Crater> craters;

    public static IReadOnlyList<Trench> Trenches
    {
        get
        {
            if (trenches == null)
            {
                BuildTrenches();
            }

            return trenches;
        }
    }

    public static IReadOnlyList<Crater> Craters
    {
        get
        {
            if (craters == null)
            {
                BuildCraters();
            }

            return craters;
        }
    }

    // Smooth rolling ground both terrains agree on, so the seam between them is invisible.
    public static float BaseHeight(float x, float z)
    {
        float h = (Mathf.PerlinNoise(x / 140f + 13.1f, z / 140f + 7.7f) - 0.5f) * 3.2f;
        h += (Mathf.PerlinNoise(x / 48f + 3.3f, z / 48f + 91.2f) - 0.5f) * 1.1f;

        // Far away the land heaves into low ridges that frame the creature's silhouette.
        float distance = Mathf.Sqrt(x * x + z * z);
        float far = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(260f, 1400f, distance));
        float ridges = Mathf.PerlinNoise(x / 420f + 50f, z / 420f + 11f) * 34f
            + Mathf.PerlinNoise(x / 160f + 5f, z / 160f + 60f) * 10f;
        h += far * ridges;
        return h;
    }

    // Fine noise that only the detailed inner terrain can represent.
    public static float DetailNoise(float x, float z)
    {
        return (Mathf.PerlinNoise(x / 6.5f + 17f, z / 6.5f + 4f) - 0.5f) * 0.35f
            + (Mathf.PerlinNoise(x / 2.1f + 2f, z / 2.1f + 40f) - 0.5f) * 0.08f;
    }

    private static void BuildTrenches()
    {
        trenches = new List<Trench>();

        // Fire trench: crenellated bays and traverses so a shell or a raider cannot rake its length.
        trenches.Add(Crenellated("FireTrench", -120f, 120f, 0f, 9f, 3f, true));
        // Enemy fire trench across no man's land.
        Trench enemy = Crenellated("EnemyTrench", -150f, 150f, 118f, 10f, -3f, true);
        enemy.depth = 2.1f;
        trenches.Add(enemy);
        // Support trench behind the line.
        trenches.Add(Crenellated("SupportTrench", -90f, 90f, -55f, 12f, 2.5f, false));

        // Communication trenches zigzag back to the support line.
        trenches.Add(Zigzag("CommTrenchWest", new Vector2(-38f, -1f), new Vector2(-30f, -55f), 5, 4.5f));
        trenches.Add(Zigzag("CommTrenchEast", new Vector2(52f, -1f), new Vector2(60f, -55f), 5, 4.5f));
        // Communication trench running further back, out of the playable area.
        Trench rear = Zigzag("CommTrenchRear", new Vector2(8f, -55f), new Vector2(20f, -170f), 9, 5f);
        rear.exitRampLength = 10f;
        trenches.Add(rear);

        // Sap out into no man's land, ending at a listening post.
        Trench sap = new Trench { name = "Sap", hasParapet = true, depth = 1.7f, exitRampLength = 7f };
        sap.points.Add(new Vector2(18f, 2f));
        sap.points.Add(new Vector2(20f, 12f));
        sap.points.Add(new Vector2(16f, 20f));
        sap.points.Add(new Vector2(19f, 27f));
        trenches.Add(sap);
    }

    private static Trench Crenellated(string name, float xStart, float xEnd, float z, float bay, float traverse, bool parapet)
    {
        Trench trench = new Trench { name = name, hasParapet = parapet };
        bool forward = false;
        for (float x = xStart; x <= xEnd; x += bay)
        {
            float offset = forward ? traverse : 0f;
            trench.points.Add(new Vector2(x, z + offset));
            trench.points.Add(new Vector2(Mathf.Min(x + bay - 2.2f, xEnd), z + offset));
            forward = !forward;
        }

        return trench;
    }

    private static Trench Zigzag(string name, Vector2 from, Vector2 to, int legs, float amplitude)
    {
        Trench trench = new Trench { name = name };
        Vector2 direction = (to - from).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x);
        for (int i = 0; i <= legs; i++)
        {
            float t = i / (float)legs;
            float swing = (i == 0 || i == legs) ? 0f : (i % 2 == 0 ? amplitude : -amplitude);
            trench.points.Add(Vector2.Lerp(from, to, t) + side * swing);
        }

        return trench;
    }

    private static void BuildCraters()
    {
        craters = new List<Crater>();
        System.Random random = new System.Random(1916);

        // No man's land is a moonscape; behind the lines it thins out.
        for (int i = 0; i < 170; i++)
        {
            Vector2 center = new Vector2(Range(random, -190f, 190f), Range(random, 8f, 112f));
            AddCrater(random, center, 1.6f, 6.5f);
        }

        for (int i = 0; i < 90; i++)
        {
            Vector2 center = new Vector2(Range(random, -190f, 190f), Range(random, -190f, 190f));
            AddCrater(random, center, 1.4f, 4.5f);
        }

        // Big old craters across the whole outer battlefield.
        for (int i = 0; i < 700; i++)
        {
            Vector2 center = new Vector2(Range(random, -1900f, 1900f), Range(random, -1900f, 1900f));
            if (Mathf.Abs(center.x) < InnerSize * 0.5f + 35f && Mathf.Abs(center.y) < InnerSize * 0.5f + 35f)
            {
                continue;
            }

            AddCrater(random, center, 6f, 22f);
        }
    }

    private static void AddCrater(System.Random random, Vector2 center, float minRadius, float maxRadius)
    {
        // Keep the trenches walkable: no crater swallows a trench line.
        if (DistanceToTrench(center, out _) < 4f)
        {
            return;
        }

        float radius = Range(random, minRadius, maxRadius);
        craters.Add(new Crater { center = center, radius = radius, depth = radius * Range(random, 0.22f, 0.38f) });
    }

    private static float Range(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    public static float DistanceToTrench(Vector2 point, out Trench closest)
    {
        float best = float.MaxValue;
        closest = null;
        foreach (Trench trench in Trenches)
        {
            for (int i = 0; i < trench.points.Count - 1; i++)
            {
                float d = DistanceToSegment(point, trench.points[i], trench.points[i + 1]);
                if (d < best)
                {
                    best = d;
                    closest = trench;
                }
            }
        }

        return best;
    }

    public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        return Vector2.Distance(p, a + ab * t);
    }

    // Height offset of trenches (negative) and their parapets (positive) at a point.
    public static float TrenchOffset(float x, float z)
    {
        Vector2 p = new Vector2(x, z);
        float distance = DistanceToTrench(p, out Trench trench);
        if (trench == null || distance > TrenchTopHalfWidth + 3.2f)
        {
            return 0f;
        }

        float depth = trench.depth;
        if (trench.exitRampLength > 0f)
        {
            // The floor climbs back to ground level over the last few metres.
            Vector2 end = trench.points[trench.points.Count - 1];
            depth *= Mathf.Clamp01(Vector2.Distance(p, end) / trench.exitRampLength);
        }

        if (distance <= TrenchFloorHalfWidth)
        {
            return -depth;
        }

        if (distance <= TrenchTopHalfWidth)
        {
            float t = Mathf.InverseLerp(TrenchFloorHalfWidth, TrenchTopHalfWidth, distance);
            return -depth * (1f - Mathf.SmoothStep(0f, 1f, t));
        }

        // Spoil heaped along both lips: parapet toward the enemy, lower parados behind.
        float lip = Mathf.InverseLerp(TrenchTopHalfWidth + 3.2f, TrenchTopHalfWidth + 0.6f, distance);
        float height = trench.hasParapet ? ParapetHeight : ParapetHeight * 0.5f;
        return Mathf.Sin(Mathf.Clamp01(lip) * Mathf.PI * 0.5f) * height;
    }

    public static float CraterOffset(float x, float z, float minRadius)
    {
        float offset = 0f;
        foreach (Crater crater in Craters)
        {
            if (crater.radius < minRadius)
            {
                continue;
            }

            float dx = x - crater.center.x;
            float dz = z - crater.center.y;
            float reach = crater.radius * 1.5f;
            if (Mathf.Abs(dx) > reach || Mathf.Abs(dz) > reach)
            {
                continue;
            }

            float r = Mathf.Sqrt(dx * dx + dz * dz) / crater.radius;
            if (r < 1f)
            {
                // Bowl: deepest in the middle.
                offset += -crater.depth * (1f - r * r) + crater.depth * 0.18f;
            }
            else if (r < 1.5f)
            {
                // Raised rim thrown out by the blast.
                float rim = 1f - (r - 1f) / 0.5f;
                offset += crater.depth * 0.18f * rim * rim;
            }
        }

        return offset;
    }

    public static float InnerHeight(float x, float z)
    {
        // Fine detail fades out toward the edge so it meets the coarse outer terrain exactly.
        float edge = InnerSize * 0.5f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
        float detail = Mathf.Clamp01(edge / 15f);
        return BaseHeight(x, z)
            + (DetailNoise(x, z) + TrenchOffset(x, z) + CraterOffset(x, z, 0f)) * detail;
    }

    public static float OuterHeight(float x, float z)
    {
        return BaseHeight(x, z) + CraterOffset(x, z, 6f);
    }
}
