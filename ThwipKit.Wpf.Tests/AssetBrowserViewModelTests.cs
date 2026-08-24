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
    public void InternalTargetFiltersExposesAllOptions()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(3, viewModel.InternalTargetFilters.Count);
        Assert.Contains(InternalTargetFilter.All, viewModel.InternalTargetFilters);
        Assert.Contains(InternalTargetFilter.InternalTargetsOnly, viewModel.InternalTargetFilters);
        Assert.Contains(InternalTargetFilter.NonInternalTargetsOnly, viewModel.InternalTargetFilters);
    }

    [Fact]
    public void InternalTargetFilterNarrowsVisibleAssets()
    {
        var viewModel = CreateViewModelWithInternalTargets();
        viewModel.LoadAssets();

        viewModel.SelectedInternalTargetFilter = InternalTargetFilter.InternalTargetsOnly;

        Assert.Single(viewModel.AssetsView.Cast<AssetInfo>());
        Assert.True(viewModel.AssetsView.Cast<AssetInfo>().Single().IsInternalTarget);
    }

    [Fact]
    public void NonInternalTargetFilterExcludesInternalTargets()
    {
        var viewModel = CreateViewModelWithInternalTargets();
        viewModel.LoadAssets();

        viewModel.SelectedInternalTargetFilter = InternalTargetFilter.NonInternalTargetsOnly;

        Assert.Equal(2, viewModel.AssetsView.Cast<AssetInfo>().Count());
        Assert.All(viewModel.AssetsView.Cast<AssetInfo>(), asset => Assert.False(asset.IsInternalTarget));
    }

    [Fact]
    public void ClearFiltersResetsInternalTargetFilter()
    {
        var viewModel = CreateViewModelWithInternalTargets();
        viewModel.LoadAssets();
        viewModel.SelectedInternalTargetFilter = InternalTargetFilter.InternalTargetsOnly;

        viewModel.ClearFiltersCommand.Execute(null);

        Assert.Equal(InternalTargetFilter.All, viewModel.SelectedInternalTargetFilter);
        Assert.Equal(3, viewModel.AssetsView.Cast<AssetInfo>().Count());
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

    private static AssetBrowserViewModel CreateViewModelWithInternalTargets()
    {
        AssetInfo[] assets =
        [
            new() { AssetId = 2, ArchiveName = "ArchiveB", Offset = 20, Size = 2048, IsInternalTarget = true },
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
