namespace ThwipKit.Core.Sections;

public static class AssetIdsSection
{
    public static List<ulong> Parse(byte[] data)
    {
        const int recordSize = 8;
        SectionParsing.ValidateDivisibility(data, recordSize, nameof(AssetIdsSection));
        var entries = new List<ulong>(data.Length / recordSize);
        using var reader = new BinaryReader(new MemoryStream(data));
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            entries.Add(reader.ReadUInt64());
        }
        return entries;
    }
}
