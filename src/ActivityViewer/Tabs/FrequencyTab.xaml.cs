using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Tabs
{
    public partial class FrequencyTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public FrequencyTab()
        {
            InitializeComponent();
        }

        public ActivityViewer.Controls.ResultView ResultsView => Results;

        public string Title => Lang.T("tab.frequency");

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database) => Results.Clear();

        public void RefreshSuggestions(ActivityDatabase database)
        {
        }

        public async Task<int?> SearchAsync()
        {
            if (_host == null) return null;
            if (!InputParser.TryParseInt(LimitBox.Text, out int limit) || limit < 1)
            {
                _host.ShowError(Lang.T("frequency.invalidLimit"));
                return null;
            }
            ResultTable? table = await _host.RunQueryAsync(Lang.T("frequency.searching"), (db, filter, token) => FrequencyQuery.Run(db, filter, limit, token), Results);
            return table?.Rows.Count;
        }

        private async void OnSearch(object sender, RoutedEventArgs e) => await SearchAsync();
    }
}
