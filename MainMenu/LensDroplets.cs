using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Rain on the menu camera's lens (Gustavo 10/10: "umas gotículas na câmera, que apareçam de vez em quando, poucas"):
// every few seconds one or two drops land somewhere on the screen; most sit and dry out over several seconds, some
// grow heavy and slide down leaving a wet streak that fades. (A version where every drop ran at once and was gone in
// 2 to 3 s was rejected: "ficou muito ruim".) Never more than a handful at once. A strike of
// lightning lights them up for a moment. Sprites from SourceAssets/Tools/lens_drop_textures.py; on a canvas under
// the menu's, so the text stays clean.
public sealed class LensDroplets : MonoBehaviour
{
    [SerializeField]
    private Sprite drop;

    [SerializeField]
    private Sprite trail;

    [SerializeField]
    private Vector2 intervalSeconds = new Vector2(3.5f, 9f);

    [SerializeField]
    private int maxDrops = 6;

    [Tooltip("Diameter in pixels of a 1080-high screen.")]
    [SerializeField]
    private Vector2 size = new Vector2(34f, 96f);

    [SerializeField]
    private Vector2 drySeconds = new Vector2(5f, 10f);

    [SerializeField]
    [Range(0f, 1f)]
    private float slideChance = 0.35f;

    [SerializeField]
    [Range(0f, 1f)]
    private float opacity = 0.9f;

    private sealed class Drop
    {
        public Image image;
        public Image streak;
        public float born;
        public float life;
        public float diameter;
        public bool slides;
        public float slideAfter;
        public float speed;
        public Vector2 start;
    }

    private readonly List<Drop> live = new List<Drop>();
    private readonly Stack<Drop> pool = new Stack<Drop>();
    private RectTransform area;
    private float next;
    private float flash;

    private void Awake()
    {
        area = (RectTransform)transform;
        next = Time.unscaledTime + Random.Range(1.5f, 4f);
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
        flash = Mathf.Max(flash, amount);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);
        if (now >= next)
        {
            next = now + Random.Range(intervalSeconds.x, intervalSeconds.y);
            int count = Random.value < 0.3f ? 2 : 1;
            for (int i = 0; i < count && live.Count < maxDrops; i++)
            {
                Spawn(now);
            }
        }

        for (int i = live.Count - 1; i >= 0; i--)
        {
            Drop d = live[i];
            float age = now - d.born;
            float k = age / d.life;
            if (k >= 1f)
            {
                Recycle(d);
                live.RemoveAt(i);
                continue;
            }

            // Lands in a tenth of a second, holds, then dries: fainter and a little smaller.
            float land = Mathf.Clamp01(age / 0.1f);
            float dry = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.45f, 1f, k));
            float scale = Mathf.Lerp(0.7f, 1f, land) * Mathf.Lerp(0.82f, 1f, dry);
            RectTransform rect = d.image.rectTransform;
            rect.sizeDelta = new Vector2(d.diameter, d.diameter * 1.04f) * scale;
            Color c = Color.white;
            c.a = opacity * land * dry * (1f + flash * 0.6f);
            d.image.color = c;

            if (d.slides && age > d.slideAfter)
            {
                // Heavy enough to run: slow at first, then faster, wobbling a little.
                float run = age - d.slideAfter;
                float fall = d.speed * run * run * 0.5f + d.speed * 0.3f * run;
                float wobble = Mathf.Sin(run * 3.1f + d.start.x) * d.diameter * 0.15f;
                rect.anchoredPosition = d.start + new Vector2(wobble, -fall);
                RectTransform s = d.streak.rectTransform;
                s.gameObject.SetActive(true);
                // Grows up from under the drop back to where it landed.
                s.anchoredPosition = d.start + new Vector2(wobble * 0.5f, -fall);
                s.sizeDelta = new Vector2(d.diameter * 0.45f, fall + d.diameter * 0.3f);
                Color sc = Color.white;
                sc.a = c.a * 0.8f;
                d.streak.color = sc;
            }
        }
    }

    private void Spawn(float now)
    {
        Drop d = pool.Count > 0 ? pool.Pop() : Create();
        d.image.gameObject.SetActive(true);
        d.streak.gameObject.SetActive(false);
        Vector2 half = area.rect.size * 0.5f;
        d.start = new Vector2(Random.Range(-half.x * 0.92f, half.x * 0.92f), Random.Range(-half.y * 0.6f, half.y * 0.92f));
        d.image.rectTransform.anchoredPosition = d.start;
        d.diameter = Mathf.Lerp(size.x, size.y, Random.value * Random.value);
        d.born = now;
        d.life = Random.Range(drySeconds.x, drySeconds.y);
        d.slides = d.diameter > Mathf.Lerp(size.x, size.y, 0.35f) && Random.value < slideChance;
        d.slideAfter = Random.Range(0.6f, 2f);
        d.speed = Random.Range(60f, 140f);
        d.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-8f, 8f));
        live.Add(d);
    }

    private Drop Create()
    {
        Drop d = new Drop();
        d.streak = Make("Streak", trail, new Vector2(0.5f, 0f));
        d.image = Make("Drop", drop, new Vector2(0.5f, 0.5f));
        return d;
    }

    private Image Make(string name, Sprite sprite, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        return image;
    }

    private void Recycle(Drop d)
    {
        d.image.gameObject.SetActive(false);
        d.streak.gameObject.SetActive(false);
        pool.Push(d);
    }
}
