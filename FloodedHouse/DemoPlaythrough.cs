using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Development tool (17/10): drives the demo's combat and finale from code in Play, without window focus, and writes
// screenshots and a log: new GameObject("Test").AddComponent<DemoPlaythrough>().Run(folder, fastPath).
//   1. skips the intro, draws the revolver (2), shoots the power room's Drowned dead (head), takes a blow from the
//      laundry's and reads the health bar, then plays the finale: the stair grab and the hiding (slow or fast path).
//   Every step: a screenshot of the game view and a line in <folder>/log.txt with what was measured (rounds, health,
//   the Drowned's state, the time). Reflection reaches the director's private stage and helpers.
public sealed class DemoPlaythrough : MonoBehaviour
{
    private string folder;
    private bool fast;
    private readonly List<string> log = new List<string>();
    private FloodedHouseDirector director;
    private FirstPersonController player;
    private Inventory inventory;
    private Revolver revolver;
    private AirSupply air;
    private DemoFinale finale;
    private PlayerItems items;
    private DiveHud hud;

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public void Run(string outputFolder, bool fastPath)
    {
        folder = outputFolder;
        fast = fastPath;
        Directory.CreateDirectory(folder);
        Application.runInBackground = true;
        StartCoroutine(Routine());
    }

    private void Note(string line)
    {
        string text = Time.time.ToString("F1") + " s  " + line;
        log.Add(text);
        Debug.Log("[Playthrough] " + text);
        File.WriteAllLines(Path.Combine(folder, "log.txt"), log);
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        yield return null;
        yield return null;
    }

    private object Field(object target, string name) => target.GetType().GetField(name, Any)?.GetValue(target);

    private void SetStage(string stage)
    {
        FieldInfo field = typeof(FloodedHouseDirector).GetField("stage", Any);
        System.Type type = field.FieldType;
        field.SetValue(director, System.Enum.Parse(type, stage));
    }

    private void Advance(string stage)
    {
        MethodInfo method = typeof(FloodedHouseDirector).GetMethod("Advance", Any);
        System.Type type = typeof(FloodedHouseDirector).GetField("stage", Any).FieldType;
        method.Invoke(director, new[] { System.Enum.Parse(type, stage) });
    }

