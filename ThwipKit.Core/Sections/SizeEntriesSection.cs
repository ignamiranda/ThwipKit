namespace ThwipKit.Core.Sections;

public sealed class SizeEntriesSection
{
    public uint Always1 { get; init; }
    public uint Value { get; init; }
    public uint Index { get; init; }

    public static List<SizeEntriesSection> Parse(byte[] data)
    {
        const int recordSize = 12;
        SectionParsing.ValidateDivisibility(data, recordSize, nameof(SizeEntriesSection));
        var entries = new List<SizeEntriesSection>(data.Length / recordSize);
        using var reader = new BinaryReader(new MemoryStream(data));
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            entries.Add(new SizeEntriesSection
            {
                Always1 = reader.ReadUInt32(),
                Value = reader.ReadUInt32(),
                Index = reader.ReadUInt32()
            });
        }
        return entries;
    }
}
