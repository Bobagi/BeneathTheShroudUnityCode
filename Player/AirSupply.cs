using UnityEngine;

// The only resource on screen: breath. Drains while the eyes are under water, 50% faster when
// swimming hard and 50% faster again near a hot vent. Refills quickly whenever the head is in air
// (the surface or an air pocket). At zero the body starts to fail instead of dying on the spot: health
// drains over GameTuning.drownSeconds (the window to find air, shown by SuffocationEffects) and comes
// back slowly once breathing again. When health runs out the player blacks out: Drowned fires once
// and the scene decides what happens.
public sealed class AirSupply : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    private float Capacity => GameTuning.Instance.airSeconds;

    [SerializeField]
    private float refillPerSecond = 22f;

    [SerializeField]
    private float effortMultiplier = 1.5f;

    [SerializeField]
    private float heatMultiplier = 1.5f;

    [SerializeField]
    private AudioSource breathAudio;

    private float air;
    private float health = 1f;
    private bool wasUnderwater;
    private float lastBlow = -100f;

    public event System.Action Drowned;
    // Health ran out on land (a creature's blows): fired once; the scene decides what happens.
    public event System.Action Killed;

    public float Normalized => air / Capacity;
    public float Health => health;
    public float Suffocation => 1f - health;
    public bool IsUnderwater { get; private set; }

    // Out of air with the head still under water: health is draining right now.
    public bool IsSuffocating { get; private set; }
    public bool IsUnconscious { get; private set; }
    // Health was taken by a blow in the last moments (the bar shows health instead of air meanwhile).
    public bool IsHurt => Time.time - lastBlow < 60f && health < 0.999f;
    public bool IsDraining { get; private set; }
    public bool NearHeat { get; private set; }

    private void Awake()
    {
        Refill();
    }

    // A medkit or a bandage (Inventory): back toward full health.
    public void Heal(float amount)
    {
        health = Mathf.Clamp01(health + amount);
    }

    public void Refill()
    {
        air = Capacity;
        health = 1f;
        IsSuffocating = false;
        IsUnconscious = false;
        lastBlow = -100f;
    }

    // A creature's blow (DrownedBrain): takes `amount` of the health (0 to 1). Health comes back slowly on its own
    // (GameTuning.recoverSeconds, the same recovery as after choking). At zero the player goes down: Killed fires once.
    public void Damage(float amount)
    {
        if (IsUnconscious || amount <= 0f)
        {
            return;
        }

        lastBlow = Time.time;
        health = Mathf.Clamp01(health - amount);
        if (health <= 0f)
        {
            IsUnconscious = true;
            Killed?.Invoke();
        }
    }

    private void Update()
    {
        Vector3 eye = controller.EyePosition;
        IsUnderwater = WaterVolume.IsUnderwater(eye);
        NearHeat = IsUnderwater && HotVent.IsNearHeat(eye);
        if (IsUnconscious)
        {
            return;
        }

        if (IsUnderwater)
        {
            float rate = 1f;
            if (controller.IsSprinting)
            {
                rate *= effortMultiplier;
            }

            if (NearHeat)
            {
                rate *= heatMultiplier;
            }

            air -= rate * Time.deltaTime;
            IsDraining = true;
            if (air <= 0f)
            {
                air = 0f;
                IsSuffocating = true;
                health -= Time.deltaTime / Mathf.Max(0.1f, GameTuning.Instance.drownSeconds);
                if (health <= 0f)
                {
                    health = 0f;
                    IsUnconscious = true;
                    Drowned?.Invoke();
                }
            }
        }
        else
        {
            if (wasUnderwater && (air < Capacity * 0.6f || health < 1f) && breathAudio != null)
            {
                // Coming up while already choking is a desperate, full-volume gulp.
                breathAudio.PlayOneShot(ProceduralAudio.Gasp(), IsSuffocating ? 1f : Mathf.Lerp(0.8f, 0.3f, Normalized));
            }

            air = Mathf.Min(Capacity, air + refillPerSecond * Time.deltaTime);
            // A blow's wound closes slower than the lungs recover (GameTuning.blowRecoverSeconds), and only after a
            // few seconds without another hit.
            float recover = Time.time - lastBlow < 4f ? 0f : Time.time - lastBlow < 60f ? GameTuning.Instance.blowRecoverSeconds : GameTuning.Instance.recoverSeconds;
            if (recover > 0f)
            {
                health = Mathf.Min(1f, health + Time.deltaTime / Mathf.Max(0.1f, recover));
            }
            IsDraining = false;
            IsSuffocating = false;
        }

        wasUnderwater = IsUnderwater;
    }
}
