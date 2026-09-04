using System;
using System.IO;
using UnityEngine;

static class PlanetTextureLoader
{
    public static Texture2D LoadEarthColorMap()
    {
        if (!PlanetSurfaceMaps.TryResolveEarthColorPath(out string path))
        {
            return null;
        }

        return LoadColorTexture(path);
    }

    public static Texture2D LoadEarthNormalMap()
    {
        if (!PlanetSurfaceMaps.TryResolveEarthNormalPath(out string path))
        {
            return null;
        }

        return LoadNormalTexture(path);
    }

    public static Texture2D LoadEarthHeightMapForDisplacement()
    {
        if (!PlanetSurfaceMaps.TryResolveEarthHeightPath(out string path))
        {
            return null;
        }

        if (!DdsTextureLoader.CanLoad(path))
        {
            return LoadColorTexture(path);
        }

        try
        {
            return DdsTextureLoader.LoadUInt16GrayscaleHeightMap(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "PlanetTextureLoader: RSS height DDS load failed for " +
                path +
                ": " +
                exception.Message
            );
            return null;
        }
    }

    static Texture2D LoadColorTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (DdsTextureLoader.CanLoad(path))
        {
            try
            {
                return DdsTextureLoader.LoadColorMap(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "PlanetTextureLoader: color DDS load failed for " +
                    path +
                    ": " +
                    exception.Message
                );
            }
        }

        return LoadImageTexture(path, linear: false);
    }

    static Texture2D LoadNormalTexture(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (DdsTextureLoader.CanLoad(path))
        {
            try
            {
                return DdsTextureLoader.LoadNormalMap(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "PlanetTextureLoader: normal DDS load failed for " +
                    path +
                    ": " +
                    exception.Message
                );
            }
        }

        return LoadImageTexture(path, linear: true);
    }

    static Texture2D LoadImageTexture(string path, bool linear)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;

        if (!texture.LoadImage(bytes, markNonReadable: false))
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogWarning("PlanetTextureLoader: failed to decode " + path);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(path);
        texture.Apply(false, false);
        return texture;
    }
}
