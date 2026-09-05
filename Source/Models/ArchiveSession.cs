using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;

namespace MCP_UAsset_Toolkit.Source.Models;

/// <summary>Owns one CUE4Parse provider and serializes access to its mutable mount state.</summary>
public sealed class ArchiveSession : IDisposable
{
    public ArchiveSession(string id, string directory, EGame game, string? mappingsPath, DefaultFileProvider provider)
    {
        Id = id;
        Directory = directory;
        Game = game;
        MappingsPath = mappingsPath;
        Provider = provider;
    }

    public string Id { get; }
    public string Directory { get; }
    public EGame Game { get; }
    public string? MappingsPath { get; }
    public DefaultFileProvider Provider { get; }
    public Lock SyncRoot { get; } = new();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public bool IsDisposed { get; private set; }

    public void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(ArchiveSession), $"Archive session '{Id}' is closed.");
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        Provider.Dispose();
    }
}
