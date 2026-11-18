using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// Test tool (not used in the demo): films the revolver's reload on real frames, the way the game plays it.
// Starts itself only when <project>/Temp/reload_film.txt exists (first line: output folder, second: rounds left in the
// gun, third: frames per second), so a normal Play is untouched. Time.captureFramerate makes every frame exactly
// 1/fps long, and after each frame the player's view and two views of the body (front right, front left) are rendered
// to <folder>/<view>_<n>.jpg; done.txt marks the end. The pictures are what Gustavo and the model are judged on:
// "does the hand do what a hand would do" is answered by looking, not by the numbers (08/10).
public sealed class ReloadFilmer : MonoBehaviour
{
    private const string Trigger = "Temp/reload_film.txt";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (File.Exists(Trigger))
        {
            new GameObject("ReloadFilmer").AddComponent<ReloadFilmer>();
        }
    }

    private IEnumerator Start()
    {
        string[] config = File.ReadAllLines(Trigger);
        File.Delete(Trigger);
        string folder = config[0];
        int roundsLeft = config.Length > 1 ? int.Parse(config[1]) : 2;
        int fps = config.Length > 2 ? int.Parse(config[2]) : 30;
        int spareRounds = config.Length > 3 ? int.Parse(config[3]) : 60;
        Directory.CreateDirectory(folder);
        Application.runInBackground = true;
        Time.captureFramerate = fps;

        Revolver revolver = null;
        while (revolver == null)
        {
            revolver = FindAnyObjectByType<Revolver>(FindObjectsInactive.Include);
            yield return null;
        }

        // The inventory starts with the torch in hand: draw the revolver.
        Inventory inventory = FindAnyObjectByType<Inventory>();
        if (inventory != null)
        {
            inventory.Equip(Inventory.Hand.Revolver, true);
        }

        for (int i = 0; i < 90; i++)
        {
            yield return null;
        }

        // The gun fired down to roundsLeft: the spent cases are still in the cylinder.
        revolver.SetAmmo(roundsLeft, 6 - roundsLeft, spareRounds);
        FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
        Camera eyeCamera = Camera.main;
        Camera side = new GameObject("FilmCamera").AddComponent<Camera>();
        side.CopyFrom(eyeCamera);
        side.enabled = false;
        side.fieldOfView = 40f;
        side.nearClipPlane = 0.03f;
        RenderTexture rt = new RenderTexture(960, 540, 24);
        Texture2D tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        eyeCamera.targetTexture = null;

        focus = revolver.transform;
        for (int i = 0; i < 20; i++)
        {
            Shoot(eyeCamera, side, rt, tex, folder, i - 20);
            yield return null;
        }

        int ejected = 0;
        int inserted = 0;
        int startLoaded = revolver.Loaded;
        int startSpare = revolver.Spare;
        revolver.CaseEjected += _ => ejected++;
        revolver.RoundInserted += _ => inserted++;
        revolver.Reload();
        int frame = 0;
        int after = 0;
        while (after < 30 && frame < fps * 40)
        {
            yield return null;
            Shoot(eyeCamera, side, rt, tex, folder, frame);
            frame++;
            after = revolver.IsReloading ? 0 : after + 1;
        }

        File.WriteAllText(Path.Combine(folder, "done.txt"), frame + " frames at " + fps + " fps; before: loaded " + startLoaded + ", spare " + startSpare
            + "; cases out " + ejected + ", rounds in " + inserted + "; after: loaded " + revolver.Loaded + ", spare " + revolver.Spare);
        Time.captureFramerate = 0;
        Destroy(gameObject);
    }

    private static void Shoot(Camera eye, Camera side, RenderTexture rt, Texture2D tex, string folder, int n)
    {
        if (n < 0)
        {
            return;
        }

        Save(eye, rt, tex, Path.Combine(folder, "fp_" + n.ToString("D4") + ".jpg"));
        Transform e = eye.transform;
        Vector3 chest = e.position - e.up * 0.28f + e.forward * 0.20f;
        PlaceSide(side, chest + e.forward * 1.5f + e.right * 0.75f + e.up * 0.10f, chest);
        Save(side, rt, tex, Path.Combine(folder, "sr_" + n.ToString("D4") + ".jpg"));
        PlaceSide(side, chest + e.forward * 1.5f - e.right * 0.75f + e.up * 0.10f, chest);
        Save(side, rt, tex, Path.Combine(folder, "sl_" + n.ToString("D4") + ".jpg"));
        if (focus != null)
        {
            // Close views of the gun and both hands (right side, left side, from above): the grip is judged here.
            Vector3 c = focus.position + focus.forward * 0.05f - focus.up * 0.03f;
            side.fieldOfView = 30f;
            PlaceSide(side, c + e.right * 0.55f + e.forward * 0.1f, c);
            Save(side, rt, tex, Path.Combine(folder, "cr_" + n.ToString("D4") + ".jpg"));
            PlaceSide(side, c - e.right * 0.55f + e.forward * 0.1f, c);
            Save(side, rt, tex, Path.Combine(folder, "cl_" + n.ToString("D4") + ".jpg"));
            PlaceSide(side, c + Vector3.up * 0.6f + e.forward * 0.1f, c);
            Save(side, rt, tex, Path.Combine(folder, "ct_" + n.ToString("D4") + ".jpg"));
            side.fieldOfView = 40f;
        }
    }

    private static Transform focus;

    private static void PlaceSide(Camera side, Vector3 position, Vector3 target)
    {
        side.transform.position = position;
        side.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
    }

    private static void Save(Camera camera, RenderTexture rt, Texture2D tex, string path)
    {
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        camera.targetTexture = null;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply(false);
        RenderTexture.active = previous;
        File.WriteAllBytes(path, tex.EncodeToJPG(88));
    }
}
