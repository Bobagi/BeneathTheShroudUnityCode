using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The player's revolver, held in both hands (mechanic "Duas mãos" of the GDD: with the suit the torch rides the
// chest, so both hands are free). Hangs on the first person rig's weapon socket (FirstPersonArms, hand_r/WeaponSocket):
// where the gun is on the screen is the rig's clips (Revolver_Hold, Revolver_Aim, Revolver_Reload, in
// Art/Animations/FirstPerson/Revolver, edited in the Animation window), the hands are fixed on it by those clips, and
// the sway, bob and the kick are the ViewModelMotion on the pivot above the rig. This is the gun itself:
//   Right mouse: aims (the rig's Aim state; bullets go where the view points; from the hip they spread 2.5 degrees).
//   Left mouse: fires (double action, 0.32 s between shots): flash, report, the arms and the view kick, the hammer
//   falls, the cylinder turns a sixth. Empty: the hammer clicks on nothing.
//   R: reloads (Colt Python .357, swing-out cylinder): the rig's Revolver_Reload clip swings the cylinder out (the
//   crane is keyed in it), tips the gun up, the left hand pushes the ejector rod and the fired cases fall, a
//   speedloader from the belt puts the rounds in, the cylinder closes. The clip owns the timing; its events come
//   back through FirstPersonArms.AnimationEvent and are answered here with what depends on the ammunition. Firing a
//   loaded gun cuts the reload short.
// The bullet is a ray from the eye: an IBulletTarget (creature hitbox) takes a Shot; anything else gets dust and a
// push if it has a body. The menu "Horror > Combate > Mira do revólver" (re)builds the crosshair prefab.
public sealed class Revolver : MonoBehaviour
{
    [SerializeField]
    private FirstPersonController controller;

    [SerializeField]
    private PlayerBody body;

    [Tooltip("The first person arms rig this gun hangs on: its states aim and reload, its events time the reload.")]
    [SerializeField]
    private FirstPersonArms arms;

    [Tooltip("The procedural motion of the arms (sway, bob, kick), on the pivot above the rig.")]
    [SerializeField]
    private ViewModelMotion motion;

    [SerializeField]
    private Transform model;

    [SerializeField]
    private Transform muzzle;

    [SerializeField]
    private Transform cylinder;

    [Tooltip("A point on the cylinder's real axis, in the cylinder bone's space: the bone's own origin is 3 mm off it, "
        + "and turning about the bone made the cylinder wobble out of the frame (measured by the builder, CombatTestSetup).")]
    [SerializeField]
    private Vector3 cylinderPivot;

    [SerializeField]
    private Transform hammer;

    [SerializeField]
    private Light flash;

    [SerializeField]
    private ParticleSystem flashSmoke;

    [SerializeField]
    private AudioSource source;

    [SerializeField]
    private TMP_FontAsset font;

    [Tooltip("The bright star at the muzzle on a shot (a single additive particle) and its sparks.")]
    [SerializeField]
    private ParticleSystem flashSprite;

    [SerializeField]
    private ParticleSystem flashSparks;

    [Tooltip("The blast: a short tongue of fire out of the muzzle.")]
    [SerializeField]
    private ParticleSystem flashFire;

    [Tooltip("Gas and flame out of the gap between cylinder and barrel, to both sides (a revolver's own flash).")]
    [SerializeField]
    private ParticleSystem[] gapFlash = new ParticleSystem[0];

    [Tooltip("Decals, smoke and blood where bullets land (Editor/CombatEffectsSetup).")]
    [SerializeField]
    private ImpactEffects impacts;

    [Tooltip("The speedloader's six rounds (on the rig's left socket): only as many as go in are shown.")]
    [SerializeField]
    private Renderer[] speedloaderRounds = new Renderer[0];

    [Tooltip("An empty case, thrown out of the open cylinder for each fired round when reloading.")]
    [SerializeField]
    private SpentCase casePrefab;

    [Tooltip("Soft particle material for the dust a bullet kicks up (Art/VFX/Combat/BulletDust).")]
    [SerializeField]
    private Material impactMaterial;

    [Header("Ammunition (demo: 6 and no spare, GDD; the test scene gives spare rounds)")]
    [SerializeField]
    private int loaded = 6;

    [SerializeField]
    private int spare = 0;

    [Header("Ballistics")]
    [SerializeField]
    private float damage = 45f;

    [SerializeField]
    private float impulse = 5.5f;

