using UnityEngine;
using UnityEngine.InputSystem;

// The dive torch in the hand. Sits on the TorchHand object (the light at the lens, the model behind it, the lens
// spill), which hangs on the first person rig's torch socket (FirstPersonArms, hand_r/TorchSocket): where the hand is
// on the screen, and so the torch, is the rig's fixed clips (Torch_Hold, right mouse: Torch_Focus); nothing here moves
// the hand. This decides what shows and what the light does:
//   the model only once the torch is found and while the torch is the thing in hand (or clipped to the chest);
//   the beam is turned a few degrees (GameTuning.torchAimTurn at most) so it lands on what is under the crosshair,
//   near or far (never nearer than torchAimMinDistance), while the hand itself stays as animated;
//   right mouse holds the torch up (the rig's Aim state) with a tighter, stronger beam (DiveGear.Focus) that stuns a
//   creature looked in the eyes (TorchStun).
public sealed class TorchHold : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private DiveGear gear;

    [SerializeField]
    private FirstPersonArms arms;

    [SerializeField]
    private GameObject model;

    [SerializeField]
    private Light beam;

    [SerializeField]
    private Light spill;

    private float focus;
    private Quaternion beamRest;
    private ChestTorchMount mount;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    public float Focus => focus;

    // Held up from code (automated tests, scripted moments), as if the right button were down.
    public bool HoldFocus { get; set; }

    private void Awake()
    {
        mount = controller != null ? controller.GetComponent<ChestTorchMount>() : null;
        if (beam != null)
        {
            beamRest = beam.transform.localRotation;
        }
    }

    private void OnDisable()
    {
        // Clipped to the chest (ChestTorchMount turns this off): no longer held up.
        focus = 0f;
        if (gear != null)
        {
            gear.Focus = 0f;
        }

        if (beam != null)
        {
            beam.transform.localRotation = beamRest;
        }
    }

    private void Update()
    {
        bool onChest = mount != null && mount.IsMounted;
        bool inHand = arms == null || arms.Current == FirstPersonArms.Hands.Torch;
        bool shown = gear != null && gear.HasTorch && (inHand || onChest);
        if (model != null && model.activeSelf != shown)
        {
            model.SetActive(shown);
        }

        if (spill != null && beam != null && spill.enabled != beam.enabled)
        {
            spill.enabled = beam.enabled;
        }

        GameTuning tuning = GameTuning.Instance;
        float dt = Time.deltaTime;
        Mouse mouse = Mouse.current;
        bool raise = !onChest && inHand && gear != null && gear.HasTorch && !controller.InputLocked && Time.timeScale > 0f
            && (HoldFocus || (mouse != null && mouse.rightButton.isPressed));
        focus = Mathf.MoveTowards(focus, raise ? 1f : 0f, dt / Mathf.Max(0.02f, tuning.torchFocusSeconds));
        float f = focus * focus * (3f - 2f * focus);
        if (gear != null)
        {
            gear.Focus = f;
        }

        if (arms != null && inHand && !onChest)
        {
            arms.Aim = raise;
        }
    }

    // After the rig's animation placed the hand: the beam turned toward the point under the crosshair.
    private void LateUpdate()
    {
        if (beam == null || controller == null)
        {
            return;
        }

        GameTuning tuning = GameTuning.Instance;
        Transform eye = controller.CameraPivot;
        Vector3 point = eye.position + eye.forward * Mathf.Max(tuning.torchAimMinDistance, Sight(eye));
        Vector3 to = point - beam.transform.position;
        Quaternion rest = transform.rotation * beamRest;
        Quaternion wanted = to.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(to, eye.up) : rest;
        beam.transform.rotation = Quaternion.RotateTowards(rest, wanted, tuning.torchAimTurn);
        TorchStun.Shine(beam.transform, beam.enabled && focus > 0.8f, controller.transform, Time.deltaTime);
    }

    // Distance along the view to the first thing that is not the player (the crosshair's point).
    private float Sight(Transform eye)
    {
        const float far = 60f;
        int count = Physics.RaycastNonAlloc(eye.position, eye.forward, Hits, far, ~0, QueryTriggerInteraction.Ignore);
        float best = far;
        for (int i = 0; i < count; i++)
        {
            if (Hits[i].distance < best && !Hits[i].transform.IsChildOf(controller.transform))
            {
                best = Hits[i].distance;
            }
        }

        return best;
    }
}
