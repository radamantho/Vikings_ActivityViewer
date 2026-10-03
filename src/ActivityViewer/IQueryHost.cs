using System;
using System.Threading;
using System.Threading.Tasks;
using ActivityViewer.Controls;
using ActivityViewer.Core.Data;

namespace ActivityViewer
{
    public interface IQueryHost
    {
        Task<ResultTable?> RunQueryAsync(string status, Func<ActivityDatabase, QueryFilter, CancellationToken, ResultTable> query, ResultView target);

        Task ExportAsync(ResultView source, string path);

        void ShowError(string message);
    }

    public interface ITabPage
    {
        string Title { get; }

        ResultView ResultsView { get; }

        void Attach(IQueryHost host);

        void OnDatabaseChanged(ActivityDatabase? database);

        void RefreshSuggestions(ActivityDatabase database);

        Task<int?> SearchAsync();
    }
}