    [SerializeField]
    private float range = 80f;

    [SerializeField]
    private float hipSpread = 2.5f;

    [SerializeField]
    private float aimSpread = 0.25f;

    [Tooltip("The crosshair, a prefab (Prefabs/UI/RevolverCrosshair, made by Horror > Combate > Mira do revólver).")]
    [SerializeField]
    private RevolverCrosshair crosshairPrefab;

    private const int Capacity = 6;
    // The six chambers: what a reload animation has to show (which ones empty their cases, which ones get a round).
    public const byte ChamberEmpty = 0;
    public const byte ChamberLive = 1;
    public const byte ChamberSpent = 2;
    private readonly byte[] chambers = { ChamberLive, ChamberLive, ChamberLive, ChamberLive, ChamberLive, ChamberLive };
    private int nextChamber;
    private const float ShotInterval = 0.32f;

    private float aim;
    private float nextShot;
    private float flip;
    private float flipSpeed;
    private float hammerBack;
    private float cylinderAngle;
    private float cylinderTarget;
    private Quaternion cylinderRest;
    private Vector3 cylinderRestPosition;
    // The cylinder's axis in its bone's space: parallel to the bore (this object's +Z). The bone's own Y is about 11
    // degrees off it, and turning about the bone's Y tipped the cylinder out of the frame shot after shot (09/10).
    private Vector3 cylinderAxis = Vector3.up;
    private Quaternion hammerRest;
    private Quaternion modelRest;
    private Vector3 modelRestPosition;
    private CharacterController characterController;
    private bool reloading;
    private readonly List<int> ejecting = new List<int>();
    private readonly List<int> inserting = new List<int>();
    private TextMeshProUGUI ammoLabel;
    private RevolverCrosshair crosshair;
    // Fired cases still in the cylinder (a revolver keeps them until it is reloaded).
    private int spent;
    // Shots in the last seconds: the barrel smokes for a while after (a wisp now and then, rising).
    private float barrelHeat;
    private float nextWisp;
    private SpentCase[] cases;
    private int nextCase;
    // What the open cylinder shows, per chamber: the six rounds of the rig (hidden outside a reload) and their bones;
    // a fired chamber shows a case from the pool, held in it until the ejector throws it. The author's clips pull the
    // rounds 2.5 cm back out of the chambers at the end of Open (his own extraction), so during a reload the bones are
    // kept in their chambers from the pose they have on the closed gun, relative to the cylinder bone.
    private SkinnedMeshRenderer[] chamberRounds;
    private Transform[] chamberBones;
    private Vector3[] chamberInCylinder;
    private Quaternion[] chamberTurnInCylinder;
    private readonly SpentCase[] heldCases = new SpentCase[Capacity];
    private ParticleSystem dust;
    private float sightDistance = 20f;
    private float bloom;
    private float moving;
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    // The reload in progress, for anything that shows it chamber by chamber: chambers that throw a case out, and
    // chambers that get a round from the speedloader.
    public int[] ReloadEjectChambers { get; private set; } = new int[0];
    public int[] ReloadInsertChambers { get; private set; } = new int[0];
    public byte ChamberState(int chamber) => chambers[chamber];
    // One case left a chamber (its index) / one round went into a chamber, during a reload.
    public event System.Action<int> CaseEjected;
    public event System.Action<int> RoundInserted;

    // Test and scripted setups: the gun with `liveRounds` live chambers, `spentCases` fired ones and the rest empty.
    public void SetAmmo(int liveRounds, int spentCases, int spareRounds)
    {
        for (int i = 0; i < Capacity; i++)
        {
            chambers[i] = i < liveRounds ? ChamberLive : i < liveRounds + spentCases ? ChamberSpent : ChamberEmpty;
        }

        loaded = liveRounds;
        spent = spentCases;
        spare = spareRounds;
        nextChamber = 0;
        UpdateHud();
    }

    public int Loaded => loaded;
    public int Spare => spare;
    public bool IsAiming => aim > 0.5f;
    public bool IsReloading => reloading;
    public float Aim => aim;
    public Transform Muzzle => muzzle;
    // What is under the crosshair right now can be shot (a creature's hitbox).
    public bool OnTarget { get; private set; }
    // Degrees a bullet may stray from the crosshair right now (stance, movement and the last shots).
    public float Spread => Mathf.Lerp(hipSpread, aimSpread, aim)
        + GameTuning.Instance.revolverMoveSpread * moving * Mathf.Lerp(1f, 0.4f, aim) + bloom;
    // A bullet struck something that reacts: true when it killed it.
    public event System.Action<bool> Hit;
    public event System.Action Fired;
    // Held aim from code (automated tests, scripted moments), as if the right button were down.
    public bool HoldAim { get; set; }

