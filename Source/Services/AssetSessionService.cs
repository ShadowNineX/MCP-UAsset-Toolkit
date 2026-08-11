using System.Collections.Concurrent;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace MCP_UAsset_Toolkit.Source.Services;

public class AssetSession
{
    public string FilePath { get; set; }
    public UAsset Asset { get; set; }
    public EngineVersion EngineVersion { get; set; }
    public bool IsDirty { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public string? MappingsPath { get; set; }

    public AssetSession(string filePath, UAsset asset, EngineVersion engineVersion, string? mappingsPath = null)
    {
        FilePath = filePath;
        Asset = asset;
        EngineVersion = engineVersion;
        MappingsPath = mappingsPath;
    }
}

public static class AssetSessionService
{
    private static readonly ConcurrentDictionary<string, AssetSession> Sessions = new(StringComparer.OrdinalIgnoreCase);

    public static AssetSession GetOrLoadSession(
        string filePath,
        EngineVersion? engineVersionOverride = null,
        string? mappingsPath = null,
        bool forceReload = false)
    {
        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Asset file not found: {filePath}", fullPath);
        }

        if (!forceReload && Sessions.TryGetValue(fullPath, out var existingSession))
        {
            existingSession.LastAccessedAt = DateTime.UtcNow;
            return existingSession;
        }

        var engineVersion = engineVersionOverride
            ?? (EngineVersionService.IsVersionSet ? EngineVersionService.GetVersionOrThrow() : EngineVersion.UNKNOWN);

        Usmap? mappings = null;
        if (!string.IsNullOrEmpty(mappingsPath))
        {
            mappings = MappingService.GetOrLoadMappings(mappingsPath);
        }
        else
        {
            mappings = MappingService.GetGlobalMappings();
        }

        var asset = new UAsset(fullPath, engineVersion, mappings);
        var session = new AssetSession(fullPath, asset, engineVersion, mappingsPath);

        Sessions[fullPath] = session;
        return session;
    }

    public static AssetSession RegisterSession(string filePath, UAsset asset, EngineVersion? engineVersion = null, string? mappingsPath = null)
    {
        var fullPath = Path.GetFullPath(filePath);
        var version = engineVersion ?? (EngineVersionService.IsVersionSet ? EngineVersionService.GetVersionOrThrow() : EngineVersion.UNKNOWN);
        var session = new AssetSession(fullPath, asset, version, mappingsPath);
        Sessions[fullPath] = session;
        return session;
    }

    public static AssetSession? GetSession(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (Sessions.TryGetValue(fullPath, out var session))
        {
            session.LastAccessedAt = DateTime.UtcNow;
            return session;
        }
        return null;
    }

    public static void MarkDirty(string filePath)
    {
        var session = GetSession(filePath);
        if (session != null)
        {
            session.IsDirty = true;
        }
    }

    public static (bool success, string savedPath, long fileSize) SaveSession(string filePath, string? outputPath = null)
    {
        var session = GetOrLoadSession(filePath);
        var savePath = string.IsNullOrWhiteSpace(outputPath) ? session.FilePath : Path.GetFullPath(outputPath);

        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        session.Asset.Write(savePath);

        if (string.Equals(savePath, session.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            session.IsDirty = false;
        }

        var fileInfo = new FileInfo(savePath);
        return (true, savePath, fileInfo.Length);
    }

    public static bool CloseSession(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        return Sessions.TryRemove(fullPath, out _);
    }

    public static void ClearAllSessions()
    {
        Sessions.Clear();
    }

    public static IReadOnlyCollection<AssetSession> GetAllSessions() => Sessions.Values.ToList().AsReadOnly();
}
