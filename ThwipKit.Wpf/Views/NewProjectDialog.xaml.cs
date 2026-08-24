using System.Windows;
using System.Windows.Controls;

namespace ThwipKit.Wpf.Views;

public sealed partial class NewProjectDialog : Window
{
    public NewProjectDialog()
    {
        InitializeComponent();
    }

    public string ProjectName => NameBox.Text.Trim();
    public string TargetGame => ((ComboBoxItem)GameBox.SelectedItem).Content.ToString() ?? "MSMR";
    public string GameVersion => VersionBox.Text.Trim();
    public string Description => DescriptionBox.Text.Trim();

    public bool IsValid => !string.IsNullOrWhiteSpace(ProjectName);

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (IsValid)
        {
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Project name is required.", "New Project", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
