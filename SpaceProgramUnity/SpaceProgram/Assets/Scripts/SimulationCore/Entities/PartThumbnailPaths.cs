using System.IO;

/// <summary>
/// KSP-style part icon paths: <c>Assets/Models/Parts/Icons/@thumbs/{partId}_icon.png</c>.
/// </summary>
public static class PartThumbnailPaths
{
    public const string IconsAssetRoot = "Assets/Models/Parts/Icons/@thumbs";
    public const string IconFileSuffix = "_icon.png";

    public static string GetIconAssetPath(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
        {
            return string.Empty;
        }

        return $"{IconsAssetRoot}/{partId}{IconFileSuffix}";
    }

    public static string GetIconFileName(string partId)
    {
        return string.IsNullOrWhiteSpace(partId) ?
            string.Empty :
            partId + IconFileSuffix;
    }
}
