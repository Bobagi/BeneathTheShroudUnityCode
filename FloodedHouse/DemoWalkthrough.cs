using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Development tool (26/11): plays the demo's path the way a player does, through a virtual keyboard and mouse (the
// same Keyboard.current / Mouse.current the game reads), without window focus: it walks with W, steers with the
// mouse, opens doors and takes things with E, skips the intro holding Space, types the code on the top-row digits.
// Nothing is teleported unless a leg gets stuck, and then the log says where and against what (a place the player
// cannot pass is the finding). From the front door to the basement key; the combat and the finale are
// DemoPlaythrough's. new GameObject("Walk").AddComponent<DemoWalkthrough>().Run(folder).
//   <folder>/log.txt: one line per step (OK / FAIL / STUCK), the summary at the end; a screenshot per step.
public sealed class DemoWalkthrough : MonoBehaviour
{
    // The plan of the house to the scene (FloodedHouseManor: PlanScale 1.25 about the basement stair, ManorOffset).
    private static Vector3 P(float x, float y, float z) => new Vector3(1.25f * x - 18.8825f, y, 1.25f * z - 23.9f);

    private const float UF = 3.72f;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private string folder;
    private readonly List<string> log = new List<string>();
    private readonly HashSet<Key> held = new HashSet<Key>();
    private Keyboard keyboard;
    private Mouse mouse;
    private FloodedHouseDirector director;
    private FirstPersonController player;
    private PlayerInteractor interactor;
    private PlayerItems items;
    private int ok, failed, stuck, shots;
    private float degreesPerPixel = 0.1f;

    public bool Done { get; private set; }
    public string Summary { get; private set; } = "";

    public void Run(string outputFolder)
    {
        folder = outputFolder;
        Directory.CreateDirectory(folder);
        Application.runInBackground = true;
        StartCoroutine(Routine());
    }

    // ---------------------------------------------------------------- log

    private void Note(string line)
    {
        string text = Time.time.ToString("F1") + " s  " + line;
        log.Add(text);
        Debug.Log("[Walkthrough] " + text);
        File.WriteAllLines(Path.Combine(folder, "log.txt"), log);
    }

