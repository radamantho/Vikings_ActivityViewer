using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Cache;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Export;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Profiles;
using ActivityViewer.Core.Transfer;
using ActivityViewer.Windows;
using Microsoft.Win32;

namespace ActivityViewer
{
    public sealed class PlayerChoice
    {
        public PlayerChoice(string? platformId, string display)
        {
            PlatformId = platformId;
            Display = display;
        }

        public string? PlatformId { get; }

        public string Display { get; }

        public override string ToString() => Display;
    }

    public partial class MainWindow : Window, IQueryHost, ISettingsHost
    {
        private const string AppTitle = "Vikings Activity Viewer";

        private readonly ProfileStore _store = ProfileStore.Default();
        private readonly List<ITabPage> _pages = new List<ITabPage>();
        private readonly Dictionary<ResultView, (TabItem Tab, ITabPage Page)> _tabs = new Dictionary<ResultView, (TabItem Tab, ITabPage Page)>();
        private List<ServerProfile> _profiles = new List<ServerProfile>();
        private ActivityDatabase? _database;
        private string? _currentWorldFolder;
        private string? _currentWorldName;
        private CancellationTokenSource? _cancel;
        private bool _busy;
        private bool _fillingWorlds;
        private bool _analysisCancelled;

        public MainWindow()
        {
            InitializeComponent();
            RegisterPages();
            foreach (ITabPage page in _pages) page.Attach(this);
            ResetTabHeaders();
        }

        private static string AllPlayers => Lang.T("main.allPlayers");

        public bool IsBusy => _busy;

        public string? CurrentWorldFolder => _database != null ? _currentWorldFolder : null;

        public string? CurrentWorldName => _currentWorldName;

        private void RegisterPages()
        {
            Register(DamageTabItem, DamagePage);
            Register(ItemsTabItem, ItemsPage);
            Register(FrequencyTabItem, FrequencyPage);
            Register(SpeedTabItem, SpeedPage);
            Register(InteractionsTabItem, InteractionsPage);
        }

        private void Register(TabItem tab, ITabPage page)
        {
            _pages.Add(page);
            _tabs[page.ResultsView] = (tab, page);
        }

