using System;
using System.Collections.Generic;
using UnityEngine;

// The first person arms rig (the "view model" of any shooter), 10/10. A plain Generic skeleton of the two arms
// (clavicle, upper arm, forearm, hand, fifteen finger bones each: the same bone names as the player's body) that
// hangs from the camera: CameraPivot > ViewModel (ViewModelMotion: sway, bob, recoil) > FirstPersonArms (this).
// Everything the hands do in first person is a fixed AnimationClip played by this object's Animator
// (Art/Animations/FirstPerson, controller FirstPersonArms.controller):
//   layer "Mãos"           Empty, Torch_Hold, Torch_Focus, Revolver_Hold, Revolver_Aim, Revolver_Reload
//   layer "Ação"           Empty, Cough (left arm only)
//   layer "Mãos gravadas"  Empty and one state per take baked from video (Editor/FirstPerson/FirstPersonTakes)
// The weapon and the torch are children of sockets on the hands (hand_r/WeaponSocket, hand_r/TorchSocket,
// hand_l/LeftSocket), so a clip that moves the hand moves what it holds; no IK puts a hand on anything.
// The body (PlayerBody, through ArmsFromRig) copies these arm bones onto its own skeleton while an arm is in use, so
// the one visible body shows the clip's arms and the arms are always where the camera expects them.
// Animation Events of the clips land here (ReloadOpen, ReloadEject, ...) and go out through AnimationEvent.
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(-20)]
public sealed class FirstPersonArms : MonoBehaviour
{
    public enum Hands
    {
        Empty = 0,
        Torch = 1,
        Revolver = 2,
    }

    public const string HandsLayer = "Mãos";
    public const string ActionLayer = "Ação";
    public const string TakesLayer = "Mãos gravadas";
    public const string EmptyState = "Empty";
    public const string CoughState = "Cough";
    public const string TorchHoldState = "Torch_Hold";
    public const string TorchFocusState = "Torch_Focus";
    public const string RevolverHoldState = "Revolver_Hold";
    public const string RevolverAimState = "Revolver_Aim";
    public const string RevolverReloadState = "Revolver_Reload";
    public const string RevolverSprintState = "Revolver_Sprint";
    public const string RevolverFireState = "Revolver_Fire";
    public const string HandsParameter = "Hands";
    public const string AimParameter = "Aim";
    public const string ReloadParameter = "Reload";
    public const string SprintParameter = "Sprint";
    public const string FireParameter = "Fire";

    // Socket names on the hands (children of hand_r / hand_l in the prefab).
    public const string WeaponSocketName = "WeaponSocket";
    public const string TorchSocketName = "TorchSocket";
    public const string LeftSocketName = "LeftSocket";

    private static readonly int HandsId = Animator.StringToHash(HandsParameter);
    private static readonly int AimId = Animator.StringToHash(AimParameter);
    private static readonly int ReloadId = Animator.StringToHash(ReloadParameter);
    private static readonly int SprintId = Animator.StringToHash(SprintParameter);
    private static readonly int FireId = Animator.StringToHash(FireParameter);

    [Tooltip("Where the revolver hangs: a child of hand_r, measured so the grip sits in the palm.")]
    [SerializeField]
    private Transform weaponSocket;

    [Tooltip("Where the dive torch hangs: a child of hand_r.")]
    [SerializeField]
    private Transform torchSocket;

    [Tooltip("What the left hand holds (the speedloader): a child of hand_l.")]
    [SerializeField]
    private Transform leftSocket;

    [Tooltip("The camera's own move in a clip (degrees, down): keyed by Revolver_Reload so the eye looks down at the "
        + "open cylinder while the arms stay where they are (CameraTilt applies it). Only the reload's value counts.")]
    [SerializeField]
    private float cameraPitch;

    private Animator animator;
    private float cameraPitchNow;
    private int actionLayer = -1;
    private int takesLayer = -1;
    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private float rightOwned;
    private float leftOwned;

    public Animator Animator => animator;
    public Transform WeaponSocket => weaponSocket;
    public Transform TorchSocket => torchSocket;
    public Transform LeftSocket => leftSocket;
    public Hands Current { get; private set; }

    // 0 to 1: how much each arm of the body should follow this rig right now (the clip's arm is in use), smoothed
    // over the Animator's own transitions. 0 = the body's own animation keeps the arm (walk swing, swim stroke).
    public float RightArmWeight => rightOwned;
    public float LeftArmWeight => leftOwned;

