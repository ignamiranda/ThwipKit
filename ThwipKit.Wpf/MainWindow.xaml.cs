using System.Windows;
using ThwipKit.Wpf.Services;
using ThwipKit.Wpf.ViewModels;
using ThwipKit.Wpf.Views;

namespace ThwipKit.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void OpenGameDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select game directory"
        };

        if (dialog.ShowDialog(this) == true)
        {
            ViewModel.DetectGameCommand.Execute(dialog.FolderName);
        }
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ProjectManager is not { } pm)
        {
            return;
        }

        var dialog = new NewProjectDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            pm.CreateProject(dialog.ProjectName, dialog.TargetGame, dialog.Description, dialog.GameVersion);
        }
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ProjectManager is not { } pm)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open project",
            Filter = "Spider-Man Project (*.smproj)|*.smproj|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            string? name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            if (name is not null)
            {
                pm.OpenProject(name);
            }
        }
    }

    private void ProjectProperties_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ProjectManager is not { } pm || !pm.IsProjectOpen)
        {
            return;
        }

        var dialog = new ProjectPropertiesDialog { Owner = this };
        dialog.Populate(pm.ProjectName, string.Empty, string.Empty, ".smpcmod", string.Empty, string.Empty);
        if (dialog.ShowDialog() == true)
        {
            pm.UpdateProperties(dialog.Name, dialog.Description, dialog.TargetGame, dialog.GameVersion, dialog.ModFormat);
        }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e) => ViewModel.ProjectManager?.Save();

    private void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ProjectManager is not { } pm)
        {
            return;
        }

        var dialog = new ProjectPropertiesDialog { Owner = this, Title = "Save Project As" };
        dialog.Populate(pm.ProjectName, string.Empty, string.Empty, ".smpcmod", string.Empty, string.Empty);
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ProjectName))
        {
            pm.SaveAs(dialog.ProjectName);
        }
    }

    private void CloseProject_Click(object sender, RoutedEventArgs e) => ViewModel.ProjectManager?.CloseProject();

    private void BuildProject_Click(object sender, RoutedEventArgs e) => RunBuild(false);

    private void TestBuildProject_Click(object sender, RoutedEventArgs e) => RunBuild(true);

    private void ShareProject_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ProjectManager is not { } pm || !pm.IsProjectOpen)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select share output folder" };
        if (dialog.ShowDialog(this) == true)
        {
            pm.Share();
        }
    }

    private void ValidateProject_Click(object sender, RoutedEventArgs e)
        => ViewModel.ProjectManager?.Validate();

    private void RunBuild(bool testBuild)
    {
        if (ViewModel.ProjectManager is not { } pm || !pm.IsProjectOpen)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = testBuild ? "Select test build folder" : "Select build output folder" };
        if (dialog.ShowDialog(this) == true)
        {
            pm.Build(testBuild);
        }
    }
}
