using ThwipKit.Core.Assets;
using ThwipKit.Wpf.Services;
using ThwipKit.Wpf.ViewModels;
using Xunit;

namespace ThwipKit.Wpf.Tests;

public sealed class AssetBrowserViewModelTests
{
    [Fact]
    public void LoadAssetsPopulatesAndSortsArchiveNames()
    {
        var viewModel = CreateViewModel();

        viewModel.LoadAssets();

        Assert.Equal(3, viewModel.Assets.Count);
        Assert.Equal(["ArchiveA", "ArchiveB"], viewModel.ArchiveNames);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public void SearchTextAndArchiveFilterNarrowVisibleAssets()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadAssets();

        viewModel.SearchText = "hero";
        viewModel.SelectedArchive = "ArchiveA";

        Assert.Single(viewModel.AssetsView.Cast<AssetInfo>());
        Assert.Equal("characters/hero.texture", viewModel.AssetsView.Cast<AssetInfo>().Single().ResolvedName);
    }

    [Fact]
    public void ClearFiltersRestoresAllVisibleAssets()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadAssets();
        viewModel.SearchText = "missing";
        viewModel.SelectedArchive = "ArchiveA";

        viewModel.ClearFiltersCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Null(viewModel.SelectedArchive);
        Assert.Equal(3, viewModel.AssetsView.Cast<AssetInfo>().Count());
    }

    [Fact]
    public void TypeFilterNarrowsVisibleAssets()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadAssets();

        viewModel.SelectedType = ThwipKit.Core.Staging.AssetType.Texture;

        Assert.Single(viewModel.AssetsView.Cast<AssetInfo>());
        Assert.Equal("characters/hero.texture", viewModel.AssetsView.Cast<AssetInfo>().Single().ResolvedName);
    }

    [Fact]
    public void OrSearchMatchesEitherTerm()
    {
        var viewModel = CreateViewModel();
        viewModel.LoadAssets();

        viewModel.SearchText = "hero OR material";

        Assert.Equal(2, viewModel.AssetsView.Cast<AssetInfo>().Count());
    }

    [Fact]
    public void SelectedAssetSurfacesPopulatedMetadata()
    {
        AssetInfo[] assets =
        [
            new()
            {
                AssetId = 1,
                ArchiveName = "ArchiveA",
                Offset = 10,
                Size = 1024,
                ResolvedName = "characters/hero.texture",
                Compression = ThwipKit.Core.Games.CompressionFormat.Lz4,
                Crc32 = 0xCBF43926u,
                Crc64 = 0x995DC9BBDF1939FAul
            }
        ];
        var viewModel = new AssetBrowserViewModel(new StubAssetBrowserService(assets), "game");
        viewModel.LoadAssets();

        viewModel.SelectedAsset = viewModel.Assets[0];

        Assert.Equal(ThwipKit.Core.Games.CompressionFormat.Lz4, viewModel.SelectedAsset.Compression);
        Assert.Equal("0xCBF43926", viewModel.SelectedAsset.Crc32Hex);
        Assert.Equal("0x995DC9BBDF1939FA", viewModel.SelectedAsset.Crc64Hex);
    }

    private static AssetBrowserViewModel CreateViewModel()
    {
        AssetInfo[] assets =
        [
            new() { AssetId = 2, ArchiveName = "ArchiveB", Offset = 20, Size = 2048 },
            new() { AssetId = 1, ArchiveName = "ArchiveA", Offset = 10, Size = 1024, ResolvedName = "characters/hero.texture" },
            new() { AssetId = 3, ArchiveName = "ArchiveA", Offset = 30, Size = 4096, ResolvedName = "materials/web.material" }
        ];
        return new AssetBrowserViewModel(new StubAssetBrowserService(assets), "game");
    }

    private sealed class StubAssetBrowserService(IReadOnlyList<AssetInfo> assets) : IAssetBrowserService
    {
        public IReadOnlyList<AssetInfo> GetAllAssets(string gamePath) => assets;
        public void ExtractAsset(AssetInfo asset, string gamePath) { }
        public void ReplaceAsset(AssetInfo asset, string gamePath, string replacementFilePath) { }
        public void OpenAsset(AssetInfo asset, string gamePath) { }
    }
}
