namespace ThwipKit.Core.Assets;

public enum InternalTargetFilter
{
    All,
    InternalTargetsOnly,
    NonInternalTargetsOnly
}

public static class AssetFilters
{
    public static bool MatchesInternalTarget(AssetInfo asset, InternalTargetFilter filter) =>
        filter switch
        {
            InternalTargetFilter.InternalTargetsOnly => asset.IsInternalTarget,
            InternalTargetFilter.NonInternalTargetsOnly => !asset.IsInternalTarget,
            _ => true
        };
}