    // Degrees the camera looks down for the clip playing (the reload's camera key), eased back to 0 when the reload is
    // over or cut short (a curve left at its last value by Write Defaults off never sticks).
    public float CameraPitch => cameraPitchNow;

    public bool TakePlaying => takesLayer >= 0 && LayerWeight(takesLayer) > 0.001f;
    public bool ActionPlaying => actionLayer >= 0 && LayerWeight(actionLayer) > 0.001f;

    // The reload's events, by name: ReloadOpen, ReloadEject, ReloadRoundsIn, ReloadClosed, ReloadFinished.
    public event Action<string> AnimationEvent;

    public bool Aim
    {
        get => animator != null && animator.GetBool(AimId);
        set
        {
            if (animator != null)
            {
                animator.SetBool(AimId, value);
            }
        }
    }

    // Running (Shift): the weapon states have a sprint pose (Revolver_Sprint); ViewModelMotion sets it.
    public bool Sprint
    {
        get => animator != null && animator.GetBool(SprintId);
        set
        {
            if (animator != null)
            {
                animator.SetBool(SprintId, value);
            }
        }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        actionLayer = animator.runtimeAnimatorController != null ? animator.GetLayerIndex(ActionLayer) : -1;
        takesLayer = animator.runtimeAnimatorController != null ? animator.GetLayerIndex(TakesLayer) : -1;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            bones[t.name] = t;
        }
    }

    public Transform Bone(string name)
    {
        if (bones.Count == 0)
        {
            // Outside Play (the Animation window previewing a clip, ArmsFromRig in the editor) Awake never ran.
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                bones[t.name] = t;
            }
        }

        return bones.TryGetValue(name, out Transform found) ? found : null;
    }

    public void SetHands(Hands hands)
    {
        Current = hands;
        if (animator != null)
        {
            animator.SetInteger(HandsId, (int)hands);
        }
    }

    public void Reload()
    {
        if (animator != null)
        {
            animator.SetTrigger(ReloadId);
        }
    }

    // A shot: plays the Revolver_Fire clip when the controller has one (a clip made in Blender); otherwise nothing,
    // the kick is procedural.
    public void Fire()
    {
        if (animator != null && animator.HasState(0, Animator.StringToHash(RevolverFireState)))
        {
            animator.SetTrigger(FireId);
        }
    }

    public bool IsReloading
    {
        get
        {
            if (animator == null)
            {
                return false;
            }

            return animator.GetCurrentAnimatorStateInfo(0).IsName(RevolverReloadState)
                || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(RevolverReloadState));
        }
    }

    // Cuts a reload short (a shot fired with rounds still in): straight back to the hold.
    public void CancelReload()
    {
        if (animator != null && IsReloading)
        {
            animator.ResetTrigger(ReloadId);
            animator.CrossFadeInFixedTime(RevolverHoldState, 0.1f, 0, 0f);
        }
    }

    public bool Cough()
    {
        return PlayOn(actionLayer, CoughState, 0.15f);
    }

    // A state of the hands layer by name, when the controller has it (the finale's clips in
    // Art/Animations/FirstPerson/Final: Hide_Crouch, Revolver_OpenSlow, Revolver_LoadOne, Revolver_LoadFast,
    // Revolver_CloseSlow, Revolver_RiseAim). False when the clip does not exist yet: the caller falls back.
    public bool PlayHands(string state, float blend = 0.2f)
    {
        if (animator == null || !animator.HasState(0, Animator.StringToHash(state)))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(state, blend, 0, 0f);
        return true;
    }

    public bool HasHandsState(string state)
    {
        return animator != null && animator.HasState(0, Animator.StringToHash(state));
    }

    // Back to whatever the Hands parameter says (after a scripted state): re-enters the hold.
    public void ResumeHands()
    {
        if (animator == null)
        {
            return;
        }

        string state = Current == Hands.Revolver ? RevolverHoldState : Current == Hands.Torch ? TorchHoldState : EmptyState;
        animator.CrossFadeInFixedTime(state, 0.25f, 0, 0f);
    }

    public bool PlayTake(string state)
    {
        return PlayOn(takesLayer, state, 0.25f);
    }

    public void StopTake()
    {
        PlayOn(takesLayer, EmptyState, 0.25f);
    }

    public bool HasTake(string state)
    {
        return takesLayer >= 0 && animator.HasState(takesLayer, Animator.StringToHash(state));
    }

    private bool PlayOn(int layer, string state, float blend)
    {
        if (layer < 0 || !animator.HasState(layer, Animator.StringToHash(state)))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(state, blend, layer, 0f);
        return true;
    }

    // Hangs an item on a socket (a scene builder or a pickup): the Animator binds the item's own animated parts (the
    // revolver's crane, the speedloader's visibility) only after a rebind.
    public void Attach(Transform item, Transform socket)
    {
        item.SetParent(socket, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;
        if (animator != null && Application.isPlaying)
        {
            animator.Rebind();
            animator.SetInteger(HandsId, (int)Current);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float right = 0f;
        float left = 0f;
        OwnedByHandsLayer(ref right, ref left);
        float action = actionLayer >= 0 ? LayerWeight(actionLayer) : 0f;
        float take = takesLayer >= 0 ? LayerWeight(takesLayer) : 0f;
        left = Mathf.Max(left, action, take);
        right = Mathf.Max(right, take);
        rightOwned = Mathf.MoveTowards(rightOwned, right, dt * 6f);
        leftOwned = Mathf.MoveTowards(leftOwned, left, dt * 6f);
        float pitch = IsReloading ? cameraPitch : 0f;
        cameraPitchNow = Mathf.Lerp(cameraPitchNow, pitch, 1f - Mathf.Exp(-12f * dt));
    }

    // The hands layer: a Torch_* state owns the right arm, a Revolver_* state both, Empty neither; blended through
    // the transition in progress.
    private void OwnedByHandsLayer(ref float right, ref float left)
    {
        if (animator == null)
        {
            return;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        StateOwnership(current, out float r0, out float l0);
        if (!animator.IsInTransition(0))
        {
            right = r0;
            left = l0;
            return;
        }

        StateOwnership(animator.GetNextAnimatorStateInfo(0), out float r1, out float l1);
        float t = animator.GetAnimatorTransitionInfo(0).normalizedTime;
        right = Mathf.Lerp(r0, r1, t);
        left = Mathf.Lerp(l0, l1, t);
    }

    private static readonly int TorchHoldHash = Animator.StringToHash(TorchHoldState);
    private static readonly int TorchFocusHash = Animator.StringToHash(TorchFocusState);
    private static readonly int RevolverHoldHash = Animator.StringToHash(RevolverHoldState);
    private static readonly int RevolverAimHash = Animator.StringToHash(RevolverAimState);
    private static readonly int RevolverReloadHash = Animator.StringToHash(RevolverReloadState);
    private static readonly int RevolverSprintHash = Animator.StringToHash(RevolverSprintState);
    private static readonly int RevolverFireHash = Animator.StringToHash(RevolverFireState);

    private static void StateOwnership(AnimatorStateInfo state, out float right, out float left)
    {
        int hash = state.shortNameHash;
        if (hash == TorchHoldHash || hash == TorchFocusHash)
        {
            right = 1f;
            left = 0f;
        }
        else if (hash == RevolverHoldHash || hash == RevolverAimHash || hash == RevolverReloadHash || hash == RevolverSprintHash || hash == RevolverFireHash)
        {
            right = 1f;
            left = 1f;
        }
        else
        {
            right = 0f;
            left = 0f;
        }
    }

    // 0 with the layer on Empty, 1 with a clip playing, in between while it crossfades.
    private float LayerWeight(int layer)
    {
        if (animator.GetLayerWeight(layer) <= 0.01f)
        {
            return 0f;
        }

        float current = animator.GetCurrentAnimatorStateInfo(layer).IsName(EmptyState) ? 0f : 1f;
        if (!animator.IsInTransition(layer))
        {
            return current;
        }

        float next = animator.GetNextAnimatorStateInfo(layer).IsName(EmptyState) ? 0f : 1f;
        return Mathf.Lerp(current, next, animator.GetAnimatorTransitionInfo(layer).normalizedTime);
    }

    // ---------------------------------------------------------------- Animation Events (function names in the clips)

    public void ReloadOpen()
    {
        AnimationEvent?.Invoke("ReloadOpen");
    }

    public void ReloadEject()
    {
        AnimationEvent?.Invoke("ReloadEject");
    }

    public void ReloadRoundsIn()
    {
        AnimationEvent?.Invoke("ReloadRoundsIn");
    }

    public void ReloadClosed()
    {
        AnimationEvent?.Invoke("ReloadClosed");
    }

    public void ReloadFinished()
    {
        AnimationEvent?.Invoke("ReloadFinished");
    }

}
