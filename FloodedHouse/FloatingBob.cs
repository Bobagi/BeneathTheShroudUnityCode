using System.Collections.Generic;
using UnityEngine;

// Furniture drifting against the ceiling of a flooded room: a slow bob and a lazy turn around where
// it was placed, each piece out of phase with the others. JoltAll() shoves every piece at once, as
// if something big just moved the water. Push() lets the swimmer shove a piece aside: it drifts,
// slows in the water and never strays far from where it was placed (so it cannot end up in a wall).
public sealed class FloatingBob : MonoBehaviour
{
    [SerializeField]
    private float bobHeight = 0.06f;

    [SerializeField]
    private float bobPeriod = 5f;

    [SerializeField]
    private float swayDegrees = 4f;

    private static readonly List<FloatingBob> Active = new List<FloatingBob>();

    private Vector3 origin;
    private Quaternion baseRotation;
    private float seed;
    private float jolt;
    private Vector3 drift;
    private Vector3 driftVelocity;

    private const float MaxDrift = 0.9f;

    public static void JoltAll(float strength)
    {
        foreach (FloatingBob bob in Active)
        {
            bob.jolt = Mathf.Max(bob.jolt, strength);
        }
    }

    public void Push(Vector3 velocity)
    {
        driftVelocity = Vector3.ClampMagnitude(driftVelocity + velocity * 0.5f, 1.5f);
    }

    private void OnEnable()
    {
        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void Start()
    {
        origin = transform.localPosition;
        baseRotation = transform.localRotation;
        seed = Random.value * 10f;
    }

    private void Update()
    {
        jolt = Mathf.MoveTowards(jolt, 0f, Time.deltaTime * 0.6f);
        driftVelocity *= Mathf.Exp(-1.5f * Time.deltaTime);
        drift = Vector3.ClampMagnitude(drift + driftVelocity * Time.deltaTime, MaxDrift);
        Vector3 localDrift = transform.parent != null ? transform.parent.InverseTransformVector(drift) : drift;
        float t = (Time.time + seed) / bobPeriod * Mathf.PI * 2f;
        float shake = jolt * Mathf.Sin(Time.time * 9f + seed);
        transform.localPosition = origin + localDrift + Vector3.up * (Mathf.Sin(t) * bobHeight + shake * 0.15f) + Vector3.right * shake * 0.1f;
        float sway = swayDegrees * (1f + jolt * 6f);
        transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t * 0.7f) * sway, Mathf.Sin(t * 0.31f) * sway * 2f, Mathf.Cos(t * 0.53f) * sway);
    }
}
