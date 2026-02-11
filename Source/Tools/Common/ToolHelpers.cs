using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Tools.Common;

public static class ToolHelpers
{
    public const string FileNotFoundError = "File not found or invalid path";
    public const string VersionNotSetError = "Engine version not set. Use set_engine_version tool first.";

    public static EngineVersion ParseEngineVersion(string version)
    {
        var normalized = version.ToLower().Trim();

        // Handle special case for 5.0EA
        if (normalized is "5.0ea" or "5_0ea")
            return EngineVersion.VER_UE5_0EA;

        // Parse version parts
        var parts = normalized.Replace("_", ".").Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            throw new ArgumentException($"Invalid version format: {version}. Expected format: '4.X' or '5.X'");
        }

        // Map to enum using reflection to build the enum name
        var enumName = $"VER_UE{major}_{minor}";
        if (Enum.TryParse<EngineVersion>(enumName, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Unsupported engine version: {version}. Supported: 4.0-4.27, 5.0EA, 5.0-5.7");
    }
}
