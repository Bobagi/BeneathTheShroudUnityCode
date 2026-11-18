using System.Collections;
using UnityEngine;

// Six-shot revolver: fires only while raised, recoils, flashes, kicks up dirt where it hits and
// reloads one round at a time (a legacy WW1 prototype item).
public sealed class RevolverWeapon : HeldItemBehaviour
{
    [Header("Ammo")]
    [SerializeField]
    private int cylinderCapacity = 6;

    [SerializeField]
    private int loadedRounds = 6;

    [SerializeField]
    private int reserveRounds = 18;

    [SerializeField]
    private float secondsBetweenShots = 0.45f;

    [SerializeField]
    private float secondsPerRoundReload = 0.55f;

    [Header("Ballistics")]
    [SerializeField]
    private float range = 250f;

    [SerializeField]
    private float impactForce = 6f;

    [Header("Parts")]
    [SerializeField]
    private Transform muzzle;

    [SerializeField]
    private Transform cylinder;

    [SerializeField]
    private Vector3 cylinderSpinAxis = Vector3.forward;

    [SerializeField]
    private Light muzzleLight;

    [SerializeField]
    private ParticleSystem muzzleFlash;

    [SerializeField]
    private ParticleSystem impactDustPrefab;

    [Header("Audio")]
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip[] shotClips;

    [SerializeField]
    private AudioClip dryFireClip;

    [SerializeField]
    private AudioClip loadRoundClip;

    [SerializeField]
    private AudioClip cockClip;

    [Header("Recoil")]
    [SerializeField]
    private float recoilKickDegrees = 14f;

    [SerializeField]
    private float recoilRecoverSpeed = 10f;

    private float nextShotTime;
    private Coroutine reloadRoutine;
    private float recoilAngle;
    private Quaternion restLocalRotation;
    private bool hasRestRotation;

    // UI hooks: a shot (true when it struck something that reacts to bullets), a round loaded
    // during a reload, and the hammer falling on an empty chamber.
    public event System.Action<bool> Fired;
    public event System.Action RoundLoaded;
    public event System.Action DryFired;

    public int CylinderCapacity => cylinderCapacity;
    public int LoadedRounds => loadedRounds;
    public int ReserveRounds => reserveRounds;
    public bool IsReloading => reloadRoutine != null;

    public override string StatusText =>
        IsReloading
            ? $"Recarregando... {loadedRounds}/{cylinderCapacity}"
            : $"{loadedRounds}/{cylinderCapacity}  |  {reserveRounds}";

    private WorldItem worldItem;

    private void Awake()
    {
        worldItem = GetComponent<WorldItem>();
        if (muzzleLight != null)
        {
            muzzleLight.enabled = false;
        }
    }

    protected override void OnEquipped()
    {
        hasRestRotation = false;
    }

    protected override void OnUnequipped()
    {
        StopReload();
        recoilAngle = 0f;
        if (hasRestRotation)
        {
            transform.localRotation = restLocalRotation;
        }
    }

    protected override void OnRaisedChanged(bool raised)
    {
        if (raised && cockClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(cockClip, 0.6f);
        }
    }

    public override void OnPrimaryAction(Ray aimRay, Vector3 aimPoint)
    {
        if (!IsRaised || Time.time < nextShotTime)
        {
            return;
        }

        if (IsReloading)
        {
            StopReload();
        }

        nextShotTime = Time.time + secondsBetweenShots;

        if (loadedRounds <= 0)
        {
            Play(dryFireClip, 1f);
            DryFired?.Invoke();
            return;
        }

        loadedRounds--;
        Fire(aimRay, aimPoint);
    }

    public override void OnReloadAction()
    {
        if (IsReloading || loadedRounds >= cylinderCapacity || reserveRounds <= 0)
        {
            return;
        }

        reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    private void Fire(Ray aimRay, Vector3 aimPoint)
    {
        if (shotClips != null && shotClips.Length > 0)
        {
            Play(shotClips[Random.Range(0, shotClips.Length)], 1f);
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.Play(true);
        }

        if (muzzleLight != null)
        {
            StartCoroutine(FlashLight());
        }

        SpinCylinder();
        recoilAngle = recoilKickDegrees;
        CameraShake.Shake(0.35f, 0.18f);
        WorldEvents.RaiseNoise(transform.position, 60f);

        // The bullet travels from the muzzle toward what the reticle covers.
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        Vector3 direction = (aimPoint - origin).normalized;
        bool struckShootable = false;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                hit.rigidbody.AddForceAtPosition(direction * impactForce, hit.point, ForceMode.Impulse);
            }

            IShootable shootable = hit.collider.GetComponentInParent<IShootable>();
            if (shootable != null)
            {
                shootable.OnShot(hit.point, direction);
                struckShootable = true;
            }

            if (impactDustPrefab != null)
            {
                ParticleSystem dust = Instantiate(
                    impactDustPrefab,
                    hit.point + hit.normal * 0.02f,
                    Quaternion.LookRotation(hit.normal)
                );
                dust.Play(true);
                Destroy(dust.gameObject, 3f);
            }
        }

        Fired?.Invoke(struckShootable);
    }

    private IEnumerator FlashLight()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(0.05f);
        muzzleLight.enabled = false;
    }

    private IEnumerator ReloadRoutine()
    {
        while (loadedRounds < cylinderCapacity && reserveRounds > 0)
        {
            yield return new WaitForSeconds(secondsPerRoundReload);
            loadedRounds++;
            reserveRounds--;
            SpinCylinder();
            Play(loadRoundClip, 0.8f);
            RoundLoaded?.Invoke();
            if (Owner != null)
            {
                Owner.NotifyChanged();
            }
        }

        reloadRoutine = null;
        if (Owner != null)
        {
            Owner.NotifyChanged();
        }
    }

    private void StopReload()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
    }

    private void SpinCylinder()
    {
        if (cylinder != null)
        {
            cylinder.localRotation *= Quaternion.AngleAxis(360f / cylinderCapacity, cylinderSpinAxis);
        }
    }

    private void LateUpdate()
    {
        if (Owner == null)
        {
            return;
        }

        // Muzzle-up recoil applied on top of the hand pose, recovering smoothly.
        if (!hasRestRotation)
        {
            restLocalRotation = transform.localRotation;
            hasRestRotation = true;
        }

        recoilAngle = Mathf.Lerp(recoilAngle, 0f, 1f - Mathf.Exp(-recoilRecoverSpeed * Time.deltaTime));
        ItemDefinition definition = worldItem != null ? worldItem.Definition : null;
        Vector3 pivot = definition != null
            ? Vector3.Cross(definition.aimUpLocal, definition.aimForwardLocal).normalized
            : Vector3.right;
        transform.localRotation = restLocalRotation * Quaternion.AngleAxis(-recoilAngle, pivot);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
