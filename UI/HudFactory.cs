using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small helpers to assemble uGUI elements from code with a consistent look.
public static class HudFactory
{
    public static readonly Color Ink = new Color(0.92f, 0.88f, 0.78f, 1f);
    public static readonly Color MutedInk = new Color(0.7f, 0.65f, 0.55f, 1f);
    public static readonly Color Accent = new Color(0.86f, 0.62f, 0.3f, 1f);

    public static TMP_FontAsset Font { get; set; }

    public static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image Image(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Label(string name, Transform parent, float size, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = Rect(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null)
        {
            label.font = Font;
        }

        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    // Soft dark drop shadow so light UI stays legible over bright flares and fires.
    public static void Shadow(GameObject target)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.2f, -1.2f);
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
