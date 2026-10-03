using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Cache;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Settings;
using Microsoft.Win32;
using AppLanguage = ActivityViewer.Core.Localization.Language;

namespace ActivityViewer.Windows
{
    public interface ISettingsHost
    {
        bool IsBusy { get; }

        string? CurrentWorldFolder { get; }

        string? CurrentWorldName { get; }

        void ReleaseDatabase();

        void ReloadAfterCacheChange();
    }

    public sealed class LanguageChoice
    {
        public LanguageChoice(AppLanguage language, string display)
        {
            Language = language;
            Display = display;
        }

        public AppLanguage Language { get; }

        public string Display { get; }
    }

    public partial class SettingsWindow : Window
    {
        private readonly ISettingsHost _host;
        private bool _filling;

        public SettingsWindow(ISettingsHost host)
        {
            InitializeComponent();
            _host = host;
            FillLanguages();
            RefreshCacheInfo();
        }

        private static AppSettings LoadSettings() => SettingsStore.Default().Load(CultureInfo.CurrentUICulture);

        private void FillLanguages()
        {
            var choices = new List<LanguageChoice>
            {
                new LanguageChoice(AppLanguage.Portuguese, "Português"),
                new LanguageChoice(AppLanguage.English, "English")
            };
            _filling = true;
            LanguageBox.ItemsSource = choices;
            LanguageBox.SelectedItem = choices.First(c => c.Language == Lang.Current);
            _filling = false;
        }

        private void RefreshCacheInfo()
        {
            FolderBox.Text = CachePaths.Root;
            CacheStats stats = CacheMaintenance.Measure(CachePaths.Root);
            StatsText.Text = Lang.F("settings.copies", stats.Worlds, stats.Bytes / (1024.0 * 1024.0));
        }

        private bool EnsureIdle()
        {
            if (!_host.IsBusy) return true;
            StatusText.Text = Lang.T("main.wait");
            return false;
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filling || !(LanguageBox.SelectedItem is LanguageChoice choice) || choice.Language == Lang.Current) return;

            AppSettings settings = LoadSettings();
            settings.Language = choice.Language;
            SettingsStore.Default().Save(settings);
            if (MessageBox.Show(this, Lang.T("main.restartQuestion"), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                StatusText.Text = Lang.T("main.restartLater");
                return;
            }

            string? exe = Environment.ProcessPath;
            if (exe != null) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Application.Current.Shutdown();
        }

        private void OnChangeFolder(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = Lang.T("settings.pickFolder"), InitialDirectory = Directory.Exists(CachePaths.Root) ? CachePaths.Root : "" };
            if (dialog.ShowDialog(this) != true) return;
            ChangeRoot(dialog.FolderName, dialog.FolderName);
        }

        private void OnUseDefault(object sender, RoutedEventArgs e) => ChangeRoot(CachePaths.DefaultRoot, "");

        private void ChangeRoot(string newRoot, string savedValue)
        {
            if (!EnsureIdle()) return;
            string oldRoot = CachePaths.Root;
            if (string.Equals(Path.GetFullPath(oldRoot).TrimEnd('\\'), Path.GetFullPath(newRoot).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;

            int worlds = CacheMaintenance.Measure(oldRoot).Worlds;
            if (worlds > 0 && MessageBox.Show(this, Lang.F("settings.moveQuestion", worlds, newRoot), Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _host.ReleaseDatabase();
            int moved;
            try
            {
                Directory.CreateDirectory(newRoot);
                moved = CacheMaintenance.MoveAll(oldRoot, newRoot);
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            {
                ErrorLog.Write(error, "Move cache " + oldRoot + " -> " + newRoot);
                MessageBox.Show(this, Lang.F("settings.moveFailed", error.Message), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                _host.ReloadAfterCacheChange();
                RefreshCacheInfo();
                return;
            }

            AppSettings settings = LoadSettings();
            settings.CacheRoot = savedValue;
            SettingsStore.Default().Save(settings);
            CachePaths.UseRoot(savedValue);
            _host.ReloadAfterCacheChange();
            RefreshCacheInfo();
            StatusText.Text = Lang.F("settings.moved", moved);
        }

        private void OnClearCurrent(object sender, RoutedEventArgs e)
        {
            if (!EnsureIdle()) return;
            string? folder = _host.CurrentWorldFolder;
            if (folder == null)
            {
                StatusText.Text = Lang.T("settings.noCurrent");
                return;
            }
            if (MessageBox.Show(this, Lang.F("settings.clearCurrentQuestion", _host.CurrentWorldName ?? ""), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _host.ReleaseDatabase();
            Delete(() => { CacheMaintenance.DeleteWorld(folder); return 1; });
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            if (!EnsureIdle()) return;
            int worlds = CacheMaintenance.Measure(CachePaths.Root).Worlds;
            if (worlds == 0)
            {
                StatusText.Text = Lang.F("settings.cleared", 0);
                return;
            }
            if (MessageBox.Show(this, Lang.F("settings.clearAllQuestion", worlds), Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _host.ReleaseDatabase();
            Delete(() => CacheMaintenance.DeleteAll(CachePaths.Root));
        }

        private void Delete(Func<int> action)
        {
            try
            {
                int removed = action();
                StatusText.Text = Lang.F("settings.cleared", removed);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                ErrorLog.Write(error, "Clear cache");
                MessageBox.Show(this, Lang.F("main.writeFailed", error.Message), Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            _host.ReloadAfterCacheChange();
            RefreshCacheInfo();
        }
    }
}
