using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ThwipKit.Core;
using ThwipKit.Core.Assets;
using ThwipKit.Core.Staging;
using ThwipKit.Wpf.Mvvm;
using ThwipKit.Wpf.Services;

namespace ThwipKit.Wpf.ViewModels;

public enum AssetSection
{
    All,
    Unknown,
    Audio
}

public enum InternalTargetFilter
{
    All,
    InternalTargetsOnly,
    NonInternalTargetsOnly
}

public sealed record FilterPreset(string Name, string? SearchText, AssetType? Type, string? Archive, AssetSection Section);

public sealed class AssetBrowserViewModel : ViewModelBase
{
    private readonly IAssetBrowserService _browser;
    private readonly string _gamePath;
    private AssetInfo? _selectedAsset;
    private string _searchText = string.Empty;
    private string _jumpToText = string.Empty;
    private string? _selectedArchive;
    private AssetType? _selectedType;
    private AssetSection _selectedSection = AssetSection.All;
    private InternalTargetFilter _selectedInternalTargetFilter = InternalTargetFilter.All;
    private bool _isLoading;
    private string _newPresetName = string.Empty;
    private FilterPreset? _selectedPreset;
    private Func<AssetInfo, bool>? _searchPredicate;

    public AssetBrowserViewModel(IAssetBrowserService browser, string gamePath)
    {
        _browser = browser;
        _gamePath = gamePath;
        AssetsView = CollectionViewSource.GetDefaultView(Assets);
        AssetsView.Filter = MatchesFilters;
        AssetsView.SortDescriptions.Add(new SortDescription(nameof(AssetInfo.ArchiveName), ListSortDirection.Ascending));
        AssetsView.SortDescriptions.Add(new SortDescription(nameof(AssetInfo.Offset), ListSortDirection.Ascending));

        LoadAssetsCommand = new AsyncRelayCommand(_ => LoadAssetsAsync());
        SearchCommand = new RelayCommand(param => ExecuteSearch(param as string));
        ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
        JumpToCommand = new RelayCommand(_ => JumpTo());
        ExtractCommand = new RelayCommand(_ => ExtractSelected(), _ => CanActOnSelected());
        ReplaceCommand = new RelayCommand(_ => ReplaceSelected(), _ => CanActOnSelected());
        OpenCommand = new RelayCommand(_ => OpenSelected(), _ => CanActOnSelected());
        CopyReferenceCommand = new RelayCommand(_ => CopyReference(), _ => CanActOnSelected());
        ApplyPresetCommand = new RelayCommand(param => ApplyPreset((FilterPreset)param!));
        DeletePresetCommand = new RelayCommand(param => DeletePreset((FilterPreset)param!));
        SavePresetCommand = new RelayCommand(_ => SavePreset(NewPresetName));

        Types = new ObservableCollection<AssetType>(
            Enum.GetValues<AssetType>().Where(type => type != AssetType.Unknown).OrderBy(type => type.ToString()));
        RecentSearches = [];
        FilterPresets = [];
        LoadPresets();
    }

