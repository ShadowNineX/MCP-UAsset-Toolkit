using MCP_UAsset_Toolkit.Source.Models;
using System.Collections.Concurrent;
using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class AssetSessionService
{
    private static readonly ConcurrentDictionary<string, AssetSession> Sessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock SessionLock = new();

    internal static T ExecuteOperation<T>(Func<T> operation)
    {
        lock (SessionLock) return operation();
    }

    public static AssetSession GetOrLoadSession(
        string filePath,
        EngineVersion? engineVersionOverride = null,
        string? mappingsPath = null,
        bool forceReload = false,
        bool discardChanges = false,
        AssetLoadOptions? options = null)
    {
        lock (SessionLock)
        {
            var fullPath = Path.GetFullPath(filePath);
            Sessions.TryGetValue(fullPath, out var existingSession);
            if (!forceReload && existingSession != null)
            {
                existingSession.LastAccessedAt = DateTime.UtcNow;
                return existingSession;
            }

            EnsureCanDiscard(existingSession, discardChanges);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Asset file not found: {filePath}", fullPath);
            }

            var engineVersion = engineVersionOverride
                ?? existingSession?.EngineVersion
                ?? (EngineVersionService.IsVersionSet ? EngineVersionService.GetVersionOrThrow() : EngineVersion.UNKNOWN);
            mappingsPath ??= existingSession?.MappingsPath;

            Usmap? mappings = null;
            if (!string.IsNullOrEmpty(mappingsPath))
            {
                mappings = MappingService.GetOrLoadMappings(mappingsPath);
            }
            else
            {
                mappings = MappingService.GetGlobalMappings();
            }

            var asset = LoadAsset(fullPath, engineVersion, mappings, existingSession?.Asset, options);
            var session = new AssetSession(fullPath, asset, engineVersion, mappingsPath);

            Sessions[fullPath] = session;
            return session;
        }
    }

    private static UAsset LoadAsset(string path, EngineVersion version, Usmap? mappings, UAsset? previous, AssetLoadOptions? options)
    {
        var flags = options?.CustomSerializationFlags == null ? previous?.CustomSerializationFlags ?? CustomSerializationFlags.None
            : Enum.Parse<CustomSerializationFlags>(options.CustomSerializationFlags, true);
        var game = options?.GameSpecificOverride == null ? previous?.GameSpecificOverride ?? GameSpecificOverride.None
            : Enum.Parse<GameSpecificOverride>(options.GameSpecificOverride, true);
        if (!Enum.IsDefined(game)) throw new ArgumentException("Unknown game-specific override.");
        var asset = new UAsset(version, mappings, flags, game) { FilePath = path };
        if (previous != null)
        {
            asset.MapStructTypeOverride = new(previous.MapStructTypeOverride);
            asset.ArrayStructTypeOverride = new(previous.ArrayStructTypeOverride);
        }
        ApplyStructOverrides(asset, options);
        using var reader = asset.PathToReader(path);
        asset.Read(reader);
        return asset;
    }

    private static void ApplyStructOverrides(UAsset asset, AssetLoadOptions? options)
    {
        foreach (var pair in options?.MapStructTypes ?? [])
        {
            if (pair.Value is not { Length: 2 }) throw new ArgumentException("Map struct overrides require [keyStructType, valueStructType].");
            asset.MapStructTypeOverride[pair.Key] = Tuple.Create(new FString(pair.Value[0]), new FString(pair.Value[1]));
        }
        foreach (var pair in options?.ArrayStructTypes ?? [])
            asset.ArrayStructTypeOverride[pair.Key] = new FString(pair.Value);
    }

    public static AssetSession RegisterSession(string filePath, UAsset asset, EngineVersion? engineVersion = null, string? mappingsPath = null, bool isDirty = false)
    {
        lock (SessionLock)
        {
            var fullPath = Path.GetFullPath(filePath);
            var version = engineVersion ?? (EngineVersionService.IsVersionSet ? EngineVersionService.GetVersionOrThrow() : EngineVersion.UNKNOWN);
            var session = new AssetSession(fullPath, asset, version, mappingsPath) { IsDirty = isDirty };
            Sessions[fullPath] = session;
            return session;
        }
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
        lock (SessionLock)
        {
            var session = GetSession(filePath);
            if (session != null)
            {
                session.IsDirty = true;
            }
        }
    }

    public static (bool success, string savedPath, long fileSize) SaveSession(string filePath, string? outputPath = null)
    {
        lock (SessionLock)
        {
            var session = GetOrLoadSession(filePath);
            var savePath = string.IsNullOrWhiteSpace(outputPath) ? session.FilePath : Path.GetFullPath(outputPath);

            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AssetNamePreparation.Prepare(session.Asset);
            UAssetContext.WithNameRepair(session.Asset, () =>
            {
                session.Asset.Write(savePath);
                return true;
            });

            if (string.Equals(savePath, session.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                session.IsDirty = false;
            }

            var fileInfo = new FileInfo(savePath);
            return (true, savePath, fileInfo.Length);
        }
    }

    public static AssetSession ReloadSession(string filePath, bool discardChanges = false) =>
        GetOrLoadSession(filePath, forceReload: true, discardChanges: discardChanges);

    public static AssetSession TransformSessionAsset(string filePath, Func<UAsset, UAsset> transform)
    {
        lock (SessionLock)
        {
            var session = GetOrLoadSession(filePath);
            var replacement = transform(session.Asset) ?? throw new InvalidOperationException("Asset transformation returned null.");
            session.Asset = replacement;
            session.IsDirty = true;
            return session;
        }
    }

    public static bool CloseSession(string filePath, bool discardChanges = false)
    {
        lock (SessionLock)
        {
            var fullPath = Path.GetFullPath(filePath);
            Sessions.TryGetValue(fullPath, out var session);
            EnsureCanDiscard(session, discardChanges);
            return Sessions.TryRemove(fullPath, out _);
        }
    }

    public static void ClearAllSessions(bool discardChanges = false)
    {
        lock (SessionLock)
        {
            // Validate every session before removing any, so failure is all-or-nothing.
            foreach (var session in Sessions.Values)
            {
                EnsureCanDiscard(session, discardChanges);
            }
            Sessions.Clear();
        }
    }

    private static void EnsureCanDiscard(AssetSession? session, bool discardChanges)
    {
        if (session?.IsDirty == true && !discardChanges)
        {
            throw new InvalidOperationException($"Session '{session.FilePath}' has unsaved changes. Save it first or set discardChanges to true.");
        }
    }

    public static IReadOnlyCollection<AssetSession> GetAllSessions() => Sessions.Values.ToList().AsReadOnly();
}
