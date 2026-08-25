using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThwipKit.Core.Assets;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Core.Hashing;
using Xunit;

namespace ThwipKit.Core.Tests;

public class AssetCatalogTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _gamePath;
    private readonly string _assetArchivePath;
    private readonly string _tocPath;

    public AssetCatalogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _gamePath = Path.Combine(_tempDir, "game");
        _assetArchivePath = Path.Combine(_gamePath, "asset_archive");
        _tocPath = Path.Combine(_assetArchivePath, "TOC");

        Directory.CreateDirectory(_assetArchivePath);
        TestFileFixtures.CreateTocFile(_tocPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    private static GameBase CreateTestGame(bool isInternalTarget = false)
    {
        return new ConfiguredGame(new GameDefinition
        {
            InternalId = "MSMR",
            ExecutableName = "Spider-Man Remastered.exe",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC",
            TocFormat = TocFormat.ZlibDat1,
            IsInternalTarget = isInternalTarget,
            CompressionFormats = [CompressionFormat.Zlib],
            SectionTags = new Dictionary<string, string>
            {
                ["ArchivesMap"] = "F0BF8A39",
                ["AssetIDs"] = "8A7B6D50",
                ["SizeEntries"] = "61F4BC65",
                ["Offsets"] = "B520D7DC"
            }
        });
    }

    [Fact]
    public void GetAssetsReturnsAssetsFromToc()
    {
        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);

        Assert.Single(assets);
    }

    [Fact]
    public void GetAssetsPopulatesAllMetadata()
    {
        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);
        AssetInfo asset = assets[0];

        Assert.Equal(0x1122334455667788UL, asset.AssetId);
        Assert.Equal("0x1122334455667788", asset.AssetIdHex);
        Assert.Equal(123U, asset.Size);
        Assert.Equal(456U, asset.Offset);
        Assert.Equal("Archive0", asset.ArchiveName);
        Assert.Equal(0U, asset.ArchiveIndex);
    }

    [Fact]
    public void GetAssetsPopulatesIsInternalTargetFromDefinition()
    {
        var catalog = new AssetCatalog(CreateTestGame(isInternalTarget: true));

        AssetInfo asset = Assert.Single(catalog.GetAssets(_gamePath));

        Assert.True(asset.IsInternalTarget);
    }

    [Fact]
    public void GetAssetsPopulatesIsInternalTargetFalseFromDefinition()
    {
        var catalog = new AssetCatalog(CreateTestGame(isInternalTarget: false));

        AssetInfo asset = Assert.Single(catalog.GetAssets(_gamePath));

        Assert.False(asset.IsInternalTarget);
    }

    [Fact]
    public void GetAssetsPopulatesCrcFromDecompressedAssetData()
    {
        byte[] assetData = Enumerable.Range(0, 123).Select(i => (byte)(i * 7)).ToArray();
        TestFileFixtures.CreateDsarFile(
            Path.Combine(_assetArchivePath, "Archive0"),
            assetData,
            realOffset: 456,
            compressionType: 0);

        var catalog = new AssetCatalog(CreateTestGame());

        AssetInfo asset = Assert.Single(catalog.GetAssets(_gamePath));

        Assert.Equal(CompressionFormat.None, asset.Compression);
        Assert.Equal(Crc32.Compute(assetData), asset.Crc32);
        Assert.Equal(Crc64.Compute(assetData), asset.Crc64);
        Assert.Equal($"0x{Crc32.Compute(assetData):X8}", asset.Crc32Hex);
        Assert.Equal($"0x{Crc64.Compute(assetData):X16}", asset.Crc64Hex);
    }

    [Fact]
    public void GetAssetsLeavesCrcNullWhenArchiveMissing()
    {
        var catalog = new AssetCatalog(CreateTestGame());

        AssetInfo asset = Assert.Single(catalog.GetAssets(_gamePath));

        Assert.Null(asset.Crc32);
        Assert.Null(asset.Crc64);
    }

    [Fact]
    public void GetAssetsResolvesNamesFromHashTable()
    {
        File.WriteAllText(Path.Combine(_assetArchivePath, "hashes.txt"), "0x1122334455667788=characters/hero.texture");
        var catalog = new AssetCatalog(CreateTestGame());

        AssetInfo asset = Assert.Single(catalog.GetAssets(_gamePath));

        Assert.Equal("characters/hero.texture", asset.ResolvedName);
    }

    [Fact]
    public void GetAssetsThrowsWhenTocMissing()
    {
        var catalog = new AssetCatalog(CreateTestGame());
        string missingGamePath = Path.Combine(_tempDir, "does-not-exist");

        Assert.Throws<FileNotFoundException>(() => catalog.GetAssets(missingGamePath));
    }

    [Fact]
    public void BuildAssetsReturnsEmptyForEmptyToc()
    {
        var catalog = new AssetCatalog(CreateTestGame());
        var emptyToc = new TocData();

        IReadOnlyList<AssetInfo> assets = catalog.BuildAssets(emptyToc);

        Assert.Empty(assets);
    }

    [Fact]
    public void BuildAssetsReturnsOneAssetPerSizeEntry()
    {
        var catalog = new AssetCatalog(CreateTestGame());
        var toc = new TocData
        {
            Archives = [new ThwipKit.Core.Sections.ArchivesMapSection { Name = "Archive0" }],
            AssetIds = [0x1111UL, 0x1111UL],
            SizeEntries =
            [
                new ThwipKit.Core.Sections.SizeEntriesSection { Always1 = 1, Value = 100, Index = 0 },
                new ThwipKit.Core.Sections.SizeEntriesSection { Always1 = 1, Value = 200, Index = 1 }
            ],
            Offsets =
            [
                new ThwipKit.Core.Sections.OffsetsSection { ArchiveIndex = 0, OffsetInArchive = 10 },
                new ThwipKit.Core.Sections.OffsetsSection { ArchiveIndex = 0, OffsetInArchive = 20 }
            ]
        };

        IReadOnlyList<AssetInfo> assets = catalog.BuildAssets(toc);

        Assert.Equal(2, assets.Count);
        Assert.Equal(10U, assets[0].Offset);
        Assert.Equal(20U, assets[1].Offset);
    }

    [Fact]
    public void BuildAssetsJoinsOffsetsBySizeEntryPosition()
    {
        var catalog = new AssetCatalog(CreateTestGame());
        var toc = new TocData
        {
            Archives = [new ThwipKit.Core.Sections.ArchivesMapSection { Name = "Archive0" }],
            AssetIds = [0x1111UL, 0x2222UL],
            SizeEntries =
            [
                new ThwipKit.Core.Sections.SizeEntriesSection { Always1 = 1, Value = 100, Index = 1 },
                new ThwipKit.Core.Sections.SizeEntriesSection { Always1 = 1, Value = 200, Index = 0 }
            ],
            Offsets =
            [
                new ThwipKit.Core.Sections.OffsetsSection { ArchiveIndex = 0, OffsetInArchive = 10 },
                new ThwipKit.Core.Sections.OffsetsSection { ArchiveIndex = 0, OffsetInArchive = 20 }
            ]
        };

        IReadOnlyList<AssetInfo> assets = catalog.BuildAssets(toc);

        Assert.Equal(0x2222UL, assets[0].AssetId);
        Assert.Equal(10U, assets[0].Offset);
        Assert.Equal(0x1111UL, assets[1].AssetId);
        Assert.Equal(20U, assets[1].Offset);
    }

    [Fact]
    public void BuildAssetsThrowsWhenAssetIdIndexIsOutOfRange()
    {
        var catalog = new AssetCatalog(CreateTestGame());
        var toc = new TocData
        {
            Archives = [new ThwipKit.Core.Sections.ArchivesMapSection { Name = "Archive0" }],
            AssetIds = [0x1111UL],
            SizeEntries = [new ThwipKit.Core.Sections.SizeEntriesSection { Value = 100, Index = 5 }],
            Offsets = [new ThwipKit.Core.Sections.OffsetsSection { ArchiveIndex = 0, OffsetInArchive = 10 }]
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => catalog.BuildAssets(toc));

        Assert.Contains("asset ID index 5", exception.Message);
    }

    [Fact]
    public void BuildAssetsThrowsWhenOffsetIsMissing()
    {
        var catalog = new AssetCatalog(CreateTestGame());
        var toc = new TocData
        {
            Archives = [new ThwipKit.Core.Sections.ArchivesMapSection { Name = "Archive0" }],
            AssetIds = [0x1111UL],
            SizeEntries = [new ThwipKit.Core.Sections.SizeEntriesSection { Value = 100, Index = 0 }]
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => catalog.BuildAssets(toc));

        Assert.Contains("offset entry at position 0", exception.Message);
    }

    [Fact]
    public void BuildAssetsThrowsWhenArchiveIndexIsOutOfRange()
    {
        var catalog = new AssetCatalog(CreateTestGame());
        var toc = new TocData
        {
            Archives = [new ThwipKit.Core.Sections.ArchivesMapSection { Name = "Archive0" }],
            AssetIds = [0x1111UL],
            SizeEntries = [new ThwipKit.Core.Sections.SizeEntriesSection { Value = 100, Index = 0 }],
            Offsets = [new ThwipKit.Core.Sections.OffsetsSection { ArchiveIndex = 3, OffsetInArchive = 10 }]
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => catalog.BuildAssets(toc));

        Assert.Contains("archive index 3", exception.Message);
    }

    [Fact]
    public void GetAssetsPopulatesReferencesDependenciesAndUsageCountFromDat1()
    {
        byte[] dat1 = TestFileFixtures.BuildSyntheticDat1(
            ["textures/ui/loading.texture", "config/gameplay.config"]);
        TestFileFixtures.CreateTocFile(_tocPath, ["Archive0", "Archive1", "Archive2"],
            assetIds: [0x1111111111111111UL, 0x9EA3A7559DA1C267UL, 0x3333333333333333UL],
            sizes: [(uint)dat1.Length, 64U, 32U],
            offsets: [456U, 0U, 0U]);

        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive0"), dat1, realOffset: 456);
        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive1"),
            Enumerable.Range(0, 64).Select(i => (byte)(i * 3)).ToArray(), realOffset: 0);
        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive2"),
            Enumerable.Range(0, 32).Select(i => (byte)(i * 5)).ToArray(), realOffset: 0);

        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);
        AssetInfo config = assets[0];
        AssetInfo texture = assets[1];
        AssetInfo raw = assets[2];

        Assert.Equal(["textures/ui/loading.texture", "config/gameplay.config"], config.Dependencies);
        Assert.Equal(0x1111111111111111UL, config.AssetId);
        Assert.Equal(0x9EA3A7559DA1C267UL, texture.AssetId);
        Assert.Equal(["0x1111111111111111"], texture.References);
        Assert.Equal(1U, texture.UsageCount);
        Assert.NotNull(raw.References);
        Assert.Empty(raw.References);
        Assert.Equal(0U, raw.UsageCount);
        Assert.Null(raw.Dependencies);
    }

    [Fact]
    public void GetAssetsLeavesReferenceFieldsNullWhenAssetDataMissing()
    {
        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);

        Assert.Single(assets);
        Assert.Null(assets[0].Dependencies);
        Assert.Null(assets[0].References);
        Assert.Null(assets[0].UsageCount);
    }

    [Fact]
    public void GetAssetsTreatsMalformedDat1AsNonDat1()
    {
        byte[] malformed = new byte[123];
        "DAT1"u8.ToArray().CopyTo(malformed, 0);

        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive0"), malformed, realOffset: 456);

        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);

        Assert.Single(assets);
        Assert.Null(assets[0].Dependencies);
        Assert.NotNull(assets[0].Crc32);
        Assert.NotNull(assets[0].Crc64);
    }

    [Fact]
    public void GetAssetsSetsDependenciesEmptyForDat1WithoutReferences()
    {
        byte[] dat1 = new byte[28];
        using (var stream = new MemoryStream(dat1))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((uint)0x44415431);
            writer.Write((uint)0);
            writer.Write((uint)28);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
        }

        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive0"), dat1, realOffset: 456);

        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);

        Assert.Single(assets);
        Assert.NotNull(assets[0].Dependencies);
        Assert.Empty(assets[0].Dependencies!);
    }

    [Fact]
    public void GetAssetsResolvesReferrerPathFromHashTable()
    {
        File.WriteAllText(Path.Combine(_assetArchivePath, "hashes.txt"), "0x1111111111111111=configs/menu.config");

        byte[] dat1 = TestFileFixtures.BuildSyntheticDat1(["textures/ui/loading.texture"]);
        TestFileFixtures.CreateTocFile(_tocPath, ["Archive0", "Archive1"],
            assetIds: [0x1111111111111111UL, 0x9EA3A7559DA1C267UL],
            sizes: [(uint)dat1.Length, 64U],
            offsets: [456U, 0U]);

        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive0"), dat1, realOffset: 456);
        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive1"),
            Enumerable.Range(0, 64).Select(i => (byte)(i * 3)).ToArray(), realOffset: 0);

        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);

        Assert.Equal(["configs/menu.config"], assets[1].References);
        Assert.Equal(1U, assets[1].UsageCount);
    }

    [Fact]
    public void GetAssetsDeduplicatesInboundReferrersByIdNotDisplayPath()
    {
        File.WriteAllText(Path.Combine(_assetArchivePath, "hashes.txt"),
            "0x1111111111111111=configs/alpha.config\n" +
            "0x2222222222222222=configs/menu.config\n" +
            "0x3333333333333333=configs/alpha.config");

        byte[] dat1 = TestFileFixtures.BuildSyntheticDat1(["textures/ui/loading.texture"]);
        TestFileFixtures.CreateTocFile(_tocPath, ["Archive0", "Archive1", "Archive2", "Archive3"],
            assetIds: [0x1111111111111111UL, 0x2222222222222222UL, 0x3333333333333333UL, 0x9EA3A7559DA1C267UL],
            sizes: [(uint)dat1.Length, (uint)dat1.Length, (uint)dat1.Length, 64U],
            offsets: [456U, 456U, 456U, 0U]);

        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive0"), dat1, realOffset: 456);
        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive1"), dat1, realOffset: 456);
        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive2"), dat1, realOffset: 456);
        TestFileFixtures.CreateDsarFile(Path.Combine(_assetArchivePath, "Archive3"),
            Enumerable.Range(0, 64).Select(i => (byte)(i * 3)).ToArray(), realOffset: 0);

        var catalog = new AssetCatalog(CreateTestGame());

        IReadOnlyList<AssetInfo> assets = catalog.GetAssets(_gamePath);

        AssetInfo texture = assets[3];
        Assert.Equal(["configs/alpha.config", "configs/alpha.config", "configs/menu.config"], texture.References);
        Assert.Equal(3U, texture.UsageCount);
    }
}
