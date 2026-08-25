using System.Text;
using K4os.Compression.LZ4;
using ThwipKit.Core.Dat1;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Core.Hashing;
using Xunit;

namespace ThwipKit.Core.Tests.Hashing;

public class HashTableGeneratorTests
{
    private static byte[] BuildSyntheticDat1(IReadOnlyList<string> paths)
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

    [Fact]
    public void BuildFromDat1Files_MapsAssetIdToPath()
    {
        var paths = new[]
        {
            "characters/hero/models/hero.model",
            "textures/ui/loading.texture",
            "config/gameplay.config"
        };
        byte[] dat1 = BuildSyntheticDat1(paths);

        var table = HashTableGenerator.BuildFromDat1Files(new[] { dat1 });

        Assert.Equal(paths.Length, table.Count);
        foreach (var path in paths)
        {
            string key = HashComputer.ToAssetIdHex(HashComputer.ComputeAssetId(path));
            Assert.Equal(path, table[key]);
        }
    }

    [Fact]
    public void BuildFromDat1Files_SkipsNonDat1Buffers()
    {
        byte[] notDat1 = Encoding.ASCII.GetBytes("hello world this is not a dat1 container");
        var table = HashTableGenerator.BuildFromDat1Files(new[] { notDat1 });
        Assert.Empty(table);
    }

    [Fact]
    public void BuildFromDirectory_FindsDat1FilesAndWritesLoadableTable()
    {
        var paths = new[] { "textures/ui/loading.texture", "config/gameplay.config" };
        using var temp = new TempDirectory();
        File.WriteAllBytes(Path.Combine(temp.Path, "a.dat1"), BuildSyntheticDat1(new[] { paths[0] }));
        File.WriteAllBytes(Path.Combine(temp.Path, "sub", "b.dat1"), BuildSyntheticDat1(new[] { paths[1] }));
        File.WriteAllBytes(Path.Combine(temp.Path, "ignore.txt"), Encoding.UTF8.GetBytes("not a dat1"));

        var table = HashTableGenerator.BuildFromDirectory(temp.Path);
        Assert.Equal(2, table.Count);

        string hashFile = Path.Combine(temp.Path, "hashes.txt");
        HashTableGenerator.WriteHashFile(table, hashFile);

        // The written file must be loadable by the same parser AssetCatalog consumes.
        var reparsed = HashTableParser.Parse(File.ReadLines(hashFile), hashFile);
        Assert.Equal(table.Count, reparsed.Count);
        foreach (var pair in table)
        {
            Assert.Equal(pair.Value, reparsed[pair.Key]);
        }
    }

    [Fact]
    public void BuildFromArchives_DecompressesDsarAndExtractsReferences()
    {
        var paths = new[] { "textures/ui/loading.texture", "config/gameplay.config" };
        byte[] dat1 = BuildSyntheticDat1(paths);
        byte[] dsar = BuildSyntheticDsar(dat1, 3);

        using var temp = new TempDirectory();
        string archiveDir = System.IO.Path.Combine(temp.Path, "asset_archive");
        Directory.CreateDirectory(archiveDir);
        File.WriteAllBytes(System.IO.Path.Combine(archiveDir, "g00s000"), dsar);

        var definition = new GameDefinition
        {
            InternalId = "test-game",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC"
        };
        definition.CompressionFormats = new[] { CompressionFormat.Lz4 };
        var game = new ConfiguredGame(definition);

        var table = HashTableGenerator.BuildFromArchives(archiveDir, game);

        Assert.Equal(paths.Length, table.Count);
        foreach (var path in paths)
        {
            string key = HashComputer.ToAssetIdHex(HashComputer.ComputeAssetId(path));
            Assert.Equal(path, table[key]);
        }
    }

    private static byte[] BuildSyntheticDsar(byte[] dat1, byte compressionType)
    {
        byte[] compressed = compressionType switch
        {
            3 => Lz4Compress(dat1),
            _ => dat1
        };

        const int headerSize = 32;
        const int blockEntrySize = 32;
        uint compressedOffset = (uint)(headerSize + blockEntrySize);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[] { (byte)'D', (byte)'S', (byte)'A', (byte)'R' });
        writer.Write((uint)1);
        writer.Write((uint)1);
        writer.Write((uint)0);
        writer.Write((ulong)0);
        writer.Write(new byte[8]);

        writer.Write((uint)0);
        writer.Write((uint)0);
        writer.Write(compressedOffset);
        writer.Write((uint)0);
        writer.Write((uint)dat1.Length);
        writer.Write((uint)compressed.Length);
        writer.Write(compressionType);
        writer.Write(new byte[7]);

        writer.Write(compressed);
        return stream.ToArray();
    }

    private static byte[] Lz4Compress(byte[] source)
    {
        int maxLength = source.Length + source.Length / 255 + 16;
        var target = new byte[maxLength];
        int written = LZ4Codec.Encode(source, 0, source.Length, target, 0, target.Length);
        Array.Resize(ref target, written);
        return target;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; }
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "thwipkit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "sub"));
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch (IOException) { }
        }
    }
}
