using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Tabs
{
    public partial class DamageTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public DamageTab()
        {
            InitializeComponent();
        }

        public ActivityViewer.Controls.ResultView ResultsView => Results;

        public string Title => Lang.T("tab.damage");

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database)
        {
            Results.Clear();
            if (database != null) RefreshSuggestions(database);
            else
            {
                SuggestionBox.Fill(TargetBox, null);
                SuggestionBox.Fill(AttackerBox, null);
            }
        }

        public void RefreshSuggestions(ActivityDatabase database)
        {
            SuggestionBox.Fill(TargetBox, database.DistinctTargets("Damage"));
            SuggestionBox.Fill(AttackerBox, database.DistinctTargets("Damaged"));
        }

        public async Task<int?> SearchAsync()
        {
            if (_host == null) return null;
            if (!InputParser.TryParseNumber(MinBox.Text, out double min))
            {
                _host.ShowError(Lang.T("damage.invalidMin"));
                return null;
            }
            var criteria = new DamageCriteria { MinTotal = min, Target = TargetBox.Text.Trim(), Attacker = AttackerBox.Text.Trim() };
            ResultTable? table = await _host.RunQueryAsync(Lang.T("damage.searching"), (db, filter, token) => DamageQuery.Run(db, filter, criteria, token), Results);
            return table?.Rows.Count;
        }

        private async void OnSearch(object sender, RoutedEventArgs e) => await SearchAsync();
    }
}
