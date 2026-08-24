using System.IO;
using System.Text.RegularExpressions;
using ThwipKit.Core.Assets;

namespace ThwipKit.Core.Assets;

public static partial class AssetSearch
{
    private const double FuzzyThreshold = 0.4;

    public static Func<AssetInfo, bool> Compile(string query)
    {
        IEnumerable<string> orGroups = SplitOnOr(query);
        List<Func<AssetInfo, bool>> groupPredicates = orGroups
            .Select(CompileGroup)
            .Where(predicate => predicate is not null)
            .ToList()!;

        if (groupPredicates.Count == 0)
        {
            return _ => true;
        }

        return asset => groupPredicates.Any(predicate => predicate(asset));
    }

    public static IEnumerable<AssetInfo> Search(IEnumerable<AssetInfo> assets, string query)
    {
        Func<AssetInfo, bool> predicate = Compile(query);
        return assets.Where(predicate);
    }

    private static IEnumerable<string> SplitOnOr(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return Regex.Split(query, @"\s+OR\s+", RegexOptions.IgnoreCase)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0);
    }

    private static Func<AssetInfo, bool>? CompileGroup(string group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return null;
        }

        string[] terms = group.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<Func<AssetInfo, bool>> termPredicates = terms
            .Select(TermMatches)
            .ToList();

        return asset => termPredicates.All(predicate => predicate(asset));
    }

    private static Func<AssetInfo, bool> TermMatches(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return _ => true;
        }

        string lower = term.ToLowerInvariant();
        return asset =>
        {
            string name = asset.ResolvedName ?? string.Empty;
            string hex = asset.AssetIdHex;
            return name.Contains(lower, StringComparison.OrdinalIgnoreCase)
                || hex.Contains(lower, StringComparison.OrdinalIgnoreCase)
                || FuzzyMatch(name, term)
                || FuzzyMatch(hex, term);
        };
    }

    private static bool FuzzyMatch(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            return false;
        }

        string[] tokens = source.Split(['/', '.', '_'], StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token => TokenFuzzy(token, target));
    }

    private static bool TokenFuzzy(string token, string target)
    {
        int distance = LevenshteinDistance(token.ToLowerInvariant(), target.ToLowerInvariant());
        int maxLength = Math.Max(token.Length, target.Length);
        if (maxLength == 0)
        {
            return true;
        }

        double similarity = 1.0 - (double)distance / maxLength;
        return similarity >= FuzzyThreshold;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int[,] matrix = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }
}
