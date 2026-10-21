using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// The eye of a camera (AutoExposureFeature does the GPU side). Reads back the metered frame, a centre-weighted mean of
// log2 luminance, and eases the exposure toward key / mean, clamped: the eye opens up to `maxUp` stops in a dim room
// (2: the rooms read by their real lamps alone, without the old shadowless "bounce" lights) and closes up to `maxDown`
// when it looks at a lamp or the torch hits a near wall. It closes in about a second and opens over several, like an eye. Numbers in GameTuning ("Exposição
// automática"); `useTuning` off keeps this component's own (the cutscene sets its own range).
[RequireComponent(typeof(Camera))]
public sealed class AutoExposure : MonoBehaviour
{
    private static readonly Dictionary<Camera, AutoExposure> Registry = new Dictionary<Camera, AutoExposure>();

    [SerializeField]
    private bool useTuning = true;

    [Tooltip("Mean scene luminance that ends up unchanged (exposure 1). Above it the eye closes, below it opens.")]
    [SerializeField]
    private float key = 0.03f;

    [SerializeField]
    private float maxUp = 2f;

    [SerializeField]
    private float maxDown = 2.5f;

    [Tooltip("Seconds to close by most of the way (light got brighter).")]
    [SerializeField]
    private float closeSeconds = 0.9f;

    [Tooltip("Seconds to open by most of the way (light got darker).")]
    [SerializeField]
    private float openSeconds = 4f;

    private Camera cam;
    private RTHandle meter;
    private bool pending;
    private float ev;
    private float target;
    private float close = 0.9f;
    private float open = 4f;
    private bool measured;

    public float Exposure => Mathf.Pow(2f, ev);

    // The current exposure in stops and the last metered mean luminance, for tuning and captures.
    public float Stops => ev;
    public float MeteredLuminance { get; private set; }

    public static AutoExposure For(Camera camera)
    {
        return camera != null && Registry.TryGetValue(camera, out AutoExposure eye) ? eye : null;
    }

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        Registry[cam] = this;
    }

    private void OnDisable()
    {
        if (cam != null && Registry.TryGetValue(cam, out AutoExposure eye) && eye == this)
        {
            Registry.Remove(cam);
        }

        meter?.Release();
        meter = null;
        pending = false;
        measured = false;
        ev = 0f;
    }

    public RTHandle MeterTarget(int width, int height)
    {
        if (meter == null)
        {
            meter = RTHandles.Alloc(width, height, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
                filterMode: FilterMode.Point, name: "AutoExposureMeter");
        }

        return meter;
    }

    public void RequestReadback()
    {
        if (pending || meter == null || meter.rt == null || !SystemInfo.supportsAsyncGPUReadback)
        {
            return;
        }

        pending = true;
        AsyncGPUReadback.Request(meter.rt, 0, OnReadback);
    }

    private void OnReadback(AsyncGPUReadbackRequest request)
    {
        pending = false;
        if (request.hasError || this == null)
        {
            return;
        }

        NativeArray<float> data = request.GetData<float>();
        int w = AutoExposureFeature.MeterWidth;
        int h = AutoExposureFeature.MeterHeight;
        if (data.Length < w * h)
        {
            return;
        }

        // Centre-weighted: what the player looks at counts most, the frame's edges a third as much.
        double sum = 0.0;
        double weights = 0.0;
        for (int y = 0; y < h; y++)
        {
            float fy = (y + 0.5f) / h * 2f - 1f;
            for (int x = 0; x < w; x++)
            {
                float fx = (x + 0.5f) / w * 2f - 1f;
                float weight = Mathf.Lerp(1f, 0.33f, Mathf.Clamp01(fx * fx + fy * fy));
                float v = data[y * w + x];
                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    continue;
                }

                sum += v * weight;
                weights += weight;
            }
        }

        if (weights <= 0.0)
        {
            return;
        }

        float meanLog = (float)(sum / weights);
        MeteredLuminance = Mathf.Pow(2f, meanLog);
        float k = key, up = maxUp, down = maxDown;
        close = closeSeconds;
        open = openSeconds;
        GameTuning tuning = useTuning ? GameTuning.Instance : null;
        if (tuning != null)
        {
            k = tuning.exposureKey;
            up = tuning.exposureMaxUp;
            down = tuning.exposureMaxDown;
            close = tuning.exposureCloseSeconds;
            open = tuning.exposureOpenSeconds;
            if (!tuning.autoExposure)
            {
                up = 0f;
                down = 0f;
            }
        }

        target = Mathf.Clamp(Mathf.Log(Mathf.Max(k, 1e-5f), 2f) - meanLog, -down, up);
        if (!measured)
        {
            // The first reading: no adaptation from a made-up start.
            ev = target;
            measured = true;
        }
    }

    private void LateUpdate()
    {
        if (!measured)
        {
            return;
        }

        float seconds = target < ev ? close : open;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        ev = Mathf.Lerp(ev, target, 1f - Mathf.Exp(-dt * 2.3f / Mathf.Max(seconds, 0.05f)));
    }

    // For the builders: this camera's own range (useTuning off).
    public void Configure(float key, float maxUp, float maxDown, float closeSeconds, float openSeconds)
    {
        useTuning = false;
        this.key = key;
        this.maxUp = maxUp;
        this.maxDown = maxDown;
        this.closeSeconds = closeSeconds;
        this.openSeconds = openSeconds;
    }
}
