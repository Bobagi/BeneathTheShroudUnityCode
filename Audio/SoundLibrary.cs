using UnityEngine;

// Every recorded sound the game code asks for by role (a big splash, a step in water, a drip), in one asset:
// Settings/Resources/SoundLibrary.asset, filled from the Audio folders by Horror > Som > Atualizar biblioteca
// (the Flooded House rebuild runs it too). Code asks SoundLibrary.Instance for the role and falls back to the
// procedural sound when a slot is empty, so a missing file never silences the game. Levels are not here:
// they live in GameTuning (Som).
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Horror/Sound Library")]
public sealed class SoundLibrary : ScriptableObject
{
    public const string ResourceName = "SoundLibrary";

    [Header("Água")]
    public AudioClip splashSoft;
    public AudioClip[] splashMedium = new AudioClip[0];
    public AudioClip splashBig;
    public AudioClip splashHeardUnderwater;
    public AudioClip emergeHeardUnderwater;
    public AudioClip[] drips = new AudioClip[0];
    public AudioClip dripsLoop;
    public AudioClip underwaterLoop;

    [Header("Passos")]
    public AudioClip[] woodCreaks = new AudioClip[0];
    public AudioClip[] waterSteps = new AudioClip[0];

    [Header("Ambiente")]
    public AudioClip rainRoofInsideLoop;
    public AudioClip seaLoop;
    public AudioClip[] houseCreaks = new AudioClip[0];

    [Header("Respiração")]
    public AudioClip[] heavyBreaths = new AudioClip[0];
    public AudioClip exhaustedBreathingLoop;
    public AudioClip[] coughs = new AudioClip[0];
    public AudioClip gasMaskBreathingLoop;

    [Header("Tempestade e casa")]
    public AudioClip[] thunderClose = new AudioClip[0];
    public AudioClip[] thunderFar = new AudioClip[0];
    public AudioClip powerCut;
    public AudioClip wallCollapse;
    public AudioClip atticLadder;
    public AudioClip metalBoxOpen;
    public AudioClip metalBoxClose;

    [Header("Lanterna")]
    public AudioClip[] torchClicks = new AudioClip[0];
    public AudioClip batteriesInsert;

    [Header("Criatura e Lumina")]
    public AudioClip creatureCrawlLoop;
    public AudioClip[] clawsInWall = new AudioClip[0];
    public AudioClip luminaTone;
    public AudioClip luminaHumLoop;
    // Worms crossing a wall: a wet squelching texture (WallWorms plays it while they are out).
    public AudioClip wormsLoop;

    [Header("Máscara")]
    public AudioClip[] visorCracks = new AudioClip[0];

    [Header("Abertura (barco na tempestade)")]
    public AudioClip outboardMotorLoop;
    public AudioClip rainOutdoorLoop;
    public AudioClip seaNightLoop;

    [Header("Combate (magnum e criaturas menores; ainda sem uso na demo)")]
    public AudioClip[] magnumShots = new AudioClip[0];
    public AudioClip magnumShotTail;
    public AudioClip[] magnumDryFire = new AudioClip[0];
    public AudioClip[] magnumCock = new AudioClip[0];
    public AudioClip magnumCylinderSpin;
    public AudioClip[] bulletFlesh = new AudioClip[0];
    // Empty brass cases bouncing on a hard floor (SpentCase).
    public AudioClip[] caseDrops = new AudioClip[0];
    // A bullet into a wall, the floor, a crate (ImpactEffects via Revolver).
    public AudioClip[] bulletImpacts = new AudioClip[0];
    public AudioClip[] smallCreatureGrunts = new AudioClip[0];
    public AudioClip[] smallCreatureSqueals = new AudioClip[0];
    public AudioClip[] smallCreatureDeaths = new AudioClip[0];

    // The demo's finale (17/10): the cylinder by hand, the Drowned searching, the room, the shot in the black.
    public AudioClip[] cylinderClicks = new AudioClip[0];
    public AudioClip cylinderOpenHand;
    public AudioClip cylinderSlam;
    public AudioClip casesIntoHand;
    public AudioClip[] brassOnFloor = new AudioClip[0];
    public AudioClip[] drownedSearch = new AudioClip[0];
    public AudioClip drownedBreathLoop;
    public AudioClip potDrop;
    public AudioClip doorBurst;
    public AudioClip gunshotTailIndoor;
    public AudioClip machinePowerDown;

    private static SoundLibrary instance;

    public static SoundLibrary Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<SoundLibrary>(ResourceName);
                if (instance == null)
                {
                    instance = CreateInstance<SoundLibrary>();
                    instance.hideFlags = HideFlags.DontSave;
                }
            }

            return instance;
        }
    }

    // A random clip of the set, never the same one twice in a row; null when the set is empty.
    public static AudioClip Pick(AudioClip[] set, ref int last)
    {
        if (set == null || set.Length == 0)
        {
            return null;
        }

        int index = Random.Range(0, set.Length);
        if (index == last && set.Length > 1)
        {
            index = (index + 1) % set.Length;
        }

        last = index;
        return set[index];
    }

    // Splash for a body going in, by how hard it hits (0 slow wade, 1 a drop).
    public AudioClip Splash(float strength)
    {
        int ignored = -1;
        AudioClip clip = strength < 0.25f ? splashSoft : strength < 0.65f ? Pick(splashMedium, ref ignored) : splashBig;
        return clip != null ? clip : ProceduralAudio.Splash();
    }

    private int lastDrip = -1;
    private int lastBreath = -1;

    public AudioClip Drip()
    {
        AudioClip clip = Pick(drips, ref lastDrip);
        return clip != null ? clip : ProceduralAudio.Drip();
    }

    public AudioClip HeavyBreath()
    {
        AudioClip clip = Pick(heavyBreaths, ref lastBreath);
        return clip != null ? clip : ProceduralAudio.Gasp();
    }

    private int lastCough = -1;
    private int lastThunder = -1;
    private int lastClaw = -1;
    private int lastClick = -1;

    public AudioClip Cough()
    {
        AudioClip clip = Pick(coughs, ref lastCough);
        return clip != null ? clip : ProceduralAudio.Cough();
    }

    // A strike right over the house (the blackout, the box) or one across the water (the storm going on).
    public AudioClip Thunder(bool close)
    {
        AudioClip clip = Pick(close ? thunderClose : thunderFar, ref lastThunder);
        return clip != null ? clip : ProceduralAudio.Thunder();
    }

    public AudioClip Claws()
    {
        AudioClip clip = Pick(clawsInWall, ref lastClaw);
        return clip != null ? clip : ProceduralAudio.Scratch();
    }

    public AudioClip TorchClick()
    {
        AudioClip clip = Pick(torchClicks, ref lastClick);
        return clip;
    }

    public AudioClip PowerCut()
    {
        return powerCut != null ? powerCut : ProceduralAudio.ShortCircuit();
    }

    public AudioClip WallCollapse()
    {
        return wallCollapse != null ? wallCollapse : ProceduralAudio.Crash();
    }
}
