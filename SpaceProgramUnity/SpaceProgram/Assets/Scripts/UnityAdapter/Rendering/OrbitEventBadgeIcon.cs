using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSP-style Ap/Pe house icon: teal shield pointing down onto the orbit line.
/// </summary>
public static class OrbitEventBadgeIcon
{
    const int TextureSize = 128;
    const int IconVersion = 2;

    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    static readonly Vector2[] HousePolygon =
    {
        new Vector2(0.50f, 0.04f),
        new Vector2(0.20f, 0.30f),
        new Vector2(0.14f, 0.94f),
        new Vector2(0.86f, 0.94f),
        new Vector2(0.80f, 0.30f),
    };

    static readonly string[] GlyphA =
    {
        "01110",
        "10001",
        "10001",
        "11111",
        "10001",
        "10001",
        "10001",
    };

    static readonly string[] GlyphP =
    {
        "11110",
        "10001",
        "10001",
        "11110",
        "10000",
        "10000",
        "10000",
    };

    static readonly string[] GlyphE =
    {
        "00000",
        "01111",
        "10000",
        "11110",
        "10000",
        "01111",
        "00000",
    };

    static readonly string[] GlyphSmallP =
    {
        "00000",
        "01111",
        "10001",
        "10001",
        "01111",
        "10000",
        "10000",
    };

    public static Texture2D Get(string label, Color fillColor)
    {
        string key = IconVersion + label + fillColor.r.ToString("0.00") + fillColor.g.ToString("0.00") +
            fillColor.b.ToString("0.00");

        if (cache.TryGetValue(key, out Texture2D cached) && cached != null)
        {
            return cached;
        }

        Texture2D texture = Bake(label, fillColor);
        cache[key] = texture;
        return texture;
    }

    public static Texture2D Bake(string label, Color fillColor)
    {
        Color outline = new Color(
            fillColor.r * 0.22f,
            fillColor.g * 0.22f,
            fillColor.b * 0.22f,
            1.0f
        );
        Color textColor = new Color(
            Mathf.Min(1.0f, fillColor.r * 0.45f + 0.70f),
            Mathf.Min(1.0f, fillColor.g * 0.25f + 0.82f),
            Mathf.Min(1.0f, fillColor.b * 0.25f + 0.82f),
            1.0f
        );

        Color32[] pixels = new Color32[TextureSize * TextureSize];
        float inset = 0.010f;

        for (int y = 0; y < TextureSize; y++)
        {
            float v = (y + 0.5f) / TextureSize;

            for (int x = 0; x < TextureSize; x++)
            {
                float u = (x + 0.5f) / TextureSize;
                float coverage = SampleHouseCoverage(u, v);
                float inner = SampleHouseCoverage(u, v, inset);

                Color32 pixel;
                if (inner > 0.5f)
                {
                    pixel = fillColor;
                }
                else if (coverage > 0.5f)
                {
                    pixel = outline;
                }
                else
                {
                    pixel = new Color32(0, 0, 0, 0);
                }

                pixels[y * TextureSize + x] = pixel;
            }
        }

        DrawLabel(pixels, label, textColor);

        Texture2D texture = new Texture2D(
            TextureSize,
            TextureSize,
            TextureFormat.RGBA32,
            false
        )
        {
            name = "OrbitEventBadge_" + label,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    static float SampleHouseCoverage(float u, float v, float inset = 0.0f)
    {
        if (inset <= 0.0f)
        {
            return PointInPolygon(u, v, HousePolygon) ? 1.0f : 0.0f;
        }

        Vector2 center = new Vector2(0.50f, 0.52f);
        Vector2[] shrunk = new Vector2[HousePolygon.Length];

        for (int i = 0; i < HousePolygon.Length; i++)
        {
            shrunk[i] = Vector2.Lerp(HousePolygon[i], center, inset / 0.50f);
        }

        return PointInPolygon(u, v, shrunk) ? 1.0f : 0.0f;
    }

    static bool PointInPolygon(float x, float y, Vector2[] polygon)
    {
        bool inside = false;
        int j = polygon.Length - 1;

        for (int i = 0; i < polygon.Length; j = i++)
        {
            float yi = polygon[i].y;
            float yj = polygon[j].y;
            bool crosses = (yi > y) != (yj > y);

            if (!crosses)
            {
                continue;
            }

            float xi = polygon[i].x;
            float xj = polygon[j].x;
            float atX = (xj - xi) * (y - yi) / (yj - yi) + xi;

            if (x < atX)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    static void DrawLabel(Color32[] pixels, string label, Color textColor)
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        string[] left = GlyphFor(label[0]);
        string[] right = label.Length > 1 ? GlyphFor(label[1]) : null;
        const int scale = 5;
        int glyphWidth = 5 * scale;
        int gap = 4;
        int totalWidth = right == null ? glyphWidth : glyphWidth * 2 + gap;
        int originX = (TextureSize - totalWidth) / 2;
        int originY = 66;

        BlitGlyph(pixels, left, originX, originY, scale, textColor);

        if (right != null)
        {
            BlitGlyph(
                pixels,
                right,
                originX + glyphWidth + gap,
                originY,
                scale,
                textColor
            );
        }
    }

    static string[] GlyphFor(char character)
    {
        switch (character)
        {
            case 'A':
            case 'a':
                return GlyphA;
            case 'P':
                return GlyphP;
            case 'p':
                return GlyphSmallP;
            case 'E':
            case 'e':
                return GlyphE;
            default:
                return GlyphA;
        }
    }

    static void BlitGlyph(
        Color32[] pixels,
        string[] glyph,
        int originX,
        int originY,
        int scale,
        Color textColor
    )
    {
        Color32 packed = textColor;

        for (int row = 0; row < glyph.Length; row++)
        {
            string line = glyph[row];
            int y = originY + (glyph.Length - 1 - row) * scale;

            for (int column = 0; column < line.Length; column++)
            {
                if (line[column] != '1')
                {
                    continue;
                }

                int x = originX + column * scale;

                for (int dy = 0; dy < scale; dy++)
                {
                    for (int dx = 0; dx < scale; dx++)
                    {
                        int px = x + dx;
                        int py = y + dy;

                        if (px < 0 || py < 0 || px >= TextureSize || py >= TextureSize)
                        {
                            continue;
                        }

                        int index = py * TextureSize + px;

                        if (pixels[index].a < 16)
                        {
                            continue;
                        }

                        pixels[index] = packed;
                    }
                }
            }
        }
    }
}
