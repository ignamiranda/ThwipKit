using System.IO;
using System.Windows.Input;
using SpiderManModdingTool.Core.Assets;
using SpiderManModdingTool.Core.Games;
using SpiderManModdingTool.Wpf.Mvvm;

namespace SpiderManModdingTool.Wpf;

public sealed class MainWindowViewModel : ViewModelBase
{
    private string _gamePath = string.Empty;
    private int _assetCount;
    private string _statusMessage = "Select a game directory to begin.";

    public MainWindowViewModel()
    {
        DetectGameCommand = new RelayCommand(parameter => DetectGame(parameter as string));
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

    public void DetectGame(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            StatusMessage = "Select a game directory to begin.";
            return;
        }

        try
        {
            GameBase game = GameFactory.CreateGameFromPath(gamePath);
            int assetCount = new AssetBrowser(game).GetAssetCount(gamePath);
            GamePath = gamePath;
            AssetCount = assetCount;
            StatusMessage = $"Detected {game.DisplayName}.";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            StatusMessage = $"Could not load game: {ex.Message}";
        }
    }
}
