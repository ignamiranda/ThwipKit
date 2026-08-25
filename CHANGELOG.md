# Changelog

All notable changes to ThwipKit are documented here. Entries reference the
GitHub issue they resolve.

## Resolved Issues

### Issue #84 — Game / Internal Target Filtering (RESOLVED)

The asset browser now supports filtering by game and by internal target.

**Acceptance criteria met:**

1. **Game dropdown + internal target dropdown** — `MainWindow.xaml` binds a
   game-selection `ComboBox` to `KnownGames`
   (`MainWindow.xaml:19 ItemsSource="{Binding KnownGames}"`), populated by
   `MainWindowViewModel.LoadKnownGames()` via
   `KnownGames.Add(new GameDescriptor(definition.InternalId, definition.DisplayName, definition.IsInternalTarget));`.
   The internal-target filter dropdown is bound in
   `AssetBrowserView.xaml:36 ItemsSource="{Binding InternalTargetFilters}"`
   `SelectedItem="{Binding SelectedInternalTargetFilter}"`, backed by
   `AssetBrowserViewModel.cs:76 public ObservableCollection<InternalTargetFilter> InternalTargetFilters { get; } = new();`.

2. **Real-time updates** — changing `SelectedInternalTargetFilter` refreshes the
   view via `AssetBrowserViewModel.cs:97 AssetsView.Refresh();` in the property
   setter. Game switching live-reloads via
   `MainWindowViewModel.SwitchGameCommand` (a `RelayCommand`) invoked from
   `MainWindow.xaml.cs GameCombo_SelectionChanged`.

3. **Filter combinations (e.g. "MSMR textures only")** — combination test
   `AssetBrowserViewModelTests.InternalTargetAndTypeFiltersCombine`; predicate
   applied at
   `AssetBrowserViewModel.cs:434 bool internalTargetMatches = AssetFilters.MatchesInternalTarget(asset, _selectedInternalTargetFilter);`.

4. **Unit tests** — `AssetFilterTests` (6 `MatchesInternalTarget` cases),
   `AssetCatalogTests` (populates `AssetInfo.IsInternalTarget` via
   `AssetCatalog.cs asset.IsInternalTarget = _game.Definition.IsInternalTarget;`),
   `AssetBrowserViewModelTests` (dropdown exposure, narrow/exclude, ClearFilters,
   combination), `MainWindowViewModelTests` (KnownGames count, CanSwitchGame guard).

**Verification:** full-solution build 0 errors; 151 tests pass (131 Core + 20 WPF).
Independently verified against all 4 acceptance criteria.

**Commits:** `2603896`, `9e79811`, `41f0d6e` (local master), merged into
`feat/#13-integrated-editors` at `bdee66b`. GitHub issue #84 closed.
