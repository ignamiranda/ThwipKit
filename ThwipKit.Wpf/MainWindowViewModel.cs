using System.IO;
using System.Windows.Input;
using ThwipKit.Core;
using ThwipKit.Core.Assets;
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

    public MainWindowViewModel()
    {
        DetectGameCommand = new AsyncRelayCommand(parameter => DetectGameAsync(parameter as string));
    }

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
}
