using ThwipKit.Core.Assets;

namespace ThwipKit.Core.Staging;

/// <summary>
/// Bridges the asset-tracking sink that staging services call
/// (<see cref="IAssetTrackingSink"/>) to a project tracker
/// (<see cref="IProjectTracker"/>) such as <see cref="ProjectManager"/>.
/// This decouples the services from the project system while still
/// recording extraction/replacement/deletion activity.
/// </summary>
public sealed class ProjectTrackingSink : IAssetTrackingSink
{
    private readonly IProjectTracker _tracker;

    public ProjectTrackingSink(IProjectTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public void OnAssetExtracted(ulong assetId, string stagePath, AssetInfo info)
        => _tracker.RecordExtraction(assetId, stagePath, info);

    public void OnAssetReplaced(ulong assetId, string stagePath, string? replacementSourcePath, AssetInfo info)
        => _tracker.RecordReplacement(assetId, stagePath, replacementSourcePath, info);

    public void OnAssetDeleted(ulong assetId, string stagePath)
        => _tracker.RecordDeletion(assetId, stagePath);
}