    public ObservableCollection<AssetInfo> Assets { get; } = [];
    public ICollectionView AssetsView { get; }
    public ObservableCollection<string> ArchiveNames { get; } = [];
    public ObservableCollection<ArchiveTreeNode> ArchiveTree { get; } = [];
    public ObservableCollection<AssetType> Types { get; }
    public ObservableCollection<string> RecentSearches { get; }
    public ObservableCollection<FilterPreset> FilterPresets { get; }
    public ObservableCollection<InternalTargetFilter> InternalTargetFilters { get; } = new()
    {
        InternalTargetFilter.All,
        InternalTargetFilter.InternalTargetsOnly,
        InternalTargetFilter.NonInternalTargetsOnly
    };

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
                _searchPredicate = AssetSearch.Compile(_searchText);
                AssetsView.Refresh();
            }
        }
    }

    public string JumpToText
    {
        get => _jumpToText;
        set => SetProperty(ref _jumpToText, value);
    }

    public string? SelectedArchive
    {
        get => _selectedArchive;
        set
        {
            if (SetProperty(ref _selectedArchive, value))
            {
                if (value != null)
                {
                    _selectedSection = AssetSection.All;
                    OnPropertyChanged(nameof(SelectedSection));
                }

                AssetsView.Refresh();
            }
        }
    }

    public AssetType? SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                AssetsView.Refresh();
            }
        }
    }

    public AssetSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                if (value != AssetSection.All)
                {
                    _selectedArchive = null;
                    OnPropertyChanged(nameof(SelectedArchive));
                }

                AssetsView.Refresh();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public InternalTargetFilter SelectedInternalTargetFilter
    {
        get => _selectedInternalTargetFilter;
        set
        {
            if (SetProperty(ref _selectedInternalTargetFilter, value))
            {
                AssetsView.Refresh();
            }
        }
    }

    public string NewPresetName
    {
        get => _newPresetName;
        set => SetProperty(ref _newPresetName, value);
    }

    public FilterPreset? SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public ICommand LoadAssetsCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand JumpToCommand { get; }
    public ICommand ExtractCommand { get; }
    public ICommand ReplaceCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand CopyReferenceCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand SavePresetCommand { get; }

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
                    asset.Type = AssetClassifier.Classify(asset);
                    Assets.Add(asset);
                }

                ArchiveNames.Clear();
                foreach (string archiveName in archiveNames)
                {
                    ArchiveNames.Add(archiveName);
                }

                ArchiveTree.Clear();
                string root = Path.GetFileName(_gamePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                ArchiveTree.Add(new ArchiveTreeNode(
                    root,
                    null,
                    [
                        new ArchiveTreeNode("[UNKNOWN]", null, []) { Section = AssetSection.Unknown },
                        new ArchiveTreeNode("[WEM] Audio", null, []) { Section = AssetSection.Audio },
                        .. archiveNames.Select(name => new ArchiveTreeNode(name, name, [])).ToArray()
                    ]));
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
        if (node is null)
        {
            return;
        }

        if (node.Section is AssetSection.Unknown or AssetSection.Audio)
        {
            SelectedSection = node.Section.Value;
        }
        else
        {
            SelectedArchive = node.ArchiveName;
        }
    }

    public void JumpToAsset(AssetInfo asset)
    {
        SelectedAsset = asset;
        AssetsView.Refresh();
    }

    public void AddRecentSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        string trimmed = query.Trim();
        RecentSearches.Remove(trimmed);
        RecentSearches.Insert(0, trimmed);
        while (RecentSearches.Count > 20)
        {
            RecentSearches.RemoveAt(RecentSearches.Count - 1);
        }
    }

    public void SavePreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        FilterPreset preset = new(name.Trim(), SearchText, SelectedType, SelectedArchive, SelectedSection);
        FilterPreset? existing = FilterPresets.FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            FilterPresets.Remove(existing);
        }

        FilterPresets.Add(preset);
        PersistPresets();
    }

    public void ApplyPreset(FilterPreset preset)
    {
        SearchText = preset.SearchText ?? string.Empty;
        SelectedType = preset.Type;
        SelectedArchive = preset.Archive;
        SelectedSection = preset.Section;
        RefreshNow();
    }

    public void DeletePreset(FilterPreset preset)
    {
        FilterPresets.Remove(preset);
        PersistPresets();
    }

    private void ExecuteSearch(string? searchText = null)
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            SearchText = searchText;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            AddRecentSearch(SearchText);
        }

        AssetsView.Refresh();
    }

    private void JumpTo()
    {
        if (string.IsNullOrWhiteSpace(JumpToText))
        {
            return;
        }

        AssetInfo? match = AssetSearch.Search(Assets, JumpToText).FirstOrDefault();
        if (match != null)
        {
            SelectedAsset = match;
            AssetsView.Refresh();
        }
    }

    private void ExtractSelected()
    {
        if (SelectedAsset is { } asset)
        {
            _browser.ExtractAsset(asset, _gamePath);
        }
    }

    private void OpenSelected()
    {
        if (SelectedAsset is { } asset)
        {
            _browser.OpenAsset(asset, _gamePath);
        }
    }

    private void CopyReference()
    {
        if (SelectedAsset is not { } asset)
        {
            return;
        }

        string reference = asset.ResolvedName is not null
            ? $"{asset.ResolvedName} ({asset.AssetIdHex})"
            : asset.AssetIdHex;

        if (Application.Current is not null)
        {
            Clipboard.SetText(reference);
        }
    }

    private void ReplaceSelected()
    {
        if (SelectedAsset is not { } asset)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select replacement file",
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _browser.ReplaceAsset(asset, _gamePath, dialog.FileName);
        }
    }

    private bool CanActOnSelected() => SelectedAsset is not null;

    private bool MatchesFilters(object item)
    {
        if (item is not AssetInfo asset)
        {
            return false;
        }

        bool sectionMatches = _selectedSection switch
        {
            AssetSection.Unknown => asset.IsUnknown,
            AssetSection.Audio => asset.IsAudio,
            _ => true
        };

        bool archiveMatches = string.IsNullOrWhiteSpace(SelectedArchive)
            || string.Equals(asset.ArchiveName, SelectedArchive, StringComparison.OrdinalIgnoreCase);
        bool typeMatches = !SelectedType.HasValue || asset.Type == SelectedType.Value;

        bool internalTargetMatches = _selectedInternalTargetFilter switch
        {
            InternalTargetFilter.InternalTargetsOnly => asset.IsInternalTarget,
            InternalTargetFilter.NonInternalTargetsOnly => !asset.IsInternalTarget,
            _ => true
        };

        bool searchMatches = _searchPredicate is null || string.IsNullOrWhiteSpace(SearchText) || _searchPredicate(asset);

        return sectionMatches && archiveMatches && typeMatches && internalTargetMatches && searchMatches;
    }

    private void RefreshNow()
    {
        AssetsView.Refresh();
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        JumpToText = string.Empty;
        SelectedArchive = null;
        SelectedType = null;
        SelectedSection = AssetSection.All;
        SelectedInternalTargetFilter = InternalTargetFilter.All;
        RefreshNow();
    }

    private void LoadPresets()
    {
        string path = PresetPath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            FilterPreset[]? presets = JsonSerializer.Deserialize<FilterPreset[]>(json);
            if (presets != null)
            {
                foreach (FilterPreset preset in presets)
                {
                    FilterPresets.Add(preset);
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    private void PersistPresets()
    {
        try
        {
            string path = PresetPath();
            string json = JsonSerializer.Serialize(FilterPresets.ToArray());
            File.WriteAllText(path, json);
        }
        catch (IOException)
        {
        }
    }

    private static string PresetPath()
    {
        string directory = AppSettings.GetSettingsDirectory();
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "filterpresets.json");
    }
}

public sealed record ArchiveTreeNode(string Name, string? ArchiveName, IReadOnlyList<ArchiveTreeNode> Children)
{
    public AssetSection? Section { get; init; }
}
