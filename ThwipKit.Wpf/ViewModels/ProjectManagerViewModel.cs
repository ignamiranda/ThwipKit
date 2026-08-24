using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using ThwipKit.Core;
using ThwipKit.Core.Assets;
using ThwipKit.Core.Games;
using ThwipKit.Core.Mods;
using ThwipKit.Core.Staging;
using ThwipKit.Wpf.Mvvm;
using ThwipKit.Wpf.Services;

namespace ThwipKit.Wpf.ViewModels;

public sealed class ProjectManagerViewModel : ViewModelBase
{
    private readonly ProjectManager _manager;
    private readonly RecentProjectsStore _recents;
    private readonly ProjectTrackingSink _sink;
    private readonly StageManager _stageManager;
    private GameBase? _game;
    private string? _gamePath;
    private string _statusMessage = "No project open.";
    private string? _validationMessage;
    private bool _autoSave = true;
    private bool _isOperationRunning;

    public ProjectManagerViewModel(StageManager stageManager, AssetBrowser assetBrowser, string projectsRoot)
    {
        _stageManager = stageManager ?? throw new ArgumentNullException(nameof(stageManager));
        _manager = new ProjectManager(stageManager, assetBrowser, projectsRoot);
        _recents = new RecentProjectsStore();
        _sink = new ProjectTrackingSink(_manager);

        _manager.ProjectChanged += OnProjectChanged;

        NewProjectCommand = new RelayCommand(_ => RequestNewProject());
        OpenProjectCommand = new RelayCommand(_ => RequestOpenProject());
        OpenRecentCommand = new RelayCommand(p => OpenRecent(p as string), p => p is string);
        SaveCommand = new RelayCommand(_ => Save(), _ => _manager.IsOpen && _manager.IsDirty);
        SaveAsCommand = new RelayCommand(_ => RequestSaveAs(), _ => _manager.IsOpen);
        CloseProjectCommand = new RelayCommand(_ => CloseProject(), _ => _manager.IsOpen);
        ProjectPropertiesCommand = new RelayCommand(_ => RequestProperties(), _ => _manager.IsOpen);
        BuildCommand = new RelayCommand(_ => RequestBuild(false), _ => CanBuild());
        TestBuildCommand = new RelayCommand(_ => RequestBuild(true), _ => CanBuild());
        ShareCommand = new RelayCommand(_ => RequestShare(), _ => CanBuild());
        ValidateCommand = new RelayCommand(_ => Validate(), _ => _manager.IsOpen && !_isOperationRunning);
        ToggleAutoSaveCommand = new RelayCommand(_ => AutoSave = !AutoSave);

        RecentProjects = new ObservableCollection<string>(_recents.Entries);
    }

    public IAssetTrackingSink Sink => _sink;

    public bool IsProjectOpen => _manager.IsOpen;

    public string ProjectName => _manager.IsOpen ? _manager.CurrentName : string.Empty;

    public bool HasUnsavedChanges => _manager.IsOpen && _manager.IsDirty;

    public bool AutoSave
    {
        get => _autoSave;
        set => SetProperty(ref _autoSave, value);
    }

