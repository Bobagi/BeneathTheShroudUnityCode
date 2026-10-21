using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// The end of the demo (GDD "O fim da demo: o esconderijo", decision of Gustavo 09/10; the shape is the Claude
// proposal there, placed in the laundry because the basement stair comes up in it, 25 m from the kitchen):
//   On the stair up from the basement a Drowned crawls after Erick and grabs at his legs: the gun empties itself
//   on it (every round left, then the dry click) and the thing is kicked back down (AnkleGrab). So the hiding
//   always starts with an empty gun, whatever the player spent before. The loose rounds from Alex's case are the
//   only spare of the demo and only now go to the reserve (there was no time to load on the way).
//   At the top of the stair the climate control hums back for a moment and dies with the emergency lights; a
//   Drowned comes in through the broken wall and stops in the dark. Erick switches the torch off, crouches behind
//   the smashed dehumidifier and the washing machines (no cut, first person all along; the view still peeks within a
//   cone) and opens the cylinder, slowly.
//   Slow path: one round every GameTuning.finaleSecondsPerRound seconds while the Drowned searches the room
//   (DrownedBrain.Patrol); on round finaleLightningRound a lightning bolt throws Erick's shadow on the wall and it
//   comes. Fast path (R at any moment): the rest poured in, one round rings on the floor, the cylinder slammed
//   shut: it comes at once, from farther.
//   Both: Erick rises over the counter, time slows (finaleSlowMotion for finaleSlowSeconds), the crosshair is on
//   it and "Clique: Atirar" shows; the player fires, or Erick does at finaleAutoShotDistance. The flash is the
//   last frame: black at once, the report's tail, the ringing, the rain; after finaleTitleDelay the title card.
//   Holding Space or Enter skips to the shot.
// Hands: the rig's finale clips (Hide_Crouch, Revolver_OpenSlow, Revolver_LoadOne, Revolver_LoadFast,
// Revolver_CloseSlow, Revolver_RiseAim in Art/Animations/FirstPerson/Final) play when they exist; until Gustavo
// makes them in Blender the hands stay in the hold and only the gun's state changes (Revolver.BeginHandReload,
// LoadOneRound, LoadFast, EndHandReload). The timing lives here and in GameTuning, not in the clips, so the clips
// can come later without touching the scene.
public sealed class DemoFinale : MonoBehaviour
{
    [SerializeField] private FirstPersonController player;
    [SerializeField] private AirSupply air;
    [SerializeField] private DiveGear gear;
    [SerializeField] private DiveHud hud;
    [SerializeField] private Inventory inventory;
    [SerializeField] private Revolver revolver;
    [SerializeField] private FirstPersonArms arms;
    [SerializeField] private LightningStorm storm;
    [SerializeField] private DreadDirector dread;
    [SerializeField] private AudioSource houseAudio;

    [Tooltip("The Drowned of the hiding scene, inactive until it comes in through the broken wall.")]
    [SerializeField] private DrownedBrain searcher;

    [Tooltip("Where it appears (the hole) and the points of its search, in order; it loops them.")]
    [SerializeField] private Transform entry;
    [SerializeField] private Transform[] searchPath = new Transform[0];

    [Tooltip("The Drowned of the stair grab, inactive until the player is on the stair with the box.")]
    [SerializeField] private DrownedBrain grabber;

    [Tooltip("Where the grabber starts (the landing, by the water) and where it is kicked back into.")]
    [SerializeField] private Transform grabStart;
    [SerializeField] private Transform grabWater;

    [Tooltip("Where Erick hides: position and facing (its forward is the centre of the look cone).")]
    [SerializeField] private Transform hideSpot;

    [Tooltip("Emergency lights that come back for a moment and die.")]
    [SerializeField] private Light[] emergencyLights = new Light[0];

    [Tooltip("The climate control's hum (a loop), rising back for a moment and dying.")]
    [SerializeField] private AudioSource hum;

    [Tooltip("Loose rounds in Alex's case: the only spare of the demo, into the reserve when the hiding starts.")]
    [SerializeField] private int looseRounds = 6;

    private const float SkipHoldSeconds = 0.6f;
    private const float LookConeYaw = 60f;
    private const float LookConePitch = 35f;

    private bool running;
    private bool fast;
    private bool fired;
    private bool autoFiring;
    private bool skip;
    private float skipHeld;
    private float fixedStep;

    public bool Grabbed { get; private set; }

    // The last shot was the player's click, not the automatic one (achievement "Sangue frio").
    public bool PlayerShot { get; private set; }
    public bool Running => running;
    public event System.Action Finished;

    private void Awake()
    {
        fixedStep = Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = fixedStep;
    }

