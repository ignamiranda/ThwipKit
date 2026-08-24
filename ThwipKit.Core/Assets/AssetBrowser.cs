using ThwipKit.Core.Games;

namespace ThwipKit.Core.Assets;

public class AssetBrowser
{
    private readonly AssetCatalog _catalog;

    public AssetBrowser(GameBase game)
    {
        ArgumentNullException.ThrowIfNull(game);
        _catalog = new AssetCatalog(game);
    }

    public IReadOnlyList<AssetInfo> GetAllAssets(string gamePath)
        => _catalog.GetAssets(gamePath);

    public AssetInfo? GetAsset(string gamePath, ulong assetId)
        => GetAllAssets(gamePath).FirstOrDefault(asset => asset.AssetId == assetId);

    public IReadOnlyList<AssetInfo> SearchByName(string gamePath, string namePattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namePattern);

        return GetAllAssets(gamePath)
            .Where(asset => asset.AssetIdHex.Contains(namePattern, StringComparison.OrdinalIgnoreCase)
                || (asset.ResolvedName?.Contains(namePattern, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    public IReadOnlyList<AssetInfo> SearchByArchive(string gamePath, string archiveName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveName);

        return GetAllAssets(gamePath)
            .Where(asset => string.Equals(asset.ArchiveName, archiveName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<AssetInfo> FilterBySize(IReadOnlyList<AssetInfo> assets, uint minSize, uint maxSize)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return assets.Where(asset => asset.Size >= minSize && asset.Size <= maxSize).ToList();
    }

    public IEnumerable<string> GetArchiveNames(string gamePath)
        => GetAllAssets(gamePath)
            .Select(asset => asset.ArchiveName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public int GetAssetCount(string gamePath)
        => GetAllAssets(gamePath).Count;
}
