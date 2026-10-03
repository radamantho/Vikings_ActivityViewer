using System;
using System.Data;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;
using Microsoft.Win32;

namespace ActivityViewer.Controls
{
    public partial class ResultView : UserControl
    {
        public ResultView()
        {
            InitializeComponent();
        }

        public IQueryHost? Host { get; set; }

        public ResultTable? Table { get; private set; }

        public ActivityDatabase? Database { get; private set; }

        public Func<CancellationToken, ResultTable>? FullQuery { get; private set; }

        public static DataTable ToDataTable(ResultTable table)
        {
            var data = new DataTable();
            foreach (ResultColumn column in table.Columns) data.Columns.Add(column.Key, column.Type);
            data.BeginLoadData();
            foreach (object?[] row in table.Rows)
            {
                var values = new object[row.Length];
                for (int i = 0; i < row.Length; i++) values[i] = row[i] ?? DBNull.Value;
                data.Rows.Add(values);
            }
            data.EndLoadData();
            return data;
        }

        public void Show(ResultTable table, DataTable data, ActivityDatabase database, Func<CancellationToken, ResultTable> fullQuery)
        {
            Table = table;
            Database = database;
            FullQuery = fullQuery;
            ResultGrid.ItemsSource = data.DefaultView;
            Summary.Text = Lang.F("main.rowsTime", table.Rows.Count, table.Elapsed.TotalSeconds) +
                (table.Truncated ? Lang.F("result.truncated", table.Rows.Count) : "");
            ExportButton.IsEnabled = true;
            CopyIdButton.IsEnabled = false;
        }

        public void Clear()
        {
            Table = null;
            Database = null;
            FullQuery = null;
            ResultGrid.ItemsSource = null;
            Summary.Text = Lang.T("result.none");
            ExportButton.IsEnabled = false;
            CopyIdButton.IsEnabled = false;
        }

        private void OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            e.Column.Header = ResultColumn.HeaderOf(e.PropertyName);
            if (!(e.Column is DataGridTextColumn text) || !(text.Binding is Binding binding)) return;
            if (e.PropertyType == typeof(DateTime)) binding.StringFormat = TimeFormat.Pattern;
            else if (e.PropertyType == typeof(double)) binding.StringFormat = "0.##";
            binding.ConverterCulture = Lang.Culture;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CopyIdButton.IsEnabled = ResultGrid.SelectedItem is DataRowView row && row.Row.Table.Columns.Contains("Id");
        }

        private void OnCopyId(object sender, RoutedEventArgs e)
        {
            if (!(ResultGrid.SelectedItem is DataRowView row) || !row.Row.Table.Columns.Contains("Id")) return;
            string id = row["Id"]?.ToString() ?? "";
            if (id.Length == 0) return;
            try
            {
                Clipboard.SetText(id);
                Summary.Text = Lang.F("result.idCopied", id);
            }
            catch (ExternalException)
            {
                Host?.ShowError(Lang.T("result.clipboardFailed"));
            }
        }

        private async void OnExport(object sender, RoutedEventArgs e)
        {
            if (Table == null || Host == null) return;
            var dialog = new SaveFileDialog
            {
                Title = Lang.T("result.exportTitle"),
                Filter = Lang.T("result.exportFilter"),
                FileName = Lang.T("result.fileName") + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            await Host.ExportAsync(this, dialog.FileName);
        }
    }
}
