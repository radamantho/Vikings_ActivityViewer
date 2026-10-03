using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Tabs
{
    public partial class ItemsTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public ItemsTab()
        {
            InitializeComponent();
            foreach (string name in ItemQuery.AllEvents)
                EventsPanel.Children.Add(new CheckBox { Content = name, IsChecked = true, Margin = new Thickness(0, 0, 14, 0) });
        }

        public ActivityViewer.Controls.ResultView ResultsView => Results;

        public string Title => Lang.T("tab.items");

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database)
        {
            Results.Clear();
            if (database != null) RefreshSuggestions(database);
            else SuggestionBox.Fill(PrefabBox, null);
        }

        public void RefreshSuggestions(ActivityDatabase database) => SuggestionBox.Fill(PrefabBox, database.DistinctPrefabs());

        public async Task<int?> SearchAsync()
        {
            if (_host == null) return null;
            if (!InputParser.TryParseInt(MinBox.Text, out int min))
            {
                _host.ShowError(Lang.T("items.invalidMin"));
                return null;
            }
            List<string> events = EventsPanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToList();
            if (events.Count == 0)
            {
                _host.ShowError(Lang.T("items.noEvents"));
                return null;
            }
            var criteria = new ItemCriteria { Prefab = PrefabBox.Text.Trim(), MinCount = min, Events = events };
            ResultTable? table = await _host.RunQueryAsync(Lang.T("items.searching"), (db, filter, token) => ItemQuery.Run(db, filter, criteria, token), Results);
            return table?.Rows.Count;
        }

        private async void OnSearch(object sender, RoutedEventArgs e) => await SearchAsync();
    }
}
