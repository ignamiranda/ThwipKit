using ThwipKit.Core.Assets;
using ThwipKit.Core.Staging;
using Xunit;

namespace ThwipKit.Core.Tests;

public sealed class AssetSearchTests
{
    private static AssetInfo Asset(string name) =>
        new() { AssetId = 1, ResolvedName = name };

    [Fact]
    public void AndSearchMatchesOnlyAssetsContainingAllTerms()
    {
        AssetInfo[] assets =
        [
            Asset("characters/hero.texture"),
            Asset("materials/web.material"),
            Asset("enemies/villain.texture")
        ];

        Func<AssetInfo, bool> predicate = AssetSearch.Compile("hero texture");

        List<AssetInfo> result = assets.Where(predicate).ToList();
        Assert.Single(result);
        Assert.Equal("characters/hero.texture", result[0].ResolvedName);
    }

    [Fact]
    public void OrSearchMatchesEitherTerm()
    {
        AssetInfo[] assets =
        [
            Asset("characters/hero.texture"),
            Asset("materials/web.material"),
            Asset("enemies/villain.texture")
        ];

        Func<AssetInfo, bool> predicate = AssetSearch.Compile("hero OR material");

        List<AssetInfo> result = assets.Where(predicate).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void EmptyQueryMatchesAll()
    {
        AssetInfo[] assets = [Asset("a.texture"), Asset("b.material")];

        Func<AssetInfo, bool> predicate = AssetSearch.Compile("   ");

        Assert.Equal(2, assets.Count(predicate));
    }

    [Fact]
    public void FuzzySearchToleratesTypos()
    {
        AssetInfo[] assets = [Asset("characters/hero.texture"), Asset("materials/web.material")];

        Func<AssetInfo, bool> predicate = AssetSearch.Compile("herp");

        List<AssetInfo> result = assets.Where(predicate).ToList();
        Assert.Single(result);
        Assert.Equal("characters/hero.texture", result[0].ResolvedName);
    }
}

public sealed class AssetClassifierTests
{
    [Theory]
    [InlineData("characters/hero.texture", AssetType.Texture)]
    [InlineData("hero.dds", AssetType.Texture)]
    [InlineData("hero.png", AssetType.Texture)]
    [InlineData("model.character.model", AssetType.Model)]
    [InlineData("char.mesh", AssetType.Model)]
    [InlineData("char.skeleton", AssetType.Model)]
    [InlineData("surface.material", AssetType.Material)]
    [InlineData("surface.mat", AssetType.Material)]
    [InlineData("settings.config", AssetType.Config)]
    [InlineData("voices/line.wem", AssetType.Audio)]
    [InlineData("voices/line.bnk", AssetType.Audio)]
    [InlineData("mystery.unknown", AssetType.Unknown)]
    public void ClassifyMapsExtensionToType(string name, AssetType expected)
    {
        var asset = new AssetInfo { ResolvedName = name };

        Assert.Equal(expected, AssetClassifier.Classify(asset));
    }

    [Fact]
    public void ClassifyPrefersExistingType()
    {
        var asset = new AssetInfo { ResolvedName = "x.unknown", Type = AssetType.Texture };

        Assert.Equal(AssetType.Texture, AssetClassifier.Classify(asset));
    }
}
