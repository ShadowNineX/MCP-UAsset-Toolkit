using UAssetAPI.UnrealTypes;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class EngineVersionService
{
    private static EngineVersion? _currentVersion;
    private static string? _versionString;

    public static EngineVersion? CurrentVersion => _currentVersion;
    public static string? VersionString => _versionString;

    public static bool IsVersionSet => _currentVersion.HasValue;

    public static void SetVersion(EngineVersion version, string versionString)
    {
        _currentVersion = version;
        _versionString = versionString;
    }

    public static void ClearVersion()
    {
        _currentVersion = null;
        _versionString = null;
    }

    public static EngineVersion GetVersionOrThrow()
    {
        if (!_currentVersion.HasValue)
        {
            throw new InvalidOperationException("Engine version not set. Please set the engine version first using set_engine_version tool.");
        }
        return _currentVersion.Value;
    }
}
