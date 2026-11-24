using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Collects transform poses over time and writes them as an AnimationClip the Animation window edits comfortably:
// positions as m_LocalPosition, rotations as Euler angles (localEulerAnglesRaw, unwrapped so no key spins the long
// way round), GameObject activity as m_IsActive; keys that a straight line between their neighbours already explains
// are dropped (Ramer-Douglas-Peucker), so a baked motion keeps only the keys that matter. Saved in place: an existing
// clip keeps its GUID and the Animator its reference.
public sealed class ClipRecorder
{
    private sealed class Track
    {
        public readonly List<float> times = new List<float>();
        public readonly List<Vector3> positions = new List<Vector3>();
        public readonly List<Quaternion> rotations = new List<Quaternion>();
        public bool keyPosition;
    }

    private readonly Dictionary<string, Track> tracks = new Dictionary<string, Track>();
    private readonly Dictionary<string, List<(float time, bool active)>> active = new Dictionary<string, List<(float, bool)>>();
    private readonly float fps;

    // Tolerances of the key reduction: degrees for rotations, metres for positions (in the clip's units).
    public float AngleTolerance = 0.2f;
    public float DistanceTolerance = 0.0004f;

    public ClipRecorder(float fps)
    {
        this.fps = fps;
    }

    public void Pose(string path, float time, Vector3 localPosition, Quaternion localRotation, bool keyPosition)
    {
        if (!tracks.TryGetValue(path, out Track track))
        {
            track = new Track { keyPosition = keyPosition };
            tracks[path] = track;
        }

        track.times.Add(time);
        track.positions.Add(localPosition);
        track.rotations.Add(localRotation);
    }

    public void Active(string path, float time, bool isActive)
    {
        if (!active.TryGetValue(path, out List<(float, bool)> list))
        {
            list = new List<(float, bool)>();
            active[path] = list;
        }

        list.Add((time, isActive));
    }

    public AnimationClip Write(string assetPath, bool loop, AnimationEvent[] events = null)
    {
        AnimationClip clip = new AnimationClip { frameRate = fps, name = Path.GetFileNameWithoutExtension(assetPath) };
        foreach (KeyValuePair<string, Track> pair in tracks)
        {
            WriteTrack(clip, pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, List<(float time, bool active)>> pair in active)
        {
            List<Keyframe> keys = new List<Keyframe>();
            bool? last = null;
            foreach ((float time, bool isActive) in pair.Value)
            {
                if (last == null || last.Value != isActive)
                {
                    keys.Add(new Keyframe(time, isActive ? 1f : 0f));
                    last = isActive;
                }
            }

            (float endTime, bool endActive) = pair.Value[pair.Value.Count - 1];
            if (keys.Count == 0 || keys[keys.Count - 1].time < endTime)
            {
                keys.Add(new Keyframe(endTime, endActive ? 1f : 0f));
            }

            AnimationCurve curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pair.Key, typeof(GameObject), "m_IsActive"), curve);
        }

        if (events != null)
        {
            AnimationUtility.SetAnimationEvents(clip, events);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return Save(clip, assetPath);
    }

    public static AnimationClip Save(AnimationClip clip, string assetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(clip, assetPath);
            saved = clip;
        }
        else
        {
            EditorUtility.CopySerialized(clip, saved);
            saved.name = Path.GetFileNameWithoutExtension(assetPath);
            EditorUtility.SetDirty(saved);
        }

        AssetDatabase.SaveAssets();
        return saved;
    }

    private void WriteTrack(AnimationClip clip, string path, Track track)
    {
        int n = track.times.Count;
        float[] times = track.times.ToArray();
        Vector3[] euler = Unwrap(track.rotations);
        string[] axes = { "x", "y", "z" };
        for (int a = 0; a < 3; a++)
        {
            float[] values = new float[n];
            for (int i = 0; i < n; i++)
            {
                values[i] = euler[i][a];
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "localEulerAnglesRaw." + axes[a]), Curve(times, values, AngleTolerance));
        }

        if (!track.keyPosition)
        {
            return;
        }

        for (int a = 0; a < 3; a++)
        {
            float[] values = new float[n];
            for (int i = 0; i < n; i++)
            {
                values[i] = track.positions[i][a];
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + axes[a]), Curve(times, values, DistanceTolerance));
        }
    }

    // Euler angles, frame to frame, with the representation (and the turn of each axis) nearest the previous frame.
    private static Vector3[] Unwrap(List<Quaternion> rotations)
    {
        Vector3[] result = new Vector3[rotations.Count];
        Vector3 previous = Vector3.zero;
        for (int i = 0; i < rotations.Count; i++)
        {
            Quaternion q = rotations[i];
            Vector3 e = q.eulerAngles;
            Vector3 best = Vector3.zero;
            float bestCost = float.MaxValue;
            foreach (Vector3 candidate in new[] { e, new Vector3(180f - e.x, e.y + 180f, e.z + 180f) })
            {
                if (Quaternion.Angle(Quaternion.Euler(candidate), q) > 0.05f)
                {
                    continue;
                }

                Vector3 near = i == 0
                    ? new Vector3(Mathf.DeltaAngle(0f, candidate.x), Mathf.DeltaAngle(0f, candidate.y), Mathf.DeltaAngle(0f, candidate.z))
                    : new Vector3(previous.x + Mathf.DeltaAngle(previous.x, candidate.x), previous.y + Mathf.DeltaAngle(previous.y, candidate.y), previous.z + Mathf.DeltaAngle(previous.z, candidate.z));
                float cost = i == 0 ? near.sqrMagnitude : (near - previous).sqrMagnitude;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = near;
                }
            }

            previous = best;
            result[i] = best;
        }

        return result;
    }

    // Keys reduced by Ramer-Douglas-Peucker, smooth tangents.
    private static AnimationCurve Curve(float[] times, float[] values, float tolerance)
    {
        List<int> keep = new List<int> { 0 };
        if (times.Length > 1)
        {
            Reduce(times, values, 0, times.Length - 1, tolerance, keep);
            keep.Add(times.Length - 1);
        }

        keep.Sort();
        Keyframe[] keys = new Keyframe[keep.Count];
        for (int i = 0; i < keep.Count; i++)
        {
            keys[i] = new Keyframe(times[keep[i]], values[keep[i]]);
        }

        AnimationCurve curve = new AnimationCurve(keys);
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }

        return curve;
    }

    private static void Reduce(float[] times, float[] values, int first, int last, float tolerance, List<int> keep)
    {
        if (last - first < 2)
        {
            return;
        }

        float worst = 0f;
        int at = -1;
        for (int i = first + 1; i < last; i++)
        {
            float t = (times[i] - times[first]) / (times[last] - times[first]);
            float line = Mathf.Lerp(values[first], values[last], t);
            float d = Mathf.Abs(values[i] - line);
            if (d > worst)
            {
                worst = d;
                at = i;
            }
        }

        if (at < 0 || worst <= tolerance)
        {
            return;
        }

        keep.Add(at);
        Reduce(times, values, first, at, tolerance, keep);
        Reduce(times, values, at, last, tolerance, keep);
    }
}
