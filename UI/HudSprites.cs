using UnityEngine;

// Anti-aliased shapes generated once at runtime, so the HUD needs no texture assets.
public static class HudSprites
{
    private static Sprite square;
    private static Sprite circle;
    private static Sprite ring;
    private static Sprite softGlow;
    private static Sprite verticalFade;

    public static Sprite Square => square != null ? square : square = Build(8, (x, y) => 1f);

    public static Sprite Circle => circle != null ? circle : circle = Build(64, (x, y) => Edge(Radius(x, y), 0.46f));

    public static Sprite Ring => ring != null
        ? ring
        : ring = Build(64, (x, y) =>
        {
            float r = Radius(x, y);
            return Mathf.Min(Edge(r, 0.46f), 1f - Edge(r, 0.36f));
        });

    public static Sprite SoftGlow => softGlow != null
        ? softGlow
        : softGlow = Build(64, (x, y) =>
        {
            float t = Mathf.Clamp01(1f - Radius(x, y) / 0.5f);
            return t * t;
        });

    // Transparent at the top, opaque at the bottom: a backing wash for the hotbar.
    public static Sprite VerticalFade => verticalFade != null
        ? verticalFade
        : verticalFade = Build(32, (x, y) => Mathf.SmoothStep(1f, 0f, y));

    // Clear in the middle, dense at the rim: stretched over the screen it frames the view (damage flash).
    private static Sprite edgeGlow;

    public static Sprite EdgeGlow => edgeGlow != null
        ? edgeGlow
        : edgeGlow = Build(128, (x, y) =>
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.7f, Radius(x, y)));
            return t * t;
        });

    // Rounded rectangle for pills and keycaps, 9-sliced so it stretches to any size without blurring
    // the corners.
    private static Sprite roundedPanel;

    public static Sprite RoundedPanel
    {
        get
        {
            if (roundedPanel != null)
            {
                return roundedPanel;
            }

            const int size = 32;
            const float corner = 10f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(corner - (x + 0.5f), (x + 0.5f) - (size - corner), 0f);
                    float dy = Mathf.Max(corner - (y + 0.5f), (y + 0.5f) - (size - corner), 0f);
                    float a = Mathf.Clamp01(corner - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            roundedPanel = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(corner, corner, corner, corner));
            return roundedPanel;
        }
    }

    private static float Radius(float x, float y)
    {
        float dx = x - 0.5f;
        float dy = y - 0.5f;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static float Edge(float radius, float limit)
    {
        return Mathf.Clamp01((limit - radius) * 64f);
    }

    private static Sprite Build(int size, System.Func<float, float, float> alpha)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float a = alpha((x + 0.5f) / size, (y + 0.5f) / size);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
