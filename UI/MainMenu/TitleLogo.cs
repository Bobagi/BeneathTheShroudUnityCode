using UnityEngine;
using UnityEngine.UI;

// The title image over the menu (UI/Title/BeneathTheShroud_Title.png), drawn a fifth darker than the file (its cream
// letters reached 14:1 against the night sky; at 0.8 they sit near 9:1, still far over the 3:1 large type needs, and
// stop glaring: Gustavo 10/10). It catches the lightning: a cold copy of the logo on top flares with every flash
// (LightningStorm.Flashed). `breath` can add a slow glow between strikes (off by default). A slight drift against
// the mouse adds depth to the camera's own.
public sealed class TitleLogo : MonoBehaviour
{
    [SerializeField]
    private Image flash;

    [SerializeField]
    private float flashStrength = 0.55f;

    [SerializeField]
    private float breath;

    [SerializeField]
    private float parallax = 8f;

    private RectTransform rect;
    private Vector2 home;
    private float strike;

    private void Awake()
    {
        rect = (RectTransform)transform;
        home = rect.anchoredPosition;
    }

    private void OnEnable()
    {
        LightningStorm.Flashed += OnFlash;
    }

    private void OnDisable()
    {
        LightningStorm.Flashed -= OnFlash;
    }

    private void OnFlash(float amount)
    {
        strike = Mathf.Max(strike, amount);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        strike = Mathf.MoveTowards(strike, 0f, dt * 2.2f);
        if (flash != null)
        {
            float glow = breath * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.7f)) + flashStrength * strike;
            Color c = flash.color;
            c.a = glow;
            flash.color = c;
        }

        Vector2 mouse = MenuCameraDrift.MouseOffset;
        rect.anchoredPosition = home - mouse * parallax;
    }
}
