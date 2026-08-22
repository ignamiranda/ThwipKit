using System.IO;
using System.Windows.Input;
using SpiderManModdingTool.Core.Assets;
using SpiderManModdingTool.Core.Games;
using SpiderManModdingTool.Wpf.Mvvm;
using SpiderManModdingTool.Wpf.Services;
using SpiderManModdingTool.Wpf.ViewModels;

namespace SpiderManModdingTool.Wpf;

public sealed class MainWindowViewModel : ViewModelBase
{
    private string _gamePath = string.Empty;
    private int _assetCount;
    private string _statusMessage = "Select a game directory to begin.";
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
            var assetBrowser = new AssetBrowserViewModel(new AssetBrowserService(browser), gamePath);
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
