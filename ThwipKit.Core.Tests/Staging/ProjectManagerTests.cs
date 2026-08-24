using System;
using System.IO;
using System.Linq;
using ThwipKit.Core.Assets;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;
using Xunit;

namespace ThwipKit.Core.Tests.Staging;

public class ProjectManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _projectsRoot;
    private readonly ProjectManager _projectManager;
    private readonly GameBase _game;

    public ProjectManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "thwip-project-tests-" + Guid.NewGuid().ToString("N"));
        _projectsRoot = Path.Combine(_tempDir, "projects");
        Directory.CreateDirectory(_projectsRoot);

        _game = new ConfiguredGame(new GameDefinition
        {
            InternalId = "TEST",
            DisplayName = "Test Game",
            ArchiveDirectory = "archives"
        });

        var stageManager = new StageManager(_game, Path.Combine(_tempDir, "stage"));
        var assetBrowser = new AssetBrowser(_game);
        _projectManager = new ProjectManager(stageManager, assetBrowser, _projectsRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CreateProject_WritesProjectFile()
    {
        _projectManager.CreateProject("Alpha", targetGame: "MSMR", description: "desc", gameVersion: "1.2.3");
        Assert.True(_projectManager.ProjectExists("Alpha"));
        Assert.True(File.Exists(_projectManager.GetProjectFilePath("Alpha")));
    }

    [Fact]
    public void OpenProject_ThenCurrent_ReturnsMetadata()
    {
        _projectManager.CreateProject("Beta", targetGame: "MM", description: "beta desc");
        _projectManager.OpenProject("Beta");

        Assert.True(_projectManager.IsOpen);
        Assert.Equal("Beta", _projectManager.CurrentName);
        Assert.Equal("MM", _projectManager.Current.Metadata.TargetGame);
        Assert.Equal("beta desc", _projectManager.Current.Metadata.Description);
    }

    [Fact]
    public void OpenProject_NoSuchProject_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => _projectManager.OpenProject("Ghost"));
    }

    [Fact]
    public void UpdateMetadata_MarksDirty()
    {
        _projectManager.CreateProject("Dirty");
        _projectManager.OpenProject("Dirty");
        Assert.False(_projectManager.IsDirty);

        _projectManager.UpdateMetadata(description: "updated", targetGame: "RCRA");
        Assert.True(_projectManager.IsDirty);
        Assert.Equal("updated", _projectManager.Current.Metadata.Description);
        Assert.Equal("RCRA", _projectManager.Current.Metadata.TargetGame);
    }

    [Fact]
    public void Save_PersistsAndClearsDirty()
    {
        _projectManager.CreateProject("Persist");
        _projectManager.OpenProject("Persist");
        _projectManager.UpdateMetadata(description: "v2");
        Assert.True(_projectManager.IsDirty);

        _projectManager.Save();
        Assert.False(_projectManager.IsDirty);

        var reloaded = new ProjectManager(
            new StageManager(_game, Path.Combine(_tempDir, "stage")),
            new AssetBrowser(_game),
            _projectsRoot);
        reloaded.OpenProject("Persist");
        Assert.Equal("v2", reloaded.Current.Metadata.Description);
    }

    [Fact]
    public void RecordExtraction_AddsTrackedAsset()
    {
        _projectManager.CreateProject("Track");
        _projectManager.OpenProject("Track");

        var info = new AssetInfo
        {
            AssetId = 0x1234,
            ResolvedName = "hero/suit.texture",
            ArchiveName = "asset_archive",
            Offset = 100,
            Size = 200,
            Type = AssetType.Texture
        };

        _projectManager.RecordExtraction(0x1234, "TEST/textures/hero/suit.texture", info);

        TrackedAsset? asset = _projectManager.GetTrackedAsset(0x1234);
        Assert.NotNull(asset);
        Assert.Equal(TrackedAssetStatus.Extracted, asset!.Status);
        Assert.Equal("TEST/textures/hero/suit.texture", asset.RelativePath);
        Assert.True(_projectManager.IsDirty);
    }

    [Fact]
    public void RecordReplacement_MarksModified()
    {
        _projectManager.CreateProject("Replace");
        _projectManager.OpenProject("Replace");

        var info = new AssetInfo
        {
            AssetId = 0x99,
            ResolvedName = "ui/icon.texture",
            ArchiveName = "asset_archive",
            Offset = 50,
            Size = 75,
            Type = AssetType.Texture
        };

        _projectManager.RecordExtraction(0x99, "TEST/textures/ui/icon.texture", info);
        _projectManager.RecordReplacement(0x99, "TEST/textures/ui/icon.texture", "C:/src/icon.png", info);

        TrackedAsset? asset = _projectManager.GetTrackedAsset(0x99);
        Assert.Equal(TrackedAssetStatus.Modified, asset!.Status);
        Assert.Equal("C:/src/icon.png", asset.ReplacementSourcePath);
    }

    [Fact]
    public void AddReference_AndRemoveReference()
    {
        _projectManager.CreateProject("Refs");
        _projectManager.OpenProject("Refs");

        _projectManager.AddReference(new ProjectReference { Name = "BaseMod", Version = "1.0" });
        Assert.Contains(_projectManager.Current.References, r => r.Name == "BaseMod");

        _projectManager.RemoveReference("BaseMod");
        Assert.DoesNotContain(_projectManager.Current.References, r => r.Name == "BaseMod");
    }

    [Fact]
    public void ListProjects_ReturnsCreatedProjects()
    {
        _projectManager.CreateProject("One");
        _projectManager.CreateProject("Two");

        var projects = _projectManager.ListProjects().ToList();
        Assert.Contains("One", projects);
        Assert.Contains("Two", projects);
    }

    [Fact]
    public void CloseProject_WithoutSave_KeepsFileButNotOpen()
    {
        _projectManager.CreateProject("CloseMe");
        _projectManager.OpenProject("CloseMe");
        _projectManager.UpdateMetadata(description: "lost if not saved");

        _projectManager.CloseProject(saveIfDirty: false);
        Assert.False(_projectManager.IsOpen);

        var reloaded = new ProjectManager(
            new StageManager(_game, Path.Combine(_tempDir, "stage")),
            new AssetBrowser(_game),
            _projectsRoot);
        reloaded.OpenProject("CloseMe");
        Assert.NotEqual("lost if not saved", reloaded.Current.Metadata.Description);
    }

    [Fact]
    public void SinkBridge_RecordsExtractionThroughTracker()
    {
        _projectManager.CreateProject("Sink");
        _projectManager.OpenProject("Sink");

        var sink = new ProjectTrackingSink(_projectManager);
        var info = new AssetInfo
        {
            AssetId = 0xABCD,
            ResolvedName = "env/rock.texture",
            ArchiveName = "asset_archive",
            Offset = 10,
            Size = 20,
            Type = AssetType.Texture
        };

        sink.OnAssetExtracted(0xABCD, "TEST/textures/env/rock.texture", info);

        TrackedAsset? asset = _projectManager.GetTrackedAsset(0xABCD);
        Assert.NotNull(asset);
        Assert.Equal(TrackedAssetStatus.Extracted, asset!.Status);
        Assert.Equal("TEST/textures/env/rock.texture", asset.RelativePath);
    }

    [Fact]
    public void SinkBridge_NoOpenProject_IgnoresEvents()
    {
        var sink = new ProjectTrackingSink(_projectManager);
        var info = new AssetInfo
        {
            AssetId = 0x1,
            ResolvedName = "x",
            ArchiveName = "asset_archive",
            Offset = 1,
            Size = 1,
            Type = AssetType.Texture
        };

        sink.OnAssetExtracted(0x1, "rel", info);

        Assert.False(_projectManager.IsOpen);
        Assert.Null(_projectManager.GetTrackedAsset(0x1));
    }
}