    private void Start()
    {
        if (cylinder != null)
        {
            cylinderRest = cylinder.localRotation;
            cylinderRestPosition = cylinder.localPosition;
            cylinderAxis = cylinder.InverseTransformDirection(transform.forward).normalized;
        }

        if (hammer != null)
        {
            hammerRest = hammer.localRotation;
        }

        if (model != null)
        {
            modelRest = model.localRotation;
            modelRestPosition = model.localPosition;
        }

        if (flash != null)
        {
            flash.enabled = false;
        }

        FindChambers();
        characterController = controller.GetComponent<CharacterController>();
        BuildHud();
        UpdateHud();
    }

    private void OnEnable()
    {
        if (arms != null)
        {
            arms.AnimationEvent += OnArmsEvent;
        }

        if (crosshair != null)
        {
            crosshair.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (arms != null)
        {
            arms.AnimationEvent -= OnArmsEvent;
            arms.Aim = false;
        }

        // Put away in the middle of a reload: the reload ends here.
        reloading = false;
        ClearChambers();
        if (crosshair != null)
        {
            crosshair.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool locked = controller.InputLocked || Time.timeScale == 0f;
        bool aiming = !locked && (HoldAim || (mouse != null && mouse.rightButton.isPressed)) && !reloading;
        aim = Mathf.MoveTowards(aim, aiming ? 1f : 0f, Time.deltaTime / 0.18f);
        if (arms != null)
        {
            arms.Aim = aiming;
        }

        if (!locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Fire();
        }

        if (!locked && keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            Reload();
        }

        if (crosshair != null)
        {
            crosshair.Show(!locked);
        }
    }

    // Fires as the player would (also called by tests).
    public bool Fire()
    {
        if (Time.time < nextShot)
        {
            return false;
        }

        if (reloading)
        {
            if (loaded == 0)
            {
                return false;
            }

            reloading = false;
            ClearChambers();
            if (arms != null)
            {
                arms.CancelReload();
            }
        }

        nextShot = Time.time + ShotInterval;
        SoundLibrary sounds = SoundLibrary.Instance;
        int pick = -1;
        if (loaded == 0)
        {
            Play(SoundLibrary.Pick(sounds.magnumDryFire, ref pick), 0.9f);
            hammerBack = 1f;
            return false;
        }

        for (int i = 0; i < Capacity; i++)
        {
            int c = (nextChamber + i) % Capacity;
            if (chambers[c] == ChamberLive)
            {
                chambers[c] = ChamberSpent;
                nextChamber = (c + 1) % Capacity;
                break;
            }
        }

        loaded--;
        spent++;
        barrelHeat = Mathf.Min(barrelHeat + 1f, 4f);
        Play(SoundLibrary.Pick(sounds.magnumShots, ref pick), 1f);
        if (sounds.magnumShotTail != null && source != null)
        {
            source.PlayOneShot(sounds.magnumShotTail, 0.35f);
        }

        // The bullet leaves along the view before the recoil lifts it (kicking first sent shots 2 degrees high).
        Shoot();
        bloom = Mathf.Min(bloom + GameTuning.Instance.revolverShotBloom, 6f);
        Fired?.Invoke();
        StartCoroutine(Flash());
        // The kick: the arms go back and up (ViewModelMotion), the gun itself flips in the hands a little, the hammer
        // falls, the cylinder turns, and the view jumps and mostly comes back on its own.
        GameTuning tuning = GameTuning.Instance;
        if (motion != null)
        {
            motion.Kick(1f);
        }

        if (arms != null)
        {
            arms.Fire();
        }

        flipSpeed += 40f * Random.Range(0.9f, 1.1f);
        hammerBack = 1f;
        cylinderTarget += 60f;
        controller.Recoil(tuning.revolverCameraKick * Random.Range(0.85f, 1.15f) * Mathf.Lerp(1f, 0.7f, aim),
            Random.Range(-0.6f, 0.6f), tuning.revolverCameraKickReturn);
        UpdateHud();
        return true;
    }

    private void Shoot()
    {
        Transform eye = controller.CameraPivot;
        float spread = Spread;
        Vector3 direction = Quaternion.AngleAxis(Random.Range(0f, 360f), eye.forward)
            * Quaternion.AngleAxis(Random.Range(0f, spread), eye.right) * eye.forward;
        RaycastHit[] hits = Physics.RaycastAll(eye.position, direction, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (Own(hit.transform))
            {
                continue;
            }

            Shot shot = new Shot
            {
                point = hit.point,
                direction = direction,
                normal = hit.normal,
                damage = damage,
                impulse = impulse,
                collider = hit.collider,
            };
            IBulletTarget target = hit.collider.GetComponentInParent<IBulletTarget>();
            if (target != null)
            {
                target.TakeShot(shot);
                CreatureVitals vitals = hit.collider.GetComponentInParent<CreatureVitals>();
                bool killed = vitals != null && vitals.IsDead;
                if (impacts != null)
                {
                    CreatureHitbox box = hit.collider.GetComponent<CreatureHitbox>();
                    bool pumping = box == null || box.region == BodyRegion.Head || box.region == BodyRegion.Torso;
                    impacts.Blood(hit.point, direction, hit.normal, vitals != null ? vitals.transform : hit.collider.transform.root, killed, hit.collider.transform, pumping);
                }

                Hit?.Invoke(killed);
                return;
            }

            Dust(hit);
            if (impacts != null)
            {
                impacts.Bullet(hit, direction);
            }

            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                hit.rigidbody.AddForceAtPosition(direction * impulse, hit.point, ForceMode.Impulse);
            }

            return;
        }
    }

    // A box of rounds from the inventory.
    public void AddSpare(int rounds)
    {
        spare += Mathf.Max(0, rounds);
        UpdateHud();
    }

    // ---------------------------------------------------------------- scripted reload (the demo's finale, GDD 09/10)
    // The hidden Erick loads the gun by hand, round by round: the script says when each step happens (the clips of
    // the rig, when they exist, or timers), these do what each step does to the gun. Firing is not possible meanwhile
    // (IsReloading), as in the normal reload.

    // Opens the cylinder quietly: the fired cases leave the chambers one by one into the hand (no sound, no throw).
    public void BeginHandReload()
    {
        if (reloading)
        {
            return;
        }

        reloading = true;
        ejecting.Clear();
        inserting.Clear();
        ShowChambers();
        for (int i = 0; i < Capacity; i++)
        {
            if (chambers[i] == ChamberSpent)
            {
                chambers[i] = ChamberEmpty;
                spent--;
                if (heldCases[i] != null)
                {
                    heldCases[i].Release();
                    heldCases[i] = null;
                }

                CaseEjected?.Invoke(i);
            }
        }

        if (chamberRounds != null)
        {
            for (int i = 0; i < Capacity; i++)
            {
                chamberRounds[i].enabled = chambers[i] == ChamberLive;
            }
        }

        SoundLibrary library = SoundLibrary.Instance;
        if (library.casesIntoHand != null)
        {
            Play(library.casesIntoHand, 0.55f);
        }
        else
        {
            Play(library.magnumCylinderSpin, 0.22f);
        }

        if (library.cylinderOpenHand != null)
        {
            Play(library.cylinderOpenHand, 0.35f);
        }

        UpdateHud();
    }

    // One round pushed into the next empty chamber (from the spare). False when there is nothing left to load.
    public bool LoadOneRound()
    {
        if (!reloading || spare <= 0)
        {
            return false;
        }

        for (int i = 0; i < Capacity; i++)
        {
            int c = (nextChamber + i) % Capacity;
            if (chambers[c] != ChamberLive)
            {
                chambers[c] = ChamberLive;
                loaded++;
                spare--;
                if (chamberRounds != null)
                {
                    chamberRounds[c].enabled = true;
                }

                RoundInserted?.Invoke(c);
                int pick = -1;
                SoundLibrary library = SoundLibrary.Instance;
                AudioClip click = SoundLibrary.Pick(library.cylinderClicks, ref pick);
                Play(click != null ? click : SoundLibrary.Pick(library.magnumCock, ref pick), click != null ? 0.5f : 0.18f);
                UpdateHud();
                return true;
            }
        }

        return false;
    }

    // The rest poured in at once; one round slips and rings on the floor (lost). Returns how many went in.
    public int LoadFast()
    {
        if (!reloading)
        {
            return 0;
        }

        int before = loaded;
        int dropped = spare > 1 ? 1 : 0;
        spare -= dropped;
        while (spare > 0 && LoadOneRound())
        {
        }

        if (dropped > 0)
        {
            int pick = -1;
            SoundLibrary library = SoundLibrary.Instance;
            AudioClip brass = SoundLibrary.Pick(library.brassOnFloor, ref pick);
            Play(brass != null ? brass : SoundLibrary.Pick(library.caseDrops, ref pick), 0.9f);
        }

        UpdateHud();
        return loaded - before;
    }

    // Closes the cylinder: `slam` is the flick of the wrist (loud), otherwise held shut with the hand (nearly silent).
    public void EndHandReload(bool slam)
    {
        if (!reloading)
        {
            return;
        }

        int pick = -1;
        SoundLibrary library = SoundLibrary.Instance;
        if (slam && library.cylinderSlam != null)
        {
            Play(library.cylinderSlam, 1f);
        }
        else
        {
            Play(SoundLibrary.Pick(library.magnumCock, ref pick), slam ? 1f : 0.2f);
            if (slam)
            {
                Play(library.magnumCylinderSpin, 0.6f);
            }
        }

        reloading = false;
        ClearChambers();
        UpdateHud();
    }

    // How many chambers still want a round.
    public int EmptyChambers => Capacity - loaded;

    public void Reload()
    {
        if (reloading || loaded >= Capacity || spare <= 0 || arms == null)
        {
            return;
        }

        // What this reload does, from what is in the gun: every FIRED case falls out (two fired = two cases, six = six),
        // and only as many rounds go in as there are empty chambers and rounds to give.
        ejecting.Clear();
        inserting.Clear();
        for (int i = 0; i < Capacity; i++)
        {
            if (chambers[i] == ChamberSpent)
            {
                ejecting.Add(i);
            }
        }

        for (int i = 0; i < Capacity && inserting.Count < spare; i++)
        {
            if (chambers[i] != ChamberLive)
            {
                inserting.Add(i);
            }
        }

        ReloadEjectChambers = ejecting.ToArray();
        ReloadInsertChambers = inserting.ToArray();
        for (int i = 0; i < speedloaderRounds.Length; i++)
        {
            if (speedloaderRounds[i] != null)
            {
                speedloaderRounds[i].enabled = inserting.Contains(i);
            }
        }

        ShowChambers();
        reloading = true;
        arms.Reload();
    }

    // The reload clip's events (Animation Events on Revolver_Reload, received by FirstPersonArms). The clip says
    // when; these say what, from the ammunition.
    private void OnArmsEvent(string name)
    {
        switch (name)
        {
            case "ReloadOpen":
                Play(SoundLibrary.Instance.magnumCylinderSpin, 0.5f);
                break;
            case "ReloadEject":
                OnReloadEject();
                break;
            case "ReloadRoundsIn":
                OnReloadRoundsIn();
                break;
            case "ReloadClosed":
                OnReloadClosed();
                break;
            case "ReloadFinished":
                reloading = false;
                ReloadEjectChambers = new int[0];
                ReloadInsertChambers = new int[0];
                break;
        }
    }

    private void OnReloadEject()
    {
        if (!reloading)
        {
            return;
        }

        foreach (int chamber in ejecting)
        {
            EjectCase(chamber);
            chambers[chamber] = ChamberEmpty;
            spent--;
            CaseEjected?.Invoke(chamber);
        }

        if (ejecting.Count > 0)
        {
            int pick = -1;
            Play(SoundLibrary.Pick(SoundLibrary.Instance.magnumCock, ref pick), 0.6f);
        }

        ejecting.Clear();
    }

    private void OnReloadRoundsIn()
    {
        if (!reloading)
        {
            return;
        }

        foreach (int chamber in inserting)
        {
            chambers[chamber] = ChamberLive;
            loaded++;
            spare--;
            if (chamberRounds != null)
            {
                chamberRounds[chamber].enabled = true;
            }

            RoundInserted?.Invoke(chamber);
        }

        // The rounds left the speedloader for the chambers.
        foreach (Renderer round in speedloaderRounds)
        {
            if (round != null)
            {
                round.enabled = false;
            }
        }

        if (inserting.Count > 0)
        {
            int pick = -1;
            Play(SoundLibrary.Pick(SoundLibrary.Instance.magnumCock, ref pick), 0.6f);
        }

        inserting.Clear();
        UpdateHud();
    }

    private void OnReloadClosed()
    {
        if (!reloading)
        {
            return;
        }

        int pick = -1;
        Play(SoundLibrary.Pick(SoundLibrary.Instance.magnumCock, ref pick), 0.45f);
        ClearChambers();
    }

    // The rig's rounds and their bones, by name (Bullet1..6 are both a skinned renderer and a bone; the bone has an _end).
    private void FindChambers()
    {
        if (model == null)
        {
            return;
        }

        SkinnedMeshRenderer[] rounds = new SkinnedMeshRenderer[Capacity];
        Transform[] bones = new Transform[Capacity];
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            for (int i = 0; i < Capacity; i++)
            {
                string name = "Bullet" + (i + 1);
                if (t.name != name)
                {
                    continue;
                }

                if (t.TryGetComponent(out SkinnedMeshRenderer round))
                {
                    rounds[i] = round;
                }
                else if (t.Find(name + "_end") != null)
                {
                    bones[i] = t;
                }
            }
        }

        for (int i = 0; i < Capacity; i++)
        {
            if (rounds[i] == null || bones[i] == null)
            {
                return;
            }
        }

        if (cylinder == null)
        {
            return;
        }

        chamberRounds = rounds;
        chamberBones = bones;
        chamberInCylinder = new Vector3[Capacity];
        chamberTurnInCylinder = new Quaternion[Capacity];
        for (int i = 0; i < Capacity; i++)
        {
            chamberInCylinder[i] = cylinder.InverseTransformPoint(bones[i].position);
            chamberTurnInCylinder[i] = Quaternion.Inverse(cylinder.rotation) * bones[i].rotation;
        }
    }

