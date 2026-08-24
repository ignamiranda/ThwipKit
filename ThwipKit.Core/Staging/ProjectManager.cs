using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThwipKit.Core.Assets;
using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Staging;

public enum ProjectChangeKind
{
    Opened,
    Closed,
    Saved,
    MetadataChanged,
    AssetChanged,
    AssetRemoved,
    ReferenceChanged
}

public sealed class ProjectChangedEventArgs : EventArgs
{
    public ProjectChangedEventArgs(ProjectChangeKind kind, ulong? assetId = null)
    {
        Kind = kind;
        AssetId = assetId;
    }

    public ProjectChangeKind Kind { get; }
    public ulong? AssetId { get; }
}

public sealed class ProjectManager : IProjectTracker
{
    private readonly StageManager _stageManager;
    private readonly AssetBrowser _assetBrowser;
    private readonly string _projectsRoot;
    private ModProject? _current;
    private bool _isDirty;

    public ProjectManager(StageManager stageManager, AssetBrowser assetBrowser, string? projectsRoot = null)
    {
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
        _assetBrowser = assetBrowser ?? throw new ArgumentNullException(nameof(assetBrowser));
        _projectsRoot = projectsRoot ?? Path.Combine(Environment.CurrentDirectory, "projects");
    }

    public string ProjectsRoot => _projectsRoot;

    public bool IsOpen => _current != null;

    public bool IsDirty => _isDirty;

    public ModProject Current
    {
        get
        {
            if (_current == null)
            {
                throw new InvalidOperationException("No project is currently open.");
            }
            return _current;
        }
    }

    public string CurrentName
    {
        get
        {
            if (_current == null)
            {
                throw new InvalidOperationException("No project is currently open.");
            }
            return _current.Metadata.Name;
        }
    }

    public event EventHandler<ProjectChangedEventArgs>? ProjectChanged;

    private void Raise(ProjectChangeKind kind, ulong? assetId = null)
        => ProjectChanged?.Invoke(this, new ProjectChangedEventArgs(kind, assetId));

    public string GetProjectPath(string name) => Path.Combine(ProjectsRoot, name);

    public string GetProjectFilePath(string name) => Path.Combine(GetProjectPath(name), name + ".smproj");

    public bool ProjectExists(string name) => File.Exists(GetProjectFilePath(name));

