using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.VirtualFileSystem;
using MCP_UAsset_Toolkit.Source.Models;
using Newtonsoft.Json;

namespace MCP_UAsset_Toolkit.Source.Services;

/// <summary>Provides bounded, read-only CUE4Parse queries for an open archive session.</summary>
public static class ArchiveQueryService
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 1000;
    private const int DefaultMaxJsonCharacters = 1_000_000;
    private const int MaxJsonCharacters = 10_000_000;

    public static object ReadObject(string sessionId, string virtualPath, string objectName, int maxJsonCharacters = DefaultMaxJsonCharacters)
    {
        if (string.IsNullOrWhiteSpace(objectName)) throw new ArgumentException("Object name cannot be empty.", nameof(objectName));
        ValidateMaxJsonCharacters(maxJsonCharacters);
        var session = ArchiveService.Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            var file = RequirePackageFile(session, virtualPath);
            var package = session.Provider.LoadPackage(file.Path);
            if (package.GetExportOrNull(objectName, StringComparison.Ordinal) is null)
                throw new KeyNotFoundException($"Object '{objectName}' was not found in package '{file.Path}'.");
            var value = session.Provider.LoadPackageObject(file.Path, objectName)
                ?? throw new KeyNotFoundException($"Object '{objectName}' was not found in package '{file.Path}'.");
            var json = JsonConvert.SerializeObject(value, Formatting.None);
            if (json.Length > maxJsonCharacters)
            {
                return new
                {
                    virtualPath = file.Path,
                    objectName,
                    found = true,
                    name = value.Name,
                    className = value.ExportType,
                    jsonCharacters = json.Length,
                    maxJsonCharacters,
                    truncated = true,
                    continuationBlocked = true,
                    message = "The selected object exceeds the response limit. Increase maxJsonCharacters (up to 10000000) or extract the package for offline inspection."
                };
            }

            return new
            {
                virtualPath = file.Path,
                objectName,
                found = true,
                name = value.Name,
                className = value.ExportType,
                jsonCharacters = json.Length,
                maxJsonCharacters,
                truncated = false,
                json
            };
        }
    }

    public static object ListObjects(string sessionId, string virtualPath, int offset = 0, int limit = DefaultLimit)
    {
        ValidatePage(offset, limit);
        var session = ArchiveService.Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            var file = RequirePackageFile(session, virtualPath);
            var objects = session.Provider.LoadPackage(file.Path).GetExports()
                .Select(value => new ObjectMetadata(value.Name, value.ExportType))
                .ToArray();
            var page = objects.Skip(offset).Take(limit).Select(value => new { name = value.Name, className = value.ClassName }).ToArray();
            return new { virtualPath = file.Path, count = page.Length, totalCount = objects.Length, offset, limit, hasMore = offset + page.Length < objects.Length, objects = page };
        }
    }

    public static object GetFileInfo(string sessionId, string virtualPath)
    {
        var session = ArchiveService.Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            var file = RequireFile(session, virtualPath);
            var container = (file as VfsEntry)?.Vfs;
            return new
            {
                virtualPath = file.Path,
                size = file.Size,
                extension = file.Extension,
                isPackage = file.IsUePackage,
                isPackagePayload = file.IsUePackagePayload,
                isEncrypted = file.IsEncrypted,
                compressionMethod = file.CompressionMethod.ToString(),
                container = container is null ? null : new { name = container.Name, path = container.Path, mountPoint = container.MountPoint, fileCount = container.FileCount }
            };
        }
    }

    public static object ConfigureReadOptions(string sessionId, bool? readScriptData = null, bool? readShaderMaps = null, bool? readNaniteData = null, bool? skipReferencedTextures = null, bool? useLazyPackageSerialization = null)
    {
        var session = ArchiveService.Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            if (readScriptData.HasValue) session.Provider.ReadScriptData = readScriptData.Value;
            if (readShaderMaps.HasValue) session.Provider.ReadShaderMaps = readShaderMaps.Value;
            if (readNaniteData.HasValue) session.Provider.ReadNaniteData = readNaniteData.Value;
            if (skipReferencedTextures.HasValue) session.Provider.SkipReferencedTextures = skipReferencedTextures.Value;
            if (useLazyPackageSerialization.HasValue) session.Provider.UseLazyPackageSerialization = useLazyPackageSerialization.Value;
            return DescribeReadOptions(session);
        }
    }

    public static object GetReadOptions(string sessionId)
    {
        var session = ArchiveService.Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            return DescribeReadOptions(session);
        }
    }

    private static object DescribeReadOptions(ArchiveSession session) => new
    {
        sessionId = session.Id,
        readScriptData = session.Provider.ReadScriptData,
        readShaderMaps = session.Provider.ReadShaderMaps,
        readNaniteData = session.Provider.ReadNaniteData,
        skipReferencedTextures = session.Provider.SkipReferencedTextures,
        useLazyPackageSerialization = session.Provider.UseLazyPackageSerialization
    };

    private static GameFile RequireFile(ArchiveSession session, string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath)) throw new ArgumentException("Virtual path cannot be empty.", nameof(virtualPath));
        if (!session.Provider.TryGetGameFile(virtualPath, out var file)) throw new FileNotFoundException($"Virtual file not found: {virtualPath}");
        return file;
    }

    private static GameFile RequirePackageFile(ArchiveSession session, string virtualPath)
    {
        var file = RequireFile(session, virtualPath);
        if (!file.IsUePackage) throw new InvalidOperationException($"Virtual file '{file.Path}' is not an Unreal package.");
        return file;
    }

    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (limit is < 1 or > MaxLimit) throw new ArgumentOutOfRangeException(nameof(limit), $"limit must be between 1 and {MaxLimit}.");
    }

    private static void ValidateMaxJsonCharacters(int maxJsonCharacters)
    {
        if (maxJsonCharacters is < 1 or > MaxJsonCharacters) throw new ArgumentOutOfRangeException(nameof(maxJsonCharacters), $"maxJsonCharacters must be between 1 and {MaxJsonCharacters}.");
    }

    private sealed record ObjectMetadata(string Name, string ClassName);
}
