using System;
using System.Collections.Generic;
using System.IO;
using ThwipKit.Core.Assets;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Staging;

public sealed class ReplacementService
{
    private readonly GameBase _game;
    private readonly StageManager _stageManager;
    private readonly ArchiveManager _archiveManager;
    private readonly BackupSystem _backupSystem;
    private readonly AssetBrowser _assetBrowser;
    private readonly List<StagedReplacement> _stagedReplacements = [];

    public ReplacementService(
        GameBase game,
        StageManager stageManager,
        ArchiveManager archiveManager,
        BackupSystem backupSystem)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
        _archiveManager = archiveManager ?? throw new ArgumentNullException(nameof(archiveManager));
        _backupSystem = backupSystem ?? throw new ArgumentNullException(nameof(backupSystem));
        _assetBrowser = new AssetBrowser(game);
    }

    public IReadOnlyList<StagedReplacement> StagedReplacements => _stagedReplacements.AsReadOnly();

    public void StageReplacement(string gamePath, ulong assetId, string replacementFilePath)
    {
        // Validate the replacement file exists
        if (!File.Exists(replacementFilePath))
        {
            throw new FileNotFoundException("Replacement file not found", replacementFilePath);
        }

        // Get asset info
        AssetInfo? asset = _assetBrowser.GetAsset(gamePath, assetId);
        if (asset == null)
        {
            throw new InvalidDataException($"Asset 0x{assetId:X16} not found in game");
        }

        string archivePath = Path.Combine(gamePath, _game.ArchiveDirectory, asset.ArchiveName);

        // Create backup of original
        _backupSystem.CreateAssetBackup(archivePath, $"asset_{asset.AssetIdHex}");

        // Add to staged replacements
        _stagedReplacements.Add(new StagedReplacement
        {
            AssetId = assetId,
            AssetIdHex = asset.AssetIdHex,
            OriginalPath = archivePath,
            OriginalOffset = asset.Offset,
            OriginalSize = asset.Size,
            ReplacementPath = replacementFilePath,
            Timestamp = DateTime.UtcNow,
            AssetName = asset.ResolvedName ?? asset.AssetIdHex
        });
    }

    public void StageReplacementFromStage(string gamePath, ulong assetId, string stagePath)
    {
        if (!File.Exists(stagePath))
        {
            throw new FileNotFoundException("Stage file not found", stagePath);
        }

        StageReplacement(gamePath, assetId, stagePath);
    }

    public bool CommitStagedReplacement(StagedReplacement replacement)
    {
        try
        {
            byte[] replacementData = File.ReadAllBytes(replacement.ReplacementPath);
            _archiveManager.WriteToDsar(
                replacement.OriginalPath,
                replacement.OriginalOffset,
                replacement.OriginalSize,
                replacementData);
            _stagedReplacements.Remove(replacement);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void CommitAllStagedReplacements()
    {
        foreach (StagedReplacement replacement in _stagedReplacements.ToArray())
        {
            CommitStagedReplacement(replacement);
        }
    }

    public void UndoStagedReplacement(StagedReplacement replacement)
    {
        // Restore from backup
        string backupName = $"asset_{replacement.AssetIdHex}";
        if (_backupSystem.RestoreAsset(replacement.OriginalPath, backupName))
        {
            _stagedReplacements.Remove(replacement);
        }
    }

    public void UndoAllStagedReplacements()
    {
        foreach (StagedReplacement replacement in _stagedReplacements.ToArray())
        {
            UndoStagedReplacement(replacement);
        }
    }

    public void ClearAllStagedReplacements()
    {
        _stagedReplacements.Clear();
    }

    public void ReplaceAsset(string gamePath, AssetInfo asset, string replacementFilePath)
    {
        if (!File.Exists(replacementFilePath))
        {
            throw new FileNotFoundException("Replacement file not found", replacementFilePath);
        }

        string archivePath = Path.Combine(gamePath, _game.ArchiveDirectory, asset.ArchiveName);
        _backupSystem.CreateAssetBackup(archivePath, $"asset_{asset.AssetIdHex}");

        byte[] replacementData = File.ReadAllBytes(replacementFilePath);
        _archiveManager.WriteToDsar(archivePath, asset.Offset, asset.Size, replacementData);
    }

    public PreviewResult PreviewReplacement(StagedReplacement replacement)
    {
        return new PreviewResult
        {
            AssetName = replacement.AssetName,
            OriginalSize = replacement.OriginalSize,
            ReplacementSize = GetFileSize(replacement.ReplacementPath),
            CanPreview = true,
            PreviewMessage = $"Replacing {replacement.AssetName} ({replacement.OriginalSize} bytes -> {GetFileSize(replacement.ReplacementPath)} bytes)"
        };
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }
}

public sealed class StagedReplacement
{
    public ulong AssetId { get; set; }
    public string AssetIdHex { get; set; } = string.Empty;
    public required string OriginalPath { get; set; }
    public required string ReplacementPath { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public uint OriginalOffset { get; set; }
    public uint OriginalSize { get; set; }
}

public sealed class PreviewResult
{
    public string AssetName { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public long ReplacementSize { get; set; }
    public bool CanPreview { get; set; }
    public string PreviewMessage { get; set; } = string.Empty;
}
