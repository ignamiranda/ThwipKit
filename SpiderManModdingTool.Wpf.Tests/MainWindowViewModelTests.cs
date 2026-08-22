using SpiderManModdingTool.Wpf.Mvvm;
using Xunit;

namespace SpiderManModdingTool.Wpf.Tests;

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
    public void RelayCommandExecutesAction()
    {
        object? received = null;
        var command = new RelayCommand(parameter => received = parameter);

        command.Execute("game-path");

        Assert.Equal("game-path", received);
    }
}
