using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Hotbar-style inventory. Every carried item is a real WorldItem: the selected one sits in the
// right hand, the rest hang on the belt. Pick up (E), select (1-4 / mouse wheel), drop (G).
[DefaultExecutionOrder(-50)]
public sealed class PlayerInventory : MonoBehaviour
{
    [Header("Sockets")]
    [SerializeField]
    private Transform rightHandSocket;

    [SerializeField]
    private Transform beltSocket;

    [Header("Rules")]
    [SerializeField]
    [Range(1, 4)]
    private int slotCount = 3;

    [SerializeField]
    private float pickupRadius = 2.4f;

    [SerializeField]
    private float switchCooldownSeconds = 0.25f;

    [SerializeField]
    private float dropForwardSpeed = 2.2f;

    [Header("References")]
    [SerializeField]
    private Camera viewCamera;

    [SerializeField]
    private PlayerHandsController hands;

    [SerializeField]
    private AudioSource handlingAudio;

    [Header("Fallback sounds")]
    [SerializeField]
    private AudioClip defaultPickupSound;

    [SerializeField]
    private AudioClip defaultDropSound;

    [SerializeField]
    private AudioClip defaultSwitchSound;

    private WorldItem[] slots;
    private int selectedSlot;
    private bool isBusy;
    private float nextSwitchTime;
    private readonly Collider[] overlapBuffer = new Collider[48];

    private InputAction interactAction;
    private InputAction dropAction;
    private InputAction cycleAction;
    private InputAction nextAction;
    private InputAction previousAction;
    private InputAction primaryAction;
    private InputAction aimAction;
    private InputAction reloadAction;
    private InputAction toggleAction;
    private InputAction[] slotActions;

    public event Action Changed;

    public int SlotCount => slotCount;
    public int SelectedSlot => selectedSlot;
    public WorldItem HeldItem => slots[selectedSlot];
    public WorldItem FocusedItem { get; private set; }
    public bool IsAiming { get; private set; }

    // Lets other systems (tutorial prompts, automated tests) hold the raise/aim state.
    public bool AimOverride { get; set; }
    public bool IsBusy => isBusy;
    public Camera ViewCamera => viewCamera;

    public WorldItem GetSlot(int index) => slots[index];

    public void NotifyChanged() => Changed?.Invoke();

    private void Awake()
    {
        slots = new WorldItem[slotCount];
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        CreateInput();
    }

    private void OnEnable()
    {
        EnableInput(true);
    }

    private void OnDisable()
    {
        EnableInput(false);
    }

    private void OnDestroy()
    {
        foreach (InputAction action in AllActions())
        {
            action.Dispose();
        }
    }

    private void Update()
    {
        FocusedItem = isBusy ? null : FindFocusedItem();

        WorldItem held = HeldItem;
        HeldItemBehaviour heldBehaviour = held != null ? held.GetComponent<HeldItemBehaviour>() : null;
        bool wantsAim = held != null && !isBusy && (aimAction.IsPressed() || AimOverride);
        if (wantsAim != IsAiming)
        {
            IsAiming = wantsAim;
            if (heldBehaviour != null)
            {
                heldBehaviour.NotifyRaised(IsAiming);
            }

            Changed?.Invoke();
        }

        if (heldBehaviour == null || isBusy)
        {
            return;
        }

        if (primaryAction.WasPressedThisFrame())
        {
            Ray aimRay = GetAimRay();
            heldBehaviour.OnPrimaryAction(aimRay, GetAimPoint(aimRay));
            Changed?.Invoke();
        }

        if (reloadAction.WasPressedThisFrame())
        {
            heldBehaviour.OnReloadAction();
            Changed?.Invoke();
        }

        if (toggleAction.WasPressedThisFrame())
        {
            heldBehaviour.OnToggleAction();
            Changed?.Invoke();
        }
    }

    public Ray GetAimRay()
    {
        Transform cameraTransform = viewCamera.transform;
        return new Ray(cameraTransform.position, cameraTransform.forward);
    }

