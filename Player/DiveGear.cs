using UnityEngine;
using UnityEngine.InputSystem;

// The waterproof torch and the dive kit. The torch is found in the house after the blackout (Alex's dive
// torch, in the hall cabinet) with its batteries elsewhere (GiveTorch, GiveBatteries); with both, F toggles
// it: a narrow, hard cone that shows a few metres of murk and makes the dark around it darker. The mask and
// small cylinder come from the basement bench (Equip): until then invisible gates along the edge of the
// deep water keep the player out of it. Sounds: a real torch click (SoundLibrary.TorchClick, toggleClip if the
// library has none) and the batteries snapping in when the second of the two is picked up.
public sealed class DiveGear : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private Light torch;

    [SerializeField]
    private GameObject[] deepWaterGates;

    [SerializeField]
    private DiveHud hud;

    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip toggleClip;


    // Light falls off with the square of distance, so a bench 0.7 m away gets ~20x the light of a wall
    // at 3 m and blows out. Like an eye (or a camera) adapting, the torch output follows the distance
    // of whatever is in the middle of the beam, smoothed so it never visibly pumps.
    [Header("Near-surface compensation")]
    [SerializeField]
    private float fullPowerDistance = 3.2f;

    // 0.015 (04/10): at 0.06 the torch stopped dimming at 1.3 m, and on a wall half a metre away its spot was a
    // white disc 2.5 times brighter than a wall at 3 m (the "ball of light"); the square law wants 0.024 there.
    [SerializeField]
    private float minimumPower = 0.015f;

    [SerializeField]
    private float adaptSpeed = 5f;

    private float power = 1f;

    public bool HasGear { get; private set; }
    public bool HasTorch { get; private set; }
    public bool HasBatteries { get; private set; }

    public void GiveTorch()
    {
        HasTorch = true;
        LoadBatteriesIfReady();
    }

    public void GiveBatteries()
    {
        HasBatteries = true;
        LoadBatteriesIfReady();
    }

    private void LoadBatteriesIfReady()
    {
        AudioClip insert = SoundLibrary.Instance.batteriesInsert;
        if (HasTorch && HasBatteries && audioSource != null && insert != null)
        {
            audioSource.PlayOneShot(insert, 0.8f);
        }
    }
    public bool TorchOn => torch != null && torch.enabled;

    // 0 to 1: the torch held up at the centre and aimed (TorchHold sets it).
    public float Focus { get; set; }

    public event System.Action Equipped;

    private void Awake()
    {
        if (torch != null)
        {
            torch.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!TorchOn)
        {
            return;
        }

        Transform t = torch.transform;
        float distance = Physics.Raycast(t.position, t.forward, out RaycastHit hit, fullPowerDistance, ~0, QueryTriggerInteraction.Ignore)
            ? hit.distance
            : fullPowerDistance;
        float ratio = distance / fullPowerDistance;
        float target = Mathf.Clamp(ratio * ratio, minimumPower, 1f);
        power = Mathf.Lerp(power, target, 1f - Mathf.Exp(-adaptSpeed * Time.deltaTime));
        GameTuning tuning = GameTuning.Instance;
        torch.range = tuning.torchRange;
        // Held up and aimed (TorchHold, right mouse): a tighter, stronger beam.
        float narrow = Mathf.Lerp(1f, tuning.torchFocusAngle, Focus);
        torch.innerSpotAngle = tuning.TorchInnerAngle * narrow;
        torch.spotAngle = tuning.torchAngle * narrow;
        torch.intensity = tuning.torchIntensity * power * Mathf.Lerp(1f, tuning.torchFocusBoost, Focus);
    }

    // Loading a checkpoint: what the player had, without sounds or the story's events.
    public void RestoreState(bool hasTorch, bool hasBatteries, bool hasGear, bool torchOn)
    {
        HasTorch = hasTorch || hasGear;
        HasBatteries = hasBatteries || hasGear;
        if (hasGear && !HasGear)
        {
            HasGear = true;
            foreach (GameObject gate in deepWaterGates)
            {
                if (gate != null)
                {
                    gate.SetActive(false);
                }
            }
        }

        if (torch != null)
        {
            torch.enabled = torchOn && HasTorch && HasBatteries;
        }
    }

    public void Equip()
    {
        if (HasGear)
        {
            return;
        }

        HasGear = true;
        HasTorch = true;
        HasBatteries = true;
        foreach (GameObject gate in deepWaterGates)
        {
            if (gate != null)
            {
                gate.SetActive(false);
            }
        }

        SetTorch(true);
        Equipped?.Invoke();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (controller.InputLocked || keyboard == null || !keyboard.fKey.wasPressedThisFrame)
        {
            return;
        }

        // Every key must answer: silence reads as a bug.
        if (HasTorch && HasBatteries)
        {
            SetTorch(!TorchOn);
        }
        else if (hud != null)
        {
            hud.ShowMessage(Loc.T(HasTorch ? "msg.torch_no_batteries" : "msg.no_light"), 2.5f);
        }
    }

    private void SetTorch(bool on)
    {
        if (torch == null)
        {
            return;
        }

        torch.enabled = on;
        AudioClip click = SoundLibrary.Instance.TorchClick();
        click = click != null ? click : toggleClip;
        if (audioSource != null && click != null)
        {
            audioSource.PlayOneShot(click, 0.7f);
        }
    }
}
