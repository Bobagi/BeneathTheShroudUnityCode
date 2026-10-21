using UnityEngine;
using UnityEngine.InputSystem;

// The menu camera floats at the waterline: a slow rise and fall with the swell, a little roll, a hand-held drift,
// and it leans a degree or two toward the mouse (the title drifts the other way: depth). Nothing fast: it is a
// still picture that breathes. The underwater camera is its child and moves with it.
public sealed class MenuCameraDrift : MonoBehaviour
{
    [SerializeField]
    private float bob = 0.09f;

    [SerializeField]
    private float bobSeconds = 6.5f;

    [SerializeField]
    private float roll = 0.8f;

    [SerializeField]
    private float drift = 0.5f;

    [SerializeField]
    private Vector2 mouseLean = new Vector2(1.6f, 0.9f);

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector2 mouse;

    // -1..1 on each axis, smoothed: where the mouse is on the screen, for parallax.
    public static Vector2 MouseOffset { get; private set; }

    private void Awake()
    {
        homePosition = transform.localPosition;
        homeRotation = transform.localRotation;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        Mouse m = Mouse.current;
        Vector2 target = Vector2.zero;
        if (m != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 p = m.position.ReadValue();
            target = new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
        }

        mouse = Vector2.Lerp(mouse, target, 1f - Mathf.Exp(-1.8f * Time.unscaledDeltaTime));
        MouseOffset = mouse;

        float phase = t * Mathf.PI * 2f / bobSeconds;
        float rise = Mathf.Sin(phase) * bob + Mathf.Sin(phase * 2.3f + 1.1f) * bob * 0.3f;
        transform.localPosition = homePosition + Vector3.up * rise;
        float yaw = (Mathf.PerlinNoise(t * 0.07f, 0.3f) - 0.5f) * 2f * drift + mouse.x * mouseLean.x;
        float pitch = (Mathf.PerlinNoise(0.7f, t * 0.06f) - 0.5f) * 2f * drift * 0.6f - mouse.y * mouseLean.y + Mathf.Cos(phase) * 0.25f;
        float tilt = Mathf.Sin(phase * 0.8f + 0.4f) * roll;
        transform.localRotation = homeRotation * Quaternion.Euler(pitch, yaw, tilt);
    }
}
