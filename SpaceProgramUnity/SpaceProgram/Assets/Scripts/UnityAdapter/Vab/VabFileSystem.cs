using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Reads and writes VAB vessel blueprints to <c>Ships/</c> next to the game install
/// (folder that contains <c>*_Data</c> / the Unity project root in the editor).
/// </summary>
public static class VabFileSystem
{
    const string ShipsFolderName = "Ships";
    const string ThumbnailsFolderName = "@thumbs";
    const string BlueprintExtension = ".json";
    const string ThumbnailExtension = ".png";

    public static string GameDirectory
    {
        get
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }

    public static string ShipsDirectory
    {
        get
        {
            return Path.Combine(GameDirectory, ShipsFolderName);
        }
    }

    static IEnumerable<string> ShipSearchDirectories()
    {
        yield return ShipsDirectory;
        yield return PlayerContentPaths.ShipsStreamingDirectory();
    }

    public static string GetShipFilePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Ship file name is required.", nameof(fileName));
        }

        return Path.Combine(ShipsDirectory, fileName.Trim());
    }

    public static void SaveVehicle(VesselBlueprint blueprint)
    {
        if (blueprint == null)
        {
            throw new ArgumentNullException(nameof(blueprint));
        }

        if (string.IsNullOrWhiteSpace(blueprint.vesselName))
        {
            throw new ArgumentException("Vessel name is required.", nameof(blueprint));
        }

        string shipsDirectory = ShipsDirectory;
        Directory.CreateDirectory(shipsDirectory);

        string safeFileName = SanitizeFileName(blueprint.vesselName);
        string filePath = Path.Combine(shipsDirectory, safeFileName + BlueprintExtension);
        string json = VesselBlueprint.SerializeToJson(blueprint, prettyPrint: true);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    public static VesselBlueprint LoadVehicle(string vesselName)
    {
        if (string.IsNullOrWhiteSpace(vesselName))
        {
            throw new ArgumentException("Vessel name is required.", nameof(vesselName));
        }

        string safeFileName = SanitizeFileName(vesselName);
        string fileName = safeFileName + BlueprintExtension;

        foreach (string directory in ShipSearchDirectories())
        {
            string filePath = Path.Combine(directory, fileName);

            if (!File.Exists(filePath))
            {
                continue;
            }

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            return VesselBlueprint.DeserializeFromJson(json);
        }

        throw new FileNotFoundException(
            "Vessel blueprint file was not found.",
            Path.Combine(ShipsDirectory, fileName)
        );
    }

    public static List<string> GetSavedVehicleNames()
    {
        List<string> names = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string shipsDirectory in ShipSearchDirectories())
        {
            if (!Directory.Exists(shipsDirectory))
            {
                continue;
            }

            string[] files = Directory.GetFiles(shipsDirectory, "*" + BlueprintExtension);

            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);

                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    public static string GetBlueprintPath(string vesselName)
    {
        string safeFileName = SanitizeFileName(vesselName);
        return Path.Combine(ShipsDirectory, safeFileName + BlueprintExtension);
    }

    public static string ThumbnailsDirectory
    {
        get
        {
            return Path.Combine(ShipsDirectory, ThumbnailsFolderName);
        }
    }

    public static string GetThumbnailPath(string vesselName)
    {
        string safeFileName = SanitizeFileName(vesselName);
        return Path.Combine(ThumbnailsDirectory, safeFileName + ThumbnailExtension);
    }

    public static void SaveThumbnail(string vesselName, byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0)
        {
            return;
        }

        string thumbnailsDirectory = ThumbnailsDirectory;
        Directory.CreateDirectory(thumbnailsDirectory);
        string safeFileName = SanitizeFileName(vesselName);
        string filePath = Path.Combine(thumbnailsDirectory, safeFileName + ThumbnailExtension);
        File.WriteAllBytes(filePath, pngBytes);
    }

    public static bool TryLoadThumbnailBytes(string vesselName, out byte[] pngBytes)
    {
        pngBytes = null;
        string filePath = GetThumbnailPath(vesselName);

        if (!File.Exists(filePath))
        {
            return false;
        }

        pngBytes = File.ReadAllBytes(filePath);
        return pngBytes.Length > 0;
    }

    public static bool VehicleExists(string vesselName)
    {
        if (string.IsNullOrWhiteSpace(vesselName))
        {
            return false;
        }

        return File.Exists(GetBlueprintPath(vesselName)) ||
            File.Exists(Path.Combine(PlayerContentPaths.ShipsStreamingDirectory(), SanitizeFileName(vesselName) + BlueprintExtension));
    }

    public static void DeleteVehicle(string vesselName)
    {
        if (string.IsNullOrWhiteSpace(vesselName))
        {
            return;
        }

        string blueprintPath = GetBlueprintPath(vesselName);

        if (File.Exists(blueprintPath))
        {
            File.Delete(blueprintPath);
        }

        string thumbnailPath = GetThumbnailPath(vesselName);

        if (File.Exists(thumbnailPath))
        {
            File.Delete(thumbnailPath);
        }
    }

    static string SanitizeFileName(string vesselName)
    {
        string trimmed = vesselName.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(trimmed.Length);

        for (int i = 0; i < trimmed.Length; i++)
        {
            char current = trimmed[i];
            bool isInvalid = false;

            for (int j = 0; j < invalidChars.Length; j++)
            {
                if (current == invalidChars[j])
                {
                    isInvalid = true;
                    break;
                }
            }

            builder.Append(isInvalid ? '_' : current);
        }

        string sanitized = builder.ToString();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException(
                "Vessel name does not contain valid file name characters.",
                nameof(vesselName)
            );
        }

        return sanitized;
    }
}
