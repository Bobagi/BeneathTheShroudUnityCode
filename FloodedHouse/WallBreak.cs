using System.Collections;
using UnityEngine;

// A stretch of wall the creature comes through: the intact piece disappears and its chunks fly into the
// room (rigid bodies, pushed away from the outside), with dust and the crash. Used once.
public sealed class WallBreak : MonoBehaviour
{
    [SerializeField]
    private GameObject intact;

    [SerializeField]
    private Rigidbody[] chunks;

    [SerializeField]
    private Vector3 inward = Vector3.left;

    [SerializeField]
    private ParticleSystem dust;

    [SerializeField]
    private AudioSource source;

    public bool Broken { get; private set; }

    private void Awake()
    {
        foreach (Rigidbody chunk in chunks)
        {
            chunk.gameObject.SetActive(false);
        }
    }

    // Loading a checkpoint: the wall is already down (the rubble was cleared by then).
    public void RestoreBroken()
    {
        Broken = true;
        intact.SetActive(false);
    }

    public void Break()
    {
        if (!Broken)
        {
            Broken = true;
            StartCoroutine(Collapse());
        }
    }

    private IEnumerator Collapse()
    {
        source.PlayOneShot(SoundLibrary.Instance.WallCollapse(), 1f);
        intact.SetActive(false);
        foreach (Rigidbody chunk in chunks)
        {
            chunk.gameObject.SetActive(true);
            chunk.AddForce((inward + Random.insideUnitSphere * 0.4f) * Random.Range(3f, 6f), ForceMode.VelocityChange);
            chunk.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);
        }

        if (dust != null)
        {
            dust.Play();
        }

        // Let the rubble settle, then freeze it so it costs nothing.
        yield return new WaitForSeconds(4f);
        foreach (Rigidbody chunk in chunks)
        {
            chunk.isKinematic = true;
        }
    }
}
