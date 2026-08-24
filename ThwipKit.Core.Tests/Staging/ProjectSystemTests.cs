using System;
using System.IO;
using System.Linq;
using ThwipKit.Core.Assets;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Core.Staging;
using Xunit;

namespace ThwipKit.Core.Tests;

public class ProjectSystemTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StageManager _stageManager;
    private readonly ProjectSystem _projectSystem;

    public ProjectSystemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        GameBase game = new ConfiguredGame(new GameDefinition
        {
            InternalId = "MSMR",
            ExecutableName = "Spider-Man Remastered.exe",
            ArchiveDirectory = "asset_archive",
            TocFileName = "TOC",
            TocFormat = TocFormat.ZlibDat1,
            CompressionFormats = [CompressionFormat.Lz4],
        });

        _stageManager = new StageManager(game, _tempDir);
        _projectSystem = new ProjectSystem(_stageManager, new AssetBrowser(game), Path.Combine(_tempDir, "projects"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void CreateProject_CreatesProjectFile()
    {
        _projectSystem.CreateProject("TestProject");

        Assert.True(_projectSystem.ProjectExists("TestProject"));
        Assert.True(File.Exists(_projectSystem.GetProjectFilePath("TestProject")));
    }

    [Fact]
    public void CreateProject_IsIdempotent()
    {
        _projectSystem.CreateProject("TestProject");
        _projectSystem.UpdateProjectMetadata("TestProject", description: "custom");

        // Second create must not clobber existing metadata
        _projectSystem.CreateProject("TestProject");

        ProjectInfo project = _projectSystem.LoadProject("TestProject");
        Assert.Equal("custom", project.Description);
    }

    [Fact]
    public void LoadProject_ReturnsMetadata()
    {
        _projectSystem.CreateProject("Meta", targetGame: "MSM2");
        ProjectInfo project = _projectSystem.LoadProject("Meta");

        Assert.Equal("Meta", project.Name);
        Assert.Equal("MSM2", project.TargetGame);
        Assert.NotEqual(default, project.CreatedUtc);
    }

    [Fact]
    public void LoadProject_NonExistent_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => _projectSystem.LoadProject("Missing"));
    }

    [Fact]
    public void UpdateProjectMetadata_ModifiesFields()
    {
        _projectSystem.CreateProject("Upd");
        DateTime before = _projectSystem.LoadProject("Upd").ModifiedUtc;

        _projectSystem.UpdateProjectMetadata("Upd", description: "desc", modFormat: "smpcmod", gameVersion: "2.0");

        ProjectInfo updated = _projectSystem.LoadProject("Upd");
        Assert.Equal("desc", updated.Description);
        Assert.Equal("smpcmod", updated.ModFormat);
        Assert.Equal("2.0", updated.GameVersion);
        Assert.True(updated.ModifiedUtc >= before);
    }

    [Fact]
    public void DeleteProject_RemovesDirectoryAndFile()
    {
        _projectSystem.CreateProject("Doomed");
        Assert.True(_projectSystem.ProjectExists("Doomed"));

        _projectSystem.DeleteProject("Doomed");

        Assert.False(Directory.Exists(_projectSystem.GetProjectPath("Doomed")));
        Assert.False(_projectSystem.ProjectExists("Doomed"));
    }

    [Fact]
    public void RenameProject_MovesDirectoryAndUpdatesName()
    {
        _projectSystem.CreateProject("OldName");
        _projectSystem.AddStageOperation("OldName", new StageOperation { OperationType = "extract", AssetPath = "a.bin" });

        _projectSystem.RenameProject("OldName", "NewName");

        Assert.False(_projectSystem.ProjectExists("OldName"));
        Assert.True(_projectSystem.ProjectExists("NewName"));
        Assert.Equal("NewName", _projectSystem.LoadProject("NewName").Name);
        Assert.Single(_projectSystem.GetStageOperations("NewName"));
    }

    [Fact]
    public void RenameProject_TargetExists_Throws()
    {
        _projectSystem.CreateProject("A");
        _projectSystem.CreateProject("B");

        Assert.Throws<IOException>(() => _projectSystem.RenameProject("A", "B"));
    }

    [Fact]
    public void AddAssetTracking_TracksExtractedAsset()
    {
        _projectSystem.CreateProject("Track");

        var entry = new AssetTrackingEntry
        {
            OriginalGamePath = "textures/suit_a.texture",
            StagePath = "stages/MSMR/textures/suit_a.texture",
            AssetId = 0x1122334455667788UL,
            OriginalSizeBytes = 1024
        };
        _projectSystem.AddAssetTracking("Track", entry);

        ProjectInfo project = _projectSystem.LoadProject("Track");
        Assert.Single(project.ExtractedAssets);
        Assert.Equal(entry.OriginalGamePath, project.ExtractedAssets[0].OriginalGamePath);
    }

    [Fact]
    public void AddAssetTracking_ReplacesDuplicateByOriginalPath()
    {
        _projectSystem.CreateProject("Dedupe");

        _projectSystem.AddAssetTracking("Dedupe", new AssetTrackingEntry
        {
            OriginalGamePath = "same.path",
            StagePath = "stage_v1"
        });
        _projectSystem.AddAssetTracking("Dedupe", new AssetTrackingEntry
        {
            OriginalGamePath = "same.path",
            StagePath = "stage_v2"
        });

        ProjectInfo project = _projectSystem.LoadProject("Dedupe");
        Assert.Single(project.ExtractedAssets);
        Assert.Equal("stage_v2", project.ExtractedAssets[0].StagePath);
    }

    [Fact]
    public void AddStageOperation_PersistsOperations()
    {
        _projectSystem.CreateProject("Ops");

        _projectSystem.AddStageOperation("Ops", new StageOperation { OperationType = "extract" });
        _projectSystem.AddStageOperation("Ops", new StageOperation { OperationType = "replace" });

        var ops = _projectSystem.GetStageOperations("Ops");
        Assert.Equal(2, ops.Count);
        Assert.Equal("extract", ops[0].OperationType);
        Assert.Equal("replace", ops[1].OperationType);
    }

    [Fact]
    public void ListProjects_ReturnsOnlyValidProjects()
    {
        _projectSystem.CreateProject("Valid1");
        _projectSystem.CreateProject("Valid2");

        // A directory without .smproj should not count
        Directory.CreateDirectory(Path.Combine(_projectSystem.ProjectsRoot, "Invalid"));

        var projects = _projectSystem.ListProjects().ToList();

        Assert.Contains("Valid1", projects);
        Assert.Contains("Valid2", projects);
        Assert.DoesNotContain("Invalid", projects);
    }

    [Fact]
    public void CreateProject_NullOrEmpty_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => _projectSystem.CreateProject(""));
        Assert.ThrowsAny<ArgumentException>(() => _projectSystem.CreateProject(null!));
    }
}
