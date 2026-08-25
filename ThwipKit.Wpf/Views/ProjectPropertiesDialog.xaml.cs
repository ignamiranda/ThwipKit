using System.Windows;
using System.Windows.Controls;

namespace ThwipKit.Wpf.Views;

public sealed partial class ProjectPropertiesDialog : Window
{
    public ProjectPropertiesDialog()
    {
        InitializeComponent();
    }

    public string ProjectName => NameBox.Text.Trim();
    public string TargetGame => ((ComboBoxItem)GameBox.SelectedItem).Content.ToString() ?? "MSMR";
    public string GameVersion => VersionBox.Text.Trim();
    public string ModFormat => ((ComboBoxItem)FormatBox.SelectedItem).Content.ToString() ?? ".smpcmod";
    public string Author => AuthorBox.Text.Trim();
    public string Description => DescriptionBox.Text.Trim();

    public void Populate(string name, string targetGame, string gameVersion, string modFormat, string author, string description)
    {
        NameBox.Text = name;
        DescriptionBox.Text = description;
        VersionBox.Text = gameVersion;
        AuthorBox.Text = author;

        SelectByContent(GameBox, targetGame);
        SelectByContent(FormatBox, modFormat);
    }

    private static void SelectByContent(ComboBox box, string content)
    {
        foreach (ComboBoxItem item in box.Items)
        {
            if (string.Equals(item.Content?.ToString(), content, System.StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("Project name is required.", "Project Properties", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
