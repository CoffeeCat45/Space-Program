using UnityEngine;

/// <summary>
/// Shader references kept in Resources so the player build includes
/// <c>SpaceProgram/*</c> even if GraphicsSettings Always Included is empty.
/// </summary>
[CreateAssetMenu(menuName = "Space Program/Player Included Shaders")]
public sealed class PlayerIncludedShaders : ScriptableObject
{
    public const string ResourcesAssetName = "PlayerIncludedShaders";
    public const string AssetPath = "Assets/Resources/PlayerIncludedShaders.asset";

    public Shader[] shaders;
}
