using System.Collections;
using UnityEngine;

// Runs the demo, following Dayse's script "Sob o Manto" (GDD, "O corte da demo", 2026-09-30):
//  1. Erick arrives at the Luna mansion at night; the security panel blinks its emergency list. On the desk
//     under the dining-room window, a recorder: the first "Alex" recording (the creature imitating him).
//     Erick remarks on the hoarse voice; a tentacle crosses the window behind him; the system states the
//     goals (air conditioning, dehumidifier, before dawn).
//  2. Exploring, he reaches the end of the hall where the bioluminescent fungus spills out of the wrecked
//     wing; thunder, the power dies, only the fungus glows. Goal: find a light.
//  3. Alex's waterproof torch and its batteries (any order), then F (Gustavo, 01/10: a torch instead of
//     Dayse's oil lamp). A second recording appears in a room already explored (backtracking). Worms and
//     scratching in the walls.
//  4. The dehumidifier room (key on the kitchen floor): wrecked. Taking the hooked pole there, a creature
//     breaks the wall and gives chase; it will not follow into the glow at the end of the hall. A
//     recording waits there.
//  5. The attic (the pole pulls its ladder down): the old suit with a respirator, filters, the house map.
//     The mask opens the wing taken by the fungus, thick with spores; the generator room has a keypad, the hint is
//     "Nossa formatura" and the year comes from Erick's remark on the graduation photo.
//  6. The real Alex's note: the generator parts are in a case in the basement (with the basement key).
//     A last recording sends Erick to the flooded pantry (dive) and its green locker: the case, and a small
//     heavy box with his name on it.
//  7. Back on the landing he opens the box: a fragment of the Lumina cave; the fungus surges over the walls,
//     things rise from the water; he shuts it and runs up the stairs. End card.
// The countdown to dawn moves with the story (the panel), not a clock. Deaths (drowned, caught, spores)
// offer a retry at the nearest checkpoint with the progress kept.
// Saving: every step of the story (and every item picked up) writes the checkpoint file (SaveGame); "Continuar" in
// the main menu loads the house with SaveGame.PendingLoad set and Restore rebuilds that moment instead of the opening.
// Every text here is a Loc key (obj.*, msg.*, sys.*, voice.*, death.*): the objective goes to the HUD as a key so it
// follows a language change; messages and subtitles are resolved when shown.
public sealed class FloodedHouseDirector : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private FirstPersonController player;
    [SerializeField] private AirSupply air;
    [SerializeField] private DiveGear gear;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private DiveHud hud;
    [SerializeField] private SuffocationEffects suffocation;
    [SerializeField] private PlayerItems items;
    [SerializeField] private Respirator respirator;

    [Header("House")]
    [SerializeField] private SecurityPanel panel;
    [SerializeField] private PowerGrid power;
    [SerializeField] private LightningStorm storm;
    [SerializeField] private DreadDirector dread;
    [SerializeField] private AudioSource houseAudio;
    [SerializeField] private AudioSource voiceAudio;

    [Header("Recordings")]
    [SerializeField] private Recorder firstRecorder;
    [SerializeField] private Recorder safeRecorder;
    [SerializeField] private Recorder basementRecorder;

    [Header("Scenes")]
    [SerializeField] private CreaturePass windowTentacle;
    [SerializeField] private Bounds wingZone;
    [SerializeField] private Bounds dehumidifierZone;
    [SerializeField] private StoryPickup hookPole;
    [SerializeField] private WallBreak dehumidifierWall;
    [SerializeField] private CreatureChase chase;
    [SerializeField] private Transform chaseRetry;
    [SerializeField] private Transform safeRetry;
    [SerializeField] private ReadableNote generatorNote;
    [SerializeField] private PassageBlocker pantryBlocker;
    [SerializeField] private InspectableObject box;
    [SerializeField] private GameObject luminaGlow;
    [SerializeField] private Transform basementFungus;
    [SerializeField] private CreaturePass basementRising;
    [SerializeField] private Bounds stairsTopZone;

    [Tooltip("After the blackout, something passes these windows now and then (Dayse's scene 3).")]
    [SerializeField] private CreaturePass[] windowStalkers = new CreaturePass[0];
    // The concept art's hall (GDD, 13/10): "a porta para a varanda é de vidro fosco; sombras de criaturas são visíveis".
    [SerializeField] private CreaturePass[] doorStalkers = new CreaturePass[0];
    [SerializeField] private Bounds hallZone;
    [SerializeField] private Bounds eastWingZone;

    [Header("Lab (optional, under the basement)")]
    [SerializeField] private CreaturePass creature;
    [SerializeField] private Bounds labZone;
    [SerializeField] private Bounds airPocketZone;
    [SerializeField] private Bounds tankRoomZone;
    [SerializeField] private Bounds creatureTriggerZone;

    [Header("Places")]
    [SerializeField] private Transform introSpawn;
    [SerializeField] private Transform drownRespawn;
    [SerializeField] private Bounds landingZone;

    [Header("Voices (placeholders until recorded)")]
    [SerializeField] private AudioClip sysAlert;
    [SerializeField] private AudioClip sysPower;
    [SerializeField] private AudioClip sysHumidity;
    [SerializeField] private AudioClip sysThree;
    [SerializeField] private AudioClip sysTwo;
    [SerializeField] private AudioClip erickDehumidifier;
    [SerializeField] private AudioClip erickDark;
    [SerializeField] private AudioClip erickBox;
    [SerializeField] private AudioClip erickLumina;

    [SerializeField] private bool skipIntro;

    // Combat (17/10, GDD: the magnum early, killable small creatures, the hiding at the end).
    [SerializeField] private Inventory inventory;
    [SerializeField] private Revolver revolver;
    [SerializeField] private DemoFinale finale;
    [Tooltip("In the laundry by the broken wall, from the generator room on (the way down to the basement).")]
    [SerializeField] private DrownedBrain laundryDrowned;
    [Tooltip("In the power room off the courtyard gallery, from the blackout on (optional room, a filter and a bandage behind it).")]
    [SerializeField] private DrownedBrain powerRoomDrowned;
    [Tooltip("Half way up the basement stair: the grab that empties the gun before the hiding.")]
    [SerializeField] private Bounds stairGrabZone;

    private enum Stage { Arrive, Listen, Explore, FindLight, Dehumidifier, Chase, FindSuit, ReachGenerators, Basement, Dive, Pantry, Return, OpenBox, Escape, Done }

    private const float SkipHoldSeconds = 0.6f;
    private const string ClimateGoals = "obj.climate";

    private Stage stage = Stage.Arrive;
    private bool dead;
    private bool airHintShown;
    private bool pocketFound;
    private bool reachedDeep;
    private bool dehumidifierSeen;
    private bool mapHintShown;
    private Coroutine introRoutine;
    private float skipHeld;
    private float wingEnteredAt = -1f;
    private float nextStalk = -1f;
    private bool blackoutDone;
    private bool restoring;
    private bool combatHintShown;
    private Vector3 checkpointPosition;
    private float checkpointYaw;
    private bool checkpointSet;
    private Stage presenceStage = (Stage)(-1);
    private int archiveTotal;
    private int tapeTotal;

    private static readonly int MistDisabledId = Shader.PropertyToID("_GroundMistDisabled");

    private void OnEnable()
    {
        // The battlefield's full-screen ground mist would flood the house with fog.
        Shader.SetGlobalFloat(MistDisabledId, 1f);
        air.Drowned += OnDrowned;
        air.Killed += OnKilled;
        interactor.Used += OnUsed;
        gear.Equipped += OnGearEquipped;
        items.ItemAdded += OnItem;
        respirator.Overcome += OnOvercome;
        chase.Caught += OnCaught;
        chase.Repelled += OnRepelled;
        firstRecorder.Finished += OnRecorderFinished;
        safeRecorder.Finished += OnRecorderFinished;
        basementRecorder.Finished += OnRecorderFinished;
        items.ArchiveChanged += OnArchiveChanged;
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(MistDisabledId, 0f);
        air.Drowned -= OnDrowned;
        air.Killed -= OnKilled;
        interactor.Used -= OnUsed;
        gear.Equipped -= OnGearEquipped;
        items.ItemAdded -= OnItem;
        respirator.Overcome -= OnOvercome;
        chase.Caught -= OnCaught;
        chase.Repelled -= OnRepelled;
        firstRecorder.Finished -= OnRecorderFinished;
        safeRecorder.Finished -= OnRecorderFinished;
        basementRecorder.Finished -= OnRecorderFinished;
        items.ArchiveChanged -= OnArchiveChanged;
    }

    private void Start()
    {
        player.Teleport(introSpawn.position, introSpawn.eulerAngles.y);
        safeRecorder.gameObject.SetActive(false);
        box.gameObject.SetActive(false);
        luminaGlow.SetActive(false);
        basementFungus.gameObject.SetActive(false);
        PanelAt(5);
        CountCollectibles();
        SaveGame.Data saved = SaveGame.PendingLoad;
        SaveGame.PendingLoad = null;
        if (saved != null)
        {
            StartCoroutine(Restore(saved));
            return;
        }

        introRoutine = StartCoroutine(skipIntro ? SkipIntro() : Intro());
    }

    // ---------------------------------------------------------------- 1. arrival

    private IEnumerator Intro()
    {
        player.InputLocked = true;
        hud.SetFade(1f);
        yield return new WaitForSeconds(1.2f);
        hud.ShowSubtitle(Loc.T("voice.intro.1"), 3.4f);
        yield return new WaitForSeconds(3.8f);
        hud.ShowSubtitle(Loc.T("voice.intro.2"), 4.2f);
        yield return new WaitForSeconds(4.6f);
        storm.Strike();
        hud.FadeTo(0f, 2f);
        yield return new WaitForSeconds(1.4f);
        player.InputLocked = false;
        Advance(Stage.Listen);
    }

    private IEnumerator SkipIntro()
    {
        hud.SetFade(0f);
        yield return null;
        Advance(Stage.Listen);
    }

    // Hold Space or Enter through the opening to skip it (holding, so a stray tap never does).
    private void UpdateSkip()
    {
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool holding = keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed);
        skipHeld = holding ? skipHeld + Time.deltaTime : Mathf.MoveTowards(skipHeld, 0f, Time.deltaTime * 2f);
        hud.ShowSkipHint(skipHeld / SkipHoldSeconds);
        if (skipHeld < SkipHoldSeconds)
        {
            return;
        }

        StopCoroutine(introRoutine);
        hud.ClearSubtitle();
        hud.FadeTo(0f, 0.5f);
        player.InputLocked = false;
        Advance(Stage.Listen);
    }

    // ---------------------------------------------------------------- flow

    private void Update()
    {
        if (stage != Stage.Done)
        {
            SaveGame.PlaySeconds += Time.deltaTime;
        }

        if (stage != presenceStage)
        {
            presenceStage = stage;
            SteamPresence.Status(PresenceFor(stage));
        }

        Vector3 feet = player.transform.position;
        Vector3 eye = player.EyePosition;
        FirstPersonController.Mode mode = player.CurrentMode;
        hud.SetDiveControls(gear.HasGear && (mode == FirstPersonController.Mode.Diving || mode == FirstPersonController.Mode.SurfaceSwimming));
        UpdateLab(eye);

        switch (stage)
        {
            case Stage.Arrive:
                UpdateSkip();
                break;
            // Checked every frame rather than on an event: a torch switched on (or already on) before the
            // announcements end still moves the story on.
            case Stage.FindLight when blackoutDone && gear.TorchOn:
                Achievements.Unlock(Achievements.Light);
                Advance(Stage.Dehumidifier);
                break;
            case Stage.Explore when wingZone.Contains(feet):
                // A moment to see the fungus with the lights still on, then the storm takes them.
                if (wingEnteredAt < 0f)
                {
                    wingEnteredAt = Time.time;
                }
                else if (Time.time - wingEnteredAt > 2.5f)
                {
                    StartCoroutine(Blackout());
                }

                break;
        }

        UpdateStalkers(feet);
        if (!dehumidifierSeen && stage >= Stage.Explore && dehumidifierZone.Contains(feet))
        {
            dehumidifierSeen = true;
            Say(erickDehumidifier, "voice.erick.dehum");
        }

        if (stage == Stage.Return && landingZone.Contains(feet) && items.HasItem("caixa"))
        {
            Advance(Stage.OpenBox);
        }

        if (stage == Stage.Escape && finale != null && !finale.Grabbed && stairGrabZone.Contains(feet))
        {
            finale.AnkleGrab();
        }

        if (stage == Stage.Escape && stairsTopZone.Contains(feet) && !(finale != null && finale.Running))
        {
            StartCoroutine(End());
        }
    }

    // From the blackout until the basement: now and then a shape crosses one of the east wing's windows, or the hall's
    // frosted door (a shadow behind the glass), and a heavy blow outside drowns out the rain, while the player is there.
    private void UpdateStalkers(Vector3 feet)
    {
        bool inHall = doorStalkers.Length > 0 && hallZone.Contains(feet);
        if (stage < Stage.FindLight || stage >= Stage.Basement || (!inHall && (windowStalkers.Length == 0 || !eastWingZone.Contains(feet))))
        {
            return;
        }

        if (nextStalk < 0f)
        {
            nextStalk = Time.time + Random.Range(25f, 40f);
            return;
        }

        if (Time.time < nextStalk || chase.Running)
        {
            return;
        }

        nextStalk = Time.time + Random.Range(40f, 75f);
        CreaturePass[] here = inHall ? doorStalkers : windowStalkers;
        here[Random.Range(0, here.Length)].PlayAgain();
        houseAudio.PlayOneShot(ProceduralAudio.Crash(), 0.35f);
    }

    private void Advance(Stage next)
    {
        stage = next;
        switch (next)
        {
            case Stage.Listen:
                hud.SetObjectiveKey("obj.search");
                if (!combatHintShown && inventory != null)
                {
                    combatHintShown = true;
                    hud.ShowMessage(Loc.T("msg.combat_hint"), 6f);
                }

                break;
            case Stage.Explore:
                hud.SetObjectiveKey(ClimateGoals);
                break;
            case Stage.FindLight:
                LightObjective();
                WakeEnemies();
                break;
            case Stage.Dehumidifier:
                hud.SetObjectiveKey(items.HasItem("chave_desumidificador") ? "obj.dehum_open" : "obj.dehum_locked");
                break;
            case Stage.Chase:
                hud.SetObjectiveKey("obj.flee");
                SteamPresence.Event(Loc.T("timeline.chase"), string.Empty, "steam_attack", 600, true);
                break;
            case Stage.FindSuit:
                hud.SetObjectiveKey("obj.find_protection");
                break;
            case Stage.ReachGenerators:
                hud.SetObjectiveKey("obj.generators");
                break;
            case Stage.Basement:
                hud.SetObjectiveKey("obj.basement");
                WakeEnemies();
                break;
            case Stage.Dive:
                hud.SetObjectiveKey("obj.dive");
                pantryBlocker.Unlock();
                break;
            case Stage.Return:
                hud.SetObjectiveKey("obj.return");
                break;
            case Stage.OpenBox:
                box.gameObject.SetActive(true);
                hud.SetObjectiveKey("obj.open_box");
                break;
        }

        Checkpoint();
    }

    private void LightObjective()
    {
        if (!gear.HasTorch)
        {
            hud.SetObjectiveKey("obj.find_light");
        }
        else if (!gear.HasBatteries)
        {
            hud.SetObjectiveKey("obj.find_batteries");
        }
        else
        {
            hud.SetObjectiveKey("obj.torch_on");
        }
    }

    private void OnUsed(Interactable used)
    {
        if (used == box && stage == Stage.OpenBox)
        {
            StartCoroutine(OpenTheBox());
        }
        else if (used == generatorNote && stage == Stage.ReachGenerators)
        {
            StartCoroutine(GeneratorRoom());
        }
    }

    private void OnRecorderFinished(Recorder recorder)
    {
        if (recorder == firstRecorder)
        {
            Achievements.Unlock(Achievements.FirstTape);
            StartCoroutine(AfterFirstRecording());
        }
        else if (recorder == safeRecorder && stage == Stage.FindSuit)
        {
            hud.SetObjectiveKey("obj.suit");
        }
        else if (recorder == basementRecorder && !gear.HasGear)
        {
            hud.SetObjectiveKey("obj.gear");
        }
    }

    // The tentacle crosses the window behind Erick, then the system states the goals.
    private IEnumerator AfterFirstRecording()
    {
        yield return new WaitForSeconds(0.4f);
        windowTentacle.Play();
        yield return new WaitForSeconds(2.2f);
        yield return panel.Announce(sysAlert, "sys.alert");
        Advance(Stage.Explore);
    }

    // ---------------------------------------------------------------- 2. blackout

    private IEnumerator Blackout()
    {
        stage = Stage.FindLight;
        storm.Strike(true);
        yield return new WaitForSeconds(0.5f);
        power.Cut();
        dread.Provoke(0.6f);
        yield return new WaitForSeconds(2f);
        PanelAt(4);
        yield return panel.Announce(sysPower, "sys.power");
        yield return panel.Announce(sysHumidity, "sys.humidity");
        if (!gear.TorchOn)
        {
            Say(erickDark, "voice.erick.dark");
            LightObjective();
        }

        blackoutDone = true;
        Checkpoint();
    }

    private void OnItem(string id)
    {
        if (restoring)
        {
            return;
        }

        if (id == "lanterna")
        {
            gear.GiveTorch();
            if (inventory != null)
            {
                inventory.GiveTool(Inventory.Hand.Torch);
            }
        }
        else if (id == "pilhas")
        {
            gear.GiveBatteries();
        }

        if (stage == Stage.FindLight)
        {
            LightObjective();
        }

        if (id == "chave_desumidificador" && stage == Stage.Dehumidifier)
        {
            Advance(Stage.Dehumidifier);
        }
        else if (id == "gancho" && !dehumidifierWall.Broken)
        {
            StartCoroutine(WallBreaks());
        }
        else if (id == "traje")
        {
            respirator.GiveMask(1);
            // The body becomes the suit (same skeleton) and the mask shows on screen (MaskVisor).
            BodySkins skins = player.GetComponentInChildren<BodySkins>();
            if (skins != null)
            {
                skins.Apply("Traje");
            }

            hud.ShowMessage(Loc.T("msg.suit_on"), 5f);
            if (inventory != null)
            {
                // Mechanic "Duas mãos" (GDD, approved 02/10): with the suit the torch rides the chest and the hands are free.
                inventory.SetTorchOnChest(true);
            }

            if (stage <= Stage.FindSuit)
            {
                Advance(Stage.ReachGenerators);
            }
        }
        else if (id.StartsWith("filtro"))
        {
            respirator.AddFilters(1);
        }
        else if (id == "mapa" && !mapHintShown)
        {
            mapHintShown = true;
            hud.ShowMessage(Loc.T("msg.map"), 5f);
        }
        else if (id == "chave_porao")
        {
            Advance(Stage.Basement);
        }
        else if (id == "maleta")
        {
            Achievements.Unlock(Achievements.Case);
            items.Add("caixa");
            hud.ShowMessage(Loc.T("msg.case_rounds"), 4f);
            Advance(Stage.Return);
        }

        Checkpoint();
    }


    // ---------------------------------------------------------------- 4. the chase

    private IEnumerator WallBreaks()
    {
        yield return new WaitForSeconds(1.2f);
        houseAudio.PlayOneShot(SoundLibrary.Instance.Claws(), 1f);
        yield return new WaitForSeconds(1.4f);
        dehumidifierWall.Break();
        yield return new WaitForSeconds(0.3f);
        Advance(Stage.Chase);
        chase.Begin();
    }

    private void OnRepelled()
    {
        StartCoroutine(AfterChase());
    }

    private IEnumerator AfterChase()
    {
        Achievements.Unlock(Achievements.Chase);
        hud.ShowMessage(Loc.T("msg.chase_stopped"), 4.5f);
        PanelAt(3);
        yield return new WaitForSeconds(2.5f);
        panel.Announce(sysThree, "sys.three");
        safeRecorder.gameObject.SetActive(true);
        firstRecorder.gameObject.SetActive(false);
        Advance(Stage.FindSuit);
    }

    private void OnCaught()
    {
        Die("death.caught.title", "death.caught.line", RetryChase);
    }

    private void RetryChase()
    {
        StartCoroutine(Retry(chaseRetry, "msg.chase_retry", () => chase.Begin()));
    }

    // ---------------------------------------------------------------- 5 and 6. generators, basement

    private IEnumerator GeneratorRoom()
    {
        Achievements.Unlock(Achievements.Generators);
        PanelAt(2);
        yield return panel.Announce(sysTwo, "sys.two");
        if (!items.HasItem("chave_porao"))
        {
            hud.SetObjectiveKey("obj.basement_key");
        }
    }

    private void OnGearEquipped()
    {
        if (!restoring)
        {
            Advance(Stage.Dive);
        }
    }

    // ---------------------------------------------------------------- 7. the box

    private IEnumerator OpenTheBox()
    {
        Achievements.Unlock(Achievements.Box);
        SteamPresence.Event(Loc.T("timeline.box"), string.Empty, "steam_chest", 500, true);
        stage = Stage.Escape;
        box.SetPrompt(string.Empty);
        player.InputLocked = true;
        SoundLibrary sounds = SoundLibrary.Instance;
        if (sounds.metalBoxOpen != null)
        {
            houseAudio.PlayOneShot(sounds.metalBoxOpen, 0.9f);
        }

        Say(erickBox, "voice.erick.box");
        yield return new WaitForSeconds(2.8f);
        luminaGlow.SetActive(true);
        if (sounds.luminaTone != null)
        {
            // Its own low voice: the player should learn to hear the Lumina before seeing it.
            houseAudio.PlayOneShot(sounds.luminaTone, 1f);
        }

        Say(erickLumina, "voice.erick.lumina");
        dread.Provoke(0.8f);
        yield return new WaitForSeconds(1.2f);
        // The fungus answers the signal: it swells over the basement walls.
        basementFungus.gameObject.SetActive(true);
        StartCoroutine(Grow(basementFungus, 3.5f));
        houseAudio.PlayOneShot(sounds.Claws(), 1f);
        yield return new WaitForSeconds(2.2f);
        hud.ShowMessage(Loc.T("msg.box_close"), 3.5f);
        yield return new WaitForSeconds(1.2f);
        luminaGlow.SetActive(false);
        houseAudio.PlayOneShot(sounds.metalBoxClose != null ? sounds.metalBoxClose : ProceduralAudio.Beep(220f), 0.8f);
        box.gameObject.SetActive(false);
        items.Add("fragmento");
        basementRising.Play();
        storm.Strike(true);
        player.InputLocked = false;
        hud.SetObjectiveKey("obj.escape");
        Checkpoint();
    }

    private static IEnumerator Grow(Transform group, float seconds)
    {
        Vector3[] scales = new Vector3[group.childCount];
        for (int i = 0; i < scales.Length; i++)
        {
            scales[i] = group.GetChild(i).localScale;
            group.GetChild(i).localScale = Vector3.zero;
        }

        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            for (int i = 0; i < scales.Length; i++)
            {
                // Each patch starts at its own moment, so the growth crawls.
                float local = Mathf.Clamp01((t - i / (float)scales.Length * 0.5f) * 2f);
                group.GetChild(i).localScale = scales[i] * Mathf.SmoothStep(0f, 1f, local);
            }

            yield return null;
        }
    }

    private IEnumerator End()
    {
        stage = Stage.Done;
        player.InputLocked = true;
        hud.SetObjective(string.Empty);
        if (finale != null)
        {
            // The hiding scene (DemoFinale): ends on the shot, in the black.
            SteamPresence.Event(Loc.T("timeline.finale"), string.Empty, "steam_combat", 800, true);
            yield return finale.Run();
            if (finale.PlayerShot)
            {
                Achievements.Unlock(Achievements.OwnShot);
            }

            DemoCompleted();
            hud.ShowEndCard("BENEATH THE SHROUD", Loc.T("demo.end.finale"));
            yield break;
        }

        yield return hud.FadeTo(1f, 2.5f);
        DemoCompleted();
        hud.ShowEndCard("BENEATH THE SHROUD", Loc.T("demo.end.fragment"));
    }

    private static void DemoCompleted()
    {
        SteamPresence.Status(SteamPresence.Done);
        Achievements.Unlock(Achievements.Demo);
        if (SaveGame.Deaths == 0)
        {
            Achievements.Unlock(Achievements.NoDeath);
        }

        SteamPresence.Event(Loc.T("timeline.demo_end"), string.Empty, "steam_completed", 900, false);
    }

    // ---------------------------------------------------------------- Steam: presence and collectibles

    private static string PresenceFor(Stage s)
    {
        switch (s)
        {
            case Stage.Arrive:
            case Stage.Listen:
            case Stage.Explore:
                return SteamPresence.Explore;
            case Stage.FindLight:
            case Stage.Dehumidifier:
                return SteamPresence.Dark;
            case Stage.Chase:
                return SteamPresence.Chase;
            case Stage.FindSuit:
            case Stage.ReachGenerators:
                return SteamPresence.Spores;
            // Done starts with the hiding scene: still "escaping" until the end card (DemoCompleted says Done).
            case Stage.Escape:
            case Stage.Done:
                return SteamPresence.Escape;
            default:
                return SteamPresence.Basement;
        }
    }

    // How many readings (clippings, notices, photos, notes) and recordings the house holds, the hidden ones included:
    // reading or hearing them all is an achievement each. Counted once at Start, by archive id.
    private void CountCollectibles()
    {
        System.Collections.Generic.HashSet<string> readings = new System.Collections.Generic.HashSet<string>();
        foreach (ReadableNote note in FindObjectsByType<ReadableNote>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!string.IsNullOrEmpty(note.ArchiveId))
            {
                readings.Add(note.ArchiveId);
            }
        }

        System.Collections.Generic.HashSet<string> tapes = new System.Collections.Generic.HashSet<string>();
        foreach (Recorder recorder in FindObjectsByType<Recorder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!string.IsNullOrEmpty(recorder.Id))
            {
                tapes.Add(recorder.Id);
            }
        }

        archiveTotal = readings.Count;
        tapeTotal = tapes.Count;
    }

    private void OnArchiveChanged()
    {
        int readings = 0;
        int tapes = 0;
        foreach (PlayerItems.Entry entry in items.Archive)
        {
            if (entry.kind == PlayerItems.EntryKind.Recording)
            {
                tapes++;
            }
            else
            {
                readings++;
            }
        }

        if (archiveTotal > 0 && readings >= archiveTotal)
        {
            Achievements.Unlock(Achievements.Archive);
        }

        if (tapeTotal > 0 && tapes >= tapeTotal)
        {
            Achievements.Unlock(Achievements.Tapes);
        }
    }

    // ---------------------------------------------------------------- lab (optional)

    private void UpdateLab(Vector3 eye)
    {
        // The corridor pass is armed by ANY trip deep into the lab and fires on the way back.
        if (!reachedDeep && (airPocketZone.Contains(eye) || tankRoomZone.Contains(eye)))
        {
            reachedDeep = true;
        }

        if (reachedDeep && !creature.HasPlayed && creatureTriggerZone.Contains(eye))
        {
            creature.Play();
        }

        if (!pocketFound && airPocketZone.Contains(eye) && !air.IsUnderwater)
        {
            pocketFound = true;
            hud.ShowMessage(Loc.T("msg.air_pocket"), 3.5f);
        }
        else if (!airHintShown && !pocketFound && air.IsUnderwater && air.Normalized < 0.55f)
        {
            airHintShown = true;
            hud.ShowMessage(Loc.T("msg.air_low"), 4f);
        }
    }

    // ---------------------------------------------------------------- deaths

    private void OnDrowned()
    {
        if (!dead)
        {
            StartCoroutine(Drown());
        }
    }

    private IEnumerator Drown()
    {
        dead = true;
        CountDeath();
        player.InputLocked = true;
        hud.ClearPrompt();
        yield return suffocation.Blackout(4f);
        hud.SetFade(1f);
        yield return new WaitForSeconds(0.8f);
        hud.ShowDeathCard(Loc.T("death.drowned.title").ToUpperInvariant(), Loc.T("death.drowned.line"), () =>
            StartCoroutine(Retry(drownRespawn, "msg.drown_retry", null)));
    }

    // Beaten down by a Drowned: back to the last checkpoint, the creatures back where they stood.
    private void OnKilled()
    {
        if (stage == Stage.Done || (finale != null && finale.Running))
        {
            return;
        }

        Die("death.killed.title", "death.killed.line", () => StartCoroutine(RetryAtCheckpoint()));
    }

    private IEnumerator RetryAtCheckpoint()
    {
        Transform at = checkpointSet ? null : introSpawn;
        hud.HideCard();
        if (checkpointSet)
        {
            player.Teleport(checkpointPosition, checkpointYaw);
        }
        else
        {
            player.Teleport(at.position, at.eulerAngles.y);
        }

        foreach (DrownedBrain enemy in new[] { laundryDrowned, powerRoomDrowned })
        {
            if (enemy != null && enemy.gameObject.activeSelf && !enemy.IsDead)
            {
                enemy.Calm();
            }
        }

        air.Refill();
        respirator.Restore();
        suffocation.Clear();
        yield return new WaitForSeconds(0.6f);
        hud.ShowMessage(Loc.T("msg.checkpoint_retry"), 5f);
        yield return hud.FadeTo(0f, 1.2f);
        player.InputLocked = false;
        dead = false;
    }

    // The Drowned of the house take their places with the story: the power room's at the blackout, the laundry's
    // once the generator room is done (dead ones stay dead).
    private void WakeEnemies()
    {
        if (powerRoomDrowned != null && stage >= Stage.FindLight && !powerRoomDrowned.IsDead && !deadEnemies.Contains(powerRoomDrowned.name))
        {
            powerRoomDrowned.gameObject.SetActive(true);
        }

        if (laundryDrowned != null && stage >= Stage.Basement && !laundryDrowned.IsDead && !deadEnemies.Contains(laundryDrowned.name))
        {
            laundryDrowned.gameObject.SetActive(true);
        }
    }

    private readonly System.Collections.Generic.HashSet<string> deadEnemies = new System.Collections.Generic.HashSet<string>();

    private void OnOvercome()
    {
        Die("death.spores.title", "death.spores.line", () =>
            StartCoroutine(Retry(safeRetry, "msg.spores_retry", null)));
    }

    // Title and line are Loc keys (death.*); the title is drawn in upper case.
    private void Die(string titleKey, string lineKey, System.Action retry)
    {
        if (!dead)
        {
            StartCoroutine(DieRoutine(titleKey, lineKey, retry));
        }
    }

    private IEnumerator DieRoutine(string titleKey, string lineKey, System.Action retry)
    {
        dead = true;
        CountDeath();
        player.InputLocked = true;
        hud.ClearPrompt();
        hud.SetDamageEdge(1f);
        yield return hud.FadeTo(1f, 0.8f);
        hud.SetDamageEdge(0f);
        yield return new WaitForSeconds(0.6f);
        hud.ShowDeathCard(Loc.T(titleKey).ToUpperInvariant(), Loc.T(lineKey), retry);
    }

    private static void CountDeath()
    {
        SaveGame.Deaths++;
        Achievements.AddStat(Achievements.StatDeaths);
        SteamPresence.Event(Loc.T("timeline.death"), string.Empty, "steam_death", 700, true);
    }

    // Back at a checkpoint with everything already done kept. The message is a Loc key.
    private IEnumerator Retry(Transform at, string messageKey, System.Action then)
    {
        hud.HideCard();
        player.Teleport(at.position, at.eulerAngles.y);
        air.Refill();
        respirator.Restore();
        suffocation.Clear();
        yield return new WaitForSeconds(0.6f);
        hud.ShowMessage(Loc.T(messageKey), 4f);
        yield return hud.FadeTo(0f, 1.2f);
        player.InputLocked = false;
        dead = false;
        then?.Invoke();
    }

    // The security panel at each hour before dawn (the countdown moves with the story). Status lines as Loc keys.
    private void PanelAt(int hours)
    {
        switch (hours)
        {
            case 5:
                panel.SetState(5, 92,
                    "sys.line.dehum_compromised",
                    "sys.line.climate_compromised",
                    "sys.line.main_low",
                    "sys.line.emergency_unknown",
                    "sys.line.heat");
                break;
            case 4:
                panel.SetState(4, 95,
                    "sys.line.climate_off",
                    "sys.line.dehum_off",
                    "sys.line.main_off",
                    "sys.line.emergency_unknown");
                break;
            case 3:
                panel.SetState(3, 95,
                    "sys.line.dehum_silent",
                    "sys.line.structure",
                    "sys.line.climate_off_short",
                    "sys.line.emergency_unknown");
                break;
            default:
                panel.SetState(2, 95,
                    "sys.line.emergency_fail",
                    "sys.line.climate_standby",
                    "sys.line.dehum_silent");
                break;
        }
    }

    // ---------------------------------------------------------------- save and load

    // Writes the checkpoint: where the player stands and everything the story has changed so far.
    private void Checkpoint()
    {
        if (restoring || dead || stage == Stage.Arrive || stage == Stage.Done)
        {
            return;
        }

        SaveGame.Data d = new SaveGame.Data
        {
            scene = gameObject.scene.name,
            stage = stage.ToString(),
            position = player.transform.position,
            yaw = player.transform.eulerAngles.y,
            hasTorch = gear.HasTorch,
            hasBatteries = gear.HasBatteries,
            hasGear = gear.HasGear,
            torchOn = gear.TorchOn,
            hasMask = respirator.HasMask,
            spareFilters = respirator.SpareFilters,
            filter = respirator.Filter,
            powerOn = power.IsOn,
            wallBroken = dehumidifierWall.Broken,
            blockerUnlocked = pantryBlocker.IsUnlocked,
            blockerPushed = pantryBlocker.IsPushed,
            blackoutDone = blackoutDone,
            dehumidifierSeen = dehumidifierSeen,
            mapHintShown = mapHintShown,
            hours = panel.HoursToDawn,
            grabbed = finale != null && finale.Grabbed,
            hand = inventory != null ? inventory.InHand.ToString() : "",
        };
        checkpointPosition = player.transform.position;
        checkpointYaw = player.transform.eulerAngles.y;
        checkpointSet = true;
        if (revolver != null)
        {
            d.roundsLoaded = revolver.Loaded;
            d.roundsSpent = 0;
            d.roundsSpare = revolver.Spare;
            for (int i = 0; i < 6; i++)
            {
                if (revolver.ChamberState(i) == Revolver.ChamberSpent)
                {
                    d.roundsSpent++;
                }
            }
        }

        foreach (DrownedBrain enemy in new[] { laundryDrowned, powerRoomDrowned })
        {
            if (enemy != null && (enemy.IsDead || deadEnemies.Contains(enemy.name)))
            {
                d.enemiesDead.Add(enemy.name);
            }
        }

        d.items.AddRange(items.Owned);
        foreach (PlayerItems.Entry entry in items.Archive)
        {
            d.archive.Add(entry.id);
        }

        foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Include))
        {
            if (door.IsOpen)
            {
                d.doorsOpen.Add(SaveGame.PathOf(door.transform));
            }
        }

        foreach (StoryPickup pickup in FindObjectsByType<StoryPickup>(FindObjectsInactive.Include))
        {
            if (pickup.Taken)
            {
                d.pickupsTaken.Add(SaveGame.PathOf(pickup.transform));
            }
        }

        foreach (GearPickup pickup in FindObjectsByType<GearPickup>(FindObjectsInactive.Include))
        {
            if (pickup.Taken)
            {
                d.pickupsTaken.Add(SaveGame.PathOf(pickup.transform));
            }
        }

        foreach (Recorder recorder in FindObjectsByType<Recorder>(FindObjectsInactive.Include))
        {
            if (recorder.Heard)
            {
                d.recordersHeard.Add(SaveGame.PathOf(recorder.transform));
            }
        }

        AtticHatch hatch = FindAnyObjectByType<AtticHatch>(FindObjectsInactive.Include);
        d.hatchDown = hatch != null && hatch.IsDown;
        SaveGame.Write(d);
    }

    // Rebuilds a checkpoint: what the player carries and has read, what the story has changed in the house, the
    // stage and its objective, the place. Under a black screen, then a fade in.
    private IEnumerator Restore(SaveGame.Data d)
    {
        restoring = true;
        player.InputLocked = true;
        hud.SetFade(1f);
        if (!System.Enum.TryParse(d.stage, out Stage target) || target == Stage.Arrive)
        {
            target = Stage.Listen;
        }

        foreach (string id in d.items)
        {
            items.AddSilently(id);
        }

        Recorder[] recorders = FindObjectsByType<Recorder>(FindObjectsInactive.Include);
        ReadableNote[] notes = FindObjectsByType<ReadableNote>(FindObjectsInactive.Include);
        foreach (string id in d.archive)
        {
            foreach (Recorder recorder in recorders)
            {
                if (recorder.Id == id)
                {
                    recorder.RememberIn(items);
                    break;
                }
            }

            foreach (ReadableNote note in notes)
            {
                if (note.ArchiveId == id)
                {
                    note.RememberIn(items);
                    break;
                }
            }
        }

        foreach (Recorder recorder in recorders)
        {
            if (d.recordersHeard.Contains(SaveGame.PathOf(recorder.transform)))
            {
                recorder.MarkHeard();
            }
        }

        foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Include))
        {
            if (d.doorsOpen.Contains(SaveGame.PathOf(door.transform)))
            {
                door.RestoreOpen();
            }
        }

        foreach (StoryPickup pickup in FindObjectsByType<StoryPickup>(FindObjectsInactive.Include))
        {
            if (d.pickupsTaken.Contains(SaveGame.PathOf(pickup.transform)))
            {
                pickup.RestoreTaken();
            }
        }

        foreach (GearPickup pickup in FindObjectsByType<GearPickup>(FindObjectsInactive.Include))
        {
            if (d.pickupsTaken.Contains(SaveGame.PathOf(pickup.transform)))
            {
                pickup.RestoreTaken();
            }
        }

        gear.RestoreState(d.hasTorch, d.hasBatteries, d.hasGear, d.torchOn);
        respirator.RestoreState(d.hasMask, d.spareFilters, d.filter);
        if (d.hasMask)
        {
            BodySkins skins = player.GetComponentInChildren<BodySkins>();
            if (skins != null)
            {
                skins.Apply("Traje");
            }
        }

        if (!d.powerOn)
        {
            power.RestoreCut();
        }

        if (d.wallBroken)
        {
            dehumidifierWall.RestoreBroken();
        }

        AtticHatch hatch = FindAnyObjectByType<AtticHatch>(FindObjectsInactive.Include);
        if (d.hatchDown && hatch != null)
        {
            hatch.SetDown();
        }

        if (d.blockerPushed)
        {
            pantryBlocker.RestorePushed();
        }
        else if (d.blockerUnlocked)
        {
            pantryBlocker.Unlock();
        }

        blackoutDone = d.blackoutDone;
        dehumidifierSeen = d.dehumidifierSeen;
        mapHintShown = d.mapHintShown;
        combatHintShown = true;
        foreach (string name in d.enemiesDead)
        {
            deadEnemies.Add(name);
        }

        if (revolver != null && d.roundsLoaded >= 0)
        {
            revolver.SetAmmo(d.roundsLoaded, d.roundsSpent, d.roundsSpare);
        }

        if (inventory != null)
        {
            if (d.hasTorch)
            {
                inventory.GiveTool(Inventory.Hand.Torch);
            }

            if (d.hasMask)
            {
                inventory.SetTorchOnChest(true);
            }

            if (System.Enum.TryParse(d.hand, out Inventory.Hand hand))
            {
                inventory.Equip(hand, true);
            }
        }

        if (d.grabbed && finale != null)
        {
            finale.RestoreGrabbed();
        }

        if (target >= Stage.FindSuit)
        {
            safeRecorder.gameObject.SetActive(true);
            firstRecorder.gameObject.SetActive(false);
        }

        PanelAt(Mathf.Clamp(d.hours, 2, 5));
        player.Teleport(d.position, d.yaw);
        checkpointPosition = d.position;
        checkpointYaw = d.yaw;
        checkpointSet = true;

        // The stage and its objective (Advance writes no save while restoring).
        if (target == Stage.FindLight)
        {
            stage = Stage.FindLight;
            LightObjective();
        }
        else if (target >= Stage.Escape)
        {
            stage = Stage.Escape;
            basementFungus.gameObject.SetActive(true);
            items.AddSilently("fragmento");
            hud.SetObjectiveKey("obj.escape");
        }
        else
        {
            Advance(target);
        }

        if (target == Stage.Chase)
        {
            player.Teleport(chaseRetry.position, chaseRetry.eulerAngles.y);
        }

        WakeEnemies();
        restoring = false;
        yield return null;
        yield return hud.FadeTo(0f, 1.2f);
        player.InputLocked = false;
        if (target == Stage.Chase)
        {
            chase.Begin();
        }
        else if (target >= Stage.Escape)
        {
            basementRising.Play();
        }
    }

    // Erick's line: the clip (Portuguese) and its subtitle (a voice.* key, in the chosen language).
    private void Say(AudioClip clip, string key)
    {
        if (clip != null)
        {
            voiceAudio.PlayOneShot(clip, 1f);
        }

        hud.ShowSubtitle("ERICK: " + Loc.T(key), (clip != null ? clip.length : 3f) + 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        DrawZone(wingZone, new Color(0.4f, 1f, 0.7f));
        DrawZone(dehumidifierZone, Color.yellow);
        DrawZone(landingZone, Color.green);
        DrawZone(stairsTopZone, Color.magenta);
        DrawZone(stairGrabZone, new Color(1f, 0.5f, 0f));
        DrawZone(labZone, Color.cyan);
        DrawZone(creatureTriggerZone, Color.red);
    }

    private static void DrawZone(Bounds zone, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireCube(zone.center, zone.size);
    }
}