        private void SetTabHeader(TabItem tab, ITabPage page, int? count)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = page.Title });
            if (count.HasValue)
            {
                header.Children.Add(new TextBlock
                {
                    Text = count.Value.ToString("N0", Lang.Culture),
                    Margin = new Thickness(8, 0, 0, 0),
                    Opacity = 0.65,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            tab.Header = header;
        }

        private void ResetTabHeaders()
        {
            foreach ((TabItem tab, ITabPage page) in _tabs.Values) SetTabHeader(tab, page, null);
        }

        private ServerProfile? SelectedProfile => ProfileBox.SelectedItem as ServerProfile;

        private string? SelectedWorld => WorldBox.SelectedItem as string;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadProfiles(null);
            if (_store.LastBackup != null)
                MessageBox.Show(this, Lang.F("main.profilesCorrupt", _store.LastBackup), AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            if (_database == null) SetStatus(Lang.T("main.welcome"));
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            _cancel?.Cancel();
            CloseDatabase();
        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(this) { Owner = this };
            window.ShowDialog();
        }

        public void ReleaseDatabase() => CloseDatabase();

        public void ReloadAfterCacheChange()
        {
            ServerProfile? profile = SelectedProfile;
            FillWorlds(profile == null ? Array.Empty<string>() : CachePaths.CachedWorlds(profile.Name));
        }

        private void LoadProfiles(string? selectName)
        {
            _profiles = _store.Load();
            ProfileBox.ItemsSource = _profiles;
            ServerProfile? target = _profiles.FirstOrDefault(p => p.Name == selectName) ?? _profiles.FirstOrDefault();
            ProfileBox.SelectedItem = target;
            if (target == null) FillWorlds(Array.Empty<string>());
        }

        private void SaveProfiles() => _store.Save(_profiles);

        private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            ServerProfile? profile = SelectedProfile;
            FillWorlds(profile == null ? Array.Empty<string>() : CachePaths.CachedWorlds(profile.Name));
        }

        private void FillWorlds(IEnumerable<string> worlds)
        {
            string? previous = SelectedWorld;
            List<string> list = worlds.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList();
            _fillingWorlds = true;
            WorldBox.ItemsSource = list;
            _fillingWorlds = false;
            string? select = list.FirstOrDefault(w => string.Equals(w, previous, StringComparison.OrdinalIgnoreCase)) ?? list.FirstOrDefault();
            if (select == null)
            {
                if (_currentWorldFolder != null) CloseDatabase();
                if (_database == null) CopyInfo.Text = Lang.T("sidebar.noDatabase");
                return;
            }
            if (WorldBox.SelectedItem as string == select) OpenCachedWorld();
            else WorldBox.SelectedItem = select;
        }

        private void OnWorldChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_fillingWorlds || WorldBox.SelectedItem == null) return;
            OpenCachedWorld();
        }

        private void OpenCachedWorld()
        {
            ServerProfile? profile = SelectedProfile;
            string? world = SelectedWorld;
            if (profile == null || world == null) return;

            string folder = CachePaths.WorldFolder(profile.Name, world);
            CachedCopy? copy = CachedCopy.Load(folder);
            if (copy == null)
            {
                CloseDatabase();
                CopyInfo.Text = Lang.T("main.noLocalCopy");
                SetStatus(Lang.F("main.worldNotDownloaded", world));
                return;
            }
            if (OpenDatabase(copy.DatabasePathIn(folder), copy.Describe(), folder, world)) SafeDownloader.CleanupOldCopies(folder, copy.Folder);
        }

        private bool OpenDatabase(string path, string description, string? worldFolder, string? worldName)
        {
            CloseDatabase();
            try
            {
                _database = ActivityDatabase.Open(path);
            }
            catch (Exception error) when (error is InvalidActivityDatabaseException || error is FileNotFoundException)
            {
                ShowError(error.Message);
                return false;
            }
            catch (Exception error)
            {
                ReportUnexpected(error, Lang.F("log.openDatabase", path));
                return false;
            }

            _currentWorldFolder = worldFolder;
            _currentWorldName = worldName ?? Path.GetFileName(path);
            FillPlayers(_database, null);
            CopyInfo.Text = description;
            DatabaseName.Text = "— " + Path.GetFileName(path);
            Title = AppTitle + " - " + Path.GetFileName(path);
            foreach (ITabPage page in _pages) page.OnDatabaseChanged(_database);
            ResetTabHeaders();
            _ = AnalyzeAllAsync(false);
            return true;
        }

        private void FillPlayers(ActivityDatabase database, string? keepPlatformId)
        {
            var players = new List<PlayerChoice> { new PlayerChoice(null, AllPlayers) };
            players.AddRange(database.Players().Select(p => new PlayerChoice(p.PlatformId, p.Display)));
            PlayerBox.ItemsSource = players;
            PlayerChoice? keep = keepPlatformId == null ? null : players.FirstOrDefault(p => p.PlatformId == keepPlatformId);
            PlayerBox.SelectedItem = keep ?? players[0];
        }

        private async void OnAnalyze(object sender, RoutedEventArgs e) => await AnalyzeAllAsync(true);

        private async Task AnalyzeAllAsync(bool refreshLists)
        {
            ActivityDatabase? database = _database;
            if (database == null)
            {
                ShowError(Lang.T("main.openFirst"));
                return;
            }
            if (_busy)
            {
                SetStatus(Lang.T("main.wait"));
                return;
            }

            if (refreshLists)
            {
                string? keep = (PlayerBox.SelectedItem as PlayerChoice)?.PlatformId;
                FillPlayers(database, keep);
                foreach (ITabPage page in _pages) page.RefreshSuggestions(database);
            }

            _analysisCancelled = false;
            var summary = new List<string>();
            foreach (ITabPage page in _pages)
            {
                if (_analysisCancelled || !ReferenceEquals(database, _database)) break;
                int? rows = await page.SearchAsync();
                summary.Add(page.Title + " " + (rows.HasValue ? rows.Value.ToString("N0", Lang.Culture) : "-"));
            }

            SetStatus(Lang.F(_analysisCancelled ? "main.analysisCancelled" : "main.analysisDone", string.Join(" · ", summary)));
        }

        private void CloseDatabase()
        {
            if (_database == null) return;
            foreach (ITabPage page in _pages) page.OnDatabaseChanged(null);
            _database.Dispose();
            _database = null;
            _currentWorldFolder = null;
            _currentWorldName = null;
            PlayerBox.ItemsSource = null;
            DatabaseName.Text = "";
            CopyInfo.Text = Lang.T("sidebar.noDatabase");
            Title = AppTitle;
            ResetTabHeaders();
        }

        private string? GetPassword(ServerProfile profile)
        {
            if (PasswordProtector.TryUnprotect(profile.EncryptedPassword, out string saved)) return saved;

            var dialog = new PasswordWindow(profile.Name, profile.EncryptedPassword.Length > 0) { Owner = this };
            if (dialog.ShowDialog() != true) return null;
            if (dialog.SavePassword)
            {
                profile.EncryptedPassword = PasswordProtector.Protect(dialog.Password);
                SaveProfiles();
            }
            return dialog.Password;
        }

        private async void OnListWorlds(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = SelectedProfile;
            if (profile == null)
            {
                ShowError(Lang.T("main.chooseProfile"));
                return;
            }
            string? password = GetPassword(profile);
            if (password == null) return;

            IReadOnlyList<RemoteFileInfo>? found = await RunTransferAsync(Lang.F("main.connecting", profile.Host), profile, password,
                (remote, token) => remote.ListDatabasesAsync(token));
            if (found == null) return;

            FillWorlds(found.Select(f => f.Name).Concat(CachePaths.CachedWorlds(profile.Name)));
            SetStatus(found.Count == 0 ? Lang.F("main.noWorlds", profile.RemoteFolder) : Lang.F("main.worldsFound", found.Count));
        }

        private async void OnRefresh(object sender, RoutedEventArgs e)
        {
            ServerProfile? profile = SelectedProfile;
            string? world = SelectedWorld;
            if (profile == null || world == null)
            {
                ShowError(Lang.T("main.chooseProfileWorld"));
                return;
            }
            string? password = GetPassword(profile);
            if (password == null) return;

            string folder = CachePaths.WorldFolder(profile.Name, world);
            var progress = new Progress<DownloadProgress>(ShowDownloadProgress);
            DownloadResult? result = await RunTransferAsync(Lang.F("main.downloading", world), profile, password,
                (remote, token) => new SafeDownloader().DownloadAsync(remote, world, folder, progress, token));
            if (result == null) return;

            if (result.Success && result.Copy != null)
            {
                if (OpenDatabase(result.Copy.DatabasePathIn(folder), result.Copy.Describe(), folder, world)) SafeDownloader.CleanupOldCopies(folder, result.Copy.Folder);
                SetStatus(result.Message);
                return;
            }

            MessageBox.Show(this, result.Message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus(result.Message);
            if (_database == null && result.Copy != null) OpenDatabase(result.Copy.DatabasePathIn(folder), result.Copy.Describe(), folder, world);
        }

        private void ShowDownloadProgress(DownloadProgress p)
        {
            if (!_busy) return;
            BusyBar.IsIndeterminate = p.Total <= 0;
            if (p.Total > 0)
            {
                BusyBar.Maximum = p.Total;
                BusyBar.Value = Math.Min(p.Received, p.Total);
            }
            SetStatus(Lang.F("main.downloadProgress", p.Attempt, SafeDownloader.MaxAttempts, Megabytes(p.Received), Megabytes(p.Total)));
        }

        private static double Megabytes(long bytes) => bytes / (1024.0 * 1024.0);

        private async Task<T?> RunTransferAsync<T>(string status, ServerProfile profile, string password,
            Func<IRemoteFolder, CancellationToken, Task<T>> work) where T : class
        {
            if (_busy) return null;
            CancellationTokenSource cancel = BeginBusy(status);
            var prompt = new WpfTrustPrompt(this);
            bool profileChanged = false;
            try
            {
                return await Task.Run(async () =>
                {
                    using IRemoteFolder remote = RemoteFolderFactory.Create(profile, password, prompt);
                    try
                    {
                        await remote.ConnectAsync(cancel.Token);
                        return await work(remote, cancel.Token);
                    }
                    finally
                    {
                        profileChanged = remote.ProfileChanged;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                SetStatus(Lang.T("main.operationCancelled"));
                return null;
            }
            catch (Exception error)
            {
                ErrorLog.Write(error, Lang.F("log.connection", profile.Host));
                ShowError(Lang.F("main.connectionFailed", profile.Host, error.Message));
                return null;
            }
            finally
            {
                if (profileChanged) SaveProfiles();
                EndBusy();
            }
        }

        private void OnOpenLocal(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = Lang.T("main.openDialogTitle"), Filter = Lang.T("main.openDialogFilter") };
            if (dialog.ShowDialog(this) != true) return;
            OpenDatabase(dialog.FileName, Lang.F("main.localFile", dialog.FileName), null, null);
        }

        private void OnManageProfiles(object sender, RoutedEventArgs e)
        {
            string? current = SelectedProfile?.Name;
            var window = new ProfilesWindow(_store) { Owner = this };
            window.ShowDialog();
            LoadProfiles(window.SelectedName ?? current);
        }

        private void OnClearFilters(object sender, RoutedEventArgs e)
        {
            PlayerBox.SelectedIndex = PlayerBox.Items.Count > 0 ? 0 : -1;
            FromBox.Text = "";
            ToBox.Text = "";
        }

        private bool TryGetFilter(out QueryFilter filter)
        {
            filter = QueryFilter.All;
            string? platformId = null;
            string typed = PlayerBox.Text.Trim();
            if (PlayerBox.SelectedItem is PlayerChoice choice) platformId = choice.PlatformId;
            else if (typed.Length > 0 && typed != AllPlayers)
            {
                ShowError(Lang.F("main.playerNotFound", typed));
                return false;
            }

            if (!InputParser.TryParseStart(FromBox.Text, out DateTime? from))
            {
                ShowError(Lang.F("main.invalidFrom", InputParser.DateHint));
                return false;
            }
            if (!InputParser.TryParseEnd(ToBox.Text, out DateTime? to))
            {
                ShowError(Lang.F("main.invalidTo", InputParser.DateHint));
                return false;
            }
            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                ShowError(Lang.T("main.fromAfterTo"));
                return false;
            }

            filter = new QueryFilter { PlatformId = platformId, From = from, To = to };
            return true;
        }

        public async Task<ResultTable?> RunQueryAsync(string status, Func<ActivityDatabase, QueryFilter, CancellationToken, ResultTable> query, ResultView target)
        {
            ActivityDatabase? database = _database;
            if (database == null)
            {
                ShowError(Lang.T("main.openFirst"));
                return null;
            }
            if (_busy)
            {
                SetStatus(Lang.T("main.wait"));
                return null;
            }
            if (!TryGetFilter(out QueryFilter filter))
            {
                _analysisCancelled = true;
                return null;
            }

            CancellationTokenSource cancel = BeginBusy(status);
            try
            {
                ResultTable table = await Task.Run(() => query(database, filter, cancel.Token));
                DataTable data = await Task.Run(() => ResultView.ToDataTable(table));
                var full = new QueryFilter { PlatformId = filter.PlatformId, From = filter.From, To = filter.To, RowLimit = int.MaxValue };
                target.Show(table, data, database, token => query(database, full, token));
                if (_tabs.TryGetValue(target, out (TabItem Tab, ITabPage Page) entry)) SetTabHeader(entry.Tab, entry.Page, table.Rows.Count);
                SetStatus(Lang.F("main.rowsTime", table.Rows.Count, table.Elapsed.TotalSeconds));
                return table;
            }
            catch (OperationCanceledException)
            {
                _analysisCancelled = true;
                SetStatus(Lang.T("main.searchCancelled"));
                return null;
            }
            catch (Exception error)
            {
                _analysisCancelled = true;
                ReportUnexpected(error, status);
                return null;
            }
            finally
            {
                EndBusy();
            }
        }

        public async Task ExportAsync(ResultView source, string path)
        {
            ResultTable? table = source.Table;
            if (table == null) return;
            if (_busy)
            {
                SetStatus(Lang.T("main.wait"));
                return;
            }
            if (table.Truncated && !ReferenceEquals(source.Database, _database))
            {
                ShowError(Lang.T("main.dbChangedBeforeExport"));
                return;
            }

            CancellationTokenSource cancel = BeginBusy(Lang.T("main.exporting"));
            try
            {
                Func<CancellationToken, ResultTable>? fullQuery = source.FullQuery;
                await Task.Run(() =>
                {
                    ResultTable complete = table.Truncated && fullQuery != null ? fullQuery(cancel.Token) : table;
                    ResultExporter.Export(complete, path);
                });
                SetStatus(Lang.F("main.exported", path));
            }
            catch (OperationCanceledException)
            {
                SetStatus(Lang.T("main.exportCancelled"));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                ShowError(Lang.F("main.writeFailed", error.Message));
            }
            catch (Exception error)
            {
                ReportUnexpected(error, Lang.F("log.export", path));
            }
            finally
            {
                EndBusy();
            }
        }

        private CancellationTokenSource BeginBusy(string status)
        {
            _busy = true;
            _cancel = new CancellationTokenSource();
            Sidebar.IsEnabled = false;
            CancelButton.IsEnabled = true;
            BusyBar.Visibility = Visibility.Visible;
            BusyBar.IsIndeterminate = true;
            SetStatus(status);
            return _cancel;
        }

        private void EndBusy()
        {
            _busy = false;
            _cancel?.Dispose();
            _cancel = null;
            Sidebar.IsEnabled = true;
            CancelButton.IsEnabled = false;
            BusyBar.Visibility = Visibility.Collapsed;
            BusyBar.IsIndeterminate = false;
            BusyBar.Value = 0;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            _analysisCancelled = true;
            _cancel?.Cancel();
            SetStatus(Lang.T("main.cancelling"));
        }

        public void ShowError(string message)
        {
            MessageBox.Show(this, message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus(message.Split('\n')[0]);
        }

        private void ReportUnexpected(Exception error, string context)
        {
            string log = ErrorLog.Write(error, context);
            MessageBox.Show(this, Lang.F("main.unexpected", error.Message) + (log.Length > 0 ? Lang.F("main.detailsIn", log) : ""), AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus(Lang.F("main.errorStatus", error.Message));
        }

        private void SetStatus(string text) => StatusText.Text = text;
    }
}
