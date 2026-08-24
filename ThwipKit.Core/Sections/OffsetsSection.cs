namespace ThwipKit.Core.Sections;

public sealed class OffsetsSection
{
    public uint ArchiveIndex { get; init; }
    public uint OffsetInArchive { get; init; }

    public static List<OffsetsSection> Parse(byte[] data)
    {
        const int recordSize = 8;
        SectionParsing.ValidateDivisibility(data, recordSize, nameof(OffsetsSection));
        var entries = new List<OffsetsSection>(data.Length / recordSize);
        using var reader = new BinaryReader(new MemoryStream(data));
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            entries.Add(new OffsetsSection
            {
                ArchiveIndex = reader.ReadUInt32(),
                OffsetInArchive = reader.ReadUInt32()
            });
        }
        return entries;
    }
}