    // Loading a checkpoint past the stair: the grab is over.
    public void RestoreGrabbed()
    {
        Grabbed = true;
        if (grabber != null)
        {
            grabber.gameObject.SetActive(false);
        }
    }

    // ---------------------------------------------------------------- the stair grab

    public Coroutine AnkleGrab()
    {
        if (Grabbed)
        {
            return null;
        }

        Grabbed = true;
        return StartCoroutine(GrabRoutine());
    }

    private IEnumerator GrabRoutine()
    {
        if (grabber == null || grabStart == null)
        {
            yield break;
        }

        player.InputLocked = true;
        inventory.Locked = true;
        inventory.Equip(Inventory.Hand.Revolver, true);
        grabber.transform.SetPositionAndRotation(grabStart.position, grabStart.rotation);
        grabber.gameObject.SetActive(true);
        grabber.CrawlAfter();
        Transform head = grabber.Eyes != null ? grabber.Eyes : grabber.transform;
        // Out of the water behind him: a splash, a wet drag, the claws on the steps.
        houseAudio.PlayOneShot(SoundLibrary.Instance.Splash(0.8f), 1f);
        houseAudio.PlayOneShot(SoundLibrary.Instance.Claws(), 0.9f);
        dread.Provoke(0.9f);
        // The grab: the view is yanked down to it.
        yield return new WaitForSeconds(0.35f);
        player.LookAt(head.position);
        player.Jolt(-5f, 6f);
        air.Damage(GameTuning.Instance.drownedBiteDamage * 0.5f);
        hud.ShowSubtitle("ERICK: " + Loc.T("voice.erick.grab"), 2f);
        yield return new WaitForSeconds(0.45f);

        // Every round left goes into it, then the hammer falls on nothing: the gun is empty for what comes.
        revolver.HoldAim = true;
        int shots = 0;
        while (revolver.Loaded > 0 && shots < 6 && grabber.gameObject.activeSelf)
        {
            player.LookAt(head.position);
            revolver.Fire();
            shots++;
            yield return new WaitForSeconds(0.42f);
        }

        if (!grabber.IsDead)
        {
            player.LookAt(head.position);
        }

        revolver.Fire();
        yield return new WaitForSeconds(0.35f);
        revolver.Fire();
        revolver.HoldAim = false;
        yield return new WaitForSeconds(0.3f);

        // The kick: whatever is left of it goes back down into the water.
        player.Kick(-4f, -3f);
        grabber.Dismiss(grabWater != null ? grabWater.position : grabStart.position, 2.5f);
        houseAudio.PlayOneShot(SoundLibrary.Instance.Splash(1f), 1f);
        yield return new WaitForSeconds(0.6f);
        // Back up the stair: the view returns to the way up.
        player.LookAt(player.transform.position + Vector3.forward * 4f + Vector3.up * 2.2f);
        hud.ShowMessage(Loc.T(revolver.Spare > 0 ? "msg.gun_empty_spare" : "msg.gun_empty"), 3.5f);
        inventory.Locked = false;
        player.InputLocked = false;
    }

    // ---------------------------------------------------------------- the hiding

    public Coroutine Run()
    {
        if (running)
        {
            return null;
        }

        running = true;
        return StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        GameTuning tuning = GameTuning.Instance;
        player.InputLocked = true;
        inventory.Locked = true;
        hud.SetObjective(string.Empty);
        hud.ClearPrompt();
        inventory.Equip(Inventory.Hand.Revolver, true);
        revolver.AddSpare(looseRounds);

        // 1. The climate control hums back and the emergency lights come on... and all of it dies at once.
        if (hum != null)
        {
            hum.volume = 0f;
            hum.Play();
        }

        if (SoundLibrary.Instance.machinePowerDown != null)
        {
            // The machine's own wind-down under the hum: its fall lands with the cut.
            houseAudio.PlayOneShot(SoundLibrary.Instance.machinePowerDown, 0.5f);
        }

        for (float t = 0f; t < 1.4f && !skip; t += Time.deltaTime)
        {
            float k = t / 1.4f;
            if (hum != null)
            {
                hum.volume = 0.5f * k;
            }

            foreach (Light l in emergencyLights)
            {
                if (l != null)
                {
                    l.enabled = true;
                    l.intensity = 1.2f * k;
                }
            }

            yield return null;
        }

        yield return new WaitForSeconds(0.6f);
        SoundLibrary sounds = SoundLibrary.Instance;
        AudioClip cut = sounds.PowerCut();
        if (cut != null)
        {
            houseAudio.PlayOneShot(cut, 0.9f);
        }

        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.5f;
            if (hum != null)
            {
                hum.volume = 0.5f * k;
                hum.pitch = Mathf.Lerp(1f, 0.6f, 1f - k);
            }

            foreach (Light l in emergencyLights)
            {
                if (l != null)
                {
                    l.intensity = 1.2f * k * Random.Range(0.5f, 1f);
                }
            }

            yield return null;
        }

