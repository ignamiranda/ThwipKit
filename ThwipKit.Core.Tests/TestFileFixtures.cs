using System.IO.Compression;
using System.Linq;
using System.Text;
using ThwipKit.Core.Dat1;
using ThwipKit.Core.Hashing;

namespace ThwipKit.Core.Tests;

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
        => CreateTocFixture(path, archiveNames,
            assetIds: Enumerable.Range(0, archiveNames.Count).Select(i => 0x1122334455667788UL + (ulong)i).ToArray(),
            sizes: Enumerable.Range(0, archiveNames.Count).Select(i => 123U + (uint)i).ToArray(),
            offsets: Enumerable.Range(0, archiveNames.Count).Select(i => 456U + (uint)i).ToArray());

    public static (string Path, byte[] Dat1) CreateTocFixture(
        string path, IReadOnlyList<string> archiveNames,
        IReadOnlyList<ulong> assetIds, IReadOnlyList<uint> sizes, IReadOnlyList<uint> offsets)
    {
        if (assetIds.Count != archiveNames.Count || sizes.Count != archiveNames.Count || offsets.Count != archiveNames.Count)
        {
            throw new ArgumentException("assetIds, sizes, and offsets must each have one entry per archive.");
        }

        var sections = new[]
        {
            (Tag: new byte[] { 0xF0, 0xBF, 0x8A, 0x39 }, Data: archiveNames.SelectMany(CreateArchiveEntry).ToArray()),
            (Tag: new byte[] { 0x8A, 0x7B, 0x6D, 0x50 }, Data: Write(writer =>
            {
                for (int i = 0; i < assetIds.Count; i++)
                {
                    writer.Write(assetIds[i]);
                }
            })),
            (Tag: new byte[] { 0x61, 0xF4, 0xBC, 0x65 }, Data: Write(writer =>
            {
                for (int i = 0; i < sizes.Count; i++)
                {
                    writer.Write(1U);
                    writer.Write(sizes[i]);
                    writer.Write((uint)i);
                }
            })),
            (Tag: new byte[] { 0xB5, 0x20, 0xD7, 0xDC }, Data: Write(writer =>
            {
                for (int i = 0; i < offsets.Count; i++)
                {
                    writer.Write((uint)i);
                    writer.Write(offsets[i]);
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

    public static string CreateTocFile(
        string path, IReadOnlyList<string> archiveNames,
        IReadOnlyList<ulong> assetIds, IReadOnlyList<uint> sizes, IReadOnlyList<uint> offsets)
        => CreateTocFixture(path, archiveNames, assetIds, sizes, offsets).Path;

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

    /// <summary>
    /// Creates a single-block DSAR archive whose block covers [realOffset, realOffset + assetData.Length)
    /// and stores the raw asset bytes uncompressed (compressionType defaults to 0/None). The data sits
    /// immediately after the block table so decompression returns exactly assetData.
    /// </summary>
    public static void CreateDsarFile(string path, byte[] assetData, uint realOffset, byte compressionType = 0)
    {
        const int headerSize = 4 + 4 + 4 + 4 + 8 + 8; // 32
        const int blockSize = 4 + 4 + 4 + 4 + 4 + 4 + 1 + 7; // 32
        uint compressedOffset = (uint)(headerSize + blockSize);

        using var file = File.Create(path);
        using var writer = new BinaryWriter(file, Encoding.UTF8, true);
        writer.Write(new byte[] { (byte)'D', (byte)'S', (byte)'A', (byte)'R' });
        writer.Write(1U);
        writer.Write(1U);
        writer.Write(0U);
        writer.Write(0UL);
        writer.Write(new byte[8]);

        writer.Write(realOffset);
        writer.Write(0U);
        writer.Write(compressedOffset);
        writer.Write(0U);
        writer.Write((uint)assetData.Length);
        writer.Write((uint)assetData.Length);
        writer.Write(compressionType);
        writer.Write(new byte[7]);

        writer.Write(assetData);
    }

    public static byte[] Combine(params byte[][] arrays)
    {
        return arrays.SelectMany(array => array).ToArray();
    }

    public static byte[] BuildSyntheticDat1(IReadOnlyList<string> paths)
    {
        const int headerSize = 16 + 12;
        int stringsLength = 0;
        foreach (var p in paths)
        {
            stringsLength += Encoding.UTF8.GetByteCount(p) + 1;
        }

        int referencesOffset = headerSize + stringsLength;
        int referencesSize = paths.Count * 16;
        int total = referencesOffset + referencesSize;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((uint)0x44415431);
        writer.Write((uint)0);
        writer.Write((uint)total);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write((uint)ReferencesSection.Tag);
        writer.Write((uint)referencesOffset);
        writer.Write((uint)referencesSize);

        var stringOffsets = new List<uint>(paths.Count);
        int cursor = headerSize;
        foreach (var p in paths)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(p);
            stringOffsets.Add((uint)cursor);
            writer.Write(bytes);
            writer.Write((byte)0);
            cursor += bytes.Length + 1;
        }

        for (int i = 0; i < paths.Count; i++)
        {
            writer.Write(HashComputer.ComputeAssetId(paths[i]));
            writer.Write(stringOffsets[i]);
            writer.Write((uint)0);
        }

        return stream.ToArray();
    }
}
