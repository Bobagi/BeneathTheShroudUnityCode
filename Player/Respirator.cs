using UnityEngine;
using UnityEngine.InputSystem;

// The respirator of the old suit from the attic, and the spores it stops. In a FungusZone the lungs take
// spores by its density: with the mask on and a filter that still works, the filter wears down instead
// (GameTuning.filterSeconds of dense spores per filter) and the breathing turns muffled; R swaps in a spare
// filter. Without protection the spores hurt: light spores only make the player cough and never take more
// than GameTuning.sporeHarmFloor of the health; dense spores keep going and kill (Overcome fires once).
// Health comes back slowly in clean air.
public sealed class Respirator : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private AudioSource breathAudio;

    [SerializeField]
    private AudioSource maskLoop;

    [SerializeField]
    private AudioClip swapClip;

    private float filter;
    private float health = 1f;
    private float coughTimer;
    private bool overcome;

    public bool HasMask { get; private set; }
    public int SpareFilters { get; private set; }
    public float Filter => filter;
    public float Health => health;
    public float Exposure { get; private set; }
    public bool InSpores { get; private set; }

    public event System.Action Overcome;

    public void GiveMask(int spares)
    {
        HasMask = true;
        filter = 1f;
        SpareFilters += spares;
    }

    // Taking the mask off (test scenes swap the suit on and off; the game never does yet).
    // Loading a checkpoint.
    public void RestoreState(bool mask, int spares, float filterLeft)
    {
        HasMask = mask;
        SpareFilters = spares;
        filter = Mathf.Clamp01(filterLeft);
        health = 1f;
        overcome = false;
    }

    public void RemoveMask()
    {
        HasMask = false;
    }

    public void AddFilters(int count)
    {
        SpareFilters += count;
    }

    public void Restore()
    {
        health = 1f;
        overcome = false;
        if (HasMask && filter <= 0f && SpareFilters == 0)
        {
            // Back at the checkpoint with something to breathe through.
            filter = 0.5f;
        }
    }

    private void Update()
    {
        GameTuning tuning = GameTuning.Instance;
        float dt = Time.deltaTime;
        float density = FungusZone.SporeDensityAt(controller.EyePosition);
        InSpores = density > 0.01f;
        bool protectedNow = HasMask && filter > 0f;
        if (InSpores && protectedNow)
        {
            filter = Mathf.Max(0f, filter - density * dt / Mathf.Max(1f, tuning.filterSeconds));
            if (filter <= 0f && hud != null)
            {
                hud.ShowMessage(Loc.T(SpareFilters > 0 ? "msg.filter_spent_spare" : "msg.filter_spent"), 3.5f);
            }
        }

        Exposure = InSpores && !protectedNow ? density : 0f;
        if (Exposure > 0f && !overcome)
        {
            // Light spores stop at a floor; dense ones keep going.
            float floor = density < 0.6f ? tuning.sporeHarmFloor : 0f;
            health = Mathf.Max(floor, health - Exposure * dt / Mathf.Max(1f, tuning.sporeHarmSeconds));
            coughTimer -= dt;
            if (coughTimer <= 0f && breathAudio != null)
            {
                coughTimer = Random.Range(1.6f, 3.4f) / Mathf.Max(0.3f, density);
                breathAudio.pitch = Random.Range(0.92f, 1.06f);
                breathAudio.PlayOneShot(SoundLibrary.Instance.Cough(), 0.55f);
            }

            if (health <= 0f)
            {
                overcome = true;
                Overcome?.Invoke();
            }
        }
        else if (!overcome)
        {
            health = Mathf.Min(1f, health + dt / Mathf.Max(1f, tuning.recoverSeconds));
        }

        if (hud != null)
        {
            hud.SetSporeEdge(1f - health);
        }

        if (maskLoop != null)
        {
            float target = InSpores && protectedNow ? tuning.maskBreathVolume : 0f;
            maskLoop.volume = Mathf.MoveTowards(maskLoop.volume, target, dt * 0.8f);
        }

        Keyboard keyboard = Keyboard.current;
        // G: R reloads the revolver (Gustavo, 07/10).
        if (keyboard != null && keyboard.gKey.wasPressedThisFrame && !controller.InputLocked)
        {
            Swap();
        }
    }

    private void Swap()
    {
        if (hud == null)
        {
            return;
        }

        if (!HasMask)
        {
            hud.ShowMessage(Loc.T("msg.no_mask"), 2.5f);
            return;
        }

        if (SpareFilters <= 0)
        {
            hud.ShowMessage(Loc.T("msg.no_spare_filter"), 2.5f);
            return;
        }

        if (filter > 0.95f)
        {
            hud.ShowMessage(Loc.T("msg.filter_new"), 2f);
            return;
        }

        SpareFilters--;
        filter = 1f;
        if (breathAudio != null && swapClip != null)
        {
            breathAudio.PlayOneShot(swapClip, 0.8f);
        }

        hud.ShowMessage(Loc.T("msg.filter_swapped"), 2f);
    }
}
