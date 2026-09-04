using UnityEngine;

/// <summary>
/// Loads canonical equirectangular NASA PNG/JPG as-is (no runtime image flips).
/// earth_geographic.png is baked to canonical geographic UV; sample at geographicU/V directly.
/// </summary>
public static class PlanetEquirectangularTextureLoader
{
    public static Texture2D LoadRgbFromImageBytes(byte[] imageBytes, string textureName)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return null;
        }

        var loaded = new Texture2D(2, 2, TextureFormat.RGB24, false);

        if (!loaded.LoadImage(imageBytes))
        {
            Object.Destroy(loaded);
            return null;
        }

        loaded.name = textureName;
        loaded.Apply(true);
        return loaded;
    }

    public static Color SampleColorAtGeographic(
        Texture2D texture,
        double latitudeDegrees,
        double longitudeDegrees
    )
    {
        double latitudeRadians = latitudeDegrees * (System.Math.PI / 180.0);
        double longitudeRadians = longitudeDegrees * (System.Math.PI / 180.0);
        var direction = PlanetGeographicCoords.DirectionFromLatitudeLongitude(
            latitudeRadians,
            longitudeRadians
        );
        PlanetGeographicCoords.DirectionToGeographicUv(
            direction,
            out double geographicU,
            out double geographicV
        );

        return texture.GetPixelBilinear(
            (float)geographicU,
            (float)geographicV
        );
    }
}