    private IEnumerator Routine()
    {
        yield return new WaitForSeconds(1f);
        director = FindAnyObjectByType<FloodedHouseDirector>();
        player = FindAnyObjectByType<FirstPersonController>();
        inventory = player.GetComponent<Inventory>();
        revolver = player.GetComponentInChildren<Revolver>(true);
        air = player.GetComponent<AirSupply>();
        finale = FindAnyObjectByType<DemoFinale>(FindObjectsInactive.Include);
        items = player.GetComponent<PlayerItems>();
        hud = FindAnyObjectByType<DiveHud>();
        Note("inventory=" + (inventory != null) + " revolver=" + (revolver != null) + " finale=" + (finale != null));

        // Skip the intro the way the player does (holding Space): the director's counter.
        typeof(FloodedHouseDirector).GetField("skipHeld", Any).SetValue(director, 10f);
        yield return new WaitForSeconds(1.5f);
        Note("stage=" + Field(director, "stage") + " hand=" + inventory.InHand + " loaded=" + revolver.Loaded + " spare=" + revolver.Spare);
        yield return Shot("01_start_hands_free");

        // 2: the revolver out, torch not found yet.
        inventory.Equip(Inventory.Hand.Revolver, false);
        yield return new WaitForSeconds(1.2f);
        Note("after 2: hand=" + inventory.InHand + " revolverActive=" + revolver.gameObject.activeSelf);
        yield return Shot("02_revolver_drawn");
        revolver.HoldAim = true;
        yield return new WaitForSeconds(0.6f);
        yield return Shot("03_revolver_aim");
        revolver.HoldAim = false;

        // The power room's Drowned wakes at the blackout.
        Advance("FindLight");
        DrownedBrain powerRoom = (DrownedBrain)Field(director, "powerRoomDrowned");
        DrownedBrain laundry = (DrownedBrain)Field(director, "laundryDrowned");
        Note("FindLight: powerRoom active=" + powerRoom.gameObject.activeSelf + " laundry active=" + laundry.gameObject.activeSelf);
        // Stand in the courtyard gallery at the power room's door, looking in.
        Vector3 doorOutside = powerRoom.transform.position + new Vector3(0.3f, 0f, -4.2f);
        player.Teleport(doorOutside, 0f);
        yield return new WaitForSeconds(0.5f);
        player.LookAt(powerRoom.transform.position + Vector3.up * 1.4f);
        yield return new WaitForSeconds(2.5f);
        Note("at the power room door: drowned chasing=" + powerRoom.IsChasing + " threatening=" + powerRoom.Threatening + " dist=" + Vector3.Distance(player.transform.position, powerRoom.transform.position).ToString("F2"));
        yield return Shot("04_power_room_drowned");

        // Shoot it in the head until it drops.
        CreatureVitals vitals = powerRoom.GetComponent<CreatureVitals>();
        revolver.HoldAim = true;
        int shots = 0;
        while (!vitals.IsDead && shots < 6)
        {
            Transform head = powerRoom.Eyes != null ? powerRoom.Eyes : powerRoom.transform;
            player.LookAt(head.position);
            yield return null;
            bool ok = revolver.Fire();
            shots++;
            Note("shot " + shots + " fired=" + ok + " health=" + vitals.Health.ToString("F0") + " loaded=" + revolver.Loaded);
            if (shots == 1)
            {
                yield return Shot("05_first_shot");
            }

            yield return new WaitForSeconds(0.6f);
        }

        revolver.HoldAim = false;
        Note("power room drowned dead=" + vitals.IsDead + " after " + shots + " shots; loaded=" + revolver.Loaded);
        yield return new WaitForSeconds(1.5f);
        yield return Shot("06_power_room_dead");

        // The laundry's Drowned wakes once the generator room is done; stand in front of it and take a blow.
        Advance("Basement");
        Note("Basement: laundry active=" + laundry.gameObject.activeSelf);
        Vector3 inFront = laundry.transform.position + laundry.transform.forward * 2.2f;
        player.Teleport(inFront, 0f);
        yield return null;
        player.LookAt(laundry.transform.position + Vector3.up * 1.4f);
        float healthBefore = air.Health;
        float waited = 0f;
        while (air.Health >= healthBefore - 0.01f && waited < 12f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        Note("laundry drowned: blow after " + waited.ToString("F1") + " s, health " + healthBefore.ToString("F2") + " -> " + air.Health.ToString("F2") + " hurt=" + air.IsHurt);
        yield return Shot("07_blow_taken");
        // Away from it before it kills the test.
        player.Teleport(inFront + new Vector3(0f, 0f, -6f), 180f);
        laundry.gameObject.SetActive(false);

        // The finale: the box taken, Escape stage, at the bottom of the basement stair.
        items.AddSilently("maleta");
        items.AddSilently("caixa");
        items.AddSilently("fragmento");
        SetStage("Escape");
        revolver.SetAmmo(fast ? 2 : 3, fast ? 4 : 3, 0);
        player.Teleport(new Vector3(-5.4f, -2.3f, -3.6f), 0f);
        yield return new WaitForSeconds(0.5f);
        Note("Escape at the landing: loaded=" + revolver.Loaded + " grabbed=" + finale.Grabbed);
        player.TestSprint = true;
        float t0 = Time.time;
        while (!finale.Grabbed && Time.time - t0 < 8f)
        {
            yield return null;
        }

        player.TestSprint = false;
        Note("grab started at z=" + player.transform.position.z.ToString("F2") + " y=" + player.transform.position.y.ToString("F2"));
        yield return new WaitForSeconds(1.0f);
        yield return Shot("08_grab");
        yield return new WaitForSeconds(1.6f);
        yield return Shot("09_grab_shooting");
        t0 = Time.time;
        while (player.InputLocked && Time.time - t0 < 12f)
        {
            yield return null;
        }

        Note("grab over after " + (Time.time - t0).ToString("F1") + " s: loaded=" + revolver.Loaded + " spare=" + revolver.Spare + " facingYaw=" + player.transform.eulerAngles.y.ToString("F0"));
        yield return Shot("10_after_grab");
        // The player turns and goes on up (the test has no mouse).
        player.Teleport(player.transform.position, 0f);

        // Up the stairs to the top: the hiding scene.
        player.TestSprint = true;
        t0 = Time.time;
        while (!finale.Running && Time.time - t0 < 10f)
        {
            yield return null;
        }

        player.TestSprint = false;
        Note("finale running=" + finale.Running + " at z=" + player.transform.position.z.ToString("F2"));
        DrownedBrain searcher = (DrownedBrain)Field(finale, "searcher");
        float start = Time.time;
        int index = 11;
        bool pressed = false;
        while (finale.Running && Time.time - start < 70f)
        {
            yield return new WaitForSecondsRealtime(3f);
            Note("finale +" + (Time.time - start).ToString("F0") + " s: loaded=" + revolver.Loaded + " spare=" + revolver.Spare + " reloading=" + revolver.IsReloading
                + " searcher active=" + searcher.gameObject.activeSelf + " chasing=" + searcher.IsChasing + " crouched=" + player.IsCrouched + " timeScale=" + Time.timeScale.ToString("F2")
                + " dist=" + Vector3.Distance(player.transform.position, searcher.transform.position).ToString("F1"));
            yield return Shot(index.ToString("00") + "_finale_" + (Time.time - start).ToString("F0") + "s");
            index++;
            if (fast && !pressed && Time.time - start > 12f)
            {
                pressed = true;
                finale.PressFast();
                Note("R pressed (fast path)");
            }
        }

        Note("finale over: running=" + finale.Running + " endCard=" + hud.IsEndCardShown + " loaded=" + revolver.Loaded);
        yield return new WaitForSecondsRealtime(1f);
        yield return Shot(index.ToString("00") + "_end_card");
        Note("done");
    }
}