        if (hum != null)
        {
            hum.Stop();
            hum.pitch = 1f;
        }

        foreach (Light l in emergencyLights)
        {
            if (l != null)
            {
                l.enabled = false;
            }
        }

        dread.Provoke(0.7f);

        // 2. The thing comes in through the broken wall and stops in the dark.
        yield return new WaitForSeconds(0.5f);
        houseAudio.PlayOneShot(sounds.doorBurst != null ? sounds.doorBurst : ProceduralAudio.Crash(), 0.8f);
        houseAudio.PlayOneShot(sounds.Claws(), 0.9f);
        if (searcher != null && entry != null)
        {
            searcher.transform.SetPositionAndRotation(entry.position, entry.rotation);
            searcher.gameObject.SetActive(true);
            Vector3[] points = new Vector3[searchPath.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = searchPath[i].position;
            }

            searcher.Patrol(points, 2.6f, true);
        }

        // 3. Hide: torch off, crouch, back against the counter; the view peeks within a cone.
        gear.SendMessage("SetTorch", false, SendMessageOptions.DontRequireReceiver);
        arms.PlayHands("Hide_Crouch");
        yield return SlideTo(hideSpot, 1.4f);
        player.HoldCrouch = true;
        player.LookConeCentreYaw = hideSpot.eulerAngles.y;
        player.LookConeCentrePitch = 8f;
        player.LookConeYaw = LookConeYaw;
        player.LookConePitch = LookConePitch;
        player.MovementLocked = true;
        player.InputLocked = false;
        yield return new WaitForSeconds(1.2f);

        // 4. The cylinder opens, slowly; the fired cases go to the hand and the floor without a sound.
        arms.PlayHands("Revolver_OpenSlow");
        revolver.BeginHandReload();
        yield return new WaitForSeconds(1.2f);
        hud.SetPrompt("R", Loc.T("prompt.reload_fast"));

        // 5. The slow way, round by round, unless R.
        int round = 0;
        float nextRound = Time.time + tuning.finaleSecondsPerRound * 0.6f;
        float nextPan = Time.time + 6f;
        float nextCall = Time.time + 2f;
        int lastCall = -1;
        AudioSource breath = null;
        if (searcher != null && sounds.drownedBreathLoop != null)
        {
            // Its breathing, a step away.
            breath = searcher.gameObject.AddComponent<AudioSource>();
            breath.clip = sounds.drownedBreathLoop;
            breath.loop = true;
            breath.spatialBlend = 1f;
            breath.minDistance = 1.2f;
            breath.maxDistance = 9f;
            breath.volume = 0.7f;
            breath.Play();
        }

