using System.Text.RegularExpressions;

namespace MCP_UAsset_Toolkit.Source.Tools.Common;

public sealed record AssetSearchResult<T>(
    IReadOnlyList<T> Matches,
    int TotalCount,
    int Offset,
    int Limit,
    bool HasMore);

public static class AssetSearch
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static AssetSearchResult<T> Search<T>(
        IEnumerable<T> candidates,
        string query,
        Func<T, IEnumerable<string?>> searchableFields,
        int offset = 0,
        int limit = 100,
        bool useRegex = false)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(searchableFields);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be zero or greater.");
        }

        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be between 1 and 1000.");
        }

        Regex? pattern = useRegex
            ? new Regex(
                query,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
                RegexTimeout)
            : null;

        var matchingCandidates = candidates
            .Where(candidate => searchableFields(candidate).Any(value => value != null &&
                (pattern?.IsMatch(value) ?? value.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        var totalCount = matchingCandidates.Count;
        var matches = matchingCandidates.Skip(offset).Take(limit).ToList();

        return new AssetSearchResult<T>(
            matches,
            totalCount,
            offset,
            limit,
            offset + matches.Count < totalCount);
    }
}
