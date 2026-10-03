using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Tabs
{
    public partial class SpeedTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public SpeedTab()
        {
            InitializeComponent();
        }

        public ActivityViewer.Controls.ResultView ResultsView => Results;

        public string Title => Lang.T("tab.speed");

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
            if (!InputParser.TryParseNumber(LimitBox.Text, out double limit) || limit <= 0)
            {
                _host.ShowError(Lang.T("speed.invalidLimit"));
                return null;
            }
            ResultTable? table = await _host.RunQueryAsync(Lang.T("speed.searching"), (db, filter, token) => SpeedQuery.Run(db, filter, limit, token), Results);
            return table?.Rows.Count;
        }

        private async void OnSearch(object sender, RoutedEventArgs e) => await SearchAsync();
    }
}
