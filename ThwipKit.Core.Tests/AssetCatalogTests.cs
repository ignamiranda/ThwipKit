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

    private static GameBase CreateTestGame()
    {
        return new ConfiguredGame(new GameDefinition
        {
            InternalId = "MSMR",
            ExecutableName = "Spider-Man Remastered.exe",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC",
            TocFormat = TocFormat.ZlibDat1,
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
}
