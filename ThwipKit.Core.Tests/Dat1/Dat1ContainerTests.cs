using System.Text;
using ThwipKit.Core.Dat1;
using ThwipKit.Core.Hashing;
using Xunit;

namespace ThwipKit.Core.Tests.Dat1;

public class Dat1ContainerTests
{
    private static byte[] BuildSyntheticDat1(IReadOnlyList<string> paths)
    {
        const int headerSize = 16 + 12; // DAT1 header + one section descriptor
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

        writer.Write((uint)0x44415431); // magic "DAT1"
        writer.Write((uint)0); // unk1
        writer.Write((uint)total); // declared size
        writer.Write((ushort)1); // section count
        writer.Write((ushort)0); // unknown count

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
            writer.Write((uint)0); // extension hash
        }

        return stream.ToArray();
    }

    [Fact]
    public void Parse_ReadsHeaderAndSingleSection()
    {
        byte[] data = BuildSyntheticDat1(new[] { "textures/ui/loading.texture" });
        var container = Dat1Container.Parse(data);

        Assert.Equal(0u, container.Unk1);
        Assert.Equal((uint)data.Length, container.DeclaredSize);
        Assert.Single(container.Sections);
        Assert.Equal(ReferencesSection.Tag, container.Sections[0].Tag);
    }

    [Fact]
    public void FindReferencesSections_ExtractsAssetIdToPath()
    {
        var paths = new[]
        {
            "characters/hero/models/hero.model",
            "textures/ui/loading.texture",
            "config/gameplay.config"
        };
        byte[] data = BuildSyntheticDat1(paths);
        var container = Dat1Container.Parse(data);

        var references = container.FindReferencesSections();
        Assert.Single(references);
        var entries = references[0].Entries;
        Assert.Equal(paths.Length, entries.Count);

        for (int i = 0; i < paths.Length; i++)
        {
            Assert.Equal(HashComputer.ComputeAssetId(paths[i]), entries[i].AssetId);
            Assert.Equal(paths[i], container.GetStringAt(entries[i].StringOffset));
        }
    }

    [Fact]
    public void GetStringAt_ReturnsNull_ForOutOfRangeOrEmpty()
    {
        byte[] data = BuildSyntheticDat1(new[] { "textures/ui/loading.texture" });
        var container = Dat1Container.Parse(data);

        Assert.Null(container.GetStringAt(-1));
        Assert.Null(container.GetStringAt(data.Length));
        Assert.Null(container.GetStringAt(data.Length - 1)); // trailing byte region, no string
    }

    [Fact]
    public void FindReferencesSections_IgnoresNonReferenceSections()
    {
        // A 16-byte-aligned section whose offsets do not resolve to path strings must be rejected.
        byte[] random = new byte[48];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(random);

        const int headerSize = 16 + 12;
        int total = headerSize + random.Length;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((uint)0x44415431);
        writer.Write((uint)0);
        writer.Write((uint)total);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write((uint)0xDEADBEEF);
        writer.Write((uint)headerSize);
        writer.Write((uint)random.Length);
        writer.Write(random);

        var container = Dat1Container.Parse(stream.ToArray());
        Assert.Empty(container.FindReferencesSections());
    }

    [Fact]
    public void Parse_RejectsNonMagic()
    {
        byte[] bad = Encoding.ASCII.GetBytes("NOTDAT1").Concat(new byte[32]).ToArray();
        Assert.Throws<InvalidDataException>(() => Dat1Container.Parse(bad));
    }
}
