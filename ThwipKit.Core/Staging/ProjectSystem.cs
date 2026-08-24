using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ThwipKit.Core.Assets;

namespace ThwipKit.Core.Staging;

public sealed class ProjectSystem
{
    private readonly StageManager _stageManager;
    private readonly AssetBrowser _assetBrowser;

    public string ProjectsRoot { get; }

    public ProjectSystem(StageManager stageManager, AssetBrowser assetBrowser, string? projectsRoot = null)
    {
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
        _assetBrowser = assetBrowser ?? throw new ArgumentNullException(nameof(assetBrowser));
        ProjectsRoot = projectsRoot ?? Path.Combine(Environment.CurrentDirectory, "projects");
    }

    public string GetProjectPath(string projectName) => Path.Combine(ProjectsRoot, projectName);

    public string GetProjectFilePath(string projectName) => Path.Combine(GetProjectPath(projectName), $"{projectName}.smproj");

    public bool ProjectExists(string projectName) => File.Exists(GetProjectFilePath(projectName));

    public void EnsureProjectDirectory(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        string projectPath = GetProjectPath(projectName);
        if (!Directory.Exists(projectPath))
        {
            Directory.CreateDirectory(projectPath);
        }
    }

    public void CreateProject(string projectName, string targetGame = "MSMR")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        EnsureProjectDirectory(projectName);
        string projectFilePath = GetProjectFilePath(projectName);

        if (!File.Exists(projectFilePath))
        {
            var projectInfo = new ProjectInfo
            {
                Name = projectName,
                TargetGame = targetGame
            };

            SaveProjectInfo(projectFilePath, projectInfo);
        }
    }

    public ProjectInfo LoadProject(string projectName)
    {
        string projectFilePath = GetProjectFilePath(projectName);
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException("Project file not found", projectFilePath);
        }

        string json = File.ReadAllText(projectFilePath);
        return JsonSerializer.Deserialize<ProjectInfo>(json)
            ?? throw new InvalidDataException($"Project file '{projectFilePath}' could not be deserialized.");
    }

    public void DeleteProject(string projectName)
    {
        string projectPath = GetProjectPath(projectName);
        if (Directory.Exists(projectPath))
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    public void RenameProject(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        string oldProjectPath = GetProjectPath(oldName);
        string newProjectPath = GetProjectPath(newName);

        if (!Directory.Exists(oldProjectPath))
        {
            throw new DirectoryNotFoundException($"Project directory not found: {oldProjectPath}");
        }
        if (Directory.Exists(newProjectPath))
        {
            throw new IOException($"Target project directory already exists: {newProjectPath}");
        }

        // Load metadata before moving so we can update the name
        string oldProjectFilePath = Path.Combine(oldProjectPath, $"{oldName}.smproj");
        ProjectInfo? projectInfo = null;
        if (File.Exists(oldProjectFilePath))
        {
            projectInfo = JsonSerializer.Deserialize<ProjectInfo>(File.ReadAllText(oldProjectFilePath));
        }

        Directory.Move(oldProjectPath, newProjectPath);

        if (projectInfo != null)
        {
            string newProjectFilePath = GetProjectFilePath(newName);
            File.Delete(Path.Combine(newProjectPath, $"{oldName}.smproj"));
            projectInfo.Name = newName;
            projectInfo.ModifiedUtc = DateTime.UtcNow;
            SaveProjectInfo(newProjectFilePath, projectInfo);
        }
    }

    public void UpdateProjectMetadata(
        string projectName,
        string? name = null,
        string? description = null,
        string? targetGame = null,
        string? modFormat = null,
        string? gameVersion = null)
    {
        string projectFilePath = GetProjectFilePath(projectName);
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException("Project file not found", projectFilePath);
        }

        string json = File.ReadAllText(projectFilePath);
        var projectInfo = JsonSerializer.Deserialize<ProjectInfo>(json) ?? new ProjectInfo();

        if (name != null) projectInfo.Name = name;
        if (description != null) projectInfo.Description = description;
        if (targetGame != null) projectInfo.TargetGame = targetGame;
        if (modFormat != null) projectInfo.ModFormat = modFormat;
        if (gameVersion != null) projectInfo.GameVersion = gameVersion;
        projectInfo.ModifiedUtc = DateTime.UtcNow;

        SaveProjectInfo(projectFilePath, projectInfo);
    }

    private static void SaveProjectInfo(string projectFilePath, ProjectInfo projectInfo)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(projectInfo, options);
        File.WriteAllText(projectFilePath, json);
    }

    public void AddAssetTracking(string projectName, AssetTrackingEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ProjectInfo project = LoadProject(projectName);
        project.ExtractedAssets.RemoveAll(a => a.OriginalGamePath == entry.OriginalGamePath);
        project.ExtractedAssets.Add(entry);
        project.ModifiedUtc = DateTime.UtcNow;

        SaveProjectInfo(GetProjectFilePath(projectName), project);
    }

    public void AddStageOperation(string projectName, StageOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        ProjectInfo project = LoadProject(projectName);
        project.StageOperations.Add(operation);
        project.ModifiedUtc = DateTime.UtcNow;

        SaveProjectInfo(GetProjectFilePath(projectName), project);
    }

    public IReadOnlyList<StageOperation> GetStageOperations(string projectName)
        => LoadProject(projectName).StageOperations;

    public IEnumerable<string> ListProjects()
    {
        if (!Directory.Exists(ProjectsRoot))
        {
            yield break;
        }

        foreach (string dir in Directory.GetDirectories(ProjectsRoot))
        {
            string projectName = Path.GetFileName(dir);
            if (File.Exists(Path.Combine(dir, $"{projectName}.smproj")))
            {
                yield return projectName;
            }
        }
    }
}

public sealed class ProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TargetGame { get; set; } = "MSMR";
    public string GameVersion { get; set; } = "1.0";
    public string ModFormat { get; set; } = "stage";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public List<AssetTrackingEntry> ExtractedAssets { get; set; } = [];
    public List<StageOperation> StageOperations { get; set; } = [];
}

public sealed class AssetTrackingEntry
{
    public required string OriginalGamePath { get; set; }
    public required string StagePath { get; set; }
    public ulong AssetId { get; set; }
    public string? ReplacementSourcePath { get; set; }
    public bool Deleted { get; set; }
    public long OriginalSizeBytes { get; set; }
    public string? ValidationHash { get; set; }
}

public sealed class StageOperation
{
    public string OperationType { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
    public string FromPath { get; set; } = string.Empty;
    public string ToPath { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
