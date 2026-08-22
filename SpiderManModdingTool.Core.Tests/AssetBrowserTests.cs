using System.Collections.Generic;
using System.IO;
using SpiderManModdingTool.Core.Assets;
using SpiderManModdingTool.Core.GameDefinitions;
using SpiderManModdingTool.Core.Games;
using Xunit;

namespace SpiderManModdingTool.Core.Tests;

public class AssetBrowserTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _gamePath;
    private readonly AssetBrowser _browser;

    public AssetBrowserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _gamePath = Path.Combine(_tempDir, "game");
        string assetArchivePath = Path.Combine(_gamePath, "asset_archive");

        Directory.CreateDirectory(assetArchivePath);
        TestFileFixtures.CreateTocFile(
            Path.Combine(assetArchivePath, "TOC"),
            ["Archive0", "OtherArchive", "archive0", ""]);
        File.WriteAllText(Path.Combine(assetArchivePath, "hashes.txt"), "0x1122334455667788=characters/hero/suit.texture");
        _browser = new AssetBrowser(CreateTestGame());
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

    [Fact]
    public void GetAllAssetsReturnsCatalogAssets()
    {
        IReadOnlyList<AssetInfo> assets = _browser.GetAllAssets(_gamePath);

        Assert.Equal(4, assets.Count);
        Assert.Equal(0x1122334455667788UL, assets[0].AssetId);
    }

    [Theory]
    [InlineData("HERO")]
    [InlineData("7788")]
    public void SearchByNameMatchesResolvedNameAndAssetIdIgnoringCase(string pattern)
    {
        IReadOnlyList<AssetInfo> assets = _browser.SearchByName(_gamePath, pattern);

        Assert.Single(assets);
        Assert.Equal(0x1122334455667788UL, assets[0].AssetId);
    }

    [Fact]
    public void SearchByArchiveMatchesArchiveNameIgnoringCase()
    {
        IReadOnlyList<AssetInfo> assets = _browser.SearchByArchive(_gamePath, "archive0");

        Assert.Equal(2, assets.Count);
        Assert.All(assets, asset => Assert.Equal("Archive0", asset.ArchiveName, ignoreCase: true));
        Assert.DoesNotContain(assets, asset => asset.ArchiveName == "OtherArchive");
    }

    [Fact]
    public void FilterBySizeIncludesRangeBoundaries()
    {
        IReadOnlyList<AssetInfo> assets =
        [
            new AssetInfo { AssetId = 1, Size = 99 },
            new AssetInfo { AssetId = 2, Size = 100 },
            new AssetInfo { AssetId = 3, Size = 200 },
            new AssetInfo { AssetId = 4, Size = 201 }
        ];

        IReadOnlyList<AssetInfo> filtered = _browser.FilterBySize(assets, 100, 200);

        Assert.Collection(
            filtered,
            asset => Assert.Equal(2UL, asset.AssetId),
            asset => Assert.Equal(3UL, asset.AssetId));
    }

    [Fact]
    public void GetArchiveNamesReturnsDistinctNonEmptyArchiveNames()
    {
        Assert.Equal(["Archive0", "OtherArchive"], _browser.GetArchiveNames(_gamePath));
    }

    [Fact]
    public void GetAssetReturnsMatchingAsset()
    {
        AssetInfo? asset = _browser.GetAsset(_gamePath, 0x1122334455667788UL);

        Assert.NotNull(asset);
        Assert.Equal("characters/hero/suit.texture", asset.ResolvedName);
    }

    [Fact]
    public void GetAssetCountReturnsCatalogCount()
    {
        Assert.Equal(4, _browser.GetAssetCount(_gamePath));
    }

    private static GameBase CreateTestGame()
    {
        return new ConfiguredGame(new GameDefinition
        {
            InternalId = "MSMR",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC",
            TocFormat = TocFormat.ZlibDat1,
            HashFilePath = Path.Combine("asset_archive", "hashes.txt"),
            HashFormat = HashFormat.KeyValue,
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
}
