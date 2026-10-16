using System.Collections.Concurrent;
using UAssetAPI.Unversioned;

namespace MCP_UAsset_Toolkit.Source.Services;

public static class MappingService
{
    private static readonly ConcurrentDictionary<string, Usmap> LoadedMappings = new(StringComparer.OrdinalIgnoreCase);
    private static string? _globalMappingsPath;

    public static string? GlobalMappingsPath => _globalMappingsPath;

    public static void SetGlobalMappings(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Mappings file not found at '{path}'");
        }

        var fullPath = Path.GetFullPath(path);
        GetOrLoadMappings(fullPath);
        _globalMappingsPath = fullPath;
    }

    public static Usmap? GetGlobalMappings()
    {
        if (_globalMappingsPath != null && LoadedMappings.TryGetValue(_globalMappingsPath, out var mappings))
        {
            return mappings;
        }
        return null;
    }

    public static Usmap GetOrLoadMappings(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return LoadedMappings.GetOrAdd(fullPath, p =>
        {
            if (!File.Exists(p))
            {
                throw new FileNotFoundException($"Mappings file not found at '{p}'");
            }
            return new Usmap(p);
        });
    }

    public static void ClearMappings()
    {
        LoadedMappings.Clear();
        _globalMappingsPath = null;
    }

    public static IReadOnlyDictionary<string, Usmap> GetLoadedMappings() => LoadedMappings;
}
