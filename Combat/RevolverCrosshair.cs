using UnityEngine;
using UnityEngine.UI;

// The revolver's crosshair (Gustavo, 07/10: "uma crosshair de respeito"). Four ticks around a dot whose gap is the
// real spread of the next bullet on screen (stance, walking, the last shots: Revolver.Spread, turned into pixels by
// the camera's field of view), so where the ticks are is where the bullet can land. Kicks open on every shot, turns
// red over a creature, goes dull and closes on an empty or reloading gun, and fades when the gun is aimed
// down the sights. A hit marker flashes on a hit, bigger and red on a kill.
// A prefab (Prefabs/UI/RevolverCrosshair, built by Editor/RevolverCrosshairPrefab) so it can be opened and edited;
// the Revolver puts it on its HUD canvas.
public sealed class RevolverCrosshair : MonoBehaviour
{
    [SerializeField]
    private CanvasGroup group;

    [Tooltip("Up, right, down, left.")]
    [SerializeField]
    private RectTransform[] ticks = new RectTransform[4];

    [SerializeField]
    private Image dot;

    [SerializeField]
    private RectTransform hitMarker;

    [SerializeField]
    private Image[] hitLines = new Image[4];

    [Header("Look")]
    [SerializeField]
    private Color tint = new Color(0.95f, 0.93f, 0.86f, 0.95f);

    [SerializeField]
    private Color targetTint = new Color(1f, 0.38f, 0.3f, 1f);

    [SerializeField]
    private Color emptyTint = new Color(0.55f, 0.52f, 0.48f, 0.6f);

    [SerializeField]
    private Color killTint = new Color(0.9f, 0.12f, 0.08f, 1f);

    [Tooltip("Smallest gap between the centre and the ticks (pixels at 1080 high), so a steady aim still reads.")]
    [SerializeField]
    private float minGap = 6f;

    [SerializeField]
    private float maxGap = 90f;

    private Revolver revolver;
    private DiveHud hud;
    private Camera view;
    private Image[] tickImages;
    private float gap;
    private float punch;
    private float hitTime = -10f;
    private bool hitKill;
    private float shown = 1f;
    private bool visible = true;

    public void Bind(Revolver owner, Transform eye)
    {
        revolver = owner;
        view = eye != null ? eye.GetComponentInChildren<Camera>() : null;
        if (view == null)
        {
            view = Camera.main;
        }

        revolver.Fired += OnFired;
        revolver.Hit += OnHit;
        hud = FindAnyObjectByType<DiveHud>();
        OnEnable();
    }

    private void OnEnable()
    {
        if (hud != null)
        {
            hud.HideDot(true);
        }
    }

    private void OnDisable()
    {
        if (hud != null)
        {
            hud.HideDot(false);
        }
    }

    private void OnDestroy()
    {
        if (revolver != null)
        {
            revolver.Fired -= OnFired;
            revolver.Hit -= OnHit;
        }
    }

    public void Show(bool on)
    {
        visible = on;
    }

    private void Awake()
    {
        tickImages = new Image[ticks.Length];
        for (int i = 0; i < ticks.Length; i++)
        {
            tickImages[i] = ticks[i] != null ? ticks[i].GetComponent<Image>() : null;
        }

        hitMarker.gameObject.SetActive(false);
    }

    private void OnFired()
    {
        punch = 1f;
    }

    private void OnHit(bool kill)
    {
        hitTime = Time.unscaledTime;
        hitKill = kill;
    }

    private void LateUpdate()
    {
        if (revolver == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        bool empty = revolver.Loaded == 0 || revolver.IsReloading;

        // The spread cone on screen: half the screen height covers half the vertical field of view.
        float fov = view != null ? view.fieldOfView : 60f;
        float height = ((RectTransform)transform.parent).rect.height;
        float target = Mathf.Tan(revolver.Spread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * height * 0.5f;
        target = Mathf.Clamp(target, minGap, maxGap) * (empty ? 0.6f : 1f);
        // Opens fast, closes slower: the shot reads as a kick and the recovery as the gun settling.
        gap = Mathf.Lerp(gap, target, 1f - Mathf.Exp((target > gap ? -30f : -10f) * dt));
        punch = Mathf.MoveTowards(punch, 0f, dt / 0.12f);
        float g = gap + punch * 6f;

        ticks[0].anchoredPosition = new Vector2(0f, g);
        ticks[1].anchoredPosition = new Vector2(g, 0f);
        ticks[2].anchoredPosition = new Vector2(0f, -g);
        ticks[3].anchoredPosition = new Vector2(-g, 0f);

        Color c = empty ? emptyTint : revolver.OnTarget ? targetTint : tint;
        for (int i = 0; i < tickImages.Length; i++)
        {
            tickImages[i].color = c;
        }

        dot.color = c;

        // Aiming down the sights the sights are the crosshair.
        float alpha = 1f - Mathf.SmoothStep(0f, 1f, revolver.Aim);
        shown = Mathf.MoveTowards(shown, visible ? alpha : 0f, dt / 0.1f);
        group.alpha = shown;

        float since = Time.unscaledTime - hitTime;
        float life = hitKill ? 0.45f : 0.22f;
        bool hit = since < life;
        hitMarker.gameObject.SetActive(hit);
        if (hit)
        {
            float t = since / life;
            float size = (hitKill ? 1.35f : 1f) * Mathf.Lerp(1.25f, 1f, Mathf.Min(1f, t * 4f));
            hitMarker.localScale = new Vector3(size, size, 1f);
            Color h = hitKill ? killTint : tint;
            h.a *= 1f - t * t;
            for (int i = 0; i < hitLines.Length; i++)
            {
                hitLines[i].color = h;
            }
        }
    }
}