    public Vector3 GetAimPoint(Ray aimRay)
    {
        // Start the ray past the player so the character never blocks its own aim.
        float skip = Vector3.Distance(aimRay.origin, transform.position) * 0.9f;
        Ray trimmed = new Ray(aimRay.origin + aimRay.direction * skip, aimRay.direction);
        if (Physics.Raycast(trimmed, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(transform))
        {
            return hit.point;
        }

        return aimRay.origin + aimRay.direction * 400f;
    }

    // ---------- Pickup ----------

    private void HandleInteract(InputAction.CallbackContext _)
    {
        TryPickupFocused();
    }

    public bool TryPickupFocused()
    {
        if (isBusy || FocusedItem == null)
        {
            return false;
        }

        StartCoroutine(PickupRoutine(FocusedItem));
        return true;
    }

    private IEnumerator PickupRoutine(WorldItem item)
    {
        isBusy = true;
        Changed?.Invoke();

        int targetSlot = FindFreeSlot();
        if (targetSlot < 0)
        {
            // Hands full: whatever is in the selected slot is swapped for the new item.
            targetSlot = selectedSlot;
            DropSlot(selectedSlot);
        }

        if (hands != null)
        {
            yield return hands.PlayReach(item.InteractionPoint);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }

        if (item == null || !item.CanBePickedUp)
        {
            isBusy = false;
            Changed?.Invoke();
            yield break;
        }

        slots[targetSlot] = item;
        PlayClip(item.Definition != null ? item.Definition.pickupSound : null, defaultPickupSound);

        // The freshly picked item goes straight to the hand; what was held moves to the belt.
        SelectSlotImmediate(targetSlot, true);

        if (hands != null)
        {
            yield return hands.PlayReturn();
        }

        isBusy = false;
        Changed?.Invoke();
    }

    private int FindFreeSlot()
    {
        if (slots[selectedSlot] == null)
        {
            return selectedSlot;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private WorldItem FindFocusedItem()
    {
        if (viewCamera == null)
        {
            return null;
        }

        int count = Physics.OverlapSphereNonAlloc(
            transform.position + Vector3.up * 0.8f,
            pickupRadius,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide
        );

        Vector3 cameraPosition = viewCamera.transform.position;
        Vector3 cameraForward = viewCamera.transform.forward;
        WorldItem best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < count; i++)
        {
            WorldItem candidate = overlapBuffer[i].GetComponentInParent<WorldItem>();
            if (candidate == null || !candidate.CanBePickedUp)
            {
                continue;
            }

            Vector3 toItem = candidate.InteractionPoint.position - cameraPosition;
            float distance = toItem.magnitude;
            float facing = Vector3.Dot(cameraForward, toItem / Mathf.Max(distance, 0.001f));
            if (facing < 0.35f)
            {
                continue;
            }

            float playerDistance = Vector3.Distance(transform.position, candidate.transform.position);
            float score = facing * 0.8f + (1f - Mathf.Clamp01(playerDistance / pickupRadius)) * 0.2f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    // ---------- Selection ----------

    public void RequestSelect(int slotIndex)
    {
        if (isBusy || slotIndex < 0 || slotIndex >= slots.Length || slotIndex == selectedSlot)
        {
            return;
        }

        if (Time.time < nextSwitchTime)
        {
            return;
        }

        nextSwitchTime = Time.time + switchCooldownSeconds;
        SelectSlotImmediate(slotIndex, false);
    }

    private void SelectSlotImmediate(int slotIndex, bool forceRefresh)
    {
        WorldItem previous = slots[selectedSlot];
        if (!forceRefresh && slotIndex == selectedSlot)
        {
            return;
        }

        if (previous != null && selectedSlot != slotIndex)
        {
            Stow(previous);
        }

        selectedSlot = slotIndex;
        WorldItem next = slots[selectedSlot];
        if (next != null)
        {
            Equip(next);
            PlayClip(next.Definition != null ? next.Definition.equipSound : null, defaultSwitchSound);
        }
        else if (previous != null)
        {
            PlayClip(null, defaultSwitchSound);
        }

        Changed?.Invoke();
    }

    private void Cycle(int direction)
    {
        // Skip empty slots so the wheel only walks through carried items (and empty hands).
        int index = selectedSlot;
        for (int step = 0; step < slots.Length; step++)
        {
            index = (index + direction + slots.Length) % slots.Length;
            if (slots[index] != null)
            {
                RequestSelect(index);
                return;
            }
        }
    }

    private void Equip(WorldItem item)
    {
        ItemDefinition definition = item.Definition;
        item.AttachTo(
            rightHandSocket,
            definition != null ? definition.heldPositionOffset : Vector3.zero,
            Quaternion.Euler(definition != null ? definition.heldRotationEuler : Vector3.zero)
        );
        item.SetVisible(true);

        HeldItemBehaviour behaviour = item.GetComponent<HeldItemBehaviour>();
        if (behaviour != null)
        {
            behaviour.NotifyEquipped(this);
            // Aim may still be held across a switch; Update only resyncs on aim transitions.
            behaviour.NotifyRaised(IsAiming);
        }
    }

    private void Stow(WorldItem item)
    {
        HeldItemBehaviour behaviour = item.GetComponent<HeldItemBehaviour>();
        if (behaviour != null)
        {
            behaviour.NotifyUnequipped();
        }

        ItemDefinition definition = item.Definition;
        item.AttachTo(
            beltSocket,
            definition != null ? definition.stowedPositionOffset : Vector3.zero,
            Quaternion.Euler(definition != null ? definition.stowedRotationEuler : Vector3.zero)
        );
        item.SetVisible(definition == null || definition.visibleWhenStowed);
    }

    // ---------- Drop ----------

    private void HandleDrop(InputAction.CallbackContext _)
    {
        DropSelected();
    }

    public bool DropSelected()
    {
        if (isBusy || slots[selectedSlot] == null)
        {
            return false;
        }

        DropSlot(selectedSlot);
        Changed?.Invoke();
        return true;
    }

    private void DropSlot(int slotIndex)
    {
        WorldItem item = slots[slotIndex];
        if (item == null)
        {
            return;
        }

        HeldItemBehaviour behaviour = item.GetComponent<HeldItemBehaviour>();
        if (behaviour != null && slotIndex == selectedSlot)
        {
            behaviour.NotifyUnequipped();
        }

        slots[slotIndex] = null;
        if (slotIndex == selectedSlot)
        {
            IsAiming = false;
        }

        Vector3 releasePosition = ResolveDropPosition(item);
        Vector3 velocity = transform.forward * dropForwardSpeed + Vector3.up * 1.2f;
        item.ReleaseToWorld(releasePosition, item.transform.rotation, velocity);
        item.SetVisible(true);
        PlayClip(item.Definition != null ? item.Definition.dropSound : null, defaultDropSound);
    }

    private Vector3 ResolveDropPosition(WorldItem item)
    {
        // Release from the hand, but never inside a wall: pull it back toward the chest if blocked.
        Vector3 chest = transform.position + Vector3.up * 1.2f;
        Vector3 desired = transform.position + transform.forward * 0.6f + Vector3.up * 1.0f;
        Vector3 toDesired = desired - chest;
        if (Physics.SphereCast(chest, 0.12f, toDesired.normalized, out RaycastHit hit,
                toDesired.magnitude, ~0, QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(transform))
        {
            return chest + toDesired.normalized * Mathf.Max(0f, hit.distance - 0.1f);
        }

        return desired;
    }

    // ---------- Input ----------

    private void CreateInput()
    {
        interactAction = new InputAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonNorth");

        dropAction = new InputAction("Drop", InputActionType.Button);
        dropAction.AddBinding("<Keyboard>/g");
        dropAction.AddBinding("<Gamepad>/dpad/down");

        cycleAction = new InputAction("Cycle", InputActionType.Value, "<Mouse>/scroll/y");
        nextAction = new InputAction("Next", InputActionType.Button, "<Gamepad>/rightShoulder");
        previousAction = new InputAction("Previous", InputActionType.Button, "<Gamepad>/leftShoulder");

        primaryAction = new InputAction("Primary", InputActionType.Button);
        primaryAction.AddBinding("<Mouse>/leftButton");
        primaryAction.AddBinding("<Gamepad>/rightTrigger");

        aimAction = new InputAction("Aim", InputActionType.Button);
        aimAction.AddBinding("<Mouse>/rightButton");
        aimAction.AddBinding("<Gamepad>/leftTrigger");

        reloadAction = new InputAction("Reload", InputActionType.Button);
        reloadAction.AddBinding("<Keyboard>/r");
        reloadAction.AddBinding("<Gamepad>/buttonWest");

        toggleAction = new InputAction("Toggle", InputActionType.Button);
        toggleAction.AddBinding("<Keyboard>/f");
        toggleAction.AddBinding("<Gamepad>/dpad/up");

        slotActions = new InputAction[4];
        for (int i = 0; i < slotActions.Length; i++)
        {
            slotActions[i] = new InputAction("Slot" + (i + 1), InputActionType.Button, "<Keyboard>/" + (i + 1));
            int captured = i;
            slotActions[i].performed += _ => RequestSelect(captured);
        }

        interactAction.performed += HandleInteract;
        dropAction.performed += HandleDrop;
        nextAction.performed += _ => Cycle(1);
        previousAction.performed += _ => Cycle(-1);
        cycleAction.performed += context =>
        {
            float scroll = context.ReadValue<float>();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Cycle(scroll > 0f ? -1 : 1);
            }
        };
    }

    private InputAction[] AllActions()
    {
        InputAction[] all = new InputAction[9 + slotActions.Length];
        all[0] = interactAction;
        all[1] = dropAction;
        all[2] = cycleAction;
        all[3] = nextAction;
        all[4] = previousAction;
        all[5] = primaryAction;
        all[6] = aimAction;
        all[7] = reloadAction;
        all[8] = toggleAction;
        Array.Copy(slotActions, 0, all, 9, slotActions.Length);
        return all;
    }

    private void EnableInput(bool enable)
    {
        if (interactAction == null)
        {
            return;
        }

        foreach (InputAction action in AllActions())
        {
            if (enable)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }
    }

    private void PlayClip(AudioClip clip, AudioClip fallback)
    {
        AudioClip chosen = clip != null ? clip : fallback;
        if (handlingAudio != null && chosen != null)
        {
            handlingAudio.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
            handlingAudio.PlayOneShot(chosen);
        }
    }
}
