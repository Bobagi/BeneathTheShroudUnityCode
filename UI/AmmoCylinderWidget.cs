using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The revolver's ammo shown as its own cylinder: six chambers in a ring that turns one notch per
// shot with a springy overshoot, chambers popping in as rounds are loaded. Appears only while the
// revolver is in hand.
public sealed class AmmoCylinderWidget : MonoBehaviour
{
    private PlayerInventory inventory;
    private RectTransform root;
    private RectTransform cylinder;
    private Image[] chambers;
    private float[] chamberPop;
    private TextMeshProUGUI reserveLabel;
    private TextMeshProUGUI stateLabel;
    private CanvasGroup group;
    private RevolverWeapon trackedWeapon;

    private float angle;
    private float angularVelocity;
    private float targetAngle;
    private float shake;

    private static readonly Color Loaded = new Color(0.86f, 0.7f, 0.42f, 1f);
    private static readonly Color Empty = new Color(0.18f, 0.16f, 0.14f, 0.85f);

    public void Initialize(PlayerInventory owner, Transform parent)
    {
        inventory = owner;
        root = HudFactory.Rect("AmmoCylinder", parent);
        root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.sizeDelta = new Vector2(170f, 150f);
        root.anchoredPosition = new Vector2(-40f, 36f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Image backing = HudFactory.Image("Glow", root, HudSprites.SoftGlow, new Color(0f, 0f, 0f, 0.55f));
        backing.rectTransform.sizeDelta = new Vector2(190f, 190f);
        backing.rectTransform.anchoredPosition = new Vector2(0f, 10f);

        cylinder = HudFactory.Rect("Cylinder", root);
        cylinder.sizeDelta = new Vector2(96f, 96f);
        cylinder.anchoredPosition = new Vector2(0f, 12f);
        Image frame = HudFactory.Image("Frame", cylinder, HudSprites.Ring, new Color(0.55f, 0.5f, 0.42f, 0.9f));
        frame.rectTransform.sizeDelta = new Vector2(96f, 96f);
        Image hub = HudFactory.Image("Hub", cylinder, HudSprites.Circle, new Color(0.35f, 0.32f, 0.28f, 1f));
        hub.rectTransform.sizeDelta = new Vector2(16f, 16f);

        chambers = new Image[6];
        chamberPop = new float[6];
        for (int i = 0; i < 6; i++)
        {
            float a = (90f - i * 60f) * Mathf.Deg2Rad;
            Image chamber = HudFactory.Image("Chamber" + i, cylinder, HudSprites.Circle, Loaded);
            chamber.rectTransform.sizeDelta = new Vector2(22f, 22f);
            chamber.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 29f;
            Image rim = HudFactory.Image("Rim", chamber.rectTransform, HudSprites.Ring, new Color(0f, 0f, 0f, 0.6f));
            rim.rectTransform.sizeDelta = new Vector2(24f, 24f);
            chambers[i] = chamber;
        }

        reserveLabel = HudFactory.Label("Reserve", root, 22f, TextAlignmentOptions.Center, HudFactory.Ink);
        reserveLabel.rectTransform.sizeDelta = new Vector2(170f, 28f);
        reserveLabel.rectTransform.anchoredPosition = new Vector2(0f, -54f);
        HudFactory.Shadow(reserveLabel.gameObject);

        stateLabel = HudFactory.Label("State", root, 16f, TextAlignmentOptions.Center, HudFactory.Accent);
        stateLabel.rectTransform.sizeDelta = new Vector2(220f, 22f);
        stateLabel.rectTransform.anchoredPosition = new Vector2(0f, 72f);
    }

    private void Update()
    {
        if (root == null)
        {
            return;
        }

        RevolverWeapon weapon = inventory.HeldItem != null ? inventory.HeldItem.GetComponent<RevolverWeapon>() : null;
        Track(weapon);
        group.alpha = Mathf.MoveTowards(group.alpha, weapon != null ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (weapon == null)
        {
            return;
        }

        // Critically-damped-ish spring toward the next notch, with visible overshoot.
        float stiffness = 180f;
        float damping = 14f;
        angularVelocity += ((targetAngle - angle) * stiffness - angularVelocity * damping) * Time.unscaledDeltaTime;
        angle += angularVelocity * Time.unscaledDeltaTime;
        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 3f);
        cylinder.localRotation = Quaternion.Euler(0f, 0f, angle);
        cylinder.anchoredPosition = new Vector2(Random.Range(-1f, 1f) * shake * 4f, 12f + Random.Range(-1f, 1f) * shake * 4f);

        int loaded = weapon.LoadedRounds;
        for (int i = 0; i < chambers.Length; i++)
        {
            bool isLoaded = i < loaded;
            chamberPop[i] = Mathf.MoveTowards(chamberPop[i], 0f, Time.unscaledDeltaTime * 4f);
            chambers[i].color = Color.Lerp(chambers[i].color, isLoaded ? Loaded : Empty, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            chambers[i].rectTransform.localScale = Vector3.one * (1f + chamberPop[i] * 0.6f);
        }

        reserveLabel.text = $"{loaded} <size=16><color=#9a8f7a>/ {weapon.ReserveRounds}</color></size>";
        stateLabel.text = weapon.IsReloading ? "recarregando" : (loaded == 0 ? "vazio  [R]" : string.Empty);
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
            trackedWeapon.RoundLoaded -= OnRoundLoaded;
            trackedWeapon.DryFired -= OnDryFired;
        }

        trackedWeapon = weapon;
        if (trackedWeapon != null)
        {
            trackedWeapon.Fired += OnFired;
            trackedWeapon.RoundLoaded += OnRoundLoaded;
            trackedWeapon.DryFired += OnDryFired;
        }
    }

    private void OnFired(bool _)
    {
        targetAngle -= 60f;
        shake = 1f;
    }

    private void OnRoundLoaded()
    {
        targetAngle += 60f;
        int index = Mathf.Clamp(trackedWeapon.LoadedRounds - 1, 0, chambers.Length - 1);
        chamberPop[index] = 1f;
    }

    private void OnDryFired()
    {
        shake = 0.6f;
        angularVelocity -= 220f;
    }

    private void OnDestroy()
    {
        Track(null);
    }
}
