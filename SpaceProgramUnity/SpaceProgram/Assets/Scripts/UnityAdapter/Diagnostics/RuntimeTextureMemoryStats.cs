using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Samples GPU memory used by loaded <see cref="Texture"/> assets at runtime.
/// Uses <see cref="Profiler.GetRuntimeMemorySizeLong"/> per texture (same basis as Unity Profiler → Memory).
/// </summary>
public static class RuntimeTextureMemoryStats
{
    public struct Snapshot
    {
        public long TotalBytes;
        public int TextureCount;
        public int Texture2DCount;
        public long Texture2DBytes;
        public int RenderTextureCount;
        public long RenderTextureBytes;
    }

    const float DefaultSampleIntervalSeconds = 0.5f;

    static Snapshot _cachedSnapshot;
    static float _lastSampleUnscaledTime = -999f;

    public static Snapshot GetCachedSnapshot(float sampleIntervalSeconds = DefaultSampleIntervalSeconds)
    {
        if (Time.unscaledTime - _lastSampleUnscaledTime >= sampleIntervalSeconds)
        {
            _cachedSnapshot = SampleNow();
            _lastSampleUnscaledTime = Time.unscaledTime;
        }

        return _cachedSnapshot;
    }

    public static Snapshot SampleNow()
    {
        Snapshot snapshot = default;
        Texture[] textures = Resources.FindObjectsOfTypeAll<Texture>();

        for (int i = 0; i < textures.Length; i++)
        {
            Texture texture = textures[i];
            if (texture == null)
            {
                continue;
            }

            long bytes = Profiler.GetRuntimeMemorySizeLong(texture);
            snapshot.TotalBytes += bytes;
            snapshot.TextureCount++;

            if (texture is RenderTexture)
            {
                snapshot.RenderTextureCount++;
                snapshot.RenderTextureBytes += bytes;
            }
            else if (texture is Texture2D)
            {
                snapshot.Texture2DCount++;
                snapshot.Texture2DBytes += bytes;
            }
        }

        return snapshot;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024L)
        {
            return bytes + " B";
        }

        if (bytes < 1024L * 1024L)
        {
            return (bytes / 1024f).ToString("F1") + " KB";
        }

        if (bytes < 1024L * 1024L * 1024L)
        {
            return (bytes / (1024f * 1024f)).ToString("F1") + " MB";
        }

        return (bytes / (1024f * 1024f * 1024f)).ToString("F2") + " GB";
    }

    public static string FormatHudLine(Snapshot snapshot)
    {
        return "Textures GPU: " + FormatBytes(snapshot.TotalBytes) +
            " (" + snapshot.TextureCount + " tex, " +
            snapshot.Texture2DCount + "x2D=" + FormatBytes(snapshot.Texture2DBytes) +
            ", " + snapshot.RenderTextureCount + " RT=" + FormatBytes(snapshot.RenderTextureBytes) + ")";
    }
}
