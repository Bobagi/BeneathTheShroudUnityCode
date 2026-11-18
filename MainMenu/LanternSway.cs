using UnityEngine;

// A lantern hanging from its hook in the wind: a damped pendulum about the hook pushed by gusts (two slow noises),
// with a little twist. The light and the flame are children, so the shadows it throws swing with it. Silent (the
// creaks tried on 10/10 were taken out by Gustavo).
public sealed class LanternSway : MonoBehaviour
{
    [SerializeField]
    private float maxAngle = 7f;

    [SerializeField]
    private float gustStrength = 22f;

    [SerializeField]
    private float stiffness = 9f;

    [SerializeField]
    private float damping = 1.4f;

    private Quaternion rest;
    private Vector2 angle;
    private Vector2 speed;
    private float seed;

    private void Awake()
    {
        rest = transform.localRotation;
        seed = Random.value * 50f;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float t = Time.unscaledTime;
        Vector2 gust = new Vector2(Mathf.PerlinNoise(t * 0.35f, seed) - 0.45f, Mathf.PerlinNoise(seed, t * 0.28f) - 0.5f) * gustStrength;
        speed += (gust - angle * stiffness - speed * damping) * dt;
        angle += speed * dt;
        angle = Vector2.ClampMagnitude(angle, maxAngle);
        float twist = (Mathf.PerlinNoise(t * 0.2f, seed + 9f) - 0.5f) * 12f;
        transform.localRotation = rest * Quaternion.Euler(angle.x, twist, angle.y);
    }
}
