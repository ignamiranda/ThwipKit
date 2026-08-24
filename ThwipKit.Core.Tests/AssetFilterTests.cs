using ThwipKit.Core.Assets;
using Xunit;

namespace ThwipKit.Core.Tests;

public class AssetFilterTests
{
    [Theory]
    [InlineData(InternalTargetFilter.All, true, true)]
    [InlineData(InternalTargetFilter.All, false, true)]
    [InlineData(InternalTargetFilter.InternalTargetsOnly, true, true)]
    [InlineData(InternalTargetFilter.InternalTargetsOnly, false, false)]
    [InlineData(InternalTargetFilter.NonInternalTargetsOnly, true, false)]
    [InlineData(InternalTargetFilter.NonInternalTargetsOnly, false, true)]
    public void MatchesInternalTarget_FiltersByFlag(InternalTargetFilter filter, bool isInternalTarget, bool expected)
    {
        var asset = new AssetInfo { IsInternalTarget = isInternalTarget };

        Assert.Equal(expected, AssetFilters.MatchesInternalTarget(asset, filter));
    }
}
