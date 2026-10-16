using System.Collections.Concurrent;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;
using MCP_UAsset_Toolkit.Source.Models;
using Newtonsoft.Json;
using UAssetAPI;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class ArchiveService
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 1000;
    private const int DefaultMaxJsonCharacters = 1_000_000;
    private const int MaxJsonCharacters = 10_000_000;
    private const long DefaultMaxExtractBytes = 512L * 1024 * 1024;
    private static readonly ConcurrentDictionary<string, ArchiveSession> Sessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock SessionsLock = new();

    public static ArchiveSession Open(string directory, string game, bool recursive = false, string? mappingsPath = null)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Archive directory cannot be empty.", nameof(directory));
        var fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory)) throw new DirectoryNotFoundException($"Archive directory not found: {fullDirectory}");
        var parsedGame = ParseGame(game);
        var normalizedMappingsPath = string.IsNullOrWhiteSpace(mappingsPath) ? null : Path.GetFullPath(mappingsPath);
        if (normalizedMappingsPath is not null && !File.Exists(normalizedMappingsPath))
            throw new FileNotFoundException("Mappings file not found.", normalizedMappingsPath);

        var provider = new DefaultFileProvider(fullDirectory, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly,
            new VersionContainer(parsedGame), StringComparer.OrdinalIgnoreCase);
        try
        {
            if (normalizedMappingsPath is not null) provider.MappingsContainer = new FileUsmapTypeMappingsProvider(normalizedMappingsPath);
            provider.Initialize();
            provider.Mount();
            var id = Guid.NewGuid().ToString("N");
            var session = new ArchiveSession(id, fullDirectory, parsedGame, normalizedMappingsPath, provider);
            if (!Sessions.TryAdd(id, session)) throw new InvalidOperationException("Unable to allocate an archive session.");
            return session;
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    public static ArchiveSession Get(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !Sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Archive session '{sessionId}' was not found.");
        return session;
    }

    public static IReadOnlyCollection<ArchiveSession> GetAll() => Sessions.Values.OrderBy(s => s.CreatedAt).ToList().AsReadOnly();

    public static bool Close(string sessionId)
    {
        lock (SessionsLock)
        {
            if (!Sessions.TryRemove(sessionId, out var session)) return false;
            lock (session.SyncRoot) session.Dispose();
            return true;
        }
    }

    public static int SubmitKey(string sessionId, string keyGuid, string aesKey)
    {
        if (!Guid.TryParse(keyGuid, out _)) throw new ArgumentException($"Invalid encryption key GUID: '{keyGuid}'.", nameof(keyGuid));
        if (string.IsNullOrWhiteSpace(aesKey)) throw new ArgumentException("AES key cannot be empty.", nameof(aesKey));
        var session = Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            try
            {
                return session.Provider.SubmitKey(new FGuid(keyGuid), new FAesKey(aesKey.Trim()));
            }
            catch
            {
                // Some CUE4Parse validation exceptions include the supplied key. Never pass those through MCP output.
                throw new InvalidOperationException("AES key was rejected. Verify its format and that it matches the requested GUID.");
            }
        }
    }

    public static object Describe(ArchiveSession session)
    {
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            return new
            {
                sessionId = session.Id, session.Directory, game = session.Game.ToString(), session.MappingsPath,
                session.CreatedAt, session.LastAccessedAt, fileCount = session.Provider.Files.Count,
                looseFileCount = session.Provider.LooseFileCount,
                requiredKeyGuids = session.Provider.RequiredKeys.Select(x => x.ToString()).OrderBy(x => x).ToArray(),
                mountedArchives = session.Provider.MountedVfs.Select(DescribeArchive).ToArray(),
                unmountedArchives = session.Provider.UnloadedVfs.Select(DescribeArchive).ToArray()
            };
        }
    }

    public static object ListFiles(string sessionId, string? query, int offset = 0, int limit = DefaultLimit)
    {
        ValidatePage(offset, limit);
        var session = Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            var matches = session.Provider.Files.Keys
                .Where(path => string.IsNullOrWhiteSpace(query) || path.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            var page = matches.Skip(offset).Take(limit).Select(path =>
            {
                var file = session.Provider.Files[path];
                return new { virtualPath = path, extension = file.Extension, size = file.Size, isPackage = file.IsUePackage };
            }).ToArray();
            return new { count = page.Length, totalCount = matches.Length, offset, limit, hasMore = offset + page.Length < matches.Length, files = page };
        }
    }

    public static object ReadPackageExports(string sessionId, string virtualPath, int offset = 0, int limit = DefaultLimit, int maxJsonCharacters = DefaultMaxJsonCharacters)
    {
        ValidatePage(offset, limit);
        if (maxJsonCharacters is < 1 or > MaxJsonCharacters) throw new ArgumentOutOfRangeException(nameof(maxJsonCharacters), $"maxJsonCharacters must be between 1 and {MaxJsonCharacters}.");
        var session = Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            var package = session.Provider.LoadPackage(virtualPath);
            var exports = package.GetExports().ToArray();
            var page = new List<object>();
            var used = 0;
            var truncated = false;
            var firstOversizedExport = (object?)null;
            foreach (var (export, index) in exports.Skip(offset).Take(limit).Select((export, index) => (export, index: offset + index)))
            {
                var json = JsonConvert.SerializeObject(export, Formatting.None);
                if (used + json.Length > maxJsonCharacters)
                {
                    truncated = true;
                    firstOversizedExport = new { index, name = export.Name, className = export.ExportType, jsonCharacters = json.Length };
                    break;
                }
                page.Add(new { name = export.Name, className = export.ExportType, json });
                used += json.Length;
            }
            return new { virtualPath, count = page.Count, totalCount = exports.Length, offset, limit, hasMore = !truncated && offset + page.Count < exports.Length, truncated, continuationBlocked = truncated, maxJsonCharacters, oversizedExport = firstOversizedExport, message = truncated ? "An export exceeds the response limit. Increase maxJsonCharacters (up to 10000000) or extract the package for offline inspection." : null, exports = page };
        }
    }

    public static object Extract(string sessionId, string virtualPath, string outputDirectory, bool overwrite = false, long maxBytes = DefaultMaxExtractBytes)
    {
        if (maxBytes is < 1 or > DefaultMaxExtractBytes) throw new ArgumentOutOfRangeException(nameof(maxBytes), $"maxBytes must be between 1 and {DefaultMaxExtractBytes}.");
        if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));
        var root = Path.GetFullPath(outputDirectory);
        var session = Get(sessionId);
        lock (session.SyncRoot)
        {
            session.ThrowIfDisposed();
            session.LastAccessedAt = DateTime.UtcNow;
            if (!session.Provider.TryGetGameFile(virtualPath, out var file)) throw new FileNotFoundException($"Virtual file not found: {virtualPath}");
            if (file.Size > maxBytes) throw new InvalidOperationException($"Extraction exceeds the {maxBytes} byte limit.");
            var payloads = file.IsUePackage ? session.Provider.SavePackage(file) : new Dictionary<string, byte[]> { [file.Path] = session.Provider.SaveAsset(file) };
            if (payloads.Values.Sum(x => (long)x.Length) > maxBytes) throw new InvalidOperationException($"Extraction exceeds the {maxBytes} byte limit.");
            var targets = payloads.Select(x => new { Relative = SafeRelativePath(x.Key), Data = x.Value }).ToArray();
            var fullTargets = targets.Select(x => new { Path = EnsureUnderRoot(root, x.Relative), x.Data }).ToArray();
            foreach (var target in fullTargets) EnsureSafeOutputTarget(root, target.Path);
            if (!overwrite && fullTargets.Any(x => File.Exists(x.Path))) throw new IOException("At least one extraction target already exists; no files were written. Set overwrite=true to replace it.");
            Directory.CreateDirectory(root);
            foreach (var target in fullTargets)
            {
                var targetDirectory = Path.GetDirectoryName(target.Path)!;
                Directory.CreateDirectory(targetDirectory);
                EnsureSafeOutputTarget(root, target.Path);
                var temporaryPath = Path.Combine(targetDirectory, $".{Path.GetFileName(target.Path)}.{Guid.NewGuid():N}.tmp");
                try
                {
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Write(target.Data);
                    EnsureSafeOutputTarget(root, target.Path);
                    File.Move(temporaryPath, target.Path, overwrite);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            return new { virtualPath, outputDirectory = root, fileCount = fullTargets.Length, bytesWritten = fullTargets.Sum(x => (long)x.Data.Length), files = fullTargets.Select(x => Path.GetRelativePath(root, x.Path)).ToArray() };
        }
    }

    public static object CreatePakFromDirectory(string inputDirectory, string outputPakPath, string pakVersion = "V11", string mountPoint = "../../../", bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(inputDirectory)) throw new ArgumentException("Input directory cannot be empty.", nameof(inputDirectory));
        if (string.IsNullOrWhiteSpace(outputPakPath)) throw new ArgumentException("Output .pak path cannot be empty.", nameof(outputPakPath));
        var inputRoot = Path.GetFullPath(inputDirectory);
        var outputPath = Path.GetFullPath(outputPakPath);
        if (!Directory.Exists(inputRoot)) throw new DirectoryNotFoundException($"Input directory not found: {inputRoot}");
        if (!outputPath.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output path must end with .pak.", nameof(outputPakPath));
        if (!Enum.TryParse<PakVersion>(pakVersion, true, out var parsedVersion) || !Enum.IsDefined(parsedVersion)) throw new ArgumentException($"Unsupported PakVersion: '{pakVersion}'.", nameof(pakVersion));
        if (string.IsNullOrWhiteSpace(mountPoint)) throw new ArgumentException("Mount point cannot be empty.", nameof(mountPoint));

        var outputDirectory = Path.GetDirectoryName(outputPath)!;
        EnsureNoReparsePointAncestors(outputDirectory, outputPath);
        if (File.Exists(outputPath) && !overwrite) throw new IOException("Output .pak already exists. Set overwrite=true to replace it.");
        var files = EnumeratePakInputFiles(inputRoot, outputPath);
        var tempPath = Path.Combine(outputDirectory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(outputDirectory);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var builder = new PakBuilder())
            using (var writer = builder.Writer(stream, parsedVersion, mountPoint))
            {
                foreach (var file in files) writer.WriteFile(file.RelativePath.Replace('\\', '/'), File.ReadAllBytes(file.FullPath));
                writer.WriteIndex();
            }
            File.Move(tempPath, outputPath, overwrite);
            return new { inputDirectory = inputRoot, outputPakPath = outputPath, pakVersion = parsedVersion.ToString(), mountPoint, fileCount = files.Count, bytesWritten = new FileInfo(outputPath).Length };
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    public static string SafeRelativePath(string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath)) throw new ArgumentException("Virtual path cannot be empty.");
        var normalized = virtualPath.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.StartsWith("//") || Path.IsPathRooted(normalized) || normalized.Contains(':')) throw new ArgumentException("Virtual path must be relative.");
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(IsUnsafeWindowsPathSegment)) throw new ArgumentException("Virtual path contains an unsafe Windows path segment.");
        return Path.Combine(segments);
    }

    public static string EnsureUnderRoot(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Extraction path escapes the output directory.");
        return target;
    }

    private static bool IsUnsafeWindowsPathSegment(string segment)
    {
        if (segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.')) return true;
        var baseName = Path.GetFileNameWithoutExtension(segment);
        return baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) || baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) || baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (baseName.Length == 4 && (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && baseName[3] is >= '1' and <= '9');
    }

    private static void EnsureNoReparsePointAncestors(string root, string target)
    {
        var rootPath = Path.GetFullPath(root);
        for (var current = rootPath; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Output path contains a reparse-point directory: {current}");
            if (string.Equals(current, Path.GetPathRoot(current), StringComparison.OrdinalIgnoreCase)) break;
        }

        for (var current = Path.GetDirectoryName(target); !string.IsNullOrEmpty(current) && current.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase); current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Output path contains a reparse-point directory: {current}");
        }
    }

    private static void EnsureSafeOutputTarget(string root, string target)
    {
        EnsureNoReparsePointAncestors(root, target);
        if ((File.Exists(target) || Directory.Exists(target)) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Extraction target is a reparse point: {target}");
    }

    private static List<(string FullPath, string RelativePath)> EnumeratePakInputFiles(string inputRoot, string outputPath)
    {
        EnsureNoReparsePointAncestors(inputRoot, inputRoot);
        var files = new List<(string FullPath, string RelativePath)>();
        var pending = new Stack<string>();
        pending.Push(inputRoot);
        while (pending.TryPop(out var current))
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(current))
            {
                if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0) throw new IOException($"Input contains a reparse-point directory: {childDirectory}");
                pending.Push(childDirectory);
            }
            foreach (var file in Directory.EnumerateFiles(current))
            {
                if (string.Equals(Path.GetFullPath(file), outputPath, StringComparison.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException($"Input contains a reparse-point file: {file}");
                var relative = Path.GetRelativePath(inputRoot, file);
                SafeRelativePath(relative);
                files.Add((file, relative));
            }
        }
        return files.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static object DescribeArchive(IAesVfsReader reader) => new { name = reader.Name, path = reader.Path, length = reader.Length, isEncrypted = reader.IsEncrypted, encryptedFileCount = reader.EncryptedFileCount, requiredKeyGuid = reader.IsEncrypted ? reader.EncryptionKeyGuid.ToString() : null, fileCount = reader.Files.Count };
    private static EGame ParseGame(string game)
    {
        if (string.IsNullOrWhiteSpace(game)) throw new ArgumentException("Game enum is required, for example GAME_UE4_27.", nameof(game));
        var normalized = game.Trim();
        if (!normalized.StartsWith("GAME_", StringComparison.OrdinalIgnoreCase)) normalized = "GAME_" + normalized.Replace('.', '_').Replace('-', '_');
        if (!Enum.TryParse<EGame>(normalized, true, out var result) || !Enum.IsDefined(result)) throw new ArgumentException($"Unsupported CUE4Parse game enum: '{game}'.", nameof(game));
        return result;
    }
    private static void ValidatePage(int offset, int limit)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (limit is < 1 or > MaxLimit) throw new ArgumentOutOfRangeException(nameof(limit), $"limit must be between 1 and {MaxLimit}.");
    }
}
