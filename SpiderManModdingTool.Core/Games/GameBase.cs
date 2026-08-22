using SpiderManModdingTool.Core.GameDefinitions;

namespace SpiderManModdingTool.Core.Games;

public abstract class GameBase
{
    public abstract GameDefinition Definition { get; }
    public string DisplayName => Definition.DisplayName;
    public string InternalId => Definition.InternalId;
    public string Version => Definition.Version;
    public string ArchiveDirectory => Definition.ArchiveDirectory;
    public string TocFileName => Definition.TocFileName;
    public IReadOnlyList<CompressionFormat> CompressionFormats => Definition.GetCompressionFormats();

    public abstract TocData ParseToc(string tocPath);
    public abstract TocSectionData HandleSection(byte[] sectionTag, byte[] sectionData);
    public abstract IReadOnlyDictionary<string, string> LoadHashTable(string gamePath);

    public bool IsCompressionImplemented => CompressionFormats.All(CompressionSupport.IsImplemented);

    public virtual CompressionFormat DetectCompression(string archivePath)
    {
        CompressionFormat format;
        if (CompressionFormats.Count == 0)
        {
            format = CompressionFormat.None;
        }
        else if (CompressionFormats.Count == 1)
        {
            format = CompressionFormats[0];
        }
        else
        {
            throw new InvalidOperationException($"Profile '{InternalId}' declares multiple compression formats; runtime detection is not implemented.");
        }
        if (!CompressionSupport.IsImplemented(format))
        {
            throw new NotSupportedException($"Compression format '{format}' declared by profile '{InternalId}' has no decoder implemented.");
        }
        return format;
    }

    public virtual Dictionary<string, bool> GetVersionSpecificBehaviors()
    {
        var behaviors = new Dictionary<string, bool>(Definition.Capabilities, StringComparer.OrdinalIgnoreCase)
        {
            ["UsesZlibCompression"] = CompressionFormats.Contains(CompressionFormat.Zlib),
            ["UsesLz4Compression"] = CompressionFormats.Contains(CompressionFormat.Lz4),
            ["UsesZstdCompression"] = CompressionFormats.Contains(CompressionFormat.Zstd),
            ["CompressionImplemented"] = IsCompressionImplemented,
            ["SupportsHdTextures"] = Definition.SupportsHdTextures,
            ["HasDescriptionSection"] = Definition.HasDescriptionSection
        };
        return behaviors;
    }

    public virtual bool SupportsFeature(string feature) => Definition.Supports(feature);
}

public static class HashTableParser
{
    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines, string sourceName = "hash table")
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int lineNumber = 0;
        foreach (string line in lines)
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith(';') || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }
            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                throw new InvalidDataException($"Invalid {sourceName} entry at line {lineNumber}: expected key=value.");
            }
            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (key.Length == 0)
            {
                throw new InvalidDataException($"Invalid {sourceName} entry at line {lineNumber}: key is blank.");
            }
            if (!values.TryAdd(key, value))
            {
                throw new InvalidDataException($"Duplicate {sourceName} key '{key}' at line {lineNumber}.");
            }
        }
        return values;
    }
}