    public bool IsOperationRunning
    {
        get => _isOperationRunning;
        private set => SetProperty(ref _isOperationRunning, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage ?? string.Empty;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<string> RecentProjects { get; }

    public ICommand NewProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenRecentCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand CloseProjectCommand { get; }
    public ICommand ProjectPropertiesCommand { get; }
    public ICommand BuildCommand { get; }
    public ICommand TestBuildCommand { get; }
    public ICommand ShareCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand ToggleAutoSaveCommand { get; }

    public void AttachGame(GameBase game, string gamePath)
    {
        _game = game;
        _gamePath = gamePath;
        RefreshCommands();
    }

    public void CreateProject(string name, string targetGame, string description, string gameVersion, string modFormat = "spidermod")
    {
        _manager.CreateProject(name, targetGame, description, gameVersion);
        _manager.OpenProject(name);
        _manager.UpdateMetadata(modFormat: modFormat);
        _recents.Add(_manager.GetProjectFilePath(name));
        SyncRecent();
        StatusMessage = $"Created project '{name}'.";
        RefreshAll();
    }

    public void OpenProject(string name)
    {
        _manager.OpenProject(name);
        _recents.Add(_manager.GetProjectFilePath(name));
        SyncRecent();
        StatusMessage = $"Opened project '{name}'.";
        RefreshAll();
    }

    public void Save()
    {
        if (!_manager.IsOpen)
        {
            return;
        }

        _manager.Save();
        StatusMessage = $"Saved project '{ProjectName}'.";
        RefreshAll();
    }

    public void SaveAs(string newName)
    {
        if (!_manager.IsOpen)
        {
            return;
        }

        _manager.SaveAs(newName);
        _recents.Add(_manager.GetProjectFilePath(newName));
        SyncRecent();
        StatusMessage = $"Saved project as '{ProjectName}'.";
        RefreshAll();
    }

    public void CloseProject()
    {
        if (!_manager.IsOpen)
        {
            return;
        }

        _manager.CloseProject(saveIfDirty: true);
        StatusMessage = "Project closed.";
        RefreshAll();
    }

    public void UpdateProperties(string name, string description, string targetGame, string gameVersion, string modFormat)
    {
        if (!_manager.IsOpen)
        {
            return;
        }

        _manager.UpdateMetadata(description, targetGame, modFormat, gameVersion, name);
        StatusMessage = $"Updated properties for '{ProjectName}'.";
        RefreshAll();
    }

    public void Build(bool testBuild)
    {
        if (!TryGetBuildContext(out GameBase game, out string gamePath, out string? outputDir))
        {
            return;
        }

        RunOperation(() =>
        {
            var builder = new ProjectBuilder(_manager, _stageManager);
            if (testBuild)
            {
                builder.TestBuild(outputDir ?? Path.Combine(Path.GetTempPath(), "thwip-test"));
                StatusMessage = "Test build complete.";
            }
            else
            {
                builder.Build(outputDir ?? Path.Combine(Environment.CurrentDirectory, "build"));
                StatusMessage = "Build complete.";
            }
        });
    }

    public void Share()
    {
        if (!TryGetBuildContext(out GameBase game, out string gamePath, out string? outputDir))
        {
            return;
        }

        RunOperation(() =>
        {
            var builder = new ProjectBuilder(_manager, _stageManager);
            var manifest = new ModManifest
            {
                Name = ProjectName,
                Version = _manager.Current.Metadata.GameVersion,
                Description = _manager.Current.Metadata.Description,
                Author = _manager.Current.Metadata.Author
            };
            builder.Share(outputDir ?? Path.Combine(Environment.CurrentDirectory, "share"), manifest);
            StatusMessage = "Project shared.";
        });
    }

    public event EventHandler<ProjectRequestEventArgs>? NewProjectRequested;
    public event EventHandler<ProjectRequestEventArgs>? OpenProjectRequested;
    public event EventHandler<ProjectRequestEventArgs>? SaveAsRequested;
    public event EventHandler<ProjectRequestEventArgs>? PropertiesRequested;
    public event EventHandler<ProjectRequestEventArgs>? BuildRequested;
    public event EventHandler<ProjectRequestEventArgs>? ShareRequested;

    private void RequestNewProject() => NewProjectRequested?.Invoke(this, new ProjectRequestEventArgs());
    private void RequestOpenProject() => OpenProjectRequested?.Invoke(this, new ProjectRequestEventArgs());
    private void RequestSaveAs() => SaveAsRequested?.Invoke(this, new ProjectRequestEventArgs());
    private void RequestProperties() => PropertiesRequested?.Invoke(this, new ProjectRequestEventArgs());
    private void RequestBuild(bool testBuild) => BuildRequested?.Invoke(this, new ProjectRequestEventArgs { Argument = testBuild });
    private void RequestShare() => ShareRequested?.Invoke(this, new ProjectRequestEventArgs());

    public void Validate()
    {
        if (!_manager.IsOpen)
        {
            return;
        }

        RunOperation(() =>
        {
            var validator = new ProjectValidator(_manager, _stageManager);
            IReadOnlyList<ProjectValidator.AssetValidationResult> results = validator.Validate();
            int invalid = results.Count(r => !r.IsValid);
            int missing = results.Count(r => r.Status == TrackedAssetStatus.Missing);

            ValidationMessage = invalid == 0
                ? $"Validation passed: {results.Count} assets OK."
                : $"Validation found {invalid} problem(s), {missing} missing.";
            StatusMessage = ValidationMessage;
        });
    }

    private void OpenRecent(string? path)
    {
        if (path is null)
        {
            return;
        }

        string? name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (name is null)
        {
            return;
        }

        if (!_manager.ProjectExists(name))
        {
            _recents.Remove(path);
            SyncRecent();
            StatusMessage = $"Recent project '{name}' no longer exists.";
            return;
        }

        OpenProject(name);
    }

    private bool CanBuild() => _manager.IsOpen && _game is not null && !_isOperationRunning;

    private bool TryGetBuildContext(out GameBase game, out string gamePath, out string? outputDir)
    {
        game = _game!;
        gamePath = _gamePath!;
        outputDir = null;

        if (_game is null || _gamePath is null)
        {
            StatusMessage = "Load a game before building.";
            return false;
        }

        return true;
    }

    private void RunOperation(Action action)
    {
        IsOperationRunning = true;
        try
        {
            action();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Operation failed: {exception.Message}";
        }
        finally
        {
            IsOperationRunning = false;
            RefreshCommands();
        }
    }

    private void OnProjectChanged(object? sender, ProjectChangedEventArgs e)
    {
        if (AutoSave && (e.Kind == ProjectChangeKind.AssetChanged || e.Kind == ProjectChangeKind.MetadataChanged))
        {
            try
            {
                _manager.Save();
            }
            catch (InvalidOperationException)
            {
            }
        }

        RefreshAll();
    }

    private void SyncRecent()
    {
        RecentProjects.Clear();
        foreach (string entry in _recents.Entries)
        {
            RecentProjects.Add(entry);
        }
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(IsProjectOpen));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        foreach (ICommand command in new ICommand[]
                 {
                     NewProjectCommand, OpenProjectCommand, OpenRecentCommand, SaveCommand,
                     SaveAsCommand, CloseProjectCommand, ProjectPropertiesCommand,
                      BuildCommand, TestBuildCommand, ShareCommand, ValidateCommand
                 })
        {
            if (command is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }
        }
    }
}

public sealed class ProjectRequestEventArgs : EventArgs
{
    public object? Argument { get; set; }
}