    public void EnsureProjectDirectory(string name)
    {
        string path = GetProjectPath(name);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public void CreateProject(string projectName, string targetGame = "MSMR", string description = "", string gameVersion = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        if (ProjectExists(projectName))
        {
            throw new InvalidOperationException($"Project '{projectName}' already exists.");
        }

        EnsureProjectDirectory(projectName);

        var project = new ModProject
        {
            Metadata = new ProjectMetadata
            {
                Name = projectName,
                TargetGame = targetGame,
                Description = description,
                GameVersion = gameVersion
            }
        };

        ProjectSerializer.Save(GetProjectFilePath(projectName), project);
    }

    public void OpenProject(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        string path = GetProjectFilePath(projectName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Project '{projectName}' not found.", path);
        }

        _current = ProjectSerializer.Load(path);
        _isDirty = false;
        Raise(ProjectChangeKind.Opened);
    }

    public void CloseProject(bool saveIfDirty = true)
    {
        if (_current == null)
        {
            return;
        }

        if (saveIfDirty && _isDirty)
        {
            Save();
        }

        _current = null;
        _isDirty = false;
        Raise(ProjectChangeKind.Closed);
    }

    public void Save()
    {
        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        _current.Metadata.ModifiedUtc = DateTime.UtcNow;
        ProjectSerializer.Save(GetProjectFilePath(_current.Metadata.Name), _current);
        _isDirty = false;
        Raise(ProjectChangeKind.Saved);
    }

    public void SaveAs(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        _current.Metadata.Name = newName;
        _current.Metadata.ModifiedUtc = DateTime.UtcNow;
        EnsureProjectDirectory(newName);
        ProjectSerializer.Save(GetProjectFilePath(newName), _current);
        _isDirty = false;
        Raise(ProjectChangeKind.Saved);
    }

    public void UpdateMetadata(string? description = null, string? targetGame = null, string? modFormat = null, string? gameVersion = null, string? name = null)
    {
        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        if (description != null) _current.Metadata.Description = description;
        if (targetGame != null) _current.Metadata.TargetGame = targetGame;
        if (modFormat != null) _current.Metadata.ModFormat = modFormat;
        if (gameVersion != null) _current.Metadata.GameVersion = gameVersion;
        if (name != null) _current.Metadata.Name = name;
        _current.Metadata.ModifiedUtc = DateTime.UtcNow;

        _isDirty = true;
        Raise(ProjectChangeKind.MetadataChanged);
    }

    public void DeleteProject(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string path = GetProjectPath(name);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public void RenameProject(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        string oldPath = GetProjectPath(oldName);
        string newPath = GetProjectPath(newName);

        if (!Directory.Exists(oldPath))
        {
            throw new DirectoryNotFoundException($"Project '{oldName}' not found.");
        }

        if (Directory.Exists(newPath))
        {
            throw new InvalidOperationException($"Project '{newName}' already exists.");
        }

        Directory.Move(oldPath, newPath);
    }

    public IReadOnlyList<TrackedAsset> GetTrackedAssets()
    {
        if (_current == null)
        {
            return [];
        }
        return _current.Assets;
    }

    public TrackedAsset? GetTrackedAsset(ulong assetId)
    {
        if (_current == null)
        {
            return null;
        }
        return _current.Assets.FirstOrDefault(a => a.AssetId == assetId);
    }

    public void AddOrUpdateTrackedAsset(TrackedAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        TrackedAsset? existing = _current.Assets.FirstOrDefault(a => a.AssetId == asset.AssetId);
        if (existing != null)
        {
            _current.Assets.Remove(existing);
        }

        _current.Assets.Add(asset);
        _isDirty = true;
        Raise(ProjectChangeKind.AssetChanged, asset.AssetId);
    }

    public void RemoveTrackedAsset(ulong assetId)
    {
        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        TrackedAsset? existing = _current.Assets.FirstOrDefault(a => a.AssetId == assetId);
        if (existing != null)
        {
            _current.Assets.Remove(existing);
            _isDirty = true;
            Raise(ProjectChangeKind.AssetRemoved, assetId);
        }
    }

    public void SetAssetStatus(ulong assetId, TrackedAssetStatus status)
    {
        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        TrackedAsset? existing = _current.Assets.FirstOrDefault(a => a.AssetId == assetId);
        if (existing != null)
        {
            existing.Status = status;
            if (status == TrackedAssetStatus.Modified)
            {
                existing.ModifiedUtc = DateTime.UtcNow;
            }
            _isDirty = true;
            Raise(ProjectChangeKind.AssetChanged, assetId);
        }
    }

    public void AddReference(ProjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        if (_current.References.Any(r => r.Name.Equals(reference.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _current.References.Add(reference);
        _isDirty = true;
        Raise(ProjectChangeKind.ReferenceChanged);
    }

    public void RemoveReference(string name)
    {
        if (_current == null)
        {
            throw new InvalidOperationException("No project is currently open.");
        }

        ProjectReference? existing = _current.References.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _current.References.Remove(existing);
            _isDirty = true;
            Raise(ProjectChangeKind.ReferenceChanged);
        }
    }

    public IEnumerable<string> ListProjects()
    {
        if (!Directory.Exists(ProjectsRoot))
        {
            return [];
        }

        return Directory.GetDirectories(ProjectsRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name) && File.Exists(GetProjectFilePath(name!)))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    public string GetStageAbsolutePath(TrackedAsset asset)
        => Path.Combine(_stageManager.GetStageRoot(), asset.RelativePath);

    public void RecordExtraction(ulong assetId, string stagePath, AssetInfo info)
    {
        if (_current == null)
        {
            return;
        }

        var tracked = new TrackedAsset
        {
            AssetId = assetId,
            ResolvedName = info.ResolvedName,
            ArchiveName = info.ArchiveName,
            Offset = info.Offset,
            Size = info.Size,
            AssetType = info.Type.ToStageFolderName(),
            RelativePath = stagePath,
            Status = TrackedAssetStatus.Extracted,
            OriginalSizeBytes = info.Size,
            ExtractedUtc = DateTime.UtcNow
        };

        AddOrUpdateTrackedAsset(tracked);
    }

    public void RecordReplacement(ulong assetId, string stagePath, string? replacementSourcePath, AssetInfo info)
    {
        if (_current == null)
        {
            return;
        }

        TrackedAsset? existing = GetTrackedAsset(assetId);
        if (existing == null)
        {
            RecordExtraction(assetId, stagePath, info);
            existing = GetTrackedAsset(assetId);
        }

        if (existing == null)
        {
            return;
        }

        existing.Status = TrackedAssetStatus.Modified;
        existing.ReplacementSourcePath = replacementSourcePath;
        existing.ModifiedUtc = DateTime.UtcNow;
        _isDirty = true;
        Raise(ProjectChangeKind.AssetChanged, assetId);
    }

    public void RecordDeletion(ulong assetId, string stagePath)
    {
        if (_current == null)
        {
            return;
        }

        TrackedAsset? existing = GetTrackedAsset(assetId);
        if (existing == null)
        {
            return;
        }

        existing.Status = TrackedAssetStatus.Missing;
        _isDirty = true;
        Raise(ProjectChangeKind.AssetChanged, assetId);
    }
}