using ThwipKit.Core.Sections;

namespace ThwipKit.Core.Games;

public sealed class TocData
{
    public IReadOnlyList<ArchivesMapSection> Archives { get; init; } = Array.Empty<ArchivesMapSection>();
    public IReadOnlyList<ulong> AssetIds { get; init; } = Array.Empty<ulong>();
    public IReadOnlyList<SizeEntriesSection> SizeEntries { get; init; } = Array.Empty<SizeEntriesSection>();
    public IReadOnlyList<OffsetsSection> Offsets { get; init; } = Array.Empty<OffsetsSection>();
    public IReadOnlyList<TocUnknownSection> UnknownSections { get; init; } = Array.Empty<TocUnknownSection>();
}

public sealed record TocUnknownSection(string Tag, byte[] Data);

public enum CompressionFormat
{
    None,
    Zlib,
    Lz4,
    GDeflate,
    Zstd
}

public static class CompressionSupport
{
    public static bool IsImplemented(CompressionFormat format) => format switch
    {
        CompressionFormat.None => true,
        CompressionFormat.Zlib => true,
        CompressionFormat.Lz4 => true,
        _ => false
    };
}

public enum TocSectionFormat
{
    ArchivesMap,
    AssetIds,
    SizeEntries,
    Offsets,
    Unknown
}

public sealed class TocSectionData
{
    public TocSectionFormat Format { get; init; }
    public IReadOnlyList<ArchivesMapSection> Archives { get; init; } = Array.Empty<ArchivesMapSection>();
    public IReadOnlyList<ulong> AssetIds { get; init; } = Array.Empty<ulong>();
    public IReadOnlyList<SizeEntriesSection> SizeEntries { get; init; } = Array.Empty<SizeEntriesSection>();
    public IReadOnlyList<OffsetsSection> Offsets { get; init; } = Array.Empty<OffsetsSection>();
    public byte[] RawData { get; init; } = Array.Empty<byte>();
}
