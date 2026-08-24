using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Assets;

public sealed class AssetInfo
{
    public ulong AssetId { get; init; }
    public string AssetIdHex => $"0x{AssetId:X16}";
    public uint Size { get; init; }
    public uint Offset { get; init; }
    public string ArchiveName { get; init; } = string.Empty;
    public uint ArchiveIndex { get; init; }
    public string? ResolvedName { get; set; }
    public AssetType Type { get; set; } = AssetType.Unknown;
    public CompressionFormat? Compression { get; set; }

    public bool IsUnknown => string.IsNullOrWhiteSpace(ResolvedName);
    public bool IsAudio => Type == AssetType.Audio;
}
