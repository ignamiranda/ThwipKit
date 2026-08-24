using System;
using System.IO;
using System.Linq;
using Xunit;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Tests;

public class StageManagerTests : IDisposable
{
    private readonly GameBase _game;
    private readonly string _projectRoot;

    public StageManagerTests()
    {
        _game = CreateTestGame();
        _projectRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_projectRoot, true);
        }
        catch
        {
            // Best effort
        }
    }

    private static GameBase CreateTestGame()
    {
        return new ConfiguredGame(new GameDefinition
        {
            InternalId = "MSMR",
            ExecutableName = "Spider-Man Remastered.exe",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC",
            TocFormat = TocFormat.ZlibDat1,
            CompressionFormats = [CompressionFormat.Lz4],
        });
    }

    [Fact]
    public void GetStageRoot_ReturnsCorrectPath()
    {
        var manager = new StageManager(_game, _projectRoot);
        string expected = Path.Combine(_projectRoot, "stages");
        Assert.Equal(expected, manager.GetStageRoot());
    }

    [Fact]
    public void GetGameStageRoot_ReturnsCorrectPath()
    {
        var manager = new StageManager(_game, _projectRoot);
        string expected = Path.Combine(_projectRoot, "stages", _game.InternalId);
        Assert.Equal(expected, manager.GetGameStageRoot());
    }

    [Fact]
    public void GetAssetTypeStagePath_ReturnsCorrectPath()
    {
        var manager = new StageManager(_game, _projectRoot);
        string expected = Path.Combine(_projectRoot, "stages", _game.InternalId, "textures");
        Assert.Equal(expected, manager.GetAssetTypeStagePath("textures"));
    }

    [Fact]
    public void GetAssetStagePath_ReturnsCorrectPath()
    {
        var manager = new StageManager(_game, _projectRoot);
        string expected = Path.Combine(_projectRoot, "stages", _game.InternalId, "textures", "test.png");
        Assert.Equal(expected, manager.GetAssetStagePath("textures", "test.png"));
    }

    [Fact]
    public void GetAssetStagePath_HandlesLeadingSlashes()
    {
        var manager = new StageManager(_game, _projectRoot);
        string expected = Path.Combine(_projectRoot, "stages", _game.InternalId, "textures", "test.png");
        Assert.Equal(expected, manager.GetAssetStagePath("textures", "/test.png"));
    }

    [Fact]
    public void EnsureStageDirectoryExists_CreatesDirectory()
    {
        var manager = new StageManager(_game, _projectRoot);
        string assetTypePath = manager.GetAssetTypeStagePath("models");

        Assert.False(Directory.Exists(assetTypePath));
        manager.EnsureStageDirectoryExists("models");
        Assert.True(Directory.Exists(assetTypePath));
    }

    [Fact]
    public void EnsureStageDirectoryExistsForAsset_CreatesParentDirectories()
    {
        var manager = new StageManager(_game, _projectRoot);
        string stagePath = manager.GetAssetStagePath("textures", "subdir/test.png");
        string? directory = Path.GetDirectoryName(stagePath);

        Assert.False(Directory.Exists(directory));
        manager.EnsureStageDirectoryExistsForAsset("textures", "subdir/test.png");
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void GetStagedAssetPaths_ReturnsEmptyForNonExistentDirectory()
    {
        var manager = new StageManager(_game, _projectRoot);
        var paths = manager.GetStagedAssetPaths("textures");
        Assert.Empty(paths);
    }

    [Fact]
    public void GetStagedAssetPaths_ReturnsFilesInDirectory()
    {
        var manager = new StageManager(_game, _projectRoot);
        string assetTypePath = manager.GetAssetTypeStagePath("textures");
        Directory.CreateDirectory(assetTypePath);

        string file1 = Path.Combine(assetTypePath, "test1.png");
        string file2 = Path.Combine(assetTypePath, "test2.png");
        File.WriteAllBytes(file1, [0x01]);
        File.WriteAllBytes(file2, [0x02]);

        var paths = manager.GetStagedAssetPaths("textures");
        Assert.Contains(file1, paths);
        Assert.Contains(file2, paths);
    }

    [Fact]
    public void CleanAssetTypeStage_RemovesDirectory()
    {
        var manager = new StageManager(_game, _projectRoot);
        string assetTypePath = manager.GetAssetTypeStagePath("temp");
        Directory.CreateDirectory(assetTypePath);
        File.WriteAllBytes(Path.Combine(assetTypePath, "test.txt"), [0x01]);

        Assert.True(Directory.Exists(assetTypePath));
        manager.CleanAssetTypeStage("temp");
        Assert.False(Directory.Exists(assetTypePath));
    }

    [Fact]
    public void CleanGameStage_RemovesAllGameStages()
    {
        var manager = new StageManager(_game, _projectRoot);
        string gameStageRoot = manager.GetGameStageRoot();
        Directory.CreateDirectory(gameStageRoot);
        Directory.CreateDirectory(Path.Combine(gameStageRoot, "textures"));
        Directory.CreateDirectory(Path.Combine(gameStageRoot, "models"));

        Assert.True(Directory.Exists(gameStageRoot));
        manager.CleanGameStage();
        Assert.False(Directory.Exists(gameStageRoot));
    }

    [Fact]
    public void CleanAllStages_RemovesAllStages()
    {
        var manager = new StageManager(_game, _projectRoot);
        string stageRoot = manager.GetStageRoot();
        Directory.CreateDirectory(stageRoot);
        Directory.CreateDirectory(Path.Combine(stageRoot, "game1"));
        Directory.CreateDirectory(Path.Combine(stageRoot, "game2"));

        Assert.True(Directory.Exists(stageRoot));
        manager.CleanAllStages();
        Assert.False(Directory.Exists(stageRoot));
    }

    [Fact]
    public void AssetType_ToStageFolderName_ReturnsCorrectNames()
    {
        Assert.Equal("textures", AssetType.Texture.ToStageFolderName());
        Assert.Equal("models", AssetType.Model.ToStageFolderName());
        Assert.Equal("materials", AssetType.Material.ToStageFolderName());
        Assert.Equal("configs", AssetType.Config.ToStageFolderName());
        Assert.Equal("audio", AssetType.Audio.ToStageFolderName());
        Assert.Equal("unknown", AssetType.Unknown.ToStageFolderName());
    }
}