using UnityEngine;
using UnityEngine.UI;

// Four-tick reticle that breathes with the player: it only appears while the revolver is raised,
// spreads when moving, kicks open on each shot and settles as the aim steadies. A hit marker flashes
// when a bullet strikes something that reacts, and the ticks go dull on an empty chamber.
public sealed class DynamicCrosshair : MonoBehaviour
{
    [SerializeField]
    private PlayerInventory inventory;

    [SerializeField]
    private CharacterController body;

    [Header("Spread (pixels)")]
    [SerializeField]
    private float settledGap = 7f;

    [SerializeField]
    private float moveGap = 26f;

    [SerializeField]
    private float recoilGap = 34f;

    [SerializeField]
    private float settleSeconds = 0.6f;

    [Header("Look")]
    [SerializeField]
    private Color tint = new Color(0.93f, 0.9f, 0.82f, 0.9f);

    [SerializeField]
    private Color emptyTint = new Color(0.55f, 0.2f, 0.16f, 0.9f);

    [SerializeField]
    private Color hitTint = new Color(1f, 0.35f, 0.25f, 1f);

    private RectTransform root;
    private RectTransform[] ticks;
    private Image[] tickImages;
    private Image dot;
    private RectTransform hitMarker;
    private Image[] hitImages;
    private CanvasGroup group;

    private float gap;
    private float recoil;
    private float steady;
    private float hitTimer;
    private float kickScale = 1f;
    private RevolverWeapon trackedWeapon;

    public void Initialize(PlayerInventory owner, CharacterController playerBody, Transform parent)
    {
        inventory = owner;
        body = playerBody;
        Build(parent);
    }

    private void Build(Transform parent)
    {
        root = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(120f, 120f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        ticks = new RectTransform[4];
        tickImages = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image tick = HudFactory.Image("Tick" + i, root, HudSprites.Square, tint);
            bool vertical = i % 2 == 0;
            tick.rectTransform.sizeDelta = vertical ? new Vector2(3.5f, 13f) : new Vector2(13f, 3.5f);
            ticks[i] = tick.rectTransform;
            tickImages[i] = tick;
            HudFactory.Shadow(tick.gameObject);
        }

        dot = HudFactory.Image("Dot", root, HudSprites.Circle, tint);
        dot.rectTransform.sizeDelta = new Vector2(5f, 5f);

        hitMarker = new GameObject("HitMarker", typeof(RectTransform)).GetComponent<RectTransform>();
        hitMarker.SetParent(root, false);
        hitImages = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            Image stroke = HudFactory.Image("Hit" + i, hitMarker, HudSprites.Square, hitTint);
            stroke.rectTransform.sizeDelta = new Vector2(2.5f, 12f);
            float angle = 45f + i * 90f;
            stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 offset = Quaternion.Euler(0f, 0f, angle) * Vector2.up * 13f;
            stroke.rectTransform.anchoredPosition = offset;
            hitImages[i] = stroke;
        }

        hitMarker.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (root == null || inventory == null)
        {
            return;
        }

        RevolverWeapon weapon = inventory.HeldItem != null ? inventory.HeldItem.GetComponent<RevolverWeapon>() : null;
        Track(weapon);

        bool visible = weapon != null && inventory.IsAiming;
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * (visible ? 6f : 10f));

        // Aim steadies over time while raised; moving or firing unsettles it.
        steady = visible ? Mathf.MoveTowards(steady, 1f, Time.deltaTime / settleSeconds) : 0f;
        float speed = body != null ? new Vector2(body.velocity.x, body.velocity.z).magnitude : 0f;
        float movement = Mathf.Clamp01(speed / 4.5f);
        recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 2.2f);

        float target = settledGap + (1f - steady) * 10f + movement * moveGap + recoil * recoilGap;
        gap = Mathf.Lerp(gap, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
        kickScale = Mathf.Lerp(kickScale, 1f, 1f - Mathf.Exp(-14f * Time.deltaTime));

        for (int i = 0; i < 4; i++)
        {
            Vector2 direction = i == 0 ? Vector2.up : i == 1 ? Vector2.right : i == 2 ? Vector2.down : Vector2.left;
            ticks[i].anchoredPosition = direction * (gap + 5.5f);
        }

        root.localScale = Vector3.one * kickScale;

        bool empty = weapon != null && weapon.LoadedRounds == 0;
        Color current = empty ? emptyTint : tint;
        foreach (Image tick in tickImages)
        {
            tick.color = current;
        }

        dot.color = current;

        if (hitTimer > 0f)
        {
            hitTimer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(hitTimer / 0.35f);
            hitMarker.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, k);
            foreach (Image stroke in hitImages)
            {
                stroke.color = new Color(hitTint.r, hitTint.g, hitTint.b, k);
            }

            if (hitTimer <= 0f)
            {
                hitMarker.gameObject.SetActive(false);
            }
        }
    }

    private void Track(RevolverWeapon weapon)
    {
        if (weapon == trackedWeapon)
        {
            return;
        }

        if (trackedWeapon != null)
        {
            trackedWeapon.Fired -= OnFired;
            trackedWeapon.DryFired -= OnDryFired;
        }

        trackedWeapon = weapon;
        if (trackedWeapon != null)
        {
            trackedWeapon.Fired += OnFired;
            trackedWeapon.DryFired += OnDryFired;
        }
    }

    private void OnFired(bool hitShootable)
    {
        recoil = 1f;
        steady = 0.2f;
        kickScale = 1.25f;
        if (hitShootable)
        {
            hitTimer = 0.35f;
            hitMarker.gameObject.SetActive(true);
        }
    }

    private void OnDryFired()
    {
        kickScale = 0.85f;
    }

    private void OnDestroy()
    {
        Track(null);
    }
}
