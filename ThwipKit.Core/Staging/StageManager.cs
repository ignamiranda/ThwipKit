using System;
using System.Collections.Generic;
using System.IO;
using ThwipKit.Core.Games;

namespace ThwipKit.Core.Staging;

public sealed class StageManager
{
    private readonly GameBase _game;
    private readonly string _projectRoot;

    public StageManager(GameBase game, string projectRoot)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _projectRoot = Path.GetFullPath(projectRoot ?? throw new ArgumentNullException(nameof(projectRoot)));
    }

    public string GetStageRoot() => Path.Combine(_projectRoot, "stages");

    public string GetGameStageRoot() => Path.Combine(GetStageRoot(), _game.InternalId);

    public string GetAssetTypeStagePath(string assetType) => Path.Combine(GetGameStageRoot(), assetType);

    public string GetAssetStagePath(string assetType, string relativePath)
    {
        string assetTypePath = GetAssetTypeStagePath(assetType);
        return Path.Combine(assetTypePath, relativePath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public void EnsureStageDirectoryExists(string assetType)
    {
        string path = GetAssetTypeStagePath(assetType);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public void EnsureStageDirectoryExistsForAsset(string assetType, string relativePath)
    {
        string fullPath = GetAssetStagePath(assetType, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public IEnumerable<string> GetStagedAssetPaths(string assetType)
    {
        string assetTypePath = GetAssetTypeStagePath(assetType);
        if (!Directory.Exists(assetTypePath))
        {
            return [];
        }

        return Directory.GetFiles(assetTypePath, "*", SearchOption.AllDirectories);
    }

    public void CleanAssetTypeStage(string assetType)
    {
        string assetTypePath = GetAssetTypeStagePath(assetType);
        if (Directory.Exists(assetTypePath))
        {
            Directory.Delete(assetTypePath, recursive: true);
        }
    }

    public void CleanGameStage()
    {
        string gameStageRoot = GetGameStageRoot();
        if (Directory.Exists(gameStageRoot))
        {
            Directory.Delete(gameStageRoot, recursive: true);
        }
    }

    public void CleanAllStages()
    {
        string stageRoot = GetStageRoot();
        if (Directory.Exists(stageRoot))
        {
            Directory.Delete(stageRoot, recursive: true);
        }
    }
}

public enum AssetType
{
    Texture,
    Model,
    Material,
    Config,
    Audio,
    Unknown
}

public static class AssetTypeExtensions
{
    public static string ToStageFolderName(this AssetType type) => type switch
    {
        AssetType.Texture => "textures",
        AssetType.Model => "models",
        AssetType.Material => "materials",
        AssetType.Config => "configs",
        AssetType.Audio => "audio",
        AssetType.Unknown => "unknown",
        _ => "unknown"
    };
}