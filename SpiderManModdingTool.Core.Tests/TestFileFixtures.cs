using System.IO.Compression;
using System.Text;

namespace SpiderManModdingTool.Core.Tests;

internal static class TestFileFixtures
{
    public static byte[] Write(Action<BinaryWriter> action)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        action(writer);
        return stream.ToArray();
    }

    public static byte[] CreateArchiveEntry(string name)
    {
        return Write(writer =>
        {
            writer.Write(1U);
            writer.Write(2U);
            byte[] nameBytes = new byte[64];
            Encoding.ASCII.GetBytes(name).CopyTo(nameBytes, 0);
            writer.Write(nameBytes);
        });
    }

    public static (string Path, byte[] Dat1) CreateTocFixture(string path)
        => CreateTocFixture(path, ["Archive0"]);

    public static (string Path, byte[] Dat1) CreateTocFixture(string path, IReadOnlyList<string> archiveNames)
    {
        var sections = new[]
        {
            (Tag: new byte[] { 0xF0, 0xBF, 0x8A, 0x39 }, Data: archiveNames.SelectMany(CreateArchiveEntry).ToArray()),
            (Tag: new byte[] { 0x8A, 0x7B, 0x6D, 0x50 }, Data: Write(writer =>
            {
                for (int i = 0; i < archiveNames.Count; i++)
                {
                    writer.Write(0x1122334455667788UL + (ulong)i);
                }
            })),
            (Tag: new byte[] { 0x61, 0xF4, 0xBC, 0x65 }, Data: Write(writer =>
            {
                for (int i = 0; i < archiveNames.Count; i++)
                {
                    writer.Write(1U);
                    writer.Write(123U + (uint)i);
                    writer.Write((uint)i);
                }
            })),
            (Tag: new byte[] { 0xB5, 0x20, 0xD7, 0xDC }, Data: Write(writer =>
            {
                for (int i = 0; i < archiveNames.Count; i++)
                {
                    writer.Write((uint)i);
                    writer.Write(456U + (uint)i);
                }
            }))
        };

        byte[] dat1;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            int headerLength = 16 + (sections.Length * 12) + "ArchiveTOC".Length + 1;
            int dataOffset = (headerLength + 15) & ~15;
            writer.Write(new byte[] { 0x31, 0x54, 0x41, 0x44 });
            writer.Write(0U);
            writer.Write((uint)(dataOffset + sections.Sum(section => section.Data.Length)));
            writer.Write((ushort)sections.Length);
            writer.Write((ushort)0);
            int offset = dataOffset;
            foreach (var section in sections)
            {
                writer.Write(section.Tag);
                writer.Write((uint)offset);
                writer.Write((uint)section.Data.Length);
                offset += section.Data.Length;
            }
            writer.Write(Encoding.ASCII.GetBytes("ArchiveTOC"));
            writer.Write((byte)0);
            writer.Write(new byte[dataOffset - stream.Position]);
            foreach (var section in sections)
            {
                writer.Write(section.Data);
            }
            dat1 = stream.ToArray();
        }

        using var file = File.Create(path);
        using var outerWriter = new BinaryWriter(file, Encoding.UTF8, true);
        outerWriter.Write(new byte[] { 0xAF, 0x12, 0xAF, 0x77 });
        outerWriter.Write((uint)dat1.Length);
        using var zlib = new ZLibStream(file, CompressionLevel.SmallestSize, true);
        zlib.Write(dat1);
        return (path, dat1);
    }

    public static (string Path, byte[] Dat1) CreateTocFixture() => CreateTocFixture(Path.GetTempFileName());

    public static string CreateTocFile(string path) => CreateTocFixture(path).Path;

    public static string CreateTocFile(string path, IReadOnlyList<string> archiveNames)
        => CreateTocFixture(path, archiveNames).Path;

    public static string CreateTocFile() => CreateTocFixture().Path;

    public static void CreateDsarFile(string path)
    {
        using var file = File.Create(path);
        using var writer = new BinaryWriter(file, Encoding.UTF8, true);
        writer.Write(new byte[] { (byte)'D', (byte)'S', (byte)'A', (byte)'R' });
        writer.Write(1U);
        writer.Write(1U);
        writer.Write(0U);
        writer.Write(0UL);
        writer.Write(new byte[8]);

        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(123U);
        writer.Write(100U);
        writer.Write((byte)3);
        writer.Write(new byte[7]);

        writer.Write(new byte[100]);
    }

    public static byte[] Combine(params byte[][] arrays)
    {
        return arrays.SelectMany(array => array).ToArray();
    }
}
