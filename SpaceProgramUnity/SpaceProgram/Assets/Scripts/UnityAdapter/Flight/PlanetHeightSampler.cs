using System;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Fast bilinear height sampling from an R16 RSS heightmap without GetPixelBilinear.
/// Copies pixel data so sampling stays valid after the source <see cref="Texture2D"/> is destroyed.
/// </summary>
sealed class PlanetHeightSampler : IDisposable
{
    NativeArray<ushort> heightData;
    readonly int width;
    readonly int height;
    readonly bool hasHeightData;
    bool disposed;

    public PlanetHeightSampler(Texture2D heightmap)
    {
        if (heightmap != null &&
            heightmap.format == TextureFormat.R16 &&
            heightmap.isReadable)
        {
            NativeArray<ushort> raw = heightmap.GetRawTextureData<ushort>();
            width = heightmap.width;
            height = heightmap.height;

            if (width > 1 && height > 1 && raw.Length >= width * height)
            {
                heightData = new NativeArray<ushort>(raw.Length, Allocator.Persistent);
                heightData.CopyFrom(raw);
                hasHeightData = true;
                return;
            }
        }

        heightData = default;
        width = 0;
        height = 0;
        hasHeightData = false;
    }

    public bool IsValid => hasHeightData && !disposed && heightData.IsCreated;

    public float SampleHeightOffsetMeters(Vector2 uv)
    {
        if (!IsValid)
        {
            return 0f;
        }

        float u = Mathf.Repeat(uv.x, 1f);
        float v = Mathf.Clamp01(uv.y);
        float sampleX = u * (width - 1);
        float sampleY = v * (height - 1);
        int x0 = Mathf.FloorToInt(sampleX);
        int y0 = Mathf.FloorToInt(sampleY);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = sampleX - x0;
        float ty = sampleY - y0;

        float height00 = SampleNormalized(x0, y0);
        float height10 = SampleNormalized(x1, y0);
        float height01 = SampleNormalized(x0, y1);
        float height11 = SampleNormalized(x1, y1);
        float heightX0 = Mathf.Lerp(height00, height10, tx);
        float heightX1 = Mathf.Lerp(height01, height11, tx);
        float normalizedHeight = Mathf.Lerp(heightX0, heightX1, ty);
        return (float)PlanetSurfaceMaps.SampleEarthHeightMeters(normalizedHeight);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (heightData.IsCreated)
        {
            heightData.Dispose();
        }
    }

    float SampleNormalized(int x, int y)
    {
        return heightData[y * width + x] / 65535f;
    }
}
