using System.Windows;
using System.Windows.Controls;
using ThwipKit.Core.Staging;

namespace ThwipKit.Wpf.Views;

public sealed partial class NewProjectDialog : Window
{
    public NewProjectDialog()
    {
        InitializeComponent();

        TemplateBox.ItemsSource = ProjectTemplates.All;
        TemplateBox.DisplayMemberPath = "Name";
        TemplateBox.SelectedIndex = 0;
    }

    public string ProjectName => NameBox.Text.Trim();
    public string TargetGame => ((ComboBoxItem)GameBox.SelectedItem).Content.ToString() ?? "MSMR";
    public string GameVersion => VersionBox.Text.Trim();
    public string Description => DescriptionBox.Text.Trim();
    public string ModFormat { get; private set; } = "spidermod";

    public bool IsValid => !string.IsNullOrWhiteSpace(ProjectName);

    private void TemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateBox.SelectedItem is not ProjectTemplate template || template.Name == "Blank")
        {
            return;
        }

        ModFormat = template.ModFormat;
        SelectGame(template.TargetGame);
    }

    private void SelectGame(string game)
    {
        foreach (ComboBoxItem item in GameBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), game, System.StringComparison.OrdinalIgnoreCase))
            {
                GameBox.SelectedItem = item;
                return;
            }
        }
    }

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