        while (!skip && !fast)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                fast = true;
            }

            if (fast)
            {
                break;
            }

            if (Time.time >= nextRound)
            {
                if (revolver.EmptyChambers == 0 || revolver.Spare == 0)
                {
                    break;
                }

                arms.PlayHands("Revolver_LoadOne", 0.1f);
                revolver.LoadOneRound();
                round++;
                nextRound = Time.time + tuning.finaleSecondsPerRound;
                dread.Provoke(0.25f);
                if (round >= tuning.finaleLightningRound)
                {
                    // The lightning throws his shadow on the wall: it was the storm, not the player.
                    storm.Strike(true);
                    yield return new WaitForSeconds(0.25f);
                    searcher?.Alert();
                    break;
                }
            }

            if (Time.time >= nextPan && searcher != null)
            {
                // Something knocked over in the dark, from where it is.
                nextPan = Time.time + 9f;
                AudioSource.PlayClipAtPoint(sounds.potDrop != null ? sounds.potDrop : ProceduralAudio.Crash(), searcher.transform.position + Vector3.up, 0.45f);
            }

            if (Time.time >= nextCall && searcher != null && sounds.drownedSearch.Length > 0)
            {
                // Its calls between the steps, from its throat.
                nextCall = Time.time + Random.Range(3.5f, 6f);
                AudioSource.PlayClipAtPoint(SoundLibrary.Pick(sounds.drownedSearch, ref lastCall), searcher.transform.position + Vector3.up * 1.5f, 0.8f);
            }

            yield return null;
        }

        hud.ClearPrompt();
        if (fast && !skip)
        {
            // The rest poured in, one rings on the floor, the cylinder slammed: it was the player.
            arms.PlayHands("Revolver_LoadFast", 0.1f);
            revolver.LoadFast();
            yield return new WaitForSeconds(0.7f);
            revolver.EndHandReload(true);
            yield return new WaitForSeconds(0.2f);
            searcher?.Alert();
        }
        else if (!skip)
        {
            arms.PlayHands("Revolver_CloseSlow", 0.1f);
            revolver.EndHandReload(false);
        }
        else
        {
            revolver.LoadFast();
            revolver.EndHandReload(false);
            searcher?.Alert();
        }

        // 6. Up over the counter, the gun on it; time slows; the shot is the player's, or Erick's at the last moment.
        yield return new WaitForSeconds(0.4f);
        player.FreeLook();
        player.MovementLocked = true;
        arms.PlayHands("Revolver_RiseAim", 0.15f);
        Transform target = searcher != null ? (searcher.Eyes != null ? searcher.Eyes : searcher.transform) : null;
        if (target != null)
        {
            player.LookAt(target.position + Vector3.down * 0.1f);
        }

        revolver.HoldAim = true;
        revolver.Fired += OnFired;
        fired = false;
        SetTimeScale(tuning.finaleSlowMotion);
        hud.SetPrompt(Loc.T("hud.key.click"), Loc.T("prompt.shoot"));
        float slowUntil = Time.unscaledTime + tuning.finaleSlowSeconds;
        float autoAt = Time.unscaledTime + 9f;
        while (!fired)
        {
            if (Time.unscaledTime > slowUntil)
            {
                SetTimeScale(1f);
            }

            float distance = target != null ? Vector3.Distance(player.transform.position, searcher.transform.position) : 99f;
            bool gone = searcher == null || searcher.IsDead || !searcher.gameObject.activeSelf;
            if (skip || gone || distance < tuning.finaleAutoShotDistance || Time.unscaledTime > autoAt)
            {
                if (target != null && !gone)
                {
                    player.LookAt(target.position);
                }

                autoFiring = true;
                bool shot = revolver.Fire();
                autoFiring = false;
                if (!shot)
                {
                    // No round found its way in (nothing to load): the hammer's click is the end all the same.
                    fired = true;
                }
            }

            yield return null;
        }

        revolver.Fired -= OnFired;
        revolver.HoldAim = false;

        // 7. The flash was the last thing seen.
        SetTimeScale(1f);
        hud.ClearPrompt();
        hud.SetFade(1f);
        player.InputLocked = true;
        if (breath != null)
        {
            breath.Stop();
        }

        if (sounds.gunshotTailIndoor != null)
        {
            houseAudio.PlayOneShot(sounds.gunshotTailIndoor, 0.9f);
        }

        if (searcher != null)
        {
            searcher.gameObject.SetActive(false);
        }

        AudioSource ring = gameObject.AddComponent<AudioSource>();
        ring.spatialBlend = 0f;
        ring.loop = true;
        ring.volume = 0.35f;
        ring.clip = ProceduralAudio.RingingLoop();
        ring.Play();
        yield return new WaitForSeconds(tuning.finaleTitleDelay);
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            ring.volume = 0.35f * (1f - t / 1.5f);
            yield return null;
        }

        ring.Stop();
        running = false;
        Finished?.Invoke();
    }

    private void OnFired()
    {
        fired = true;
        PlayerShot = !autoFiring;
    }

    // R from code (automated tests without window focus).
    public void PressFast()
    {
        fast = true;
    }

    private void SetTimeScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = fixedStep * scale;
    }

    // Walks the player to the spot over `seconds` (the controller off meanwhile, so nothing fights the move), turning
    // to face its forward.
    private IEnumerator SlideTo(Transform spot, float seconds)
    {
        if (spot == null)
        {
            yield break;
        }

        Vector3 from = player.transform.position;
        float yawFrom = player.transform.eulerAngles.y;
        float yawTo = spot.eulerAngles.y;
        player.enabled = false;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            player.transform.position = Vector3.Lerp(from, spot.position, k);
            player.transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(yawFrom, yawTo, k), 0f);
            yield return null;
        }

        player.transform.position = spot.position;
        player.transform.rotation = Quaternion.Euler(0f, yawTo, 0f);
        player.enabled = true;
    }

    private void Update()
    {
        if (!running || fired)
        {
            return;
        }

        // Hold Space or Enter: straight to the shot and the black.
        Keyboard keyboard = Keyboard.current;
        bool holding = keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed);
        skipHeld = holding ? skipHeld + Time.unscaledDeltaTime : Mathf.MoveTowards(skipHeld, 0f, Time.unscaledDeltaTime * 2f);
        hud.ShowSkipHint(skipHeld / SkipHoldSeconds);
        if (skipHeld >= SkipHoldSeconds)
        {
            skip = true;
        }
    }
}
