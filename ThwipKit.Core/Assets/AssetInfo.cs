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
    public DateTime? LastModified { get; set; }
    public bool IsInternalTarget { get; set; }
    public uint? Crc32 { get; set; }
    public ulong? Crc64 { get; set; }

    public string? Crc32Hex => Crc32.HasValue ? $"0x{Crc32.Value:X8}" : null;
    public string? Crc64Hex => Crc64.HasValue ? $"0x{Crc64.Value:X16}" : null;

    public IReadOnlyList<string>? References { get; set; }
    public IReadOnlyList<string>? Dependencies { get; set; }
    public uint? UsageCount { get; set; }

    public bool IsUnknown => string.IsNullOrWhiteSpace(ResolvedName);
    public bool IsAudio => Type == AssetType.Audio;
}
