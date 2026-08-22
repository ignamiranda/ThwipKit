using System.Text;

namespace SpiderManModdingTool.Core.Sections;

public sealed class ArchivesMapSection
{
    public uint InstallBucket { get; init; }
    public uint ChunkMap { get; init; }
    public string Name { get; init; } = string.Empty;

    public static List<ArchivesMapSection> Parse(byte[] data)
    {
        const int recordSize = 72;
        SectionParsing.ValidateDivisibility(data, recordSize, nameof(ArchivesMapSection));
        var entries = new List<ArchivesMapSection>(data.Length / recordSize);
        using var reader = new BinaryReader(new MemoryStream(data));
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            byte[] nameBytes = reader.ReadBytes(64 + 8);
            uint installBucket = BitConverter.ToUInt32(nameBytes, 0);
            uint chunkMap = BitConverter.ToUInt32(nameBytes, 4);
            int terminator = Array.IndexOf(nameBytes, (byte)0, 8, 64);
            int nameLength = terminator < 0 ? 64 : terminator - 8;
            entries.Add(new ArchivesMapSection
            {
                InstallBucket = installBucket,
                ChunkMap = chunkMap,
                Name = Encoding.ASCII.GetString(nameBytes, 8, nameLength)
            });
        }
        return entries;
    }
}

internal static class SectionParsing
{
    public static void ValidateDivisibility(byte[] data, int recordSize, string sectionName)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length % recordSize != 0)
        {
            throw new InvalidDataException($"{sectionName} length {data.Length} is not divisible by record size {recordSize}.");
        }
    }
}
