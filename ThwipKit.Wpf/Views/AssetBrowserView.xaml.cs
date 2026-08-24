using System.Windows;
using System.Windows.Controls;
using ThwipKit.Wpf.ViewModels;

namespace ThwipKit.Wpf.Views;

public partial class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        InitializeComponent();
    }

    private void ArchiveTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is AssetBrowserViewModel viewModel)
        {
            viewModel.SelectArchive(e.NewValue as ArchiveTreeNode);
        }
    }
}
