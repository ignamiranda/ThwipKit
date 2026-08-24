using System.IO.Compression;
using ThwipKit.Core.Sections;

namespace ThwipKit.Core.Games;

public static class TocParser
{
    private static readonly byte[] WrapperMagic = [0xAF, 0x12, 0xAF, 0x77];
    private static readonly byte[] Dat1Magic = [0x31, 0x54, 0x41, 0x44];

    public static TocData Parse(string path, IReadOnlyDictionary<string, string> sectionTags)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream, sectionTags);
    }

    public static TocData Parse(Stream stream, IReadOnlyDictionary<string, string> sectionTags)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
        if (stream.CanSeek && stream.Length - stream.Position < 8)
        {
            throw new InvalidDataException("TOC wrapper is truncated.");
        }
        RequireBytes(reader, WrapperMagic, "TOC wrapper");
        uint expectedLength = reader.ReadUInt32();
        using var decompressed = new MemoryStream();
        try
        {
            using var zlib = new ZLibStream(stream, CompressionMode.Decompress, true);
            zlib.CopyTo(decompressed);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException("Invalid zlib-wrapped TOC data.", exception);
        }
        if (decompressed.Length != expectedLength)
        {
            throw new InvalidDataException($"Decompressed TOC size mismatch: expected {expectedLength}, got {decompressed.Length}.");
        }
        return ParseDat1(decompressed.ToArray(), sectionTags);
    }

    public static TocData ParseDat1(byte[] data, IReadOnlyDictionary<string, string> sectionTags)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sectionTags);
        if (data.Length < 16)
        {
            throw new InvalidDataException("DAT1 header is truncated.");
        }
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);
        RequireBytes(reader, Dat1Magic, "DAT1");
        reader.ReadUInt32();
        uint declaredSize = reader.ReadUInt32();
        if (declaredSize > data.Length || declaredSize < 16)
        {
            throw new InvalidDataException($"Invalid DAT1 declared size {declaredSize}.");
        }
        ushort sectionCount = reader.ReadUInt16();
        ushort unknownCount = reader.ReadUInt16();
        long tableLength = (long)sectionCount * 12 + (long)unknownCount * 8;
        if (stream.Position + tableLength > declaredSize)
        {
            throw new InvalidDataException("DAT1 section table exceeds declared size.");
        }

        var descriptors = new List<(byte[] Tag, uint Offset, uint Size)>(sectionCount);
        for (int i = 0; i < sectionCount; i++)
        {
            descriptors.Add((reader.ReadBytes(4), reader.ReadUInt32(), reader.ReadUInt32()));
        }
        reader.ReadBytes(unknownCount * 8);

        var archives = new List<ArchivesMapSection>();
        var assetIds = new List<ulong>();
        var sizeEntries = new List<SizeEntriesSection>();
        var offsets = new List<OffsetsSection>();
        var unknownSections = new List<TocUnknownSection>();
        foreach ((byte[] tag, uint offset, uint size) in descriptors)
        {
            if ((ulong)offset + size > declaredSize)
            {
                throw new InvalidDataException($"DAT1 section {Convert.ToHexString(tag)} exceeds declared size.");
            }
            stream.Position = offset;
            byte[] sectionData = reader.ReadBytes(checked((int)size));
            string tagText = Convert.ToHexString(tag);
            TocSectionFormat format = ResolveFormat(tagText, sectionTags);
            switch (format)
            {
                case TocSectionFormat.ArchivesMap:
                    archives.AddRange(ArchivesMapSection.Parse(sectionData));
                    break;
                case TocSectionFormat.AssetIds:
                    assetIds.AddRange(AssetIdsSection.Parse(sectionData));
                    break;
                case TocSectionFormat.SizeEntries:
                    sizeEntries.AddRange(SizeEntriesSection.Parse(sectionData));
                    break;
                case TocSectionFormat.Offsets:
                    offsets.AddRange(OffsetsSection.Parse(sectionData));
                    break;
                default:
                    unknownSections.Add(new TocUnknownSection(tagText, sectionData));
                    break;
            }
        }

        return new TocData
        {
            Archives = archives,
            AssetIds = assetIds,
            SizeEntries = sizeEntries,
            Offsets = offsets,
            UnknownSections = unknownSections
        };
    }

    internal static TocSectionData ParseSectionData(byte[] sectionTag, byte[] sectionData, IReadOnlyDictionary<string, string> sectionTags)
    {
        ArgumentNullException.ThrowIfNull(sectionTag);
        ArgumentNullException.ThrowIfNull(sectionData);
        if (sectionTag.Length != 4)
        {
            throw new InvalidDataException("A DAT1 section tag must contain exactly four bytes.");
        }
        string tagText = Convert.ToHexString(sectionTag);
        TocSectionFormat format = ResolveFormat(tagText, sectionTags);
        return format switch
        {
            TocSectionFormat.ArchivesMap => new TocSectionData { Format = format, Archives = ArchivesMapSection.Parse(sectionData) },
            TocSectionFormat.AssetIds => new TocSectionData { Format = format, AssetIds = AssetIdsSection.Parse(sectionData) },
            TocSectionFormat.SizeEntries => new TocSectionData { Format = format, SizeEntries = SizeEntriesSection.Parse(sectionData) },
            TocSectionFormat.Offsets => new TocSectionData { Format = format, Offsets = OffsetsSection.Parse(sectionData) },
            _ => new TocSectionData { Format = TocSectionFormat.Unknown, RawData = sectionData.ToArray() }
        };
    }

    internal static TocSectionFormat ResolveFormat(string tag, IReadOnlyDictionary<string, string> sectionTags)
    {
        foreach ((string name, string configuredTag) in sectionTags)
        {
            if (!string.Equals(NormalizeTag(configuredTag), tag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            return name.ToUpperInvariant() switch
            {
                "ARCHIVESMAP" => TocSectionFormat.ArchivesMap,
                "ASSETIDS" => TocSectionFormat.AssetIds,
                "SIZEENTRIES" => TocSectionFormat.SizeEntries,
                "OFFSETS" => TocSectionFormat.Offsets,
                _ => TocSectionFormat.Unknown
            };
        }
        return TocSectionFormat.Unknown;
    }

    internal static string NormalizeTag(string value)
    {
        string normalized = value.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty);
        if (normalized.Length != 8 || !normalized.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException($"Invalid section tag '{value}'.");
        }
        return normalized.ToUpperInvariant();
    }

    private static void RequireBytes(BinaryReader reader, byte[] expected, string name)
    {
        byte[] actual = reader.ReadBytes(expected.Length);
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException($"Invalid {name} magic.");
        }
    }
}