    // The rig's round bones back in their chambers, wherever the cylinder has swung.
    private void KeepRoundsInChambers()
    {
        for (int i = 0; i < Capacity; i++)
        {
            chamberBones[i].SetPositionAndRotation(cylinder.TransformPoint(chamberInCylinder[i]), cylinder.rotation * chamberTurnInCylinder[i]);
        }
    }

    // While the cylinder is open: a round where a chamber is live, a case where it was fired, nothing where it is empty.
    private void ShowChambers()
    {
        if (chamberRounds == null)
        {
            return;
        }

        for (int i = 0; i < Capacity; i++)
        {
            chamberRounds[i].enabled = chambers[i] == ChamberLive;
            if (chambers[i] == ChamberSpent && heldCases[i] == null)
            {
                heldCases[i] = NextCase();
                if (heldCases[i] != null)
                {
                    heldCases[i].Hold(cylinder, CaseHead(i), CaseRotation(i));
                }
            }
        }
    }

    // Closed again (or the reload cut short): the rig's rounds hidden, any case still held goes back to the pool.
    private void ClearChambers()
    {
        if (chamberRounds == null)
        {
            return;
        }

        for (int i = 0; i < Capacity; i++)
        {
            chamberRounds[i].enabled = false;
            if (heldCases[i] != null)
            {
                heldCases[i].Release();
                heldCases[i] = null;
            }
        }
    }

