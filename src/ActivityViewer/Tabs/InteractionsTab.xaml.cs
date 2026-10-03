using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Tabs
{
    public partial class InteractionsTab : UserControl, ITabPage
    {
        private IQueryHost? _host;

        public InteractionsTab()
        {
            InitializeComponent();
        }

        public ActivityViewer.Controls.ResultView ResultsView => Results;

        public string Title => Lang.T("tab.interactions");

        public void Attach(IQueryHost host)
        {
            _host = host;
            Results.Host = host;
        }

        public void OnDatabaseChanged(ActivityDatabase? database)
        {
            Results.Clear();
            if (database != null) RefreshSuggestions(database);
            else SuggestionBox.Fill(ObjectBox, null);
        }

        public void RefreshSuggestions(ActivityDatabase database) => SuggestionBox.Fill(ObjectBox, database.DistinctTargets(InteractionQuery.Events));

        public async Task<int?> SearchAsync()
        {
            if (_host == null) return null;
            InteractionResult result = ResultBox.SelectedIndex == 1 ? InteractionResult.Success
                : ResultBox.SelectedIndex == 2 ? InteractionResult.Failure
                : InteractionResult.Any;
            var criteria = new InteractionCriteria { Object = ObjectBox.Text.Trim(), Result = result };
            ResultTable? table = await _host.RunQueryAsync(Lang.T("interactions.searching"), (db, filter, token) => InteractionQuery.Run(db, filter, criteria, token), Results);
            return table?.Rows.Count;
        }

        private async void OnSearch(object sender, RoutedEventArgs e) => await SearchAsync();
    }
}
