using System.Windows;

namespace ThwipKit.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void OpenGameDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select game directory"
        };

        if (dialog.ShowDialog(this) == true && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.DetectGameCommand.Execute(dialog.FolderName);
        }
    }

    private void GameCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.CanSwitchGame)
        {
            viewModel.SwitchGameCommand.Execute(null);
        }
    }
}
