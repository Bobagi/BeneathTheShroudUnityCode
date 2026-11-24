using System.Collections.Generic;
using UnityEngine;

// Placeholder icons for the backpack, drawn in code until real art exists: one shape per kind of item, in the item's
// colour (a cross for a medkit, rounds for ammo, cells for batteries, a ring for a filter, a torch, a revolver).
public static class ItemIcons
{
    private const int Size = 64;
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite For(InventoryItem item)
    {
        if (item.icon != null)
        {
            return item.icon;
        }
        return For(item.kind == InventoryItem.Kind.Tool ? item.tool.ToString() : item.kind.ToString(), item.colour);
    }

    public static Sprite For(string shape, Color colour)
    {
        string key = shape + ColorUtility.ToHtmlStringRGBA(colour);
        if (cache.TryGetValue(key, out Sprite sprite) && sprite != null)
        {
            return sprite;
        }
        sprite = Draw(shape, colour);
        cache[key] = sprite;
        return sprite;
    }

    private static Sprite Draw(string shape, Color colour)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] pixels = new Color32[Size * Size];
        Color white = new Color(0.96f, 0.95f, 0.9f, 1f);
        Color dark = new Color(0.1f, 0.09f, 0.08f, 1f);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // -1..1 across the icon, y up.
                float u = (x + 0.5f) / Size * 2f - 1f;
                float v = (y + 0.5f) / Size * 2f - 1f;
                Color c = Pixel(shape, u, v, colour, white, dark);
                pixels[y * Size + x] = c;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Color Pixel(string shape, float u, float v, Color colour, Color white, Color dark)
    {
        float edge = 2f / Size;
        switch (shape)
        {
            case "Heal":
                {
                    // A box with a cross.
                    float box = RoundedBox(u, v, 0.78f, 0.62f, 0.14f);
                    float cross = Mathf.Min(Box(u, v, 0.14f, 0.42f), Box(u, v, 0.42f, 0.14f));
                    Color c = Fill(box, colour, edge);
                    return Over(c, Fill(cross, white, edge));
                }
            case "Ammo":
                {
                    // Three rounds side by side: brass body, dark tip.
                    float best = 1f;
                    float tip = 1f;
                    for (int i = -1; i <= 1; i++)
                    {
                        float cx = i * 0.46f;
                        best = Mathf.Min(best, RoundedBox(u - cx, v + 0.15f, 0.16f, 0.5f, 0.1f));
                        tip = Mathf.Min(tip, Circle(u - cx, v - 0.48f, 0.16f));
                    }
                    Color c = Fill(best, colour, edge);
                    return Over(c, Fill(tip, dark, edge));
                }
            case "Battery":
                {
                    float best = 1f, cap = 1f;
                    for (int i = 0; i <= 1; i++)
                    {
                        float cx = i == 0 ? -0.3f : 0.3f;
                        best = Mathf.Min(best, RoundedBox(u - cx, v + 0.06f, 0.2f, 0.58f, 0.06f));
                        cap = Mathf.Min(cap, Box(u - cx, v - 0.7f, 0.1f, 0.08f));
                    }
                    Color c = Fill(best, colour, edge);
                    return Over(c, Fill(cap, white, edge));
                }
            case "Filter":
                {
                    float disc = Circle(u, v, 0.76f);
                    float hole = Circle(u, v, 0.3f);
                    float ring = Mathf.Max(disc, -hole);
                    float vents = 1f;
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        vents = Mathf.Min(vents, Circle(u - Mathf.Cos(a) * 0.53f, v - Mathf.Sin(a) * 0.53f, 0.07f));
                    }
                    Color c = Fill(ring, colour, edge);
                    return Over(c, Fill(vents, dark, edge));
                }
            case "Torch":
                {
                    // A dive torch: fat head on the left, grip to the right, a lens.
                    float head = RoundedBox(u + 0.42f, v, 0.3f, 0.42f, 0.12f);
                    float grip = RoundedBox(u - 0.25f, v, 0.46f, 0.2f, 0.1f);
                    float lens = Circle(u + 0.42f, v, 0.2f);
                    Color c = Fill(Mathf.Min(head, grip), colour, edge);
                    return Over(c, Fill(lens, new Color(1f, 0.95f, 0.75f, 1f), edge));
                }
            case "Revolver":
                {
                    float barrel = RoundedBox(u - 0.05f, v - 0.25f, 0.72f, 0.14f, 0.05f);
                    float cylinder = RoundedBox(u - 0.1f, v - 0.08f, 0.26f, 0.22f, 0.08f);
                    float grip = RoundedBox((u + 0.45f) * 0.8f + (v + 0.1f) * 0.6f, (v + 0.35f) * 0.8f - (u + 0.45f) * 0.6f, 0.14f, 0.36f, 0.06f);
                    return Fill(Mathf.Min(barrel, Mathf.Min(cylinder, grip)), colour, edge);
                }
            default:
                return Fill(RoundedBox(u, v, 0.7f, 0.7f, 0.18f), colour, edge);
        }
    }

    // Signed distances (negative inside), in icon units.
    private static float Box(float u, float v, float hx, float hy)
    {
        return Mathf.Max(Mathf.Abs(u) - hx, Mathf.Abs(v) - hy);
    }

    private static float RoundedBox(float u, float v, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(u) - hx + r;
        float qy = Mathf.Abs(v) - hy + r;
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static float Circle(float u, float v, float r)
    {
        return Mathf.Sqrt(u * u + v * v) - r;
    }

    private static Color Fill(float distance, Color colour, float edge)
    {
        float a = Mathf.Clamp01(0.5f - distance / (2f * edge));
        return new Color(colour.r, colour.g, colour.b, colour.a * a);
    }

    private static Color Over(Color under, Color over)
    {
        float a = over.a + under.a * (1f - over.a);
        if (a <= 0f)
        {
            return Color.clear;
        }
        return new Color((over.r * over.a + under.r * under.a * (1f - over.a)) / a, (over.g * over.a + under.g * under.a * (1f - over.a)) / a,
            (over.b * over.a + under.b * under.a * (1f - over.a)) / a, a);
    }
}
