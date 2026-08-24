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

    private void AssetGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel viewModel && viewModel.SelectedAsset is not null)
        {
            viewModel.JumpToAsset(viewModel.SelectedAsset);
        }
    }
}
