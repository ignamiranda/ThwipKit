using ThwipKit.Core.Assets;

namespace ThwipKit.Wpf.Services;

public interface IAssetBrowserService
{
    IReadOnlyList<AssetInfo> GetAllAssets(string gamePath);
}

public sealed class AssetBrowserService(AssetBrowser browser) : IAssetBrowserService
{
    public IReadOnlyList<AssetInfo> GetAllAssets(string gamePath) => browser.GetAllAssets(gamePath);
}
