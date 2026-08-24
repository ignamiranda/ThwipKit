using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ThwipKit.Core.Assets;
using ThwipKit.Wpf.Mvvm;
using ThwipKit.Wpf.Services;

namespace ThwipKit.Wpf.ViewModels;

public sealed class AssetBrowserViewModel : ViewModelBase
{
    private readonly IAssetBrowserService _browser;
    private readonly string _gamePath;
    private readonly DispatcherTimer _searchTimer;
    private AssetInfo? _selectedAsset;
    private string _searchText = string.Empty;
    private string? _selectedArchive;
    private bool _isLoading;

    public AssetBrowserViewModel(IAssetBrowserService browser, string gamePath)
    {
        _browser = browser;
        _gamePath = gamePath;
        AssetsView = CollectionViewSource.GetDefaultView(Assets);
        AssetsView.Filter = MatchesFilters;
        AssetsView.SortDescriptions.Add(new(nameof(AssetInfo.ArchiveName), ListSortDirection.Ascending));
        AssetsView.SortDescriptions.Add(new(nameof(AssetInfo.Offset), ListSortDirection.Ascending));
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            AssetsView.Refresh();
        };
        LoadAssetsCommand = new AsyncRelayCommand(_ => LoadAssetsAsync());
        SearchCommand = new RelayCommand(_ => RefreshNow());
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
    }

    public ObservableCollection<AssetInfo> Assets { get; } = [];
    public ICollectionView AssetsView { get; }
    public ObservableCollection<string> ArchiveNames { get; } = [];
    public ObservableCollection<ArchiveTreeNode> ArchiveTree { get; } = [];

    public AssetInfo? SelectedAsset
    {
        get => _selectedAsset;
        set => SetProperty(ref _selectedAsset, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _searchTimer.Stop();
                _searchTimer.Start();
            }
        }
    }

    public string? SelectedArchive
    {
        get => _selectedArchive;
        set
        {
            if (SetProperty(ref _selectedArchive, value))
            {
                AssetsView.Refresh();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ICommand LoadAssetsCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public void LoadAssets()
    {
        LoadAssetsAsync().GetAwaiter().GetResult();
    }

    public async Task LoadAssetsAsync()
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<AssetInfo> assets = Application.Current is null
                ? _browser.GetAllAssets(_gamePath)
                : await Task.Run(() => _browser.GetAllAssets(_gamePath));
            string[] archiveNames = assets.Select(asset => asset.ArchiveName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            void UpdateCollections()
            {
                Assets.Clear();
                foreach (AssetInfo asset in assets)
                {
                    Assets.Add(asset);
                }

                ArchiveNames.Clear();
                foreach (string archiveName in archiveNames)
                {
                    ArchiveNames.Add(archiveName);
                }

                ArchiveTree.Clear();
                ArchiveTree.Add(new ArchiveTreeNode(Path.GetFileName(_gamePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), null,
                    archiveNames.Select(name => new ArchiveTreeNode(name, name, [])).ToArray()));
            }

            if (Application.Current is null || Application.Current.Dispatcher.CheckAccess())
            {
                UpdateCollections();
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(UpdateCollections);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SelectArchive(ArchiveTreeNode? node)
    {
        SelectedArchive = node?.ArchiveName;
    }

    private bool MatchesFilters(object item)
    {
        if (item is not AssetInfo asset)
        {
            return false;
        }

        bool archiveMatches = string.IsNullOrWhiteSpace(SelectedArchive)
            || string.Equals(asset.ArchiveName, SelectedArchive, StringComparison.OrdinalIgnoreCase);
        bool searchMatches = string.IsNullOrWhiteSpace(SearchText)
            || asset.AssetIdHex.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || (asset.ResolvedName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
        return archiveMatches && searchMatches;
    }

    private void RefreshNow()
    {
        _searchTimer.Stop();
        AssetsView.Refresh();
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedArchive = null;
        RefreshNow();
    }
}

public sealed record ArchiveTreeNode(string Name, string? ArchiveName, IReadOnlyList<ArchiveTreeNode> Children);
