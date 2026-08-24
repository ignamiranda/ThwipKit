using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThwipKit.Core.Assets;
using ThwipKit.Core.Games;
using ThwipKit.Core.Sections;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Staging;

public sealed class ExtractionService
{
    private readonly GameBase _game;
    private readonly StageManager _stageManager;
    private readonly ArchiveManager _archiveManager;
    private readonly AssetBrowser _assetBrowser;

    public ExtractionService(GameBase game, StageManager stageManager, ArchiveManager archiveManager)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
        _archiveManager = archiveManager ?? throw new ArgumentNullException(nameof(archiveManager));
        _assetBrowser = new AssetBrowser(game);
    }

    public void ExtractSingleAsset(string gamePath, ulong assetId, string assetType = "texture")
    {
        AssetInfo? asset = _assetBrowser.GetAsset(gamePath, assetId);
        if (asset == null)
        {
            throw new InvalidDataException($"Asset ID 0x{assetId:X16} not found in TOC");
        }

        string relativePath = $"{asset.ArchiveName}_offset_{asset.Offset}";
        _stageManager.EnsureStageDirectoryExistsForAsset(assetType, relativePath);

        string archivePath = Path.Combine(gamePath, _game.ArchiveDirectory, asset.ArchiveName);
        byte[] assetData = _archiveManager.ReadFromDsar(archivePath, asset.Offset, asset.Size);

        string stagePath = _stageManager.GetAssetStagePath(assetType, relativePath);
        File.WriteAllBytes(stagePath, assetData);
    }

    public void ExtractMultipleAssets(string gamePath, IEnumerable<ulong> assetIds, string assetType = "texture")
    {
        foreach (ulong assetId in assetIds)
        {
            ExtractSingleAsset(gamePath, assetId, assetType);
        }
    }

    public void ExtractAllFromArchive(string gamePath, string archiveName, string assetType = "texture")
    {
        IReadOnlyList<AssetInfo> assets = _assetBrowser.SearchByArchive(gamePath, archiveName);
        foreach (AssetInfo asset in assets)
        {
            ExtractSingleAsset(gamePath, asset.AssetId, assetType);
        }
    }

    public void ExtractAllAssets(string gamePath, string assetType = "texture")
    {
        IReadOnlyList<AssetInfo> assets = _assetBrowser.GetAllAssets(gamePath);
        foreach (AssetInfo asset in assets)
        {
            ExtractSingleAsset(gamePath, asset.AssetId, assetType);
        }
    }
}