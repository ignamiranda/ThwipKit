using SpiderManModdingTool.Core.Games;

namespace SpiderManModdingTool.Core.GameDefinitions;

public class GameDefinition
{
    public string DisplayName { get; set; } = string.Empty;
    public string InternalId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string[] SupportedExecutables { get; set; } = Array.Empty<string>();
    public string ArchiveDirectory { get; set; } = "asset_archive";
    public string TocFileName { get; set; } = "TOC";
    public string TocFormat { get; set; } = "ZlibDat1";
    public string HashFilePath { get; set; } = Path.Combine("asset_archive", "hashes.txt");
    public string HashFormat { get; set; } = "KeyValue";
    public CompressionFormat[] CompressionFormats { get; set; } = Array.Empty<CompressionFormat>();
    public bool UsesZlibCompression { get; set; } = true;
    public bool UsesLz4Compression { get; set; }
    public bool UsesZstdCompression { get; set; }
    public bool SupportsHdTextures { get; set; } = true;
    public bool HasDescriptionSection { get; set; } = true;
    public bool IsInternalTarget { get; set; }
    public Dictionary<string, bool> Capabilities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SectionTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] VersionFileNames { get; set; } = Array.Empty<string>();
    public int SteamAppId { get; set; }

    public IReadOnlyList<CompressionFormat> GetCompressionFormats()
    {
        if (CompressionFormats.Length > 0)
        {
            return CompressionFormats;
        }
        var formats = new List<CompressionFormat>();
        if (UsesZlibCompression)
        {
            formats.Add(CompressionFormat.Zlib);
        }
        if (UsesLz4Compression)
        {
            formats.Add(CompressionFormat.Lz4);
        }
        if (UsesZstdCompression)
        {
            formats.Add(CompressionFormat.Zstd);
        }
        return formats;
    }

    public bool Supports(string capability)
    {
        if (Capabilities.TryGetValue(capability, out bool configured))
        {
            return configured;
        }
        return capability switch
        {
            "SupportsHdTextures" => SupportsHdTextures,
            "HasDescriptionSection" => HasDescriptionSection,
            "UsesZlibCompression" => GetCompressionFormats().Contains(CompressionFormat.Zlib),
            "UsesLz4Compression" => GetCompressionFormats().Contains(CompressionFormat.Lz4),
            "UsesZstdCompression" => GetCompressionFormats().Contains(CompressionFormat.Zstd),
            _ => false
        };
    }
}
