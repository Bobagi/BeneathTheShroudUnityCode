using UnityEngine;

// The marks a boat leaves on the sea: foam laid on the water behind the stern (by distance travelled, so a stopped
// boat leaves none), spreading out to both sides in a V and fading over several seconds, plus white spray thrown
// up at the bow when it is going fast. The foam lies flat on the waves: each patch is set every frame to the height
// of the water under it (WaveRider.Height, the same waves the shader draws), so it rides the swell instead of
// hanging at sea level. The systems themselves are made by the builder; this only drives them.
public sealed class BoatWake : MonoBehaviour
{
    [SerializeField]
    private WaveRider rider;

    [SerializeField]
    private ParticleSystem foam;

    [SerializeField]
    private ParticleSystem spray;

    [Tooltip("Speed (m/s) at which the bow spray is at its fullest.")]
    [SerializeField]
    private float fullSpeed = 6f;

    [SerializeField]
    private float sprayPerSecond = 90f;

    [Tooltip("How high above the water the foam floats (keeps it from flickering into the surface).")]
    [SerializeField]
    private float lift = 0.04f;

    private ParticleSystem.Particle[] particles;
    private Vector3 last;
    private float speed;

    private void Start()
    {
        last = transform.position;
        if (foam != null)
        {
            particles = new ParticleSystem.Particle[foam.main.maxParticles];
        }
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        speed = Mathf.Lerp(speed, (transform.position - last).magnitude / dt, 1f - Mathf.Exp(-3f * dt));
        last = transform.position;

        if (spray != null)
        {
            ParticleSystem.EmissionModule emission = spray.emission;
            float throttle = Mathf.Clamp01((speed - 1f) / (fullSpeed - 1f));
            emission.rateOverTime = sprayPerSecond * throttle * throttle;
        }

        if (foam == null || rider == null)
        {
            return;
        }

        int count = foam.GetParticles(particles);
        float time = Time.time;
        float seaLevel = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = particles[i].position;
            p.y = seaLevel + rider.Height(p, time) + lift;
            particles[i].position = p;
        }

        foam.SetParticles(particles, count);
    }
}
