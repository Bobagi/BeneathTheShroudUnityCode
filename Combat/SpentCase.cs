using UnityEngine;

// An empty cartridge case (.357 Magnum brass): a revolver keeps its cases until it is reloaded; with the cylinder open
// each fired one sits in its chamber (Hold) until the ejector rod pushes it out (Revolver.EjectCase). Real physics, a ring on every bounce
// that is hard enough (SoundLibrary.caseDrops, louder the faster it hits), and it goes to sleep where it stops.
[RequireComponent(typeof(Rigidbody))]
public sealed class SpentCase : MonoBehaviour
{
    [SerializeField]
    private AudioSource source;

    private Rigidbody body;
    private float nextRing;
    private int lastClip = -1;

    public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();

    // Sitting in a chamber of the open cylinder, moving with it: no physics and no collider until it is thrown.
    public void Hold(Transform chamber, Vector3 position, Quaternion rotation)
    {
        gameObject.SetActive(true);
        Body.isKinematic = true;
        // Interpolation puts the transform back on the body's own pose every frame: held, the case would stay behind
        // in the air while the gun moves.
        Body.interpolation = RigidbodyInterpolation.None;
        SetCollider(false);
        transform.SetParent(chamber, true);
        transform.SetPositionAndRotation(position, rotation);
    }

    // Back to the pool without being thrown (a reload cut short).
    public void Release()
    {
        transform.SetParent(null, true);
        gameObject.SetActive(false);
    }

    public void Throw(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 spin)
    {
        gameObject.SetActive(true);
        transform.SetParent(null, true);
        Body.isKinematic = false;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        SetCollider(true);
        Body.position = position;
        Body.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
        Body.linearVelocity = velocity;
        Body.angularVelocity = spin;
        Body.WakeUp();
        nextRing = Time.time + 0.05f;
    }

    private void SetCollider(bool on)
    {
        if (TryGetComponent(out Collider shape))
        {
            shape.enabled = on;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        if (speed < 0.35f || Time.time < nextRing || source == null)
        {
            return;
        }

        nextRing = Time.time + 0.07f;
        AudioClip clip = SoundLibrary.Pick(SoundLibrary.Instance.caseDrops, ref lastClip);
        if (clip != null)
        {
            source.pitch = Random.Range(0.92f, 1.1f);
            source.PlayOneShot(clip, Mathf.Clamp01(speed / 3f) * 0.8f);
        }
    }
}
