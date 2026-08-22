using SpiderManModdingTool.Core.Games;

namespace SpiderManModdingTool.Core.GameDefinitions;

public enum TocFormat
{
    ZlibDat1
}

public enum HashFormat
{
    KeyValue
}

public class GameDefinition
{
    public string DisplayName { get; set; } = string.Empty;
    public string InternalId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string[] SupportedExecutables { get; set; } = Array.Empty<string>();
    public string ArchiveDirectory { get; set; } = "asset_archive";
    public string TocFileName { get; set; } = "TOC";
    public TocFormat TocFormat { get; set; } = TocFormat.ZlibDat1;
    public string HashFilePath { get; set; } = Path.Combine("asset_archive", "hashes.txt");
    public HashFormat HashFormat { get; set; } = HashFormat.KeyValue;
    public CompressionFormat[] CompressionFormats { get; set; } = Array.Empty<CompressionFormat>();
    public bool SupportsHdTextures { get; set; } = true;
    public bool HasDescriptionSection { get; set; } = true;
    public bool IsInternalTarget { get; set; }
    public Dictionary<string, bool> Capabilities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SectionTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] VersionFileNames { get; set; } = Array.Empty<string>();
    public int SteamAppId { get; set; }

    public IReadOnlyList<CompressionFormat> GetCompressionFormats() => CompressionFormats;

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
            _ => false
        };
    }
}
