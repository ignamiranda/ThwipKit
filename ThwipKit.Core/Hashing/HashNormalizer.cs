namespace ThwipKit.Core.Hashing;

/// <summary>
/// Normalizes an asset path the same way the game/community tools do before hashing:
/// lower-case, backslash to forward slash, and collapse consecutive slashes to one.
/// </summary>
public static class HashNormalizer
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path ?? string.Empty;
        }

        string result = path.ToLowerInvariant().Replace('\\', '/');
        var builder = new System.Text.StringBuilder(result.Length);
        bool hadSlash = false;
        foreach (char c in result)
        {
            if (c == '/')
            {
                if (hadSlash)
                {
                    continue;
                }
                hadSlash = true;
            }
            else
            {
                hadSlash = false;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }
}
