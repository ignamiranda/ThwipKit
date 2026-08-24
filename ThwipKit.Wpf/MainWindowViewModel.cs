using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using ThwipKit.Core;
using ThwipKit.Core.Assets;
using ThwipKit.Core.GameDefinitions;
using ThwipKit.Core.Games;
using ThwipKit.Wpf.Mvvm;
using ThwipKit.Wpf.Services;
using ThwipKit.Wpf.ViewModels;

namespace ThwipKit.Wpf;

public sealed class MainWindowViewModel : ViewModelBase
{
    private string _gamePath = string.Empty;
    private int _assetCount;
    private string _statusMessage = "Select a game directory to begin.";
    private readonly string _projectRoot = AppSettings.GetSettingsDirectory();
    private AssetBrowserViewModel? _assetBrowser;
    private readonly Dictionary<string, string> _gameDirectories = [];
    private GameDescriptor? _selectedGame;

    public MainWindowViewModel()
    {
        DetectGameCommand = new AsyncRelayCommand(parameter => DetectGameAsync(parameter as string));
        SwitchGameCommand = new RelayCommand(_ => SwitchGame(), _ => CanSwitchGame);
        LoadKnownGames();
    }

    public ObservableCollection<GameDescriptor> KnownGames { get; } = new();

    public GameDescriptor? SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (SetProperty(ref _selectedGame, value))
            {
                SwitchGameCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanSwitchGame => SelectedGame is not null && _gameDirectories.ContainsKey(SelectedGame.InternalId);

    public RelayCommand SwitchGameCommand { get; }

    public string GamePath
    {
        get => _gamePath;
        private set => SetProperty(ref _gamePath, value);
    }

    public int AssetCount
    {
        get => _assetCount;
        private set => SetProperty(ref _assetCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand DetectGameCommand { get; }

    public AssetBrowserViewModel? AssetBrowser
    {
        get => _assetBrowser;
        private set => SetProperty(ref _assetBrowser, value);
    }

    public void DetectGame(string? gamePath)
    {
        DetectGameAsync(gamePath).GetAwaiter().GetResult();
    }

    public async Task DetectGameAsync(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            StatusMessage = "Select a game directory to begin.";
            return;
        }

        try
        {
            GameBase game = GameFactory.CreateGameFromPath(gamePath);
            _gameDirectories[game.InternalId] = gamePath;
            GameDescriptor? descriptor = KnownGames.FirstOrDefault(g => g.InternalId == game.InternalId);
            if (SelectedGame != descriptor)
            {
                SelectedGame = descriptor;
            }

            var browser = new AssetBrowser(game);
            var assetBrowser = new AssetBrowserViewModel(new AssetBrowserService(browser, game, _projectRoot), gamePath);
            AssetBrowser = assetBrowser;
            GamePath = gamePath;
            StatusMessage = $"Loading {game.DisplayName} assets...";
            await assetBrowser.LoadAssetsAsync();
            AssetCount = assetBrowser.Assets.Count;
            StatusMessage = $"Detected {game.DisplayName}.";
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException or InvalidDataException or InvalidOperationException)
        {
            StatusMessage = $"Could not load game: {exception.Message}";
        }
    }

    private void LoadKnownGames()
    {
        GameDefinitionLoader.LoadBuiltInDefinitions();
        KnownGames.Clear();
        foreach (GameDefinition definition in GameDefinitionLoader.GetAllDefinitions().Values)
        {
            KnownGames.Add(new GameDescriptor(definition.InternalId, definition.DisplayName, definition.IsInternalTarget));
        }
    }

    private void SwitchGame()
    {
        if (SelectedGame is null || !_gameDirectories.TryGetValue(SelectedGame.InternalId, out string? directory))
        {
            return;
        }

        if (directory == _gamePath)
        {
            return;
        }

        DetectGame(directory);
    }
}

public sealed record GameDescriptor(string InternalId, string DisplayName, bool IsInternalTarget);
