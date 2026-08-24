using ThwipKit.Wpf.Mvvm;
using Xunit;

namespace ThwipKit.Wpf.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void InitializesWithEmptyGameState()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal(string.Empty, viewModel.GamePath);
        Assert.Equal(0, viewModel.AssetCount);
        Assert.NotNull(viewModel.DetectGameCommand);
    }

    [Fact]
    public void DetectGameReportsInvalidDirectoryWithoutChangingState()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.DetectGame(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Equal(string.Empty, viewModel.GamePath);
        Assert.Equal(0, viewModel.AssetCount);
        Assert.StartsWith("Could not load game:", viewModel.StatusMessage);
    }

    [Fact]
    public void RelayCommandExecutesAction()
    {
        object? received = null;
        var command = new RelayCommand(parameter => received = parameter);

        command.Execute("game-path");

        Assert.Equal("game-path", received);
    }
}
