using ThwipKit.Core.Assets;

namespace ThwipKit.Core.Staging;

public interface IAssetTrackingSink
{
    void OnAssetExtracted(ulong assetId, string stagePath, AssetInfo info);
    void OnAssetReplaced(ulong assetId, string stagePath, string? replacementSourcePath, AssetInfo info);
    void OnAssetDeleted(ulong assetId, string stagePath);
}

public interface IProjectTracker
{
    void RecordExtraction(ulong assetId, string stagePath, AssetInfo info);
    void RecordReplacement(ulong assetId, string stagePath, string? replacementSourcePath, AssetInfo info);
    void RecordDeletion(ulong assetId, string stagePath);
    string GetStageAbsolutePath(TrackedAsset asset);
}