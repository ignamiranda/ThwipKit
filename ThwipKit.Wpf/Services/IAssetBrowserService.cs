using System.Diagnostics;
using ThwipKit.Core;
using ThwipKit.Core.Assets;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;

namespace ThwipKit.Wpf.Services;

public interface IAssetBrowserService
{
    IReadOnlyList<AssetInfo> GetAllAssets(string gamePath);
    void ExtractAsset(AssetInfo asset, string gamePath);
    void ReplaceAsset(AssetInfo asset, string gamePath, string replacementFilePath);
    void OpenAsset(AssetInfo asset, string gamePath);
}

public sealed class AssetBrowserService : IAssetBrowserService
{
    private readonly AssetBrowser _browser;
    private readonly GameBase _game;
    private readonly string _projectRoot;

    public AssetBrowserService(AssetBrowser browser, GameBase game, string projectRoot, IAssetTrackingSink? trackingSink = null)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
        TrackingSink = trackingSink;
    }

    public IAssetTrackingSink? TrackingSink { get; set; }

    public IReadOnlyList<AssetInfo> GetAllAssets(string gamePath)
        => _browser.GetAllAssets(gamePath);

    public void ExtractAsset(AssetInfo asset, string gamePath)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var stageManager = new StageManager(_game, _projectRoot);
        var archiveManager = new ArchiveManager(_game);
        var extractionService = new ExtractionService(_game, stageManager, archiveManager, TrackingSink);
        extractionService.ExtractSingleAsset(gamePath, asset.AssetId, asset.Type.ToStageFolderName());
    }

    public void ReplaceAsset(AssetInfo asset, string gamePath, string replacementFilePath)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!System.IO.File.Exists(replacementFilePath))
        {
            throw new System.IO.FileNotFoundException("Replacement file not found", replacementFilePath);
        }

        var stageManager = new StageManager(_game, _projectRoot);
        var archiveManager = new ArchiveManager(_game);
        var backupSystem = new BackupSystem(gamePath, System.IO.Path.Combine(_projectRoot, "backups"));
        var replacementService = new ReplacementService(_game, stageManager, archiveManager, backupSystem, TrackingSink);
        replacementService.ReplaceAsset(gamePath, asset, replacementFilePath);
    }

    public void OpenAsset(AssetInfo asset, string gamePath)
    {
        ArgumentNullException.ThrowIfNull(asset);

        ExtractAsset(asset, gamePath);

        var stageManager = new StageManager(_game, _projectRoot);
        var archiveManager = new ArchiveManager(_game);
        var extractionService = new ExtractionService(_game, stageManager, archiveManager, TrackingSink);
        string stagedPath = stageManager.GetAssetStagePath(
            asset.Type.ToStageFolderName(),
            extractionService.GetCanonicalRelativePath(asset));

        if (System.IO.File.Exists(stagedPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = stagedPath,
                UseShellExecute = true
            });
        }
    }
}