    // The back of a chamber (where a case's head sits) and a case turned with its mouth toward the muzzle. The round
    // bones sit at the front of their chambers, around the cylinder bone's Y (its axis, pointing back); the bones' own
    // axes do not run along the chambers (cases placed by them stood 5 cm beside the cylinder, 09/10). The cylinder
    // is 38 mm long behind the bones, in the model's units (measured on its mesh); the case head goes 30 mm back,
    // flush with the back of the cylinder once the case's rim is counted (at 38 it stood 1 cm out, seen up close).
    private const float ChamberLength = 0.030f;

    private Vector3 CaseHead(int chamber)
    {
        return cylinder.TransformPoint(chamberInCylinder[chamber] + Vector3.up * ChamberLength);
    }

    private Quaternion CaseRotation(int chamber)
    {
        return Quaternion.FromToRotation(Vector3.up, -cylinder.up);
    }

    private IEnumerator Flash()
    {
        if (flashSmoke != null)
        {
            flashSmoke.Emit(10);
        }

        if (flashSprite != null)
        {
            flashSprite.Emit(1);
        }

        if (flashSparks != null)
        {
            flashSparks.Emit(Random.Range(6, 12));
        }

        if (flashFire != null)
        {
            flashFire.Emit(Random.Range(3, 5));
        }

        foreach (ParticleSystem gap in gapFlash)
        {
            if (gap != null)
            {
                gap.Emit(Random.Range(2, 4));
            }
        }

        if (flash == null)
        {
            yield break;
        }

        // A real flash: bright at once, gone in a few hundredths of a second (it lights the hands and the room).
        GameTuning tuning = GameTuning.Instance;
        flash.enabled = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.02f, tuning.revolverFlashSeconds))
        {
            flash.intensity = tuning.revolverFlashIntensity * (1f - t) * (1f - t);
            yield return null;
        }

        flash.enabled = false;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        GameTuning tuning = GameTuning.Instance;

        // The gun's own flip in the hands on a shot (the arms carry the rest of the kick): a spring about the grip.
        float stiffness = tuning.revolverRecoilSpring;
        float damping = 2f * Mathf.Sqrt(stiffness) * Mathf.Lerp(1f, 0.35f, tuning.revolverRecoilBounce);
        for (int step = 0; step < 4; step++)
        {
            float h = dt / 4f;
            flipSpeed += (-stiffness * flip - damping * flipSpeed) * h;
            flip += flipSpeed * h;
        }

        if (model != null)
        {
            float degrees = flip * tuning.revolverRecoilFlip * 0.5f * Mathf.Lerp(1f, tuning.revolverRecoilAimed, aim);
            model.localRotation = Quaternion.Euler(-degrees, 0f, 0f) * modelRest;
            model.localPosition = modelRestPosition;
        }

        hammerBack = Mathf.MoveTowards(hammerBack, 0f, dt / 0.12f);
        cylinderAngle = Mathf.MoveTowards(cylinderAngle, cylinderTarget, dt * 600f);
        Sight(tuning, dt);
        bloom = Mathf.MoveTowards(bloom, 0f, dt * tuning.revolverShotBloom / tuning.revolverBloomSeconds);
        BarrelSmoke(dt);

        if (cylinder != null && model != null)
        {
            // About the cylinder's real axis (cylinderAxis, parallel to the bore, through cylinderPivot, the middle of
            // the cylinder). Only while it is closed: open, the reload clip owns the cylinder.
            if (!reloading)
            {
                Quaternion spin = Quaternion.AngleAxis(-cylinderAngle, cylinderAxis);
                cylinder.localRotation = cylinderRest * spin;
                cylinder.localPosition = cylinderRestPosition + cylinderRest * Vector3.Scale(cylinder.localScale, cylinderPivot - spin * cylinderPivot);
            }
            else if (chamberBones != null)
            {
                KeepRoundsInChambers();
            }
        }

        if (hammer != null && model != null)
        {
            hammer.localRotation = hammerRest;
            hammer.rotation = Quaternion.AngleAxis(-35f * hammerBack, model.right) * hammer.rotation;
        }
    }

    // After shooting the barrel smokes: thin wisps leave the muzzle and rise, fewer as it cools.
    private void BarrelSmoke(float dt)
    {
        barrelHeat = Mathf.MoveTowards(barrelHeat, 0f, dt * 0.45f);
        if (flashSmoke == null || muzzle == null || barrelHeat < 0.15f || Time.time < nextWisp)
        {
            return;
        }

        nextWisp = Time.time + Mathf.Lerp(0.35f, 0.08f, barrelHeat / 4f);
        ParticleSystem.EmitParams wisp = new ParticleSystem.EmitParams
        {
            position = muzzle.position,
            applyShapeToPosition = false,
            velocity = Vector3.up * Random.Range(0.12f, 0.3f) + muzzle.forward * 0.05f,
            startSize = Random.Range(0.025f, 0.05f),
            startLifetime = Random.Range(1.2f, 2.2f),
            startColor = new Color(0.8f, 0.8f, 0.78f, Mathf.Clamp01(barrelHeat / 3f) * 0.35f),
        };
        flashSmoke.Emit(wisp, 1);
    }

    // One fired case out of its chamber: the ejector rod pushes it out of the back (down, with the muzzle up) and it
    // falls tumbling. Without the rig's chamber bones it leaves from the cylinder.
    private void EjectCase(int chamber)
    {
        SpentCase shell = heldCases[chamber] != null ? heldCases[chamber] : NextCase();
        heldCases[chamber] = null;
        if (shell == null)
        {
            return;
        }

        Vector3 start;
        Quaternion rotation;
        if (chamberBones != null)
        {
            start = CaseHead(chamber);
            rotation = CaseRotation(chamber);
        }
        else
        {
            start = cylinder.position;
            rotation = Quaternion.FromToRotation(Vector3.up, transform.forward);
        }

        Vector3 back = -(rotation * Vector3.up);
        Vector3 velocity = (characterController != null ? characterController.velocity : Vector3.zero)
            + back * Random.Range(0.5f, 0.9f) + Random.insideUnitSphere * 0.12f;
        shell.Throw(start, rotation, velocity, Random.insideUnitSphere * 25f);
    }

    // A case from the pool (the oldest reused).
    private SpentCase NextCase()
    {
        if (casePrefab == null)
        {
            return null;
        }

        if (cases == null)
        {
            cases = new SpentCase[18];
        }

        SpentCase shell = cases[nextCase];
        if (shell == null)
        {
            shell = Instantiate(casePrefab);
            shell.name = "SpentCase";
            cases[nextCase] = shell;
        }

        nextCase = (nextCase + 1) % cases.Length;
        return shell;
    }

    // What the crosshair is on (its distance, smoothed) and whether it can be shot.
    private void Sight(GameTuning tuning, float dt)
    {
        Transform eye = controller.CameraPivot;
        float distance = range;
        int count = Physics.RaycastNonAlloc(eye.position, eye.forward, Hits, range, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Collider bestCollider = null;
        for (int i = 0; i < count; i++)
        {
            if (Hits[i].distance < best && !Own(Hits[i].transform))
            {
                best = Hits[i].distance;
                bestCollider = Hits[i].collider;
            }
        }

        OnTarget = bestCollider != null && bestCollider.GetComponentInParent<IBulletTarget>() != null;
        if (bestCollider != null)
        {
            distance = best;
        }

        sightDistance = Mathf.Lerp(sightDistance, distance, 1f - Mathf.Exp(-12f * dt));
        Vector3 velocity = characterController != null ? characterController.velocity : Vector3.zero;
        moving = Mathf.Lerp(moving, Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / 3.5f), 1f - Mathf.Exp(-8f * dt));
    }

    private bool Own(Transform t)
    {
        return t.IsChildOf(controller.transform) || t.IsChildOf(body.transform) || t.IsChildOf(transform);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null && source != null)
        {
            source.PlayOneShot(clip, volume);
        }
    }

    private void Dust(RaycastHit hit)
    {
        if (dust == null)
        {
            GameObject go = new GameObject("BulletDust");
            dust = go.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = dust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
            main.startColor = new Color(0.45f, 0.42f, 0.38f, 0.6f);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = dust.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.01f;
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = impactMaterial;
        }

        dust.transform.SetPositionAndRotation(hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
        dust.Emit(18);
    }

    // Rounds in the cylinder and spare, bottom right, on the HUD's dark plate with the readable outline.
    private void BuildHud()
    {
        Canvas canvas = new GameObject("RevolverHud").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4;
        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject plate = new GameObject("AmmoPlate");
        plate.transform.SetParent(canvas.transform, false);
        Image back = plate.AddComponent<Image>();
        back.color = DiveHud.Plate;
        RectTransform rect = back.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-48f, 48f);
        rect.sizeDelta = new Vector2(210f, 64f);
        ammoLabel = new GameObject("Ammo").AddComponent<TextMeshProUGUI>();
        ammoLabel.transform.SetParent(plate.transform, false);
        if (font != null)
        {
            ammoLabel.font = font;
        }

        ammoLabel.fontSize = 38f;
        ammoLabel.alignment = TextAlignmentOptions.Center;
        ammoLabel.color = new Color(0.93f, 0.9f, 0.82f);
        ammoLabel.rectTransform.anchorMin = Vector2.zero;
        ammoLabel.rectTransform.anchorMax = Vector2.one;
        ammoLabel.rectTransform.offsetMin = ammoLabel.rectTransform.offsetMax = Vector2.zero;
        DiveHud.Readable(ammoLabel);

        if (crosshairPrefab != null)
        {
            crosshair = Instantiate(crosshairPrefab, canvas.transform, false);
            crosshair.Bind(this, controller.CameraPivot);
        }
    }

    private void UpdateHud()
    {
        if (ammoLabel != null)
        {
            ammoLabel.text = loaded + " <size=70%><color=#B8B2A4>| " + spare + "</color></size>";
        }
    }
}
