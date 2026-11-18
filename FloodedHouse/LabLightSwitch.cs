using System.Collections;
using UnityEngine;

// The switch the note warns about ("Não acenda a luz perto dos tanques"). Disobeying is allowed and
// the world answers, in a way that makes sense under water: the breaker snaps, the tubes stutter on
// and show the tank room for the first time, the specimen in tank 1 slowly turns toward the player,
// then the submerged wiring shorts out in sparks and the tubes burst. A moment later something
// groans far down the corridor. No mechanical punishment; the room just proves it is alive.
public sealed class LabLightSwitch : Interactable
{
    [SerializeField]
    private Light[] tubeLights;

    [SerializeField]
    private Renderer[] tubeRenderers;

    [SerializeField]
    private ParticleSystem sparks;

    [SerializeField]
    private AudioSource roomAudio;

    [SerializeField]
    private AudioClip clickClip;

    [SerializeField]
    private Transform watcher;

    [SerializeField]
    private AudioSource distantVoice;

    [SerializeField]
    private AudioClip groan;

    [SerializeField]
    private DreadDirector dread;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color TubeEmission = new Color(0.85f, 0.95f, 1f) * 3f;

    private MaterialPropertyBlock block;
    private float[] tubeIntensities;
    private bool used;
    private bool running;

    public override bool CanInteract => !running;
    public override string Prompt => Loc.T(used ? "prompt.switch_again" : "prompt.switch_on");

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        tubeIntensities = new float[tubeLights.Length];
        for (int i = 0; i < tubeLights.Length; i++)
        {
            tubeIntensities[i] = tubeLights[i].intensity;
        }

        SetTubes(0f);
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (roomAudio != null && clickClip != null)
        {
            roomAudio.PlayOneShot(clickClip, 0.9f);
        }

        if (used)
        {
            interactor.Hud.ShowMessage(Loc.T("msg.switch_dead"), 3f);
            interactor.NotifyUsed(this);
            return;
        }

        used = true;
        StartCoroutine(Sequence(interactor));
        interactor.NotifyUsed(this);
    }

    private IEnumerator Sequence(PlayerInteractor interactor)
    {
        running = true;
        yield return new WaitForSeconds(0.45f);

        // Stutter on, like old tubes do.
        float[] stutter = { 0.6f, 0f, 0.9f, 0.2f, 1f };
        foreach (float level in stutter)
        {
            SetTubes(level);
            yield return new WaitForSeconds(Random.Range(0.05f, 0.14f));
        }

        if (roomAudio != null)
        {
            roomAudio.clip = ProceduralAudio.ElectricHumLoop();
            roomAudio.loop = true;
            roomAudio.volume = 0.35f;
            roomAudio.Play();
        }

        // Lit: the thing in tank 1 turns to face whoever flipped the switch. The watcher is a pivot whose
        // +Z is the specimen's face.
        Quaternion from = watcher != null ? watcher.rotation : Quaternion.identity;
        Vector3 toPlayer = watcher != null ? interactor.transform.position + Vector3.up * 1.5f - watcher.position : Vector3.forward;
        Quaternion to = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.8f)
        {
            if (watcher != null)
            {
                watcher.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            }

            SetTubes(Random.value < 0.04f ? 0.5f : 1f);
            yield return null;
        }

        // Water and mains: the short circuit.
        if (sparks != null)
        {
            sparks.Play();
        }

        if (roomAudio != null)
        {
            roomAudio.Stop();
            roomAudio.loop = false;
            roomAudio.PlayOneShot(ProceduralAudio.ShortCircuit(), 1f);
        }

        SetTubes(1.6f);
        yield return new WaitForSeconds(0.06f);
        SetTubes(0f);
        yield return new WaitForSeconds(0.2f);
        SetTubes(0.3f);
        yield return new WaitForSeconds(0.05f);
        SetTubes(0f);

        yield return new WaitForSeconds(1.6f);
        if (distantVoice != null && groan != null)
        {
            distantVoice.pitch = 0.5f;
            distantVoice.PlayOneShot(groan, 1f);
        }

        if (dread != null)
        {
            dread.Provoke(0.6f);
        }

        interactor.Hud.ShowMessage(Loc.T("msg.switch_blown"), 4f);
        running = false;
    }

    private void SetTubes(float level)
    {
        for (int i = 0; i < tubeLights.Length; i++)
        {
            tubeLights[i].enabled = level > 0f;
            tubeLights[i].intensity = tubeIntensities[i] * level;
        }

        foreach (Renderer tube in tubeRenderers)
        {
            tube.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, TubeEmission * level);
            tube.SetPropertyBlock(block);
        }
    }
}