    private void Check(string what, bool good, string detail = "")
    {
        if (good)
        {
            ok++;
        }
        else
        {
            failed++;
        }

        Note((good ? "OK    " : "FAIL  ") + what + (detail.Length > 0 ? "  [" + detail + "]" : ""));
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        shots++;
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, shots.ToString("00") + "_" + name + ".png"));
        yield return null;
        yield return null;
    }

    private string Stage => typeof(FloodedHouseDirector).GetField("stage", Any).GetValue(director).ToString();

    // ---------------------------------------------------------------- virtual devices

    private void Devices()
    {
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        keyboard = InputSystem.AddDevice<Keyboard>("WalkthroughKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("WalkthroughMouse");
        Send();
    }

    private void Send()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.ToArray()));
    }

    private void Hold(Key key)
    {
        if (held.Add(key))
        {
            Send();
        }
    }

    private void Release(Key key)
    {
        if (held.Remove(key))
        {
            Send();
        }
    }

    private IEnumerator Tap(Key key)
    {
        Hold(key);
        yield return null;
        yield return null;
        Release(key);
        yield return null;
        yield return null;
    }

    private void MoveMouse(Vector2 pixels)
    {
        InputSystem.QueueDeltaStateEvent(mouse.delta, pixels);
    }

    // ---------------------------------------------------------------- looking and walking

    // Turns the view with the mouse until it points at `target` (closed loop: the gain is measured from what the last
    // movement did, so the sensitivity setting does not matter).
    private IEnumerator LookAt(Vector3 target, float tolerance = 2f, float timeout = 3f)
    {
        float until = Time.time + timeout;
        while (Time.time < until)
        {
            Vector3 to = target - player.EyePosition;
            float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            float yawError = Mathf.DeltaAngle(player.transform.eulerAngles.y, wantYaw);
            float pitchError = wantPitch - player.Pitch;
            if (Mathf.Abs(yawError) < tolerance && Mathf.Abs(pitchError) < tolerance)
            {
                yield break;
            }

            float before = player.transform.eulerAngles.y;
            Vector2 pixels = new Vector2(yawError, -pitchError) * (0.5f / Mathf.Max(degreesPerPixel, 0.001f));
            pixels = Vector2.ClampMagnitude(pixels, 400f);
            MoveMouse(pixels);
            yield return null;
            yield return null;
            float turned = Mathf.DeltaAngle(before, player.transform.eulerAngles.y);
            if (Mathf.Abs(pixels.x) > 2f && Mathf.Abs(turned) > 0.01f)
            {
                degreesPerPixel = Mathf.Lerp(degreesPerPixel, Mathf.Abs(turned / pixels.x), 0.5f);
            }
        }
    }

    // Walks to `target` holding W (Shift too when `run`), steering with the mouse. A leg that makes no progress for
    // two seconds tries a side step; after three of those it is logged as STUCK with what is in front, and the player
    // is put at the target so the rest of the path can still be tested.
    private IEnumerator GoTo(string name, Vector3 target, bool run = false, float arrive = 0.45f, float timeout = 40f)
    {
        float until = Time.time + timeout;
        float bestDistance = float.MaxValue, bestAt = Time.time;
        int sideSteps = 0;
        Hold(Key.W);
        if (run)
        {
            Hold(Key.LeftShift);
        }

        while (true)
        {
            Vector3 feet = player.transform.position;
            Vector3 flat = new Vector3(target.x - feet.x, 0f, target.z - feet.z);
            float distance = flat.magnitude;
            if (distance < arrive)
            {
                break;
            }

            if (distance < bestDistance - 0.2f)
            {
                bestDistance = distance;
                bestAt = Time.time;
            }

            // Steer: only the yaw, the pitch level.
            float wantYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float yawError = Mathf.DeltaAngle(player.transform.eulerAngles.y, wantYaw);
            float before = player.transform.eulerAngles.y;
            Vector2 pixels = new Vector2(yawError, player.Pitch * 0.5f) * (0.5f / Mathf.Max(degreesPerPixel, 0.001f));
            pixels = Vector2.ClampMagnitude(pixels, 400f);
            if (pixels.sqrMagnitude > 1f)
            {
                MoveMouse(pixels);
            }

            // Turning on the spot when the target is behind, walking otherwise.
            if (Mathf.Abs(yawError) > 60f)
            {
                Release(Key.W);
            }
            else
            {
                Hold(Key.W);
            }

            yield return null;
            float turned = Mathf.DeltaAngle(before, player.transform.eulerAngles.y);
            if (Mathf.Abs(pixels.x) > 2f && Mathf.Abs(turned) > 0.01f)
            {
                degreesPerPixel = Mathf.Lerp(degreesPerPixel, Mathf.Abs(turned / pixels.x), 0.3f);
            }

            bool late = Time.time > until;
            if (Time.time - bestAt > 2f || late)
            {
                if (sideSteps < 3 && !late)
                {
                    sideSteps++;
                    Key sideKey = sideSteps % 2 == 1 ? Key.A : Key.D;
                    Hold(sideKey);
                    yield return new WaitForSeconds(0.45f);
                    Release(sideKey);
                    bestAt = Time.time;
                    continue;
                }

                string blocker = "nothing in front";
                Vector3 chest = player.transform.position + Vector3.up * 0.9f;
                if (Physics.SphereCast(chest - flat.normalized * 0.3f, 0.25f, flat.normalized, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    blocker = PathOf(hit.collider.transform) + " at " + hit.distance.ToString("F2") + " m";
                }

                stuck++;
                Note("STUCK " + name + ": " + distance.ToString("F2") + " m short at " + feet.ToString("F2") + ", stage " + Stage + ", blocked by " + blocker);
                Release(Key.W);
                Release(Key.LeftShift);
                yield return Shot("stuck_" + name);
                player.Teleport(target, player.transform.eulerAngles.y);
                yield return new WaitForSeconds(0.3f);
                yield break;
            }
        }

        Release(Key.W);
        Release(Key.LeftShift);
        ok++;
        Note("OK    walked to " + name + " " + player.transform.position.ToString("F2"));
    }

    private IEnumerator Legs(string name, bool run, params Vector3[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            yield return GoTo(name + (points.Length > 1 ? "#" + (i + 1) : ""), points[i], run);
        }
    }

    private static string PathOf(Transform t)
    {
        string path = t.name;
        for (int i = 0; i < 3 && t.parent != null; i++)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }

    // ---------------------------------------------------------------- using things

    private static Vector3 AimPoint(Component target)
    {
        Collider c = target.GetComponentInChildren<Collider>();
        if (c != null)
        {
            return c.bounds.center;
        }

        Renderer r = target.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : target.transform.position;
    }

    private string Focused()
    {
        object focused = typeof(PlayerInteractor).GetField("focused", Any)?.GetValue(interactor);
        return focused is Interactable i && i != null ? i.name + " \"" + i.Prompt + "\"" : "nothing";
    }

    // Looks at it with the mouse and presses E; says what the crosshair had.
    private IEnumerator Use(Component target, Vector3? aim = null)
    {
        if (target == null)
        {
            yield break;
        }

        yield return LookAt(aim ?? AimPoint(target), 1.2f);
        yield return new WaitForSeconds(0.25f);
        Note("      crosshair on: " + Focused() + " (wanted " + target.name + ")");
        yield return Tap(Key.E);
        // Real time: a keypad or a reading pauses the game.
        yield return new WaitForSecondsRealtime(0.4f);
    }

    private StoryPickup Pickup(string item)
    {
        return FindObjectsByType<StoryPickup>(FindObjectsInactive.Exclude).FirstOrDefault(p => p.ItemId == item);
    }

    private T Nearest<T>(Vector3 at, float within, System.Func<T, bool> where = null) where T : Component
    {
        return FindObjectsByType<T>(FindObjectsInactive.Exclude)
            .Where(c => (c.transform.position - at).magnitude < within && (where == null || where(c)))
            .OrderBy(c => (c.transform.position - at).sqrMagnitude).FirstOrDefault();
    }

    private IEnumerator Take(string item, Vector3 standAt)
    {
        StoryPickup pickup = Pickup(item);
        if (pickup == null)
        {
            Check("take " + item, false, "no pickup with that id in the scene");
            yield break;
        }

        yield return GoTo("to " + item, standAt);
        yield return Use(pickup);
        if (!items.HasItem(item))
        {
            // A second try from a step closer: small things on a table can sit under the crosshair's first hit.
            Vector3 closer = Vector3.MoveTowards(player.transform.position, new Vector3(pickup.transform.position.x, player.transform.position.y, pickup.transform.position.z), 0.4f);
            yield return GoTo("closer to " + item, closer, false, 0.15f, 4f);
            yield return Use(pickup);
        }

        Check("take " + item + " with E", items.HasItem(item), "distance " + Vector3.Distance(player.EyePosition, AimPoint(pickup)).ToString("F2"));
        yield return Shot("took_" + item);
        yield return CloseNote();
    }

    // A note or a reading that paused the game: Esc or E closes it.
    private IEnumerator CloseNote()
    {
        for (int i = 0; i < 3 && (player.InputLocked || MansionHud.PanelOpen); i++)
        {
            yield return Tap(Key.E);
            yield return new WaitForSecondsRealtime(0.4f);
            if (player.InputLocked || MansionHud.PanelOpen)
            {
                yield return Tap(Key.Escape);
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }
    }

    private IEnumerator OpenDoorAt(string name, Vector3 doorAt)
    {
        Door door = Nearest<Door>(doorAt, 2.2f, d => !d.IsOpen);
        if (door == null)
        {
            Note("      " + name + ": no closed door within 2.2 m (already open)");
            yield break;
        }

        yield return Use(door);
        yield return new WaitForSeconds(1.5f);
        Check("open " + name + " with E", door.IsOpen, door.name);
    }

    private IEnumerator WaitFor(string what, System.Func<bool> condition, float timeout)
    {
        float until = Time.time + timeout;
        while (!condition() && Time.time < until)
        {
            yield return null;
        }

        Check(what, condition(), "stage " + Stage);
    }

    // ---------------------------------------------------------------- the path

    private IEnumerator Routine()
    {
        yield return new WaitForSeconds(1f);
        director = FindAnyObjectByType<FloodedHouseDirector>();
        player = FindAnyObjectByType<FirstPersonController>();
        interactor = player.GetComponent<PlayerInteractor>();
        if (interactor == null)
        {
            interactor = player.GetComponentInChildren<PlayerInteractor>();
        }

        items = player.GetComponent<PlayerItems>();
        Devices();
        yield return null;
        Note("start: stage " + Stage + " at " + player.transform.position.ToString("F2") + ", barriers " + GrowthBarrier.All.Count);

        // 1. The intro is skipped by holding Space.
        Hold(Key.Space);
        yield return WaitFor("intro skipped holding Space", () => Stage != "Arrive", 12f);
        Release(Key.Space);
        yield return new WaitForSeconds(1.5f);
        yield return Shot("after_intro");

        // 2. Map and diary answer their keys.
        yield return Tap(Key.M);
        yield return new WaitForSecondsRealtime(0.5f);
        bool mapOpened = MansionHud.PanelOpen;
        yield return Shot("map");
        yield return Tap(Key.M);
        yield return new WaitForSecondsRealtime(0.5f);
        Check("M opens and closes the map (or says there is none yet)", !MansionHud.PanelOpen, "opened " + mapOpened);
        yield return Tap(Key.Tab);
        yield return new WaitForSecondsRealtime(0.5f);
        bool diaryOpened = MansionHud.PanelOpen;
        yield return Shot("diary");
        yield return Tap(Key.Tab);
        yield return new WaitForSecondsRealtime(0.5f);
        Check("Tab opens and closes the diary", diaryOpened && !MansionHud.PanelOpen);

        // 3. Crouch and run answer.
        Hold(Key.C);
        yield return new WaitForSeconds(0.6f);
        bool crouched = player.IsCrouched;
        Release(Key.C);
        yield return new WaitForSeconds(0.6f);
        Check("C crouches and standing comes back", crouched && !player.IsCrouched);

        // 4. The torch in the hall's cabinet.
        yield return GoTo("hall", P(1.2f, 0f, 4.8f));
        yield return Take("lanterna", P(2.9f, 0f, 6.3f));

        // 5. The first recorder, on the bay window's table of the dining room.
        yield return Legs("dining room", false, P(-2.6f, 0f, 5.4f), P(-5.4f, 0f, 5.4f), P(-7.6f, 0f, 4.1f));
        Recorder first = Nearest<Recorder>(P(-8.05f, 0.8f, 2.55f), 3f);
        if (first != null)
        {
            yield return Use(first);
            Check("recorder 1 plays with E", first.IsPlaying || first.Heard, first.name);
            yield return Shot("recorder1");
            yield return WaitFor("recorder 1 ends and the story moves on", () => Stage != "Listen", 150f);
        }
        else
        {
            Check("recorder 1 found", false);
        }

        // 6. The kitchen through the dining room's door (shut now): batteries, then F.
        yield return Legs("to kitchen door", false, P(-7.6f, 0f, 6.6f), P(-10.7f, 0f, 6.3f));
        yield return OpenDoorAt("dining room to kitchen door", P(-11.6f, 1f, 6.3f));
        yield return Legs("kitchen", false, P(-12.9f, 0f, 6.3f), P(-13.6f, 0f, 9.5f));
        StoryPickup cells = Pickup("pilhas");
        if (cells != null)
        {
            Vector3 c = cells.transform.position;
            yield return Take("pilhas", new Vector3(c.x + 0.2f, 0f, c.z - 1.0f));
        }

        yield return Tap(Key.F);
        yield return new WaitForSeconds(1.2f);
        yield return Shot("torch_on");

        // 7. Out to the courtyard by the kitchen's gallery door, over the lawn, up the grand stair to the fungus light.
        yield return Legs("to gallery door", false, P(-12.2f, 0f, 12.6f), P(-10.7f, 0f, 12.6f));
        yield return OpenDoorAt("kitchen to gallery door", P(-9.7f, 1f, 12.6f));
        yield return Legs("courtyard", false, P(-8.6f, 0f, 12.6f), P(-8.6f, 0f, 15f), P(-6.3f, -0.36f, 15.4f), P(-2.6f, -0.36f, 13.6f), P(0.1f, -0.36f, 13.7f));
        yield return Shot("lawn");
        yield return Legs("grand stair", false, P(0.1f, UF, 19.5f), P(-1.4f, UF, 20.4f));
        Check("on the upper floor after the stair", Mathf.Abs(player.transform.position.y - UF) < 0.4f, "y " + player.transform.position.y.ToString("F2"));
        yield return WaitFor("blackout in the fungus light", () => Stage != "Explore", 12f);
        yield return new WaitForSeconds(3f);
        yield return Shot("blackout");
        Note("      stage " + Stage + ", torch item " + items.HasItem("lanterna") + ", batteries " + items.HasItem("pilhas"));

        // 8. A grown doorway upstairs: E says what is missing and it does not open.
        yield return Legs("west gallery", false, P(-6.2f, UF, 20.4f), P(-8.8f, UF, 19.8f), P(-8.8f, UF, 16.8f));
        GrowthBarrier westBarrier = Nearest<GrowthBarrier>(P(-10f, UF, 16.8f), 2f);
        if (westBarrier != null)
        {
            yield return Use(westBarrier, westBarrier.transform.position + Vector3.up * 1.3f);
            Check("barrier refuses without the hook", !westBarrier.IsOpen, westBarrier.name);
            yield return Shot("barrier_blocked");
            yield return GoTo("into the grown doorway (must not pass)", P(-11.6f, UF, 16.8f), false, 0.45f, 5f);
        }

        // 9. Down again, the key on the kitchen floor.
        yield return Legs("down the stair", false, P(-8.8f, UF, 18.2f), P(-8.8f, UF, 20.2f), P(-1.4f, UF, 20.4f), P(0.1f, UF, 19.4f), P(0.1f, -0.36f, 13.5f), P(-3.2f, -0.36f, 13.6f), P(-6.3f, -0.36f, 15.4f), P(-8.6f, 0f, 15f), P(-8.6f, 0f, 12.6f), P(-11f, 0f, 12.6f));
        yield return Take("chave_desumidificador", P(-12.5f, 0f, 12.3f));

        // 10. Round the gallery to the laundry, its door with the key, the hooked pole.
        yield return Legs("to laundry", false, P(-8.6f, 0f, 12.6f), P(-8.6f, 0f, 20.3f), P(-2f, 0f, 20.3f), P(4f, 0f, 20.3f), P(8.9f, 0f, 20.6f));
        yield return OpenDoorAt("laundry door with the key", P(10.1f, 1f, 20.7f));
        yield return new WaitForSeconds(1f);
        yield return Legs("laundry", false, P(11.9f, 0f, 20.9f), P(12.4f, 0f, 22.6f));
        yield return Shot("laundry");
        yield return Take("gancho", P(12.2f, 0f, 23.3f));
        Note("      stage after the hook: " + Stage);

        // 11. The chase: run to the fungus light.
        yield return Legs("chase run", true, P(12.2f, 0f, 21f), P(8.9f, 0f, 20.5f), P(8.9f, 0f, 16.4f), P(6.9f, -0.36f, 16.1f), P(3.3f, -0.36f, 15.6f), P(2.9f, -0.36f, 13.6f), P(0.1f, -0.36f, 13.5f), P(0.1f, UF, 19.5f), P(-1.2f, UF, 20.4f));
        yield return Shot("chase_end");
        yield return WaitFor("the chase ends in the fungus light", () => Stage == "FindSuit", 45f);
        if (Stage != "FindSuit")
        {
            Note("      caught or lost: the rest of the path needs the chase done; stopping here.");
            Summary = "walkthrough: " + ok + " ok, " + failed + " failed, " + stuck + " stuck; stopped at the chase, stage " + Stage;
            Note(Summary);
            held.Clear();
            Send();
            Done = true;
            yield break;
        }


        // 12. With the hook the grown doorway opens, and the room behind it can be walked into.
        yield return Legs("west gallery again", false, P(-6.2f, UF, 20.4f), P(-8.8f, UF, 19.8f), P(-8.8f, UF, 16.8f));
        if (westBarrier != null)
        {
            yield return Use(westBarrier, westBarrier.transform.position + Vector3.up * 1.3f);
            yield return new WaitForSeconds(3f);
            Check("barrier opens with the hook", westBarrier.IsOpen, westBarrier.name);
            yield return Shot("barrier_open");
            yield return GoTo("into the room behind the barrier", P(-11.8f, UF, 16.8f));
            yield return GoTo("back to the gallery", P(-8.8f, UF, 16.8f));
        }

        // 13. The attic: pull the ladder with the hook, climb, the suit, the map, the filter, down again.
        yield return GoTo("under the hatch", P(-9.3f, UF, 14.3f));
        AtticHatch hatch = FindObjectsByType<AtticHatch>(FindObjectsInactive.Exclude).OrderBy(h => (h.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
        if (hatch != null)
        {
            yield return Use(hatch);
            yield return new WaitForSeconds(2.5f);
            Check("hatch comes down with the hook", hatch.IsDown);
            yield return Use(hatch);
            yield return new WaitForSeconds(4f);
            Check("E climbs to the attic", player.transform.position.y > 6.5f, "y " + player.transform.position.y.ToString("F2"));
            yield return Shot("attic");
            foreach (string item in new[] { "traje", "mapa", "filtro_1" })
            {
                StoryPickup pickup = Pickup(item);
                if (pickup != null)
                {
                    Vector3 at = pickup.transform.position;
                    Vector3 from = player.transform.position;
                    Vector3 stand = at + (new Vector3(from.x, at.y, from.z) - at).normalized * 1.0f;
                    yield return Take(item, new Vector3(stand.x, from.y, stand.z));
                    // Putting the suit on holds the input for a moment.
                    float free = Time.time + 12f;
                    while (player.InputLocked && Time.time < free)
                    {
                        yield return null;
                    }

                }
                else
                {
                    Check("pickup " + item + " exists", false);
                }
            }

            yield return new WaitForSeconds(2f);
            yield return CloseNote();
            Note("      stage after the suit: " + Stage + ", input locked " + player.InputLocked);
            // Down: the hatch from above.
            AtticHatch down = FindObjectsByType<AtticHatch>(FindObjectsInactive.Exclude).OrderBy(h => (h.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            if (down != null)
            {
                Vector3 over = down.transform.position;
                yield return GoTo("to the hatch from above", new Vector3(over.x, player.transform.position.y, over.z - 1.1f), false, 0.5f, 15f);
                yield return Use(down);
                yield return new WaitForSeconds(4f);
            }

            Check("back on the upper floor from the attic", player.transform.position.y < 5f, "y " + player.transform.position.y.ToString("F2"));
            if (player.transform.position.y > 5f)
            {
                player.Teleport(P(-9.3f, UF, 14.3f), 0f);
                yield return new WaitForSeconds(0.5f);
            }
        }
        else
        {
            Check("attic hatch found", false);
        }

        // 14. Past the spores with the mask, the keypad on the top-row digits, the basement key.
        yield return Legs("to the generator room", false, P(-8.8f, UF, 19.8f), P(-1.4f, UF, 20.4f), P(4f, UF, 20.4f), P(9.2f, UF, 20.4f));
        Check("the spore wall lets the mask through", player.transform.position.x > P(4f, 0f, 0f).x, "x " + player.transform.position.x.ToString("F2"));
        yield return Shot("east_gallery");
        Door generator = Nearest<Door>(P(10.4f, UF + 1f, 20.4f), 2.5f, d => !d.IsOpen);
        if (generator != null)
        {
            yield return Use(generator);
            yield return new WaitForSecondsRealtime(0.6f);
            bool keypad = MansionHud.PanelOpen;
            yield return Shot("keypad");
            foreach (Key digit in new[] { Key.Digit2, Key.Digit0, Key.Digit1, Key.Digit4 })
            {
                yield return Tap(digit);
                yield return new WaitForSecondsRealtime(0.15f);
            }

            yield return Tap(Key.Enter);
            yield return new WaitForSecondsRealtime(2.5f);
            Check("keypad opens and takes 2014 on the top-row digits", keypad && generator.IsOpen, "panel " + keypad + " open " + generator.IsOpen);
            if (MansionHud.PanelOpen)
            {
                yield return Tap(Key.Escape);
                yield return new WaitForSecondsRealtime(0.4f);
            }

            if (!generator.IsOpen)
            {
                generator.RestoreOpen();
            }
        }
        else
        {
            Check("generator door found shut", false);
        }

        yield return GoTo("generator room", P(11.4f, UF, 20.4f));
        StoryPickup key = Pickup("chave_porao");
        if (key != null)
        {
            Vector3 k = key.transform.position;
            yield return Take("chave_porao", new Vector3(k.x - 0.9f, UF, k.z - 0.3f));
        }

        yield return new WaitForSeconds(1f);
        yield return CloseNote();
        Note("      stage after the basement key: " + Stage);

        // 15. Down to the china room: the damp growth gives way now.
        yield return Legs("to the china room", false, P(9.2f, UF, 20.4f), P(0.1f, UF, 20.4f), P(0.1f, UF, 19.4f), P(0.1f, -0.36f, 13.5f), P(-3.2f, -0.36f, 13.6f), P(-6.3f, -0.36f, 15.4f), P(-8.6f, 0f, 15f), P(-8.6f, 0f, 20.2f), P(-7f, 0f, 20.2f));
        GrowthBarrier china = Nearest<GrowthBarrier>(P(-7f, 0f, 21.4f), 2f);
        if (china != null)
        {
            yield return Use(china, china.transform.position + Vector3.up * 1.3f);
            yield return new WaitForSeconds(3f);
            Check("damp barrier opens with the basement key", china.IsOpen, china.name);
            yield return GoTo("into the china room", P(-7f, 0f, 22.9f));
            yield return Shot("china_room");
        }

        Summary = "walkthrough: " + ok + " ok, " + failed + " failed, " + stuck + " stuck; stage " + Stage;
        Note(Summary);
        held.Clear();
        Send();
        InputSystem.RemoveDevice(keyboard);
        InputSystem.RemoveDevice(mouse);
        Done = true;
    }
}
